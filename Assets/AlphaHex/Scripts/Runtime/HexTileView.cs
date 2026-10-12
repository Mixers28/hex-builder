using AlphaHex.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlphaHex.Runtime
{
    /// <summary>Owns the mesh/collider for a tile and replaces only changed visual groups.</summary>
    public sealed class HexTileView : MonoBehaviour
    {
        public HexCoord Coord { get; private set; }
        public float SurfaceHeight { get; private set; }

        private WorldArt _art;
        private MeshFilter _ground;
        private MeshRenderer _renderer;
        private MeshCollider _collider;
        private Mesh _ownedMesh;
        private Transform _details, _roads;
        private GameObject _selection;
        private int _terrainSignature = int.MinValue, _detailSignature = int.MinValue, _roadSignature = int.MinValue;

        private static readonly HexCoord[] Directions =
        {
            new HexCoord(1, 0), new HexCoord(0, 1), new HexCoord(-1, 1),
            new HexCoord(-1, 0), new HexCoord(0, -1), new HexCoord(1, -1)
        };

        internal void Initialize(HexCoord coord, WorldArt art)
        {
            Coord = coord;
            _art = art;
            name = "Hex " + coord.Q + ", " + coord.R;
            transform.localPosition = HexWorldView.GridPosition(coord);
            _ground = gameObject.AddComponent<MeshFilter>();
            _renderer = gameObject.AddComponent<MeshRenderer>();
            _collider = gameObject.AddComponent<MeshCollider>();
            _selection = WorldArt.MeshObject("Selected outline", transform, art.SelectionRing, art.Selection);
            var selectionRenderer = _selection.GetComponent<Renderer>();
            selectionRenderer.shadowCastingMode = ShadowCastingMode.Off;
            selectionRenderer.receiveShadows = false;
            _selection.SetActive(false);
        }

        internal void Refresh(TileState state, Simulation simulation)
        {
            bool quarry = state.Building == BuildingKind.Mine || state.Building == BuildingKind.DeepMine || state.Building == BuildingKind.Quarry || state.ExcavationDepth > 0;
            SurfaceHeight = Height(state);
            int terrainSignature;
            int detailSignature;
            unchecked
            {
                terrainSignature = (int)state.Terrain;
                terrainSignature = terrainSignature * 31 + state.Elevation;
                terrainSignature = terrainSignature * 31 + state.ExcavationDepth;
                terrainSignature = terrainSignature * 31 + (state.Claimed ? 1 : 0);
                terrainSignature = terrainSignature * 31 + (quarry ? 1 : 0);
                detailSignature = terrainSignature * 31 + (int)state.Building;
                detailSignature = detailSignature * 31 + Mathf.Clamp(state.TreesRemaining, 0, 12);
                detailSignature = detailSignature * 31 + OreVisualCount(state);
                detailSignature = detailSignature * 31 + state.StoneRemaining / 10;
                detailSignature = detailSignature * 31 + state.ClayRemaining / 12;
                detailSignature = detailSignature * 31 + state.TowerLevel;
                detailSignature = detailSignature * 31 + (state.SaplingPlanted ? 1 + (int)(state.SaplingAge / 15) : 0);
            }

            if (_terrainSignature != terrainSignature)
            {
                _terrainSignature = terrainSignature;
                _collider.sharedMesh = null;
                _ground.sharedMesh = null;
                WorldArt.Release(_ownedMesh);
                _ownedMesh = WorldArt.BuildTerrain(SurfaceHeight, state.ExcavationDepth, quarry);
                _ground.sharedMesh = _ownedMesh;
                _renderer.sharedMaterials = new[] { _art.Surface(state.Terrain, state.Claimed), _art.Cliff, _art.PitFloor };
                bool land = state.Terrain != TerrainKind.Water;
                _renderer.enabled = true;
                _collider.enabled = land;
                if (land) _collider.sharedMesh = _ownedMesh;
                _selection.transform.localPosition = new Vector3(0, SurfaceHeight + 0.018f, 0);
            }

            if (_detailSignature != detailSignature)
            {
                _detailSignature = detailSignature;
                ReplaceGroup(ref _details, "Structures and resources");
                _details.localPosition = new Vector3(0, SurfaceHeight, 0);
                if (state.Terrain != TerrainKind.Water) BuildDetails(state);
            }

            int roadSignature = GetRoadSignature(state, simulation);
            if (_roadSignature != roadSignature)
            {
                _roadSignature = roadSignature;
                ReplaceGroup(ref _roads, "Connected roads");
                if (state.Terrain != TerrainKind.Water) BuildRoads(state, simulation);
            }
        }

        internal static float Height(TileState state)
        {
            if (state.Terrain == TerrainKind.Water) return .08f;
            return Mathf.Max(0.22f, 0.50f + state.Elevation * 0.28f - state.ExcavationDepth * 0.12f);
        }

        private static int OreVisualCount(TileState state)
        {
            if (state.OreRemaining <= 0) return 0;
            return Mathf.Clamp(Mathf.CeilToInt(6f * state.OreRemaining / Mathf.Max(1, state.InitialOre)), 1, 6);
        }

        private void BuildDetails(TileState state)
        {
            int seed = unchecked(Coord.Q * 73856093 ^ Coord.R * 19349663);
            var random = new System.Random(seed);
            int trees = Mathf.Clamp(state.TreesRemaining, 0, 12);
            for (int i = 0; i < trees; i++)
            {
                float angle = (i * 137.5f + (float)random.NextDouble() * 22f) * Mathf.Deg2Rad;
                float radius = state.Building == BuildingKind.None
                    ? 0.55f + (i % 3) * 0.52f : 1.25f + (i % 2) * 0.35f;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius);
                float height = 1.05f + (float)random.NextDouble() * 0.60f;
                _art.Tree(_details, position, height, i + Mathf.Abs(seed % 9));
            }

            int ore = OreVisualCount(state);
            if (state.Building != BuildingKind.Mine && state.Building != BuildingKind.DeepMine)
            {
                int rocks = state.Terrain == TerrainKind.Rock && state.Building == BuildingKind.None ? 5 : ore;
                for (int i = 0; i < rocks; i++)
                {
                    float angle = (i * 137.5f + seed % 45) * Mathf.Deg2Rad;
                    float radius = 0.12f + i * 0.085f;
                    float size = 0.27f + (float)random.NextDouble() * 0.22f;
                    _art.Boulder(_details, new Vector3(Mathf.Cos(angle) * radius, 0, Mathf.Sin(angle) * radius),
                        new Vector3(size, size * (i == 0 ? 1.7f : 1f), size), i,
                        ore > 0 && i % 3 == 0 ? _art.Ochre : _art.Cliff);
                }
            }
            else
            {
                for (int i = 0; i < ore; i++)
                    _art.Boulder(_details, new Vector3(-0.34f + (i % 3) * 0.11f, 0, -0.65f + (i / 3) * 0.10f),
                        new Vector3(0.15f, 0.13f, 0.15f), i, i % 2 == 0 ? _art.Ochre : _art.Cliff);
            }

            if (state.StoneRemaining > 0 || state.ClayRemaining > 0)
                for (int i = 0; i < 6; i++) _art.Boulder(_details,
                    new Vector3(Mathf.Cos(i * 2.4f) * 1.3f, 0, Mathf.Sin(i * 2.4f) * 1.3f),
                    state.StoneRemaining > 0 ? new Vector3(.7f, .8f, .7f) : new Vector3(.75f, .18f, .6f), i,
                    state.StoneRemaining > 0 ? _art.Cliff : _art.Clay);
            if (state.SaplingPlanted)
                _art.Tree(_details, new Vector3(0.57f, 0, -0.35f), 0.18f + 0.10f * (int)(state.SaplingAge / 15), 2);

            if (state.Building != BuildingKind.None)
            {
                var buildingRoot = new GameObject(state.Building.ToString()).transform;
                buildingRoot.SetParent(_details, false);
                buildingRoot.localScale = Vector3.one * 1.6f;
                _art.Building(buildingRoot, state.Building, state.TowerLevel);
            }
            else if (trees == 0 && ore == 0 && state.Terrain == TerrainKind.Grass)
            {
                for (int i = 0; i < 2; i++)
                {
                    float x = (float)random.NextDouble() * 1.1f - 0.55f;
                    float z = (float)random.NextDouble() * 1.1f - 0.55f;
                    _art.Boulder(_details, new Vector3(x, 0, z), new Vector3(0.07f, 0.04f, 0.06f), i);
                }
            }
        }

        private int GetRoadSignature(TileState state, Simulation simulation)
        {
            unchecked
            {
                int signature = state.HasRoad ? 1 : 0;
                signature = signature * 31 + (int)state.Building;
                signature = signature * 31 + state.Elevation;
                signature = signature * 31 + state.ExcavationDepth;
                foreach (HexCoord direction in Directions)
                {
                    TileState neighbor = simulation.GetTile(new HexCoord(Coord.Q + direction.Q, Coord.R + direction.R));
                    int connection = neighbor != null && neighbor.Claimed &&
                                     (neighbor.HasRoad || neighbor.Building != BuildingKind.None) ? 1 : 0;
                    signature = signature * 31 + connection;
                    signature = signature * 31 + (neighbor == null ? 0 : neighbor.Elevation * 7 - neighbor.ExcavationDepth);
                }
                return signature;
            }
        }

        private void BuildRoads(TileState state, Simulation simulation)
        {
            if (!state.HasRoad && state.Building == BuildingKind.None) return;
            Vector3 localCenter = new Vector3(0, SurfaceHeight + 0.025f, 0);
            foreach (HexCoord direction in Directions)
            {
                HexCoord coord = new HexCoord(Coord.Q + direction.Q, Coord.R + direction.R);
                TileState neighbor = simulation.GetTile(coord);
                if (neighbor == null || !neighbor.Claimed || neighbor.Terrain == TerrainKind.Water ||
                    (!neighbor.HasRoad && neighbor.Building == BuildingKind.None)) continue;
                Vector3 delta = HexWorldView.GridPosition(coord) - HexWorldView.GridPosition(Coord);
                Vector3 endpoint = delta * 0.5f;
                endpoint.y = (SurfaceHeight + Height(neighbor)) * 0.5f + 0.025f;
                Vector3 start = localCenter;
                if (state.Building != BuildingKind.None)
                {
                    start += new Vector3(delta.x, 0, delta.z).normalized * 0.84f;
                    if (state.Building == BuildingKind.Mine || state.Building == BuildingKind.DeepMine) start += delta.normalized * 0.10f;
                }
                WorldArt.Beam("Haul road", _roads, start, endpoint, 0.30f, 0.025f, _art.Road);
            }
            if (state.HasRoad && state.Building == BuildingKind.None)
                WorldArt.Primitive("Road junction", PrimitiveType.Cylinder, _roads,
                    localCenter, new Vector3(0.22f, 0.013f, 0.22f), _art.Road);
        }

        private void ReplaceGroup(ref Transform group, string groupName)
        {
            if (group != null)
            {
                group.gameObject.SetActive(false);
                WorldArt.Release(group.gameObject);
            }
            group = new GameObject(groupName).transform;
            group.SetParent(transform, false);
        }

        public void SetSelected(bool selected)
        {
            if (_selection != null) _selection.SetActive(selected && _renderer.enabled);
        }

        private void OnDestroy()
        {
            WorldArt.Release(_ownedMesh);
        }
    }
}
