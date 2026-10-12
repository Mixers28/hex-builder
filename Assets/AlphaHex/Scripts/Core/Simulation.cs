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
        public const int CurrentSchemaVersion = 4;
        public const int WorldRadius = 16;
        public const int MaxExcavationDepth = 3;
        public const int TimberPerTree = 3;
        public const int OrePerMetal = 2;
        public const float ForestrySeconds = 12f;
        public const float MineSeconds = 15f;
        public const float SmelterSeconds = 18f;
        public const float DeliverySecondsPerEdge = 1.25f;
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
            foreach (TileState tile in _orderedTiles)
                _tiles.Add(tile.Coord, tile);
        }

        public static Simulation Create(int seed = 417)
        {
            var state = new WorldState { Seed = seed, Stock = new ResourceStock(80, 0, 0, StartingFood) };
            for (int q = -3; q <= 3; q++) for (int r = -3; r <= 3; r++)
            {
                var coord = new HexCoord(q, r);
                if (HexCoord.Distance(coord, DepotCoord) > 3) continue;
                var tile = GenerateTile(seed, coord);
                tile.Claimed = HexCoord.Distance(coord, DepotCoord) <= 1;
                if (coord == DepotCoord) { tile.Building = BuildingKind.Depot; tile.HasRoad = true; }
                state.Tiles.Add(tile);
            }
            return new Simulation(state);
        }

        public TileState GetTile(HexCoord coord) => _tiles.TryGetValue(coord, out TileState tile) ? tile : null;

        public static ResourceStock GetBuildCost(BuildingKind building)
        {
            switch (building)
            {
                case BuildingKind.Forestry: return new ResourceStock(12, 0, 0);
                case BuildingKind.Mine: return new ResourceStock(16, 0, 0, brick: 6);
                case BuildingKind.Smelter: return new ResourceStock(24, 0, 4, brick: 16);
                case BuildingKind.House: return new ResourceStock(10, 0, 0);
                case BuildingKind.Forager: return new ResourceStock(8, 0, 0);
                case BuildingKind.Farm: return new ResourceStock(18, 0, 0, brick: 6);
                case BuildingKind.Forester: return new ResourceStock(18, 0, 0, clay: 4);
                case BuildingKind.DeepMine: return new ResourceStock(26, 0, 8);
                case BuildingKind.Watchtower: return new ResourceStock(24, 0, 4);
                case BuildingKind.Quarry: return new ResourceStock(22, 0, 6);
                case BuildingKind.ClayPit: return new ResourceStock(12, 0, 0);
                case BuildingKind.Kiln: return new ResourceStock(16, 0, 0, clay: 6);
                case BuildingKind.Bloomery: return new ResourceStock(16, 0, 0, brick: 8);
                default: return new ResourceStock();
            }
        }

        public bool TryClaim(HexCoord coord, out string message)
        {
            TileState tile = GetTile(coord);
            if (tile == null) return Fail("Explore this land with a connected watchtower first.", out message);
            if (tile.Terrain == TerrainKind.Water) return Fail("Water cannot be claimed.", out message);
            if (tile.Claimed) return Fail("You already own this tile.", out message);

            bool touchesClaim = false;
            foreach (HexCoord neighbor in coord.Neighbors())
            {
                TileState adjacent = GetTile(neighbor);
                if (adjacent != null && adjacent.Claimed) { touchesClaim = true; break; }
            }
            if (!touchesClaim) return Fail("Claim a tile next to your existing land.", out message);
            if (!CanAfford(6, 0, 0)) return Fail("Claiming costs 6 timber.", out message);

            Spend(6, 0, 0);
            tile.Claimed = true;
            State.ExpansionClaims++;
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

            if (building == BuildingKind.Quarry && tile.StoneRemaining <= 0) return Fail("A quarry needs a stone deposit.", out message);
            if (building == BuildingKind.ClayPit && tile.ClayRemaining <= 0) return Fail("A clay pit needs remaining clay.", out message);
            if (building != BuildingKind.ClayPit && tile.ClayRemaining > 0) return Fail("Gather the surface clay before building here.", out message);
            if (building != BuildingKind.Quarry && tile.StoneRemaining > 0) return Fail("Choose land without a stone deposit.", out message);
            if ((building == BuildingKind.Kiln || building == BuildingKind.Bloomery || building == BuildingKind.Watchtower)
                && (tile.TreesRemaining > 0 || tile.OreRemaining > 0 || tile.SaplingPlanted)) return Fail("Choose clear land.", out message);
            ResourceStock cost = GetBuildCost(building);
            if (!CanAfford(cost))
                return Fail(BuildingName(building) + " needs " + CostText(cost) + ".", out message);

            Spend(cost);
            tile.Building = building;
            tile.WorkProgress = 0;
            tile.InputOreReserved = 0;
            tile.WorkPaused = false;
            tile.Workers = 0;
            tile.TowerLevel = building == BuildingKind.Watchtower ? 1 : 0;
            _networkDirty = true;
            RevealConnectedTowers();
            message = BuildingName(building) + " built. " + (GetPathToDepot(coord) == null
                ? "It needs a road connection to the depot." : "It is connected." + (NeedsWorker(building) ? " Assign a crew to begin." : ""));
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
            RevealConnectedTowers();
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
                RefundInputs(tile);
                tile.BatchRequested = false;
                tile.TowerLevel = tile.SurveyedLevel = 0;
                tile.Workers = 0;
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
            if (tile == null) return "Uncharted land. Build a connected watchtower to reveal it.";
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
                if (tile.Building == BuildingKind.Watchtower) return "Survey radius " + (tile.TowerLevel == 2 ? 6 : 4) + ". Discoveries remain visible. Research masonry to reinforce this tower.";
                if (GetWorkersAssigned(coord) == 0) return "Assign a crew here. New citizens stay available until you choose their workplace.";
                if (tile.Building == BuildingKind.Forester && FindPlantingSite(tile) == null)
                    return "No planting space. Claim adjacent grass or forest; occupied sites, quarries and full forests cannot be replanted.";
                if (tile.Building == BuildingKind.Bloomery && !tile.BatchRequested) return "Start a batch: 3 ore + 3 timber → 1 metal in 90 s, plus delivery. One assigned worker required.";
                if (IsProcessor(tile.Building) && !HasInputs(tile) && !CanAfford(InputCost(tile.Building)))
                    return "Waiting for " + CostText(InputCost(tile.Building)) + ". Reserved inputs belong to their current jobs.";
                if (tile.Building != BuildingKind.Forester && !CanReceive(OutputResource(tile.Building), OutputAmount(tile)))
                    return "Waiting: depot capacity is full, including cargo and reserved input. Spend resources to resume.";
                return BuildingName(tile.Building) + ": "
                    + Math.Floor(tile.WorkProgress / JobSeconds(tile.Building) * 100).ToString(CultureInfo.InvariantCulture)
                    + "%. " + ((JobSeconds(tile.Building) - tile.WorkProgress) / CrewSpeed(tile.Workers)).ToString("0.0", CultureInfo.InvariantCulture)
                    + " s to " + (tile.Building == BuildingKind.Forester ? "plant a sapling. Growth takes 90 s." : "next output.\nDelivery: "
                    + ((path.Count - 1) * DeliverySecondsPerEdge).ToString("0.0", CultureInfo.InvariantCulture) + " s.")
                    + (HasInputs(tile) ? " Inputs reserved for this job." : "");
            }

            if (tile.SaplingPlanted) return "A sapling is growing: " + Math.Max(0, TreeGrowthSeconds - tile.SaplingAge).ToString("0.0", CultureInfo.InvariantCulture) + " s to maturity.";
            if (tile.OreRemaining == 0 && tile.DeepOreRemaining > 0) return "Surface deposit exhausted. " + tile.DeepOreRemaining
                + " deep ore remains. " + (HasResearch(ResearchKind.DeepMining) ? "Build a deep mine to reach it." : "Unlock Deep mining in Town > Research.");
            if (tile.HasRoad) return GetPathToDepot(coord) == null ? "Road is disconnected from the depot." : "Road connected to the depot.";
            if (tile.TreesRemaining > 0) return "Forest: " + tile.TreesRemaining + " trees. Hand gather timber or build forestry.";
            if (tile.OreRemaining > 0) return "Deposit: " + tile.OreRemaining + " ore. Hand mine or build a mine.";
            if (tile.ClayRemaining > 0) return "Clay bed: " + tile.ClayRemaining + " clay. Gather by hand or build a clay pit.";
            if (tile.StoneRemaining > 0) return "Stone outcrop: " + tile.StoneRemaining + ". Research quarrying tools to work it.";
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
            RevealConnectedTowers();
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
                if (tile.Building == BuildingKind.Quarry && tile.StoneRemaining == 0) continue;
                if (tile.Building == BuildingKind.ClayPit && tile.ClayRemaining == 0) continue;
                if (tile.Building == BuildingKind.Bloomery && !tile.BatchRequested) continue;
                List<HexCoord> path = GetPathToDepot(tile.Coord);
                if (path == null) continue;

                if (tile.Building == BuildingKind.Forester)
                {
                    TileState plantingSite = FindPlantingSite(tile);
                    if (plantingSite == null) continue;
                    tile.WorkProgress += (float)FixedStepSeconds * CrewSpeed(tile.Workers);
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

                if (IsProcessor(tile.Building) && !HasInputs(tile))
                {
                    if (!CanAfford(InputCost(tile.Building))) continue;
                    ReserveInputs(tile);
                }

                tile.WorkProgress += (float)FixedStepSeconds * CrewSpeed(tile.Workers);
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
                else if (tile.Building == BuildingKind.Quarry)
                { tile.StoneRemaining -= amount; tile.ExcavationDepth = TileDepth(tile); if (tile.StoneRemaining == 0) tile.Terrain = TerrainKind.Rock; }
                else if (tile.Building == BuildingKind.ClayPit)
                { tile.ClayRemaining -= amount; if (tile.ClayRemaining == 0) tile.Terrain = TerrainKind.Grass; }
                else ClearInputs(tile);
                tile.BatchRequested = false;

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
            || tile.Building == BuildingKind.DeepMine && tile.DeepOreRemaining == 0
            || tile.Building == BuildingKind.Quarry && tile.StoneRemaining == 0
            || tile.Building == BuildingKind.ClayPit && tile.ClayRemaining == 0;

        private bool TryClearDepletedExtractor(TileState tile)
        {
            if (!IsDepletedExtractor(tile) || FindShipment(tile.Coord) != null) return false;
            if (tile.Building == BuildingKind.Forestry && IsManagedForest(tile)) return false;
            tile.Building = BuildingKind.None;
            tile.WorkProgress = 0;
            tile.WorkPaused = false;
            tile.Workers = 0;
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
            if (tile == null) return Fail("Explore this land with a connected watchtower first.", out message);
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
            foreach (TileState tile in _orderedTiles) owned += Reserved(tile, resource);
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
                    case ResourceKind.Stone: State.Stock.Stone += amount; break;
                    case ResourceKind.Clay: State.Stock.Clay += amount; break;
                    case ResourceKind.Brick: State.Stock.Brick += amount; break;
                    default: throw new InvalidOperationException("Unknown resource.");
                }
            }
        }

        public static long StockAmount(ResourceStock stock, ResourceKind resource) =>
            resource == ResourceKind.Timber ? stock.Timber : resource == ResourceKind.Ore ? stock.Ore
                : resource == ResourceKind.Metal ? stock.Metal : resource == ResourceKind.Food ? stock.Food
                : resource == ResourceKind.Stone ? stock.Stone : resource == ResourceKind.Clay ? stock.Clay : stock.Brick;

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

        public static float JobSeconds(BuildingKind building) => building == BuildingKind.Forestry ? ForestrySeconds
            : building == BuildingKind.Mine ? MineSeconds : building == BuildingKind.Smelter ? SmelterSeconds
            : building == BuildingKind.Forager ? ForagerSeconds : building == BuildingKind.Farm ? FarmSeconds
            : building == BuildingKind.Forester ? ForesterSeconds : building == BuildingKind.DeepMine ? DeepMineSeconds
            : building == BuildingKind.Quarry ? QuarrySeconds : building == BuildingKind.ClayPit ? ClaySeconds
            : building == BuildingKind.Kiln ? KilnSeconds : building == BuildingKind.Bloomery ? BloomerySeconds : 0;

        public static string BuildingName(BuildingKind building) => building == BuildingKind.Forestry ? "Woodcutter"
            : building == BuildingKind.DeepMine ? "Deep mine" : building == BuildingKind.Forager ? "Forager lodge" : building == BuildingKind.Smelter ? "Foundry"
            : building == BuildingKind.House ? "Cottage" : building == BuildingKind.Depot ? "Village hall"
            : building == BuildingKind.ClayPit ? "Clay pit" : building == BuildingKind.Kiln ? "Brick kiln"
            : building == BuildingKind.Bloomery ? "Primitive bloomery" : building == BuildingKind.Forester ? "Tree nursery"
            : building == BuildingKind.Mine ? "Iron mine" : building == BuildingKind.Farm ? "Farmstead" : building == BuildingKind.Quarry ? "Stone quarry" : building.ToString();

        private static bool Fail(string reason, out string message) { message = reason; return false; }
        private static bool IsFinite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);

    }
}
