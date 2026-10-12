using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public enum TerrainKind { Grass, Forest, Rock, Ore, Sand, Water, Stone, Clay }
    public enum BuildingKind { None, Depot, Forestry, Mine, Smelter, House, Forager, Farm, Forester, DeepMine, Watchtower, Quarry, ClayPit, Kiln, Bloomery }
    public enum ResourceKind { Timber, Ore, Metal, Food, Stone, Clay, Brick }
    [Flags]
    public enum ResearchKind { None = 0, Farming = 1, Reforestation = 2, DeepMining = 4, Surveying = 8,
        Stoneworking = 16, QuarryTools = 32, Masonry = 64, Brickmaking = 128, PrimitiveSmelting = 256, Refining = 512 }

    [Serializable]
    public sealed class TownState
    {
        internal TownState Copy() => (TownState)MemberwiseClone();
        public int Population = Simulation.PioneerCitizens;
        public int EarnedMilestones;
        public ResearchKind Research;
        public float ImmigrationProgress;
        public float MealProgress;
        public float ForageCooldown;
        public bool LastMealMissed;
        public ResearchKind ResearchJob;
        public float ResearchProgress;
        public int LegacyResearchPoints;
    }

    [Serializable]
    public sealed class ResourceStock
    {
        public int Timber;
        public int Ore;
        public int Metal;
        public int Food;
        public int Stone;
        public int Clay;
        public int Brick;

        public ResourceStock() { }

        public ResourceStock(int timber, int ore, int metal, int food = 0, int stone = 0, int clay = 0, int brick = 0)
        {
            Timber = timber;
            Ore = ore;
            Metal = metal;
            Food = food;
            Stone = stone;
            Clay = clay;
            Brick = brick;
        }
    }

    [Serializable]
    public sealed class TileState
    {
        internal TileState Copy() => (TileState)MemberwiseClone();
        public HexCoord Coord;
        public TerrainKind Terrain;
        public int Elevation;
        public bool Claimed;
        public bool HasRoad;
        public BuildingKind Building;
        public int TreesRemaining;
        public int OreRemaining;
        public int InitialOre;
        public int ExcavationDepth;
        public float WorkProgress;
        public int Residents;
        public bool WorkPaused;
        public int DeepOreInitial;
        public int DeepOreRemaining;
        public bool SaplingPlanted;
        public float SaplingAge;
        public int Workers;
        public int InitialStone;
        public int StoneRemaining;
        public int InitialClay;
        public int ClayRemaining;
        public int TowerLevel;
        public int SurveyedLevel;
        public bool BatchRequested;
        public bool LegacyOreJob;

        // An active smelting job owns these inputs until it completes or is demolished.
        // This prevents two smelters from spending the same depot ore.
        public int InputOreReserved;
        public int InputTimberReserved;
        public int InputClayReserved;
    }

    [Serializable]
    public sealed class Shipment
    {
        public ResourceKind Resource;
        public int Amount;
        public float Elapsed;
        public float Duration;
        public List<HexCoord> Path = new List<HexCoord>();
    }

    /// <summary>
    /// Complete, versioned save payload. Public fields and lists are intentional:
    /// Unity JsonUtility can serialize this without custom converters or Unity references.
    /// Mutate the world through Simulation, not by changing these fields during play.
    /// </summary>
    [Serializable]
    public sealed class WorldState
    {
        public int SchemaVersion = Simulation.CurrentSchemaVersion;
        public int Seed;
        public int ExpansionClaims;
        public float ElapsedSeconds;
        public ResourceStock Stock = new ResourceStock();
        public List<TileState> Tiles = new List<TileState>();
        public List<Shipment> Shipments = new List<Shipment>();
        public TownState Town = new TownState();

        // Persist the fixed-step clock so a save in the middle of a job or delivery resumes
        // at the same point, including the fraction of time before the next simulation step.
        public long SimulationSteps;
        public double PendingSeconds;
    }
}
