using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public enum TerrainKind { Grass, Forest, Rock, Ore, Sand, Water }
    public enum BuildingKind { None, Depot, Forestry, Mine, Smelter, House, Forager, Farm, Forester, DeepMine }
    public enum ResourceKind { Timber, Ore, Metal, Food }
    [Flags]
    public enum ResearchKind { None = 0, Farming = 1, Reforestation = 2, DeepMining = 4 }

    [Serializable]
    public sealed class TownState
    {
        public int Population = Simulation.PioneerCitizens;
        public int EarnedMilestones;
        public ResearchKind Research;
        public float ImmigrationProgress;
        public float MealProgress;
        public float ForageCooldown;
        public bool LastMealMissed;
    }

    [Serializable]
    public sealed class ResourceStock
    {
        public int Timber;
        public int Ore;
        public int Metal;
        public int Food;

        public ResourceStock() { }

        public ResourceStock(int timber, int ore, int metal, int food = 0)
        {
            Timber = timber;
            Ore = ore;
            Metal = metal;
            Food = food;
        }
    }

    [Serializable]
    public sealed class TileState
    {
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

        // An active smelting job owns these inputs until it completes or is demolished.
        // This prevents two smelters from spending the same depot ore.
        public int InputOreReserved;
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
