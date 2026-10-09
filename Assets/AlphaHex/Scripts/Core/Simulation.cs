using System;
using System.Collections.Generic;
using System.Globalization;

namespace AlphaHex.Core
{
    /// <summary>
    /// Deterministic local industrial-builder simulation. No rendering, file I/O, Unity
    /// objects, network calls, or real-time clock dependencies belong in this class.
    /// </summary>
    public sealed partial class Simulation
    {
        public const int CurrentSchemaVersion = 2;
        public const int WorldRadius = 6;
        public const int MaxExcavationDepth = 3;
        public const int TimberPerTree = 3;
        public const int OrePerMetal = 2;
        public const float ForestrySeconds = 4f;
        public const float MineSeconds = 5f;
        public const float SmelterSeconds = 6f;
        public const float DeliverySecondsPerEdge = 0.75f;
        public const double FixedStepSeconds = 0.125;

        private const double ClockEpsilon = 0.00001;
        private static readonly HexCoord DepotCoord = new HexCoord(0, 0);
        private readonly Dictionary<HexCoord, TileState> _tiles;
        private readonly List<TileState> _orderedTiles;
        private readonly Dictionary<HexCoord, HexCoord> _towardDepot = new Dictionary<HexCoord, HexCoord>();
        private bool _networkDirty = true;

        public WorldState State { get; private set; }

        /// <summary>Rejects unsupported or corrupt saves before accepting their data.</summary>
        public Simulation(WorldState state)
        {
            ValidateState(state);
            State = state;
            _tiles = new Dictionary<HexCoord, TileState>(state.Tiles.Count);
            _orderedTiles = new List<TileState>(state.Tiles);
            _orderedTiles.Sort((a, b) => a.Coord.Q != b.Coord.Q
                ? a.Coord.Q.CompareTo(b.Coord.Q) : a.Coord.R.CompareTo(b.Coord.R));
            foreach (TileState tile in _orderedTiles)
                _tiles.Add(tile.Coord, tile);
        }

        public static Simulation Create(int seed = 417)
        {
            var state = new WorldState
            {
                Seed = seed,
                Stock = new ResourceStock(80, 0, 10, StartingFood)
            };

            for (int q = -WorldRadius; q <= WorldRadius; q++)
            {
                int minR = Math.Max(-WorldRadius, -q - WorldRadius);
                int maxR = Math.Min(WorldRadius, -q + WorldRadius);
                for (int r = minR; r <= maxR; r++)
                {
                    var coord = new HexCoord(q, r);
                    int distance = HexCoord.Distance(coord, DepotCoord);
                    uint value = Hash(seed, q, r, 0);
                    uint detail = Hash(seed, q, r, 1);
                    int roll = (int)(value % 100);
                    TerrainKind terrain;

                    if (distance == WorldRadius)
                        terrain = roll < 76 ? TerrainKind.Water : TerrainKind.Sand;
                    else if (distance == WorldRadius - 1 && roll < 38)
                        terrain = TerrainKind.Sand;
                    else if (roll < 32)
                        terrain = TerrainKind.Forest;
                    else if (roll < 49)
                        terrain = TerrainKind.Ore;
                    else if (roll < 62)
                        terrain = TerrainKind.Rock;
                    else
                        terrain = TerrainKind.Grass;

                    int elevation = terrain == TerrainKind.Ore || terrain == TerrainKind.Rock
                        ? 1 + (int)(detail % 3)
                        : terrain == TerrainKind.Forest || terrain == TerrainKind.Grass
                            ? (int)(detail % 2) : 0;

                    var tile = new TileState { Coord = coord, Claimed = distance <= 1 };
                    SetTerrain(tile, terrain, elevation,
                        terrain == TerrainKind.Forest ? 6 + (int)(detail % 7) : 0,
                        terrain == TerrainKind.Ore ? 24 + (int)(detail % 25) : 0);

                    // These starter tiles guarantee the complete first production chain
                    // and useful frontier land for every seed.
                    if (coord == DepotCoord)
                    {
                        SetTerrain(tile, TerrainKind.Grass, 0, 0, 0);
                        tile.Building = BuildingKind.Depot;
                        tile.HasRoad = true;
                    }
                    else if (coord == new HexCoord(-1, 0))
                        SetTerrain(tile, TerrainKind.Forest, 0, 12, 0);
                    else if (coord == new HexCoord(1, 0))
                        SetTerrain(tile, TerrainKind.Ore, 1, 0, 36);
                    else if (distance == 1)
                        SetTerrain(tile, TerrainKind.Grass, 0, 0, 0);
                    else if (coord == new HexCoord(-2, 0))
                        SetTerrain(tile, TerrainKind.Forest, 1, 12, 0);
                    else if (coord == new HexCoord(2, 0))
                        SetTerrain(tile, TerrainKind.Ore, 2, 0, 48);
                    else if (coord == new HexCoord(0, 2))
                        SetTerrain(tile, TerrainKind.Grass, 0, 0, 0);

                    state.Tiles.Add(tile);
                }
            }

            return new Simulation(state);
        }

        public TileState GetTile(HexCoord coord) => _tiles.TryGetValue(coord, out TileState tile) ? tile : null;

