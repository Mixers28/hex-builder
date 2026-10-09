using System.Collections.Generic;
using AlphaHex.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace AlphaHex.Runtime
{
    /// <summary>Scene adapter for simulation state. The simulation is the only source of production.</summary>
    public sealed class HexWorldView : MonoBehaviour
    {
        public float Radius { get; private set; } = 8f;

        private Simulation _simulation;
        private WorldArt _art;
        private Transform _tilesRoot, _shipmentsRoot, _environment, _citizensRoot;
        private readonly Dictionary<HexCoord, HexTileView> _tiles = new Dictionary<HexCoord, HexTileView>();
        private readonly List<GameObject> _carriers = new List<GameObject>();
        private readonly List<Renderer> _cargoRenderers = new List<Renderer>();
        private readonly List<GameObject> _citizens = new List<GameObject>();
        private readonly List<HexCoord> _citizenHomes = new List<HexCoord>();
        private readonly List<List<HexCoord>> _citizenRoutes = new List<List<HexCoord>>();
        private readonly HashSet<HexCoord> _present = new HashSet<HexCoord>();
        private readonly List<HexCoord> _removed = new List<HexCoord>();
        private HexCoord? _selected;

        public void Initialize(Simulation simulation)
        {
            Clear();
            _simulation = simulation;
            if (_simulation == null) return;
            _art = new WorldArt();
            _tilesRoot = Group("Terrain");
            _shipmentsRoot = Group("Shipments");
            _citizensRoot = Group("Citizens");
            _environment = Group("Coastal backdrop");
            Refresh();
            BuildEnvironment();
        }

        private Transform Group(string groupName)
        {
            var group = new GameObject(groupName).transform;
            group.SetParent(transform, false);
            return group;
        }

        public static Vector3 GridPosition(HexCoord coord)
        {
            return new Vector3(1.732050808f * (coord.Q + coord.R * 0.5f), 0, 1.5f * coord.R);
        }

        public Vector3 WorldPosition(HexCoord coord)
        {
            Vector3 position = GridPosition(coord);
            TileState tile = _simulation == null ? null : _simulation.GetTile(coord);
            position.y = tile == null ? 0.5f : HexTileView.Height(tile);
            position.y += 0.04f;
            return transform.TransformPoint(position);
        }

        public void Refresh()
        {
            if (_simulation == null || _art == null) return;
            _present.Clear();
            float extent = 1f;
            foreach (TileState state in _simulation.State.Tiles)
            {
                _present.Add(state.Coord);
                HexTileView view;
                if (!_tiles.TryGetValue(state.Coord, out view))
                {
                    var go = new GameObject();
                    go.transform.SetParent(_tilesRoot, false);
                    view = go.AddComponent<HexTileView>();
                    view.Initialize(state.Coord, _art);
                    _tiles.Add(state.Coord, view);
                }
                view.Refresh(state, _simulation);
                view.SetSelected(_selected.HasValue && state.Coord.Equals(_selected.Value));
                if (state.Terrain != TerrainKind.Water)
                    extent = Mathf.Max(extent, GridPosition(state.Coord).magnitude + 1.2f);
            }
            _removed.Clear();
            foreach (var pair in _tiles)
                if (!_present.Contains(pair.Key)) _removed.Add(pair.Key);
            foreach (HexCoord coord in _removed)
            {
                _tiles[coord].gameObject.SetActive(false);
                WorldArt.Release(_tiles[coord].gameObject);
                _tiles.Remove(coord);
            }
            Radius = extent;
            AnimateShipments();
            RefreshCitizens();
        }

        public void SetSelected(HexCoord? coord)
        {
            if (_selected.HasValue)
            {
                HexTileView previous;
                if (_tiles.TryGetValue(_selected.Value, out previous)) previous.SetSelected(false);
            }
            _selected = coord;
            if (coord.HasValue)
            {
                HexTileView current;
                if (_tiles.TryGetValue(coord.Value, out current)) current.SetSelected(true);
            }
        }

        public bool TryPick(Ray ray, out HexCoord coord)
        {
            coord = default(HexCoord);
            RaycastHit hit;
            if (!Physics.Raycast(ray, out hit, 1000, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
                return false;
            var tile = hit.collider.GetComponentInParent<HexTileView>();
            if (tile == null || !tile.transform.IsChildOf(_tilesRoot)) return false;
            coord = tile.Coord;
            return true;
        }

        private void Update()
        {
            AnimateShipments();
            AnimateCitizens();
        }

        private void RefreshCitizens()
        {
            _citizenHomes.Clear();
            _citizenRoutes.Clear();
            for (int i = 0; i < Simulation.PioneerCitizens; i++) _citizenHomes.Add(new HexCoord(0, 0));
            foreach (TileState tile in _simulation.State.Tiles)
                for (int resident = 0; resident < tile.Residents; resident++) _citizenHomes.Add(tile.Coord);
            List<HexCoord> workplaces = _simulation.GetStaffedWorkplaces();
            for (int i = 0; i < _citizenHomes.Count; i++)
            {
                List<HexCoord> route = i < workplaces.Count ? _simulation.GetPathToDepot(_citizenHomes[i]) : null;
                if (route != null)
                {
                    List<HexCoord> workRoute = _simulation.GetPathToDepot(workplaces[i]);
                    for (int edge = workRoute.Count - 2; edge >= 0; edge--) route.Add(workRoute[edge]);
                }
                _citizenRoutes.Add(route);
            }
            while (_citizens.Count < _citizenHomes.Count) _citizens.Add(_art.Citizen(_citizensRoot, _citizens.Count));
            AnimateCitizens();
        }

        private void AnimateCitizens()
        {
            if (_simulation == null) return;
            float time = _simulation.State.ElapsedSeconds + (float)_simulation.State.PendingSeconds;
            for (int i = 0; i < _citizens.Count; i++)
            {
                bool active = i < _citizenHomes.Count;
                _citizens[i].SetActive(active);
                if (!active) continue;
                Transform citizen = _citizens[i].transform;
                List<HexCoord> route = _citizenRoutes[i];
                Vector3 position;
                if (route != null && route.Count > 1)
                {
                    float pathPosition = Mathf.PingPong(time * 0.27f + i * 0.59f, route.Count - 1);
                    int from = Mathf.Min(Mathf.FloorToInt(pathPosition), route.Count - 2);
                    Vector3 a = WorldPosition(route[from]);
                    Vector3 b = WorldPosition(route[from + 1]);
                    Vector3 lane = Vector3.Cross((b - a).normalized, Vector3.up) * 0.13f;
                    position = Vector3.Lerp(a, b, pathPosition - from) + lane;
                    Vector3 heading = b - a;
                    heading.y = 0;
                    if (heading.sqrMagnitude > 0.001f) citizen.rotation = Quaternion.LookRotation(heading);
                }
                else
                {
                    float angle = time * 0.13f + i * 2.4f;
                    position = WorldPosition(_citizenHomes[i]) + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * 0.58f;
                }
                citizen.position = position + Vector3.up * (Mathf.Sin(time * 7 + i) * 0.009f);
            }
        }

        public void AnimateShipments()
        {
            if (_simulation == null || _art == null) return;
            var shipments = _simulation.State.Shipments;
            while (_carriers.Count < shipments.Count)
            {
                GameObject truck = _art.Carrier(_shipmentsRoot);
                _carriers.Add(truck);
                _cargoRenderers.Add(truck.transform.Find("Cargo").GetComponent<Renderer>());
            }
            for (int i = 0; i < _carriers.Count; i++)
            {
                bool active = i < shipments.Count && shipments[i].Path != null && shipments[i].Path.Count >= 2;
                GameObject carrier = _carriers[i];
                if (carrier.activeSelf != active) carrier.SetActive(active);
                if (!active) continue;
                var shipment = shipments[i];
                float progress = Mathf.Clamp01(shipment.Elapsed / Mathf.Max(0.0001f, shipment.Duration));
                float pathPosition = progress * (shipment.Path.Count - 1);
                int from = Mathf.Min(Mathf.FloorToInt(pathPosition), shipment.Path.Count - 2);
                Vector3 a = WorldPosition(shipment.Path[from]);
                Vector3 b = WorldPosition(shipment.Path[from + 1]);
                // Anchor the wheels to the road deck rather than the carrier's model origin.
                carrier.transform.position = Vector3.Lerp(a, b, pathPosition - from) - Vector3.up * 0.025f;
                Vector3 heading = b - a;
                heading.y = 0;
                if (heading.sqrMagnitude > 0.00001f) carrier.transform.rotation = Quaternion.LookRotation(heading, Vector3.up);
                Material cargo = _art.CargoMaterial(shipment.Resource);
                if (_cargoRenderers[i].sharedMaterial != cargo) _cargoRenderers[i].sharedMaterial = cargo;
            }
        }

        private void BuildEnvironment()
        {
            float width = Mathf.Max(65f, Radius * 9f);
            GameObject ocean = WorldArt.Primitive("Ocean", PrimitiveType.Plane, _environment,
                new Vector3(0, 0.04f, 0), new Vector3(width / 10f, 1, width / 10f), _art.Water);
            var renderer = ocean.GetComponent<Renderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            // Static glints are environmental decoration; all moving vehicles represent real shipments.
            var random = new System.Random(91374);
            for (int i = 0; i < 72; i++)
            {
                float angle = (float)random.NextDouble() * Mathf.PI * 2;
                float radius = Radius * (1.03f + (float)random.NextDouble() * 1.7f);
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.047f, Mathf.Sin(angle) * radius);
                GameObject glint = WorldArt.Box("Small water glint", _environment, position,
                    new Vector3(0.12f + (float)random.NextDouble() * 0.68f, 0.003f, 0.025f), _art.WaterGlint,
                    Quaternion.Euler(0, -18, 0));
                glint.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
                glint.GetComponent<Renderer>().receiveShadows = false;
            }
        }

        private void Clear()
        {
            foreach (Transform group in new[] { _tilesRoot, _shipmentsRoot, _environment, _citizensRoot })
            {
                if (group == null) continue;
                group.gameObject.SetActive(false);
                WorldArt.Release(group.gameObject);
            }
            _tiles.Clear();
            _carriers.Clear();
            _cargoRenderers.Clear();
            _citizens.Clear();
            _citizenHomes.Clear();
            _citizenRoutes.Clear();
            _selected = null;
            if (_art != null) _art.Dispose();
            _art = null;
            _simulation = null;
            _tilesRoot = _shipmentsRoot = _environment = _citizensRoot = null;
        }

        private void OnDestroy()
        {
            Clear();
        }
    }
}
