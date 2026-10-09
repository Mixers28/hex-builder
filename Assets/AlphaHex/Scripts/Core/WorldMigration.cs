using System;
using System.Collections.Generic;

namespace AlphaHex.Core
{
    public sealed partial class Simulation
    {
        /// <summary>Version-one worlds receive pioneers, provisions and untouched deep reserves once.
        /// Copy before upgrading: rejected loads never alter the caller's world or its backup.</summary>
        public static Simulation Restore(WorldState state)
        {
            if (state == null || state.SchemaVersion != 1) return new Simulation(state);
            if (state.Stock == null || state.Tiles == null || state.Shipments == null)
                throw InvalidSave("legacy world is incomplete");
            var upgraded = new WorldState
            {
                SchemaVersion = CurrentSchemaVersion,
                Seed = state.Seed,
                ElapsedSeconds = state.ElapsedSeconds,
                SimulationSteps = state.SimulationSteps,
                PendingSeconds = state.PendingSeconds,
                Stock = new ResourceStock(state.Stock.Timber, state.Stock.Ore, state.Stock.Metal, StartingFood),
                Town = new TownState { Population = PioneerCitizens }
            };
            foreach (TileState tile in state.Tiles)
            {
                if (tile == null || tile.Building < BuildingKind.None || tile.Building > BuildingKind.Smelter)
                    throw InvalidSave("legacy building type is invalid");
                upgraded.Tiles.Add(new TileState
                {
                    Coord = tile.Coord, Terrain = tile.Terrain, Elevation = tile.Elevation, Claimed = tile.Claimed,
                    HasRoad = tile.HasRoad, Building = tile.Building, TreesRemaining = tile.TreesRemaining,
                    OreRemaining = tile.OreRemaining, InitialOre = tile.InitialOre, ExcavationDepth = tile.ExcavationDepth,
                    WorkProgress = tile.WorkProgress, InputOreReserved = tile.InputOreReserved,
                    DeepOreInitial = tile.InitialOre * 2, DeepOreRemaining = tile.InitialOre * 2
                });
            }
            foreach (Shipment shipment in state.Shipments)
            {
                if (shipment == null || shipment.Path == null || shipment.Resource < ResourceKind.Timber || shipment.Resource > ResourceKind.Metal)
                    throw InvalidSave("legacy cargo is invalid");
                upgraded.Shipments.Add(new Shipment
                {
                    Resource = shipment.Resource, Amount = shipment.Amount, Elapsed = shipment.Elapsed,
                    Duration = shipment.Duration, Path = new List<HexCoord>(shipment.Path)
                });
            }
            return new Simulation(upgraded);
        }
    }
}