        public static ResourceStock GetBuildCost(BuildingKind building)
        {
            switch (building)
            {
                case BuildingKind.Forestry: return new ResourceStock(10, 0, 0);
                case BuildingKind.Mine: return new ResourceStock(14, 0, 2);
                case BuildingKind.Smelter: return new ResourceStock(20, 0, 4);
                case BuildingKind.House: return new ResourceStock(8, 0, 0);
                case BuildingKind.Forager: return new ResourceStock(6, 0, 0);
                case BuildingKind.Farm: return new ResourceStock(14, 0, 2);
                case BuildingKind.Forester: return new ResourceStock(12, 0, 2);
                case BuildingKind.DeepMine: return new ResourceStock(20, 0, 6);
                default: return new ResourceStock();
            }
        }

        public bool TryClaim(HexCoord coord, out string message)
        {
            TileState tile = GetTile(coord);
            if (tile == null) return Fail("That tile is outside this island.", out message);
            if (tile.Terrain == TerrainKind.Water) return Fail("Water cannot be claimed.", out message);
            if (tile.Claimed) return Fail("You already own this tile.", out message);

            bool touchesClaim = false;
            foreach (HexCoord neighbor in coord.Neighbors())
            {
                TileState adjacent = GetTile(neighbor);
                if (adjacent != null && adjacent.Claimed) { touchesClaim = true; break; }
            }
            if (!touchesClaim) return Fail("Claim a tile next to your existing land.", out message);
            if (!CanAfford(6, 0, 1)) return Fail("Claiming costs 6 timber and 1 metal.", out message);

            Spend(6, 0, 1);
            tile.Claimed = true;
            _networkDirty = true;
            message = "Land claimed. Connect production to the depot with roads.";
            return true;
        }

        public bool TryBuild(HexCoord coord, BuildingKind building, out string message)
        {
            if (!Enum.IsDefined(typeof(BuildingKind), building) || building == BuildingKind.None || building == BuildingKind.Depot)
                return Fail("Choose a building from the construction tools. The starting depot is permanent.", out message);
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.Building != BuildingKind.None) return Fail("This tile already has a building.", out message);
            ResearchKind required = RequiredResearch(building);
            if (required != ResearchKind.None && !HasResearch(required))
                return Fail("Research " + ResearchName(required) + " in the Town panel first.", out message);

            if (building == BuildingKind.Forestry && tile.TreesRemaining == 0)
                return Fail("Forestry needs a tile with remaining trees.", out message);
            if (building == BuildingKind.Mine && tile.OreRemaining == 0)
                return Fail("A mine needs a tile with remaining ore.", out message);
            if (building == BuildingKind.DeepMine && (tile.InitialOre == 0 || tile.OreRemaining > 0 || tile.DeepOreRemaining == 0))
                return Fail("Deep mines need an exhausted surface deposit with remaining deep ore.", out message);
            if ((building == BuildingKind.Smelter || building == BuildingKind.House) && (tile.TreesRemaining > 0 || tile.OreRemaining > 0 || tile.SaplingPlanted))
                return Fail("Place this building on clear land without growing saplings.", out message);
            if ((building == BuildingKind.Farm || building == BuildingKind.Forester)
                && (tile.Terrain != TerrainKind.Grass || tile.InitialOre > 0 || tile.SaplingPlanted))
                return Fail("Farms and foresters need clear grassland without growing saplings.", out message);
            if (building == BuildingKind.Forager && ((tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest) || tile.SaplingPlanted))
                return Fail("A forager needs grassland or forest without a growing sapling.", out message);

            ResourceStock cost = GetBuildCost(building);
            if (!CanAfford(cost.Timber, cost.Ore, cost.Metal))
                return Fail(BuildingName(building) + " costs " + cost.Timber + " timber and " + cost.Metal + " metal.", out message);

            Spend(cost.Timber, cost.Ore, cost.Metal);
            tile.Building = building;
            tile.WorkProgress = 0;
            tile.InputOreReserved = 0;
            tile.WorkPaused = false;
            _networkDirty = true;
            message = BuildingName(building) + " built. " + (GetPathToDepot(coord) == null
                ? "It needs a road connection to the depot." : "It is connected to the depot.");
            return true;
        }

        public bool TryBuildRoad(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.HasRoad) return Fail("This tile already has a road.", out message);
            if (tile.Building != BuildingKind.None)
                return Fail("Buildings already connect adjoining roads and buildings.", out message);
            if (!CanAfford(2, 0, 0)) return Fail("A road costs 2 timber.", out message);

