using System;
using System.Collections.Generic;
using System.Globalization;

namespace AlphaHex.Core
{
    // Wire DTOs match the released Godot schema. Doubles are validated before narrowing JSON counts.
    [Serializable] public sealed class GodotSaveRecord
    {
        public double schema = double.NaN, seed = double.NaN, clock = double.NaN, remainder = double.NaN;
        public double population = double.NaN, migration = double.NaN, meal = double.NaN, forage = double.NaN;
        public double research_progress = double.NaN, expansions = double.NaN, legacy_research_points = double.NaN;
        public bool hungry;
        public GodotStock stock;
        public string[] research;
        public string research_job;
        public GodotTile[] tiles;
        public GodotCargo[] shipments;
    }
    [Serializable] public sealed class GodotStock
    {
        public double timber = double.NaN, ore = double.NaN, metal = double.NaN, food = double.NaN, stone = double.NaN;
    }
    [Serializable] public sealed class GodotTile
    {
        public double q = double.NaN, r = double.NaN, height = double.NaN, trees = double.NaN;
        public double ore = double.NaN, initial_ore = double.NaN, deep = double.NaN, initial_deep = double.NaN;
        public double stone = double.NaN, initial_stone = double.NaN, workers = double.NaN, residents = double.NaN;
        public double reserved = double.NaN, tower_level = double.NaN, surveyed_level = double.NaN, work = double.NaN, sapling = double.NaN;
        public string terrain, building;
        public bool claimed, road, paused;
    }
    [Serializable] public sealed class GodotCargo
    {
        public string source, resource;
        public double amount = double.NaN, elapsed = double.NaN, duration = double.NaN;
        public string[] path;
    }
    public sealed partial class Simulation
    {
        public static Simulation ImportGodot(GodotSaveRecord input)
        {
            if (input == null || input.schema != 3 || input.stock == null || input.tiles == null || input.shipments == null || input.research == null || input.research_job == null)
                throw InvalidSave("incomplete or unsupported Godot record");
            if (!IsFinite(input.clock) || input.clock < 0 || input.clock > 1e12
                || Math.Abs(input.clock / FixedStepSeconds - Math.Round(input.clock / FixedStepSeconds)) > 0.0001)
                throw InvalidSave("Godot clock is not on a simulation step");
            var world = new WorldState { Seed = Count(input.seed, int.MinValue, int.MaxValue),
                SimulationSteps = (long)Math.Round(input.clock / FixedStepSeconds), ElapsedSeconds = (float)input.clock,
                PendingSeconds = input.remainder, ExpansionClaims = Count(input.expansions),
                Stock = new ResourceStock(Count(input.stock.timber), Count(input.stock.ore), Count(input.stock.metal), Count(input.stock.food), Count(input.stock.stone)),
                Town = new TownState { Population = Count(input.population), LegacyResearchPoints = Count(input.legacy_research_points, 0, 3),
                    ImmigrationProgress = Timer(input.migration), MealProgress = Timer(input.meal), ForageCooldown = Timer(input.forage),
                    ResearchJob = GodotResearch(input.research_job), ResearchProgress = Timer(input.research_progress), LastMealMissed = input.hungry } };
            foreach (string research in input.research)
            {
                var kind = GodotResearch(research);
                if (kind == ResearchKind.None || (world.Town.Research & kind) != 0) throw InvalidSave("duplicate or empty research");
                world.Town.Research |= kind;
            }
            if (world.Town.ResearchJob == ResearchKind.Reforestation && world.Town.Population < 8) throw InvalidSave("invalid legacy research population");
            foreach (var old in input.tiles)
            {
                if (old == null) throw InvalidSave("missing Godot tile");
                var tile = new TileState { Coord = new HexCoord(Count(old.q, -WorldRadius, WorldRadius), Count(old.r, -WorldRadius, WorldRadius)),
                    Terrain = GodotTerrain(old.terrain), Building = GodotBuilding(old.building), Elevation = Count(old.height, 0, 5),
                    Claimed = old.claimed, HasRoad = old.road, WorkPaused = old.paused, TreesRemaining = Count(old.trees, 0, 64),
                    InitialOre = Count(old.initial_ore, 0, 1000000), OreRemaining = Count(old.ore, 0, 1000000),
                    DeepOreInitial = Count(old.initial_deep, 0, 1000000), DeepOreRemaining = Count(old.deep, 0, 1000000),
                    InitialStone = Count(old.initial_stone, 0, 1000000), StoneRemaining = Count(old.stone, 0, 1000000),
                    Workers = Count(old.workers, 0, 3), Residents = Count(old.residents, 0, 4),
                    TowerLevel = Count(old.tower_level, 0, 2), SurveyedLevel = Count(old.surveyed_level, 0, 2),
                    InputOreReserved = Count(old.reserved, 0, 2), WorkProgress = Timer(old.work) };
                if (!IsFinite(old.sapling) || old.sapling < -1 || old.sapling >= 90) throw InvalidSave("invalid Godot sapling timer");
                tile.SaplingPlanted = old.sapling >= 0; tile.SaplingAge = tile.SaplingPlanted ? Timer(old.sapling) : 0;
                tile.LegacyOreJob = tile.InputOreReserved > 0; tile.ExcavationDepth = TileDepth(tile);
                world.Tiles.Add(tile);
            }
            foreach (var old in input.shipments)
            {
                if (old == null || old.path == null || old.path.Length < 2 || old.source != old.path[0]) throw InvalidSave("invalid Godot cargo source");
                var cargo = new Shipment { Resource = GodotResource(old.resource), Amount = Count(old.amount, 1, 100000), Elapsed = Timer(old.elapsed), Duration = Timer(old.duration) };
                foreach (string coord in old.path) cargo.Path.Add(ParseGodotCoord(coord));
                world.Shipments.Add(cargo);
            }
            foreach (var tile in world.Tiles) PreserveIndustryUnlock(world, tile.Building);
            // First validate the unmodified region, then add the new finite clay opportunity once.
            new Simulation(world);
            AddLegacyClay(world);
            return new Simulation(world);
        }
        private static int Count(double value, int min = 0, int max = int.MaxValue)
        {
            if (!IsFinite(value) || value < min || value > max || value != Math.Floor(value)) throw InvalidSave("invalid Godot integer count");
            return (int)value;
        }
        private static float Timer(double value)
        {
            if (!IsFinite(value) || value < 0 || value > 1e12) throw InvalidSave("invalid Godot timer");
            return (float)value;
        }
        private static HexCoord ParseGodotCoord(string value)
        {
            var parts = value?.Split(',');
            if (parts == null || parts.Length != 2 || !int.TryParse(parts[0], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int q)
                || !int.TryParse(parts[1], NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out int r)) throw InvalidSave("invalid Godot route coordinate");
            return new HexCoord(q, r);
        }
        private static ResourceKind GodotResource(string value) => value == "timber" ? ResourceKind.Timber : value == "ore" ? ResourceKind.Ore
            : value == "metal" ? ResourceKind.Metal : value == "food" ? ResourceKind.Food : value == "stone" ? ResourceKind.Stone : throw InvalidSave("unknown Godot resource");
        private static TerrainKind GodotTerrain(string value) => value == "grass" ? TerrainKind.Grass : value == "forest" ? TerrainKind.Forest
            : value == "ore" ? TerrainKind.Ore : value == "rock" ? TerrainKind.Rock : value == "stone" ? TerrainKind.Stone
            : value == "sand" ? TerrainKind.Sand : value == "water" ? TerrainKind.Water : throw InvalidSave("unknown Godot terrain");
        private static ResearchKind GodotResearch(string value) => value == "" ? ResearchKind.None : value == "farming" ? ResearchKind.Farming
            : value == "reforestation" ? ResearchKind.Reforestation : value == "deep_mining" ? ResearchKind.DeepMining
            : value == "surveying" ? ResearchKind.Surveying : value == "stoneworking" ? ResearchKind.Stoneworking
            : value == "quarry_tools" ? ResearchKind.QuarryTools : value == "masonry" ? ResearchKind.Masonry : throw InvalidSave("unknown Godot research");
        private static BuildingKind GodotBuilding(string value) => value == "" ? BuildingKind.None : value == "depot" ? BuildingKind.Depot
            : value == "house" ? BuildingKind.House : value == "forager" ? BuildingKind.Forager : value == "logging" ? BuildingKind.Forestry
            : value == "mine" ? BuildingKind.Mine : value == "smelter" ? BuildingKind.Smelter : value == "farm" ? BuildingKind.Farm
            : value == "forester" ? BuildingKind.Forester : value == "deep_mine" ? BuildingKind.DeepMine
            : value == "watchtower" ? BuildingKind.Watchtower : value == "quarry" ? BuildingKind.Quarry : throw InvalidSave("unknown Godot building");
    }
}
