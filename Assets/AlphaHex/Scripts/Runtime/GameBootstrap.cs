using System;
using System.Collections.Generic;
using AlphaHex.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;

namespace AlphaHex.Runtime
{
    public enum WorldTool { Inspect, Claim, Road, Forestry, Mine, Smelter, Home }

    [DefaultExecutionOrder(-100)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private int startingSeed = 417;
        [SerializeField] private bool loadSavedWorld = true;
        public Simulation Simulation { get; private set; }
        public HexCoord? Selected { get; private set; }
        public WorldTool Tool { get; private set; }
        public bool Paused { get; private set; }
        public int Speed { get; private set; } = 1;
        public string Notice { get; private set; } = "Select a tile to inspect your settlement.";
        public uint NoticeRevision { get; private set; }

        private HexWorldView world;
        private OrbitCamera orbit;
        private Camera gameCamera;
        private GameHud hud;
        private GameObject preview;
        private Material previewMaterial;
        private float nextRefresh;
        private float nextAutosave;
        private bool pointerArmed;
        private Vector2 pointerStart;
        private int trackedFinger = -1;
        private static readonly List<RaycastResult> UiHits = new List<RaycastResult>();

        private void Awake()
        {
            Application.targetFrameRate = 60;
            Screen.sleepTimeout = SleepTimeout.SystemSetting;
            Simulation = AlphaHex.Core.Simulation.Create(startingSeed);
            if (loadSavedWorld && SaveService.TryLoad(out Simulation restored, out string loadMessage))
            {
                Simulation = restored;
                Notice = loadMessage;
            }
            else if (loadSavedWorld && System.IO.File.Exists(SaveService.SavePath))
            {
                Notice = "The save could not be read. A fresh island is open; the old file is still on disk.";
                // Do not overwrite an unreadable save through automatic saving this session.
                nextAutosave = float.PositiveInfinity;
            }

            CreateLighting();
            world = new GameObject("Hex World").AddComponent<HexWorldView>();
            world.Initialize(Simulation);
            gameCamera = Camera.main;
            if (gameCamera == null)
            {
                var cameraObject = new GameObject("Main Camera");
                cameraObject.tag = "MainCamera";
                gameCamera = cameraObject.AddComponent<Camera>();
            }
            gameCamera.clearFlags = CameraClearFlags.SolidColor;
            gameCamera.backgroundColor = new Color(0.66f, 0.76f, 0.79f);
            gameCamera.fieldOfView = 42;
            gameCamera.nearClipPlane = 0.1f;
            gameCamera.farClipPlane = 180;
            if (gameCamera.GetComponent<AudioListener>() == null) gameCamera.gameObject.AddComponent<AudioListener>();
            orbit = gameCamera.gameObject.AddComponent<OrbitCamera>();
            orbit.Initialize(Vector3.zero, world.Radius);

            hud = new GameObject("Game HUD").AddComponent<GameHud>();
            hud.Initialize(this);
            CreatePlacementPreview();
            Select(new HexCoord(0, 0), false);
            if (!float.IsPositiveInfinity(nextAutosave)) nextAutosave = Time.unscaledTime + 30;
        }

        private void CreateLighting()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.58f, 0.69f, 0.78f);
            RenderSettings.ambientEquatorColor = new Color(0.43f, 0.48f, 0.49f);
            RenderSettings.ambientGroundColor = new Color(0.20f, 0.25f, 0.28f);
            var lightObject = new GameObject("Afternoon Sun");
            Light sun = lightObject.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1.0f, 0.91f, 0.77f);
            sun.intensity = 1.5f;
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.65f;
            sun.shadowBias = 0.05f;
            sun.transform.rotation = Quaternion.Euler(48, -32, 0);
            RenderSettings.sun = sun;
        }

        private void CreatePlacementPreview()
        {
            preview = GameObject.CreatePrimitive(PrimitiveType.Cube);
            preview.name = "Proposed Building Footprint";
            var collider = preview.GetComponent<Collider>();
            collider.enabled = false;
            Destroy(collider);
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
            previewMaterial = new Material(shader);
            previewMaterial.color = new Color(0.18f, 0.61f, 1f);
            Renderer renderer = preview.GetComponent<Renderer>();
            renderer.sharedMaterial = previewMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            preview.transform.localScale = new Vector3(0.9f, 0.045f, 0.9f);
            preview.SetActive(false);
        }

        private void Update()
        {
            if (Simulation == null) return;
            orbit.PointerOverUI = PointerOverUI();
            if (!Paused) Simulation.Tick(Mathf.Min(Time.unscaledDeltaTime, 0.25f) * Speed);
            ReadPointer();
            ReadShortcuts();
            if (Time.unscaledTime >= nextRefresh)
            {
                nextRefresh = Time.unscaledTime + 0.15f;
                Refresh();
            }
            if (Time.unscaledTime >= nextAutosave)
            {
                nextAutosave = Time.unscaledTime + 30;
                if (!SaveService.TrySave(Simulation.State, out string error))
                    SetNotice(error);
            }
        }

        private static bool PointerOverUI()
        {
            if (EventSystem.current == null) return false;
            if (Input.touchCount > 0)
            {
                for (int i = 0; i < Input.touchCount; i++)
                    if (HitUI(Input.GetTouch(i).position)) return true;
                return false;
            }
            return HitUI(Input.mousePosition);
        }

        private static bool HitUI(Vector2 position)
        {
            UiHits.Clear();
            var pointer = new PointerEventData(EventSystem.current) { position = position };
            EventSystem.current.RaycastAll(pointer, UiHits);
            return UiHits.Count > 0;
        }

        private void ReadPointer()
        {
            if (Input.touchCount > 1)
            {
                pointerArmed = false;
                trackedFinger = -1;
                return;
            }
            if (Input.touchCount == 1)
            {
                Touch touch = Input.GetTouch(0);
                if (touch.phase == TouchPhase.Began)
                {
                    trackedFinger = touch.fingerId;
                    pointerStart = touch.position;
                    pointerArmed = !PointerOverUI();
                }
                else if (touch.phase == TouchPhase.Ended || touch.phase == TouchPhase.Canceled)
                {
                    if (touch.phase == TouchPhase.Ended && touch.fingerId == trackedFinger)
                        TrySelectFromPointer(touch.position);
                    pointerArmed = false;
                    trackedFinger = -1;
                }
                return;
            }
            if (Input.GetMouseButtonDown(0))
            {
                pointerStart = Input.mousePosition;
                pointerArmed = !PointerOverUI();
            }
            if (Input.GetMouseButtonUp(0))
            {
                TrySelectFromPointer(Input.mousePosition);
                pointerArmed = false;
            }
        }

        private void TrySelectFromPointer(Vector2 position)
        {
            if (!pointerArmed || orbit.WasDragging || PointerOverUI() || Vector2.Distance(position, pointerStart) > 12)
                return;
            if (world.TryPick(gameCamera.ScreenPointToRay(position), out HexCoord coord)) Select(coord);
        }

        private void ReadShortcuts()
        {
            if (Input.GetKeyDown(KeyCode.F5)) SaveGame();
            if (Input.GetKeyDown(KeyCode.F9)) LoadGame();
            if (Input.GetKeyDown(KeyCode.Space)) TogglePause();
            if (Input.GetKeyDown(KeyCode.I)) hud.ToggleInspector();
            if (Input.GetKeyDown(KeyCode.M)) hud.ToggleMenu();
            if (Input.GetKeyDown(KeyCode.G)) FocusObjective();
            if (Input.GetKeyDown(KeyCode.Escape)) { hud.CloseMenu(); SetTool(WorldTool.Inspect); }
            for (int i = 0; i <= (int)WorldTool.Home; i++)
                if (Input.GetKeyDown((KeyCode)((int)KeyCode.Alpha1 + i))) SetTool((WorldTool)i);
        }

        public void Select(HexCoord coord, bool inspect = true)
        {
            Selected = coord;
            world.SetSelected(coord);
            if (inspect) hud.ShowInspector();
            Refresh();
        }

        public void SetTool(WorldTool tool)
        {
            if (tool == WorldTool.Home)
            {
                orbit.Initialize(Vector3.zero, world.Radius);
                Tool = WorldTool.Inspect;
                hud.HideInspector();
                Select(new HexCoord(0, 0), false);
                return;
            }
            Tool = tool;
            if (tool != WorldTool.Inspect) hud.ShowInspector();
            Refresh();
        }

        public void ApplyTool()
        {
            if (!Selected.HasValue) { SetNotice("Select a tile first."); return; }
            HexCoord coord = Selected.Value;
            string message;
            switch (Tool)
            {
                case WorldTool.Claim: Simulation.TryClaim(coord, out message); break;
                case WorldTool.Road: Simulation.TryBuildRoad(coord, out message); break;
                case WorldTool.Forestry: Simulation.TryBuild(coord, BuildingKind.Forestry, out message); break;
                case WorldTool.Mine: Simulation.TryBuild(coord, BuildingKind.Mine, out message); break;
                case WorldTool.Smelter: Simulation.TryBuild(coord, BuildingKind.Smelter, out message); break;
                default: message = "Choose a build or expansion tool below."; break;
            }
            SetNotice(message);
        }

        public void GatherSelected()
        {
            if (!Selected.HasValue) return;
            TileState tile = Simulation.GetTile(Selected.Value);
            string message;
            if (tile != null && tile.TreesRemaining > 0) Simulation.TryHarvest(tile.Coord, out message);
            else Simulation.TryExcavate(Selected.Value, out message);
            SetNotice(message);
        }

        public void DemolishSelected()
        {
            if (!Selected.HasValue) return;
            Simulation.TryDemolish(Selected.Value, out string message);
            SetNotice(message);
        }

        public void TogglePause() { Paused = !Paused; Refresh(); }
        public void CycleSpeed() { Speed = Speed == 1 ? 2 : 1; Refresh(); }

        public void SaveGame()
        {
            if (SaveService.TrySave(Simulation.State, out string message))
                nextAutosave = Time.unscaledTime + 30;
            SetNotice(message);
        }

        public void LoadGame()
        {
            if (!SaveService.TryLoad(out Simulation restored, out string message))
            {
                SetNotice(message);
                return;
            }
            ReplaceWorld(restored);
            SetNotice(message);
        }

        public void StartNewWorld()
        {
            // Explicit in-game confirmation comes from the HUD. This archive survives ordinary autosaves.
            if (!SaveService.TryArchive(Simulation.State, out string message))
            {
                SetNotice("The current island could not be archived. " + message);
                return;
            }
            int seed = Simulation.State.Seed == int.MaxValue ? 417 : Simulation.State.Seed + 1;
            Simulation candidate = AlphaHex.Core.Simulation.Create(seed);
            if (!SaveService.TrySave(candidate.State, out message))
            {
                SetNotice(message);
                return;
            }
            ReplaceWorld(candidate);
            SetNotice("A new island is ready. Use Previous to return to the island you just left.");
        }

        public void LoadPreviousWorld()
        {
            if (!SaveService.TryLoadPrevious(out Simulation previous, out string message))
            {
                SetNotice(message);
                return;
            }
            ReplaceWorld(previous);
            SetNotice(message);
        }

        private void ReplaceWorld(Simulation simulation)
        {
            Simulation = simulation;
            world.Initialize(simulation);
            orbit.Initialize(Vector3.zero, world.Radius);
            Tool = WorldTool.Inspect;
            Paused = false;
            nextAutosave = Time.unscaledTime + 30;
            hud.HideInspector();
            Select(new HexCoord(0, 0), false);
        }

        private void SetNotice(string message)
        {
            Notice = message;
            unchecked { NoticeRevision++; }
            Refresh();
        }

        public void FocusObjective()
        {
            SettlementObjective next = SettlementGuide.Next(Simulation);
            if (!next.Target.HasValue) return;
            Tool = WorldTool.Inspect;
            Select(next.Target.Value);
            orbit.Focus(world.WorldPosition(next.Target.Value));
        }

        private void Refresh()
        {
            if (world == null) return;
            world.Refresh();
            if (preview != null)
            {
                bool show = Selected.HasValue && Tool >= WorldTool.Forestry && Tool <= WorldTool.Smelter;
                TileState tile = Selected.HasValue ? Simulation.GetTile(Selected.Value) : null;
                show &= tile != null && tile.Building == BuildingKind.None && tile.Terrain != TerrainKind.Water;
                preview.SetActive(show);
                if (show) preview.transform.position = world.WorldPosition(Selected.Value) + Vector3.up * 0.09f;
            }
            if (hud != null) hud.Refresh();
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Simulation != null && !float.IsPositiveInfinity(nextAutosave))
                SaveService.TrySave(Simulation.State, out _);
        }

        private void OnApplicationQuit()
        {
            if (Simulation != null && !float.IsPositiveInfinity(nextAutosave)) SaveService.TrySave(Simulation.State, out _);
        }

        private void OnDestroy()
        {
            if (preview != null) Destroy(preview);
            if (previewMaterial != null) Destroy(previewMaterial);
        }
    }
}