            Spend(2, 0, 0);
            tile.HasRoad = true;
            _networkDirty = true;
            message = GetPathToDepot(coord) == null
                ? "Road built. Extend it to the depot network."
                : "Road built and connected to the depot.";
            return true;
        }

        /// <summary>Manual starter action: one tree becomes three depot timber immediately.</summary>
        public bool TryHarvest(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.TreesRemaining <= 0) return Fail("There are no trees left on this tile.", out message);
            if (!CanReceive(ResourceKind.Timber, TimberPerTree)) return Fail("The depot timber counter is full.", out message);

            tile.TreesRemaining--;
            if (tile.TreesRemaining == 0)
            {
                tile.Terrain = TerrainKind.Grass;
                tile.WorkProgress = 0;
            }
            AddToStock(ResourceKind.Timber, TimberPerTree);
            message = "Hand gathered 3 timber. " + tile.TreesRemaining + " trees remain.";
            if (TryClearDepletedExtractor(tile)) message += " Camp removed; access road kept.";
            return true;
        }

        /// <summary>Manual starter action: hand-mine one ore, crediting the depot immediately.</summary>
        public bool TryExcavate(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.OreRemaining <= 0) return Fail("There is no ore left on this tile.", out message);
            if (!CanReceive(ResourceKind.Ore, 1)) return Fail("The depot ore counter is full.", out message);

            RemoveOre(tile, 1);
            AddToStock(ResourceKind.Ore, 1);
            message = "Hand mined 1 ore. " + tile.OreRemaining + " ore remain.";
            if (TryClearDepletedExtractor(tile)) message += " Mine removed; access road kept.";
            return true;
        }

        public bool TryDemolish(HexCoord coord, out string message)
        {
            if (!TryGetOwnedLand(coord, out TileState tile, out message)) return false;
            if (tile.Building == BuildingKind.Depot) return Fail("The starting depot cannot be demolished.", out message);
            if (tile.Building == BuildingKind.House && tile.Residents > 0)
                return Fail("This house is occupied. Keep its citizens housed.", out message);
            if (tile.Building == BuildingKind.None && !tile.HasRoad)
                return Fail("There is no building or road to remove here.", out message);
            foreach (Shipment shipment in State.Shipments)
                if (shipment.Path.Contains(coord))
                    return Fail("A delivery is using this tile. Wait for it to arrive before demolishing.", out message);

            if (tile.Building != BuildingKind.None)
            {
                string name = BuildingName(tile.Building);
                if (tile.InputOreReserved > 0)
                    AddToStock(ResourceKind.Ore, tile.InputOreReserved);
                tile.InputOreReserved = 0;
                tile.WorkProgress = 0;
                tile.WorkPaused = false;
                tile.Building = BuildingKind.None;
                message = name + " removed. Construction materials are not refunded.";
            }
            else
            {
                tile.HasRoad = false;
                message = "Road removed. Construction materials are not refunded.";
            }
            _networkDirty = true;
            return true;
        }

        /// <summary>
        /// Advance foreground simulation time. Calls may cover at most one hour;
        /// there is no wall-clock/offline production in this prototype.
        /// </summary>
        public void Tick(float seconds)
        {
            if (!IsFinite(seconds) || seconds < 0 || seconds > 3600f)
                throw new ArgumentOutOfRangeException(nameof(seconds), "Pass finite elapsed seconds between 0 and 3600.");
            if (seconds == 0) return;

            double available = State.PendingSeconds + seconds;
            int steps = (int)Math.Floor((available + ClockEpsilon) / FixedStepSeconds);
            State.PendingSeconds = Math.Max(0, available - steps * FixedStepSeconds);
            for (int i = 0; i < steps; i++)
            {
                Step();
                State.SimulationSteps++;
            }
            State.ElapsedSeconds = (float)(State.SimulationSteps * FixedStepSeconds);
        }

        /// <summary>A fresh shortest path, ordered from the source tile to the central depot.</summary>
        public List<HexCoord> GetPathToDepot(HexCoord coord)
        {
            EnsureNetwork();
            if (!_towardDepot.ContainsKey(coord)) return null;
            var path = new List<HexCoord> { coord };
            HexCoord current = coord;
            while (current != DepotCoord)
            {
                current = _towardDepot[current];
                path.Add(current);
            }
            return path;
        }

        public string GetStatus(HexCoord coord)
        {
            TileState tile = GetTile(coord);
            if (tile == null) return "Outside this island.";
            if (tile.Terrain == TerrainKind.Water) return "Coastal water. Building is available on land.";
            if (!tile.Claimed) return "Unclaimed land. Claim an adjacent tile to expand.";
            if (tile.Building == BuildingKind.Depot) return "Available stock excludes cargo in transit. "
                + GetReservedOre() + " ore reserved by smelters. Deliveries enter stock on arrival.";
            if (tile.Building == BuildingKind.House) return tile.Residents + " / " + HouseCapacity + " residents. "
                + (GetPathToDepot(coord) == null ? "Connect this home to welcome citizens." : GetImmigrationStatus());

            Shipment shipment = FindShipment(coord);
            if (shipment != null)
            {
                bool final = IsDepletedExtractor(tile) && !(tile.Building == BuildingKind.Forestry && IsManagedForest(tile));
                return (final ? "Final delivery: " : "Delivering ")
                    + shipment.Amount + " " + shipment.Resource.ToString().ToLowerInvariant()
                    + ". Arrival in " + Math.Max(0, shipment.Duration - shipment.Elapsed).ToString("0.0", CultureInfo.InvariantCulture) + " s."
                    + (final ? " Building clears on arrival; road stays." : " Next job waits for this delivery.");
            }

            if (tile.Building == BuildingKind.Forestry && tile.TreesRemaining == 0 && IsManagedForest(tile))
                return "Waiting for regrowth. The nearby forester replants this woodland; its logging camp stays for the next harvest.";
            if (IsDepletedExtractor(tile))
                return "Site exhausted. Building clears when simulation resumes; road stays.";

            if (tile.Building != BuildingKind.None)
            {
                List<HexCoord> path = GetPathToDepot(coord);
                if (path == null) return "Disconnected. Production is stopped. Connect roads or adjoining buildings to the depot.";
                if (tile.WorkPaused) return "Work paused. Resume this workplace to assign a citizen again.";
                if (GetWorkersAssigned(coord) == 0) return "Waiting for a citizen. Build connected housing and maintain food, or pause another workplace.";
                if (tile.Building == BuildingKind.Forester && FindPlantingSite(tile) == null)
                    return "No planting space. Claim adjacent grass or forest; occupied sites, quarries and full forests cannot be replanted.";
                if (tile.Building == BuildingKind.Smelter && tile.InputOreReserved == 0 && State.Stock.Ore < OrePerMetal)
                    return "Waiting for ore: " + State.Stock.Ore + " / 2 available. " + GetInTransit(ResourceKind.Ore)
                        + " ore in transit. Hand-mine or connect a mine to supply the depot.";
                if (tile.Building != BuildingKind.Forester && !CanReceive(OutputResource(tile.Building), OutputAmount(tile)))
                    return "Waiting: depot capacity is full, including cargo and reserved input. Spend resources to resume.";
                return BuildingName(tile.Building) + ": "
                    + Math.Floor(tile.WorkProgress / JobSeconds(tile.Building) * 100).ToString(CultureInfo.InvariantCulture)
                    + "%. " + (JobSeconds(tile.Building) - tile.WorkProgress).ToString("0.0", CultureInfo.InvariantCulture)
                    + " s to " + (tile.Building == BuildingKind.Forester ? "plant a sapling. Growth takes 45 s." : "next output.\nDelivery: "
                    + ((path.Count - 1) * DeliverySecondsPerEdge).ToString("0.0", CultureInfo.InvariantCulture) + " s.")
                    + (tile.InputOreReserved > 0 ? " 2 ore reserved for this job." : "");
            }

            if (tile.SaplingPlanted) return "A sapling is growing: " + Math.Max(0, TreeGrowthSeconds - tile.SaplingAge).ToString("0.0", CultureInfo.InvariantCulture) + " s to maturity.";
            if (tile.OreRemaining == 0 && tile.DeepOreRemaining > 0) return "Surface deposit exhausted. " + tile.DeepOreRemaining
                + " deep ore remains. " + (HasResearch(ResearchKind.DeepMining) ? "Build a deep mine to reach it." : "Unlock Deep mining in Town > Research.");
            if (tile.HasRoad) return GetPathToDepot(coord) == null ? "Road is disconnected from the depot." : "Road connected to the depot.";
            if (tile.TreesRemaining > 0) return "Forest: " + tile.TreesRemaining + " trees. Hand gather timber or build forestry.";
            if (tile.OreRemaining > 0) return "Deposit: " + tile.OreRemaining + " ore. Hand mine or build a mine.";
            return "Clear land. Ready for construction.";
        }

        public int GetInTransit(ResourceKind resource)
        {
            int amount = 0;
            foreach (Shipment shipment in State.Shipments)
                if (shipment.Resource == resource) amount += shipment.Amount;
            return amount;
        }

        public int GetReservedOre()
        {
            int amount = 0;
            foreach (TileState tile in _orderedTiles) amount += tile.InputOreReserved;
            return amount;
        }

        /// <summary>The active job or outgoing delivery, for a read-only progress indicator.</summary>
        public float GetProgress(HexCoord coord)
        {
            Shipment shipment = FindShipment(coord);
            if (shipment != null) return shipment.Elapsed / shipment.Duration;
            TileState tile = GetTile(coord);
            float duration = tile == null ? 0 : JobSeconds(tile.Building);
            return duration > 0 ? tile.WorkProgress / duration : 0;
        }

        private void Step()
        {
            StepTown();
            EnsureStaffing();
            // Only shipments that existed at the start of this step move. New output
            // begins its trip after the job finishes, and arriving input is used next step.
            int movingShipments = State.Shipments.Count;
            foreach (TileState tile in _orderedTiles)
            {
                if (!NeedsWorker(tile.Building) || !_staffed.Contains(tile.Coord) || FindShipment(tile.Coord) != null)
                    continue;
                if (tile.Building == BuildingKind.Forestry && tile.TreesRemaining == 0) continue;
                if (tile.Building == BuildingKind.Mine && tile.OreRemaining == 0) continue;
                if (tile.Building == BuildingKind.DeepMine && tile.DeepOreRemaining == 0) continue;
                List<HexCoord> path = GetPathToDepot(tile.Coord);
                if (path == null) continue;

                if (tile.Building == BuildingKind.Forester)
                {
                    TileState plantingSite = FindPlantingSite(tile);
                    if (plantingSite == null) continue;
                    tile.WorkProgress += (float)FixedStepSeconds;
                    if (tile.WorkProgress >= ForesterSeconds)
                    {
                        tile.WorkProgress = 0;
                        plantingSite.SaplingPlanted = true;
                        plantingSite.SaplingAge = 0;
                    }
                    continue;
                }

                ResourceKind output = OutputResource(tile.Building);
                int amount = OutputAmount(tile);
                if (!CanReceive(output, amount)) continue;

                if (tile.Building == BuildingKind.Smelter && tile.InputOreReserved == 0)
                {
                    if (State.Stock.Ore < OrePerMetal) continue;
                    State.Stock.Ore -= OrePerMetal;
                    tile.InputOreReserved = OrePerMetal;
                }

                tile.WorkProgress += (float)FixedStepSeconds;
                if (tile.WorkProgress < JobSeconds(tile.Building)) continue;
                tile.WorkProgress = 0;

                if (tile.Building == BuildingKind.Forestry)
                {
                    tile.TreesRemaining--;
                    if (tile.TreesRemaining == 0) tile.Terrain = TerrainKind.Grass;
                }
                else if (tile.Building == BuildingKind.Mine)
                    RemoveOre(tile, amount);
                else if (tile.Building == BuildingKind.DeepMine)
                {
                    tile.DeepOreRemaining -= amount;
                    tile.ExcavationDepth = TileDepth(tile);
                }
                else
                    tile.InputOreReserved = 0;

                State.Shipments.Add(new Shipment
                {
                    Resource = output,
                    Amount = amount,
                    Path = path,
                    Duration = (path.Count - 1) * DeliverySecondsPerEdge
                });
            }

            for (int i = movingShipments - 1; i >= 0; i--)
            {
                Shipment shipment = State.Shipments[i];
                shipment.Elapsed += (float)FixedStepSeconds;
                if (shipment.Elapsed < shipment.Duration) continue;
                AddToStock(shipment.Resource, shipment.Amount);
                State.Shipments.RemoveAt(i);
            }

            // Also clears exhausted buildings restored from older saves. Keep each
            // source until its final cargo arrives so in-flight saves remain valid.
            foreach (TileState tile in _orderedTiles) TryClearDepletedExtractor(tile);
        }

        private static bool IsDepletedExtractor(TileState tile) =>
            tile.Building == BuildingKind.Forestry && tile.TreesRemaining == 0
            || tile.Building == BuildingKind.Mine && tile.OreRemaining == 0
            || tile.Building == BuildingKind.DeepMine && tile.DeepOreRemaining == 0;

        private bool TryClearDepletedExtractor(TileState tile)
        {
            if (!IsDepletedExtractor(tile) || FindShipment(tile.Coord) != null) return false;
            if (tile.Building == BuildingKind.Forestry && IsManagedForest(tile)) return false;
            tile.Building = BuildingKind.None;
            tile.WorkProgress = 0;
            tile.WorkPaused = false;
            _staffingDirty = true;
            // Buildings already carry roads and other producers' deliveries. Retain
            // that connection when removing the structure, without refunding its cost.
            tile.HasRoad = true;
            return true;
        }

        private void EnsureNetwork()
        {
            if (!_networkDirty) return;
            _staffingDirty = true;
            _towardDepot.Clear();
            _towardDepot.Add(DepotCoord, DepotCoord);
            var queue = new Queue<HexCoord>();
            queue.Enqueue(DepotCoord);
            while (queue.Count > 0)
            {
                HexCoord current = queue.Dequeue();
                foreach (HexCoord neighbor in current.Neighbors())
                {
                    TileState tile = GetTile(neighbor);
                    if (tile == null || !IsNetworkTile(tile) || _towardDepot.ContainsKey(neighbor)) continue;
                    _towardDepot.Add(neighbor, current);
                    queue.Enqueue(neighbor);
                }
            }
            _networkDirty = false;
        }

        private Shipment FindShipment(HexCoord coord)
        {
            foreach (Shipment shipment in State.Shipments)
                if (shipment.Path[0] == coord) return shipment;
            return null;
        }

        private bool TryGetOwnedLand(HexCoord coord, out TileState tile, out string message)
        {
            tile = GetTile(coord);
            if (tile == null) return Fail("That tile is outside this island.", out message);
            if (tile.Terrain == TerrainKind.Water) return Fail("This action needs a land tile.", out message);
            if (!tile.Claimed) return Fail("Claim this tile first.", out message);
            message = string.Empty;
            return true;
        }

        private bool CanAfford(int timber, int ore, int metal) =>
            State.Stock.Timber >= timber && State.Stock.Ore >= ore && State.Stock.Metal >= metal;

        private void Spend(int timber, int ore, int metal)
        {
            State.Stock.Timber -= timber;
            State.Stock.Ore -= ore;
            State.Stock.Metal -= metal;
        }

        private bool CanReceive(ResourceKind resource, int amount)
        {
            long owned = StockAmount(State.Stock, resource);
            foreach (Shipment shipment in State.Shipments)
                if (shipment.Resource == resource) owned += shipment.Amount;
            if (resource == ResourceKind.Ore)
                foreach (TileState tile in _orderedTiles) owned += tile.InputOreReserved;
            return owned + amount <= int.MaxValue;
        }

        private void AddToStock(ResourceKind resource, int amount)
        {
            checked
            {
                switch (resource)
                {
                    case ResourceKind.Timber: State.Stock.Timber += amount; break;
                    case ResourceKind.Ore: State.Stock.Ore += amount; break;
                    case ResourceKind.Metal: State.Stock.Metal += amount; break;
                    case ResourceKind.Food: State.Stock.Food += amount; break;
                    default: throw new InvalidOperationException("Unknown resource.");
                }
            }
        }

        private static long StockAmount(ResourceStock stock, ResourceKind resource) =>
            resource == ResourceKind.Timber ? stock.Timber : resource == ResourceKind.Ore ? stock.Ore
                : resource == ResourceKind.Metal ? stock.Metal : stock.Food;

        private static bool IsNetworkTile(TileState tile) => tile.Claimed && tile.Terrain != TerrainKind.Water
            && (tile.HasRoad || tile.Building != BuildingKind.None);

        private static void RemoveOre(TileState tile, int amount)
        {
            tile.OreRemaining -= amount;
            tile.ExcavationDepth = TileDepth(tile);
            if (tile.OreRemaining == 0)
            {
                tile.Terrain = TerrainKind.Rock;
                tile.WorkProgress = 0;
            }
        }

        private static int CalculateDepth(int initial, int remaining) => initial == 0 ? 0
            : (int)((long)(initial - remaining) * MaxExcavationDepth / initial);

        private static float JobSeconds(BuildingKind building) => building == BuildingKind.Forestry ? ForestrySeconds
            : building == BuildingKind.Mine ? MineSeconds : building == BuildingKind.Smelter ? SmelterSeconds
            : building == BuildingKind.Forager ? ForagerSeconds : building == BuildingKind.Farm ? FarmSeconds
            : building == BuildingKind.Forester ? ForesterSeconds : building == BuildingKind.DeepMine ? DeepMineSeconds : 0;

        public static string BuildingName(BuildingKind building) => building == BuildingKind.Forestry ? "Logging camp"
            : building == BuildingKind.DeepMine ? "Deep mine" : building == BuildingKind.Forager ? "Forager hut" : building.ToString();

        private static bool Fail(string reason, out string message) { message = reason; return false; }
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

        private static void SetTerrain(TileState tile, TerrainKind terrain, int elevation, int trees, int ore)
        {
            tile.Terrain = terrain;
            tile.Elevation = elevation;
            tile.TreesRemaining = trees;
            tile.OreRemaining = ore;
            tile.InitialOre = ore;
            tile.DeepOreInitial = tile.DeepOreRemaining = ore * 2;
            tile.ExcavationDepth = 0;
        }

        // A fixed integer hash avoids runtime-specific Random implementations and yields
        // the same generated island in Unity, .NET tests, desktop, and iOS.
        private static uint Hash(int seed, int q, int r, int salt)
        {
            unchecked
            {
                uint h = (uint)seed ^ 0x9E3779B9u;
                h ^= (uint)q * 0x85EBCA6Bu;
                h ^= (uint)r * 0xC2B2AE35u;
                h ^= (uint)salt * 0x27D4EB2Fu;
                h ^= h >> 16;
                h *= 0x7FEB352Du;
                h ^= h >> 15;
                h *= 0x846CA68Bu;
                h ^= h >> 16;
                return h;
            }
        }

        private static void ValidateState(WorldState state)
        {
            if (state == null) throw InvalidSave("payload is null");
            if (state.SchemaVersion != CurrentSchemaVersion) throw InvalidSave("unsupported schema version");
            if (state.Stock == null || state.Stock.Timber < 0 || state.Stock.Ore < 0 || state.Stock.Metal < 0 || state.Stock.Food < 0)
                throw InvalidSave("resource stock is missing or negative");
            if (state.Tiles == null || state.Tiles.Count != 1 + 3 * WorldRadius * (WorldRadius + 1))
                throw InvalidSave("the island must contain every radius-6 hex exactly once");
            if (state.Shipments == null || state.Shipments.Count > state.Tiles.Count - 1)
                throw InvalidSave("shipment list is missing or too large");
            if (state.SimulationSteps < 0 || !IsFinite(state.ElapsedSeconds) || state.ElapsedSeconds < 0
                || !IsFinite(state.PendingSeconds) || state.PendingSeconds < 0 || state.PendingSeconds >= FixedStepSeconds)
                throw InvalidSave("simulation clock is invalid");
            float expectedElapsed = (float)(state.SimulationSteps * FixedStepSeconds);
            if (state.ElapsedSeconds != expectedElapsed)
                throw InvalidSave("elapsed time disagrees with the saved simulation steps");

            ValidateTown(state);
            var tiles = new Dictionary<HexCoord, TileState>();
            int depots = 0;
            int residents = PioneerCitizens;
            long reservedOre = 0;
            foreach (TileState tile in state.Tiles)
            {
                if (tile == null) throw InvalidSave("tile entry is null");
                if (HexCoord.Distance(tile.Coord, DepotCoord) > WorldRadius || tiles.ContainsKey(tile.Coord))
                    throw InvalidSave("duplicate or out-of-bounds tile coordinates");
                tiles.Add(tile.Coord, tile);
                if (!Enum.IsDefined(typeof(TerrainKind), tile.Terrain) || !Enum.IsDefined(typeof(BuildingKind), tile.Building))
                    throw InvalidSave("unknown terrain or building type");
                if (tile.Elevation < 0 || tile.Elevation > 5) throw InvalidSave("terrain elevation is outside the supported range");
                if (tile.Terrain == TerrainKind.Water && (tile.Claimed || tile.HasRoad || tile.Building != BuildingKind.None || tile.Elevation != 0))
                    throw InvalidSave("water cannot be owned or developed");
                if ((tile.HasRoad || tile.Building != BuildingKind.None) && !tile.Claimed)
                    throw InvalidSave("infrastructure is on unclaimed land");
                if (tile.TreesRemaining < 0 || tile.TreesRemaining > 64
                    || (tile.Terrain == TerrainKind.Forest) != (tile.TreesRemaining > 0))
                    throw InvalidSave("forest resource state is inconsistent");
                if (tile.InitialOre < 0 || tile.InitialOre > 100000 || tile.OreRemaining < 0 || tile.OreRemaining > tile.InitialOre
                    || (tile.Terrain == TerrainKind.Ore) != (tile.OreRemaining > 0))
                    throw InvalidSave("ore resource state is inconsistent");
                if (tile.InitialOre > 0 && tile.Terrain != TerrainKind.Ore && tile.Terrain != TerrainKind.Rock)
                    throw InvalidSave("ore history is attached to unrelated terrain");
                if (tile.DeepOreInitial != tile.InitialOre * 2 || tile.DeepOreRemaining < 0 || tile.DeepOreRemaining > tile.DeepOreInitial
                    || (tile.DeepOreRemaining < tile.DeepOreInitial && (tile.OreRemaining > 0 || (state.Town.Research & ResearchKind.DeepMining) == 0)))
                    throw InvalidSave("deep ore reserves are inconsistent");
                if (tile.ExcavationDepth != TileDepth(tile))
                    throw InvalidSave("excavation depth disagrees with resource depletion");
                if (tile.Residents < 0 || tile.Residents > HouseCapacity || tile.Residents % ImmigrationWave != 0
                    || tile.Building != BuildingKind.House && tile.Residents != 0)
                    throw InvalidSave("house residents are inconsistent");
                residents += tile.Residents;
                if (tile.WorkPaused && !NeedsWorker(tile.Building)) throw InvalidSave("only workplaces can pause work");
                if (!IsFinite(tile.SaplingAge) || tile.SaplingAge < 0 || tile.SaplingAge >= TreeGrowthSeconds
                    || !tile.SaplingPlanted && tile.SaplingAge != 0
                    || tile.SaplingPlanted && (!tile.Claimed || tile.InitialOre > 0 || tile.TreesRemaining >= ForestCapacity
                        || (state.Town.Research & ResearchKind.Reforestation) == 0
                        || (tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest)
                        || (tile.Building != BuildingKind.None && tile.Building != BuildingKind.Forestry)))
                    throw InvalidSave("sapling growth is inconsistent");
                if (!IsFinite(tile.WorkProgress) || tile.WorkProgress < 0)
                    throw InvalidSave("job progress is invalid");

                if (tile.Building == BuildingKind.Depot)
                {
                    depots++;
                    if (tile.Coord != DepotCoord || tile.Terrain != TerrainKind.Grass)
                        throw InvalidSave("the depot must occupy the central grass tile");
                }
                if (tile.Building == BuildingKind.Forestry && tile.Terrain != TerrainKind.Forest && tile.Terrain != TerrainKind.Grass)
                    throw InvalidSave("forestry is on unsuitable terrain");
                if (tile.Building == BuildingKind.Mine && (tile.InitialOre == 0 || (tile.Terrain != TerrainKind.Ore && tile.Terrain != TerrainKind.Rock)))
                    throw InvalidSave("a mine lacks a valid ore deposit");
                if ((tile.Building == BuildingKind.Smelter || tile.Building == BuildingKind.House)
                    && (tile.TreesRemaining > 0 || tile.OreRemaining > 0 || tile.SaplingPlanted))
                    throw InvalidSave("building terrain is not clear");
                if ((tile.Building == BuildingKind.Farm || tile.Building == BuildingKind.Forester)
                    && (tile.Terrain != TerrainKind.Grass || tile.InitialOre > 0 || tile.SaplingPlanted))
                    throw InvalidSave("farm or forester is not on clear grassland");
                if (tile.Building == BuildingKind.Forager && ((tile.Terrain != TerrainKind.Grass && tile.Terrain != TerrainKind.Forest) || tile.SaplingPlanted))
                    throw InvalidSave("forager is on unsuitable terrain");
                if (tile.Building == BuildingKind.DeepMine && (tile.InitialOre == 0 || tile.OreRemaining > 0))
                    throw InvalidSave("deep mine needs an exhausted surface deposit");
                ResearchKind required = RequiredResearch(tile.Building);
                if ((state.Town.Research & required) != required) throw InvalidSave("building research is missing");

                float jobSeconds = JobSeconds(tile.Building);
                if ((jobSeconds == 0 && tile.WorkProgress != 0) || (jobSeconds > 0 && tile.WorkProgress >= jobSeconds))
                    throw InvalidSave("job progress exceeds its duration");
                if (IsDepletedExtractor(tile) && tile.WorkProgress != 0)
                    throw InvalidSave("a depleted extractor still has an active job");
                if (tile.Building == BuildingKind.Smelter)
                {
                    if ((tile.InputOreReserved != 0 && tile.InputOreReserved != OrePerMetal)
                        || (tile.InputOreReserved == 0 && tile.WorkProgress != 0))
                        throw InvalidSave("smelter input reservation is inconsistent");
                }
                else if (tile.InputOreReserved != 0)
                    throw InvalidSave("only a smelter can reserve ore");
                reservedOre += tile.InputOreReserved;
            }
            if (depots != 1) throw InvalidSave("exactly one central depot is required");
            if (residents != state.Town.Population) throw InvalidSave("population disagrees with occupied homes");

            // Every claim must be reachable through other claimed land: this is the same
            // frontier rule enforced by TryClaim, independent of the road network.
            var claimed = new HashSet<HexCoord> { DepotCoord };
            var frontier = new Queue<HexCoord>();
            frontier.Enqueue(DepotCoord);
            while (frontier.Count > 0)
            {
                foreach (HexCoord neighbor in frontier.Dequeue().Neighbors())
                    if (tiles.TryGetValue(neighbor, out TileState adjacent) && adjacent.Claimed && claimed.Add(neighbor))
                        frontier.Enqueue(neighbor);
            }
            foreach (TileState tile in state.Tiles)
                if (tile.Claimed && !claimed.Contains(tile.Coord))
                    throw InvalidSave("claimed land is disconnected from the depot territory");

            var sources = new HashSet<HexCoord>();
            long timber = state.Stock.Timber;
            long ore = state.Stock.Ore + reservedOre;
            long metal = state.Stock.Metal;
            long food = state.Stock.Food;
            foreach (Shipment shipment in state.Shipments)
            {
                if (shipment == null || !Enum.IsDefined(typeof(ResourceKind), shipment.Resource) || shipment.Amount <= 0)
                    throw InvalidSave("shipment resource or amount is invalid");
                if (shipment.Path == null || shipment.Path.Count < 2 || shipment.Path.Count > state.Tiles.Count
                    || shipment.Path[shipment.Path.Count - 1] != DepotCoord)
                    throw InvalidSave("shipment path must lead from a source to the depot");
                if (!IsFinite(shipment.Duration) || !IsFinite(shipment.Elapsed) || shipment.Elapsed < 0
                    || shipment.Elapsed >= shipment.Duration
                    || shipment.Duration != (shipment.Path.Count - 1) * DeliverySecondsPerEdge)
                    throw InvalidSave("shipment timing is inconsistent with its route");

                var visited = new HashSet<HexCoord>();
                for (int i = 0; i < shipment.Path.Count; i++)
                {
                    HexCoord coord = shipment.Path[i];
                    if (!visited.Add(coord) || !tiles.TryGetValue(coord, out TileState pathTile) || !IsNetworkTile(pathTile)
                        || (i > 0 && HexCoord.Distance(shipment.Path[i - 1], coord) != 1))
                        throw InvalidSave("shipment path is disconnected, repeated, or blocked");
                }
                TileState source = tiles[shipment.Path[0]];
                if (!sources.Add(source.Coord) || source.WorkProgress != 0 || source.InputOreReserved != 0)
                    throw InvalidSave("a source has duplicate output or another job during delivery");
                if (shipment.Resource == ResourceKind.Timber && (source.Building != BuildingKind.Forestry || shipment.Amount != TimberPerTree)
                    || shipment.Resource == ResourceKind.Ore && ((source.Building != BuildingKind.Mine && source.Building != BuildingKind.DeepMine) || shipment.Amount > 2)
                    || shipment.Resource == ResourceKind.Metal && (source.Building != BuildingKind.Smelter || shipment.Amount != 1)
                    || shipment.Resource == ResourceKind.Food && !((source.Building == BuildingKind.Forager && shipment.Amount == 2)
                        || (source.Building == BuildingKind.Farm && shipment.Amount == 6)))
                    throw InvalidSave("shipment contents disagree with its source building");
                if (shipment.Resource == ResourceKind.Timber) timber += shipment.Amount;
                else if (shipment.Resource == ResourceKind.Ore) ore += shipment.Amount;
                else if (shipment.Resource == ResourceKind.Metal) metal += shipment.Amount;
                else food += shipment.Amount;
            }
            if (timber > int.MaxValue || ore > int.MaxValue || metal > int.MaxValue || food > int.MaxValue)
                throw InvalidSave("stock and owned goods would overflow a resource counter");
        }

        private static ArgumentException InvalidSave(string reason) => new ArgumentException("Invalid world save: " + reason + ".", "state");
    }
}
