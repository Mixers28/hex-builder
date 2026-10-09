using System;
using AlphaHex.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlphaHex.Runtime
{
    /// <summary>Code-built prototype HUD; all mutations go through GameBootstrap and validated Core commands.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.022f, 0.053f, 0.088f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.07f, 0.13f, 0.20f, 1);
        private static readonly Color AccentColor = new Color(0.18f, 0.61f, 1f, 1);
        private static readonly Color MutedColor = new Color(0.66f, 0.76f, 0.84f, 1);
        private GameBootstrap game;
        private Font font;
        private RectTransform safeRoot;
        private Rect previousSafeArea;
        private Vector2Int previousScreen;
        private Text timber;
        private Text ore;
        private Text metal;
        private Text notice;
        private Text objective;
        private Text tileTitle;
        private Text tileInfo;
        private Text tileStatus;
        private Text selectedTool;
        private Text applyLabel;
        private Text gatherLabel;
        private Text demolitionLabel;
        private Text newLabel;
        private Text pauseLabel;
        private Text speedLabel;
        private Button apply;
        private Button gather;
        private Button demolish;
        private readonly Button[] tools = new Button[7];
        private float confirmNewUntil;
        private float confirmRemoveUntil;
        private HexCoord? confirmationCoord;
        private BuildingKind confirmationBuilding;
        private bool confirmationRoad;

        public void Initialize(GameBootstrap controller)
        {
            game = controller;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var canvas = gameObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 10;
            var scaler = gameObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280, 720);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            gameObject.AddComponent<GraphicRaycaster>();
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
                events.GetComponent<EventSystem>().sendNavigationEvents = false;
            }
            else EventSystem.current.sendNavigationEvents = false;

            safeRoot = MakeRect("Safe Area", transform);
            BuildTopBar();
            BuildNotice();
            BuildInspector();
            BuildToolbar();
            BuildObjective();
            UpdateSafeArea();
            Refresh();
        }

        private void BuildTopBar()
        {
            RectTransform top = Panel("Top Bar", safeRoot, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -88), new Vector2(-16, -16));
            var layout = top.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.padding = new RectOffset(16, 16, 10, 10);
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlHeight = true;
            layout.childControlWidth = true;
            layout.childForceExpandHeight = true;
            layout.childForceExpandWidth = false;

            Text title = Label("Title", top, "ALPHA  /  HEX BUILDER", 19, Color.white);
            Size(title.gameObject, 220, 48, 1);
            timber = Stat(top, "TIMBER");
            ore = Stat(top, "IRON ORE");
            metal = Stat(top, "METAL");
            Button save = MakeButton(top, "Save", game.SaveGame, out _); Size(save.gameObject, 64, 44);
            Button load = MakeButton(top, "Load", game.LoadGame, out _); Size(load.gameObject, 64, 44);
            Button previous = MakeButton(top, "Previous", game.LoadPreviousWorld, out _); Size(previous.gameObject, 76, 44);
            Button fresh = MakeButton(top, "New", ConfirmNew, out newLabel); Size(fresh.gameObject, 76, 44);
            Button pause = MakeButton(top, "Pause", game.TogglePause, out pauseLabel); Size(pause.gameObject, 72, 44);
            Button speed = MakeButton(top, "1x", game.CycleSpeed, out speedLabel); Size(speed.gameObject, 44, 44);
        }

        private Text Stat(Transform parent, string name)
        {
            Text text = Label(name, parent, name, 12, MutedColor);
            text.supportRichText = true;
            Size(text.gameObject, 86, 50);
            return text;
        }

        private void BuildNotice()
        {
            RectTransform panel = Panel("Notice", safeRoot, new Vector2(0, 1), new Vector2(1, 1),
                new Vector2(16, -146), new Vector2(-326, -100));
            notice = Label("Message", panel, "", 14, Color.white);
            Stretch(notice.rectTransform, 14, 7, 14, 7);
        }

        private void BuildInspector()
        {
            RectTransform panel = Panel("Selection Panel", safeRoot, new Vector2(1, 0), new Vector2(1, 1),
                new Vector2(-314, 126), new Vector2(-16, -100));
            var scroll = panel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            RectTransform viewport = MakeRect("Viewport", panel);
            Stretch(viewport, 14, 12, 14, 12);
            viewport.gameObject.AddComponent<RectMask2D>();
            RectTransform content = MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = new Vector2(1, 1);
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            var vertical = content.gameObject.AddComponent<VerticalLayoutGroup>();
            vertical.spacing = 8;
            vertical.childControlWidth = true;
            vertical.childControlHeight = true;
            vertical.childForceExpandWidth = true;
            vertical.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Text small = Label("Selection", content, "SITE INSPECTOR", 11, AccentColor); Size(small.gameObject, 0, 18);
            tileTitle = Label("Tile Title", content, "Choose a tile", 24, Color.white); Size(tileTitle.gameObject, 0, 34);
            tileInfo = Label("Tile Details", content, "", 14, MutedColor); Size(tileInfo.gameObject, 0, 96);
            tileStatus = Label("Production Status", content, "", 14, Color.white); Size(tileStatus.gameObject, 0, 64);
            selectedTool = Label("Selected Tool", content, "", 12, AccentColor); Size(selectedTool.gameObject, 0, 26);
            apply = MakeButton(content, "Apply", game.ApplyTool, out applyLabel); Size(apply.gameObject, 0, 44);
            gather = MakeButton(content, "Gather", game.GatherSelected, out gatherLabel); Size(gather.gameObject, 0, 40);
            demolish = MakeButton(content, "Remove", ConfirmRemove, out demolitionLabel); Size(demolish.gameObject, 0, 36);
            Text help = Label("Placement Help", content,
                "Choose a tool, select its site, then apply.\nDrag to pan. Pinch or scroll to zoom.", 11, MutedColor);
            Size(help.gameObject, 0, 38);
        }

        private void BuildToolbar()
        {
            RectTransform bar = Panel("Build Tools", safeRoot, Vector2.zero, new Vector2(1, 0),
                new Vector2(16, 16), new Vector2(-16, 110));
            var row = bar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 12, 12);
            row.spacing = 8;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlHeight = true;
            row.childControlWidth = true;
            row.childForceExpandHeight = true;
            row.childForceExpandWidth = true;
            string[] names = {"INSPECT", "CLAIM", "ROAD", "FORESTRY", "MINE", "SMELTER", "HOME"};
            string[] costs = {"Select a tile", "6 timber / 1 metal", "2 timber", "10 timber", "14 timber / 2 metal", "20 timber / 4 metal", "Return to depot"};
            for (int i = 0; i < names.Length; i++)
            {
                var tool = (WorldTool)i;
                tools[i] = MakeButton(bar, names[i] + "\n<size=11>" + costs[i] + "</size>", () => game.SetTool(tool), out Text label);
                label.supportRichText = true;
                label.fontSize = 14;
                Size(tools[i].gameObject, 90, 68, 1);
            }
        }

        private void BuildObjective()
        {
            RectTransform panel = Panel("Next Objective", safeRoot, Vector2.zero, new Vector2(1, 0),
                new Vector2(16, 126), new Vector2(-326, 226));
            Text heading = Label("Heading", panel, "GROW YOUR SETTLEMENT", 11, AccentColor);
            heading.rectTransform.anchorMin = new Vector2(0, 1);
            heading.rectTransform.anchorMax = new Vector2(1, 1);
            heading.rectTransform.offsetMin = new Vector2(14, -30);
            heading.rectTransform.offsetMax = new Vector2(-14, -10);
            objective = Label("Objective", panel, "", 15, Color.white);
            objective.rectTransform.anchorMin = Vector2.zero;
            objective.rectTransform.anchorMax = Vector2.one;
            objective.rectTransform.offsetMin = new Vector2(14, 10);
            objective.rectTransform.offsetMax = new Vector2(-14, -35);
        }

        public void Refresh()
        {
            if (game == null || game.Simulation == null || timber == null) return;
            UpdateSafeArea();
            ResourceStock stock = game.Simulation.State.Stock;
            timber.text = "TIMBER\n<size=24><color=#FFFFFF>" + stock.Timber + "</color></size>";
            ore.text = "IRON ORE\n<size=24><color=#FFFFFF>" + stock.Ore + "</color></size>";
            metal.text = "METAL\n<size=24><color=#FFFFFF>" + stock.Metal + "</color></size>";
            notice.text = game.Notice;
            objective.text = game.Objective();
            pauseLabel.text = game.Paused ? "Resume" : "Pause";
            speedLabel.text = game.Speed + "x";
            newLabel.text = Time.unscaledTime < confirmNewUntil ? "Confirm?" : "New";
            for (int i = 0; i < tools.Length; i++)
            {
                ColorBlock colors = tools[i].colors;
                colors.normalColor = (int)game.Tool == i ? new Color(0.10f, 0.34f, 0.56f) : ButtonColor;
                colors.selectedColor = colors.normalColor;
                tools[i].colors = colors;
            }

            TileState tile = game.Selected.HasValue ? game.Simulation.GetTile(game.Selected.Value) : null;
            if (tile == null)
            {
                tileTitle.text = "Choose a tile";
                tileInfo.text = "Select part of the island to inspect its resources, claim it, or build.";
                tileStatus.text = "";
                selectedTool.text = "";
                apply.interactable = gather.interactable = demolish.interactable = false;
                return;
            }

            tileTitle.text = tile.Terrain + "  " + tile.Coord.Q + "," + tile.Coord.R;
            tileInfo.text = (tile.Claimed ? "Owned land" : "Unclaimed land") + "  |  height " + tile.Elevation +
                "\nTrees: " + tile.TreesRemaining + "     Iron ore: " + tile.OreRemaining +
                "\nExcavation: " + tile.ExcavationDepth + " / 3" +
                "\nBuilding: " + (tile.Building == BuildingKind.None ? "None" : tile.Building.ToString()) +
                "\nRoad: " + (tile.HasRoad ? "Yes" : "No");
            tileStatus.text = game.Simulation.GetStatus(tile.Coord);
            selectedTool.text = "TOOL: " + game.Tool.ToString().ToUpperInvariant();
            applyLabel.text = ApplyText(game.Tool);
            apply.interactable = game.Tool != WorldTool.Inspect;
            gather.interactable = tile.Claimed &&
                (tile.TreesRemaining > 0 || tile.OreRemaining > 0);
            gatherLabel.text = tile.TreesRemaining > 0 ? "Gather timber  +3" : "Hand-mine ore  +1";
            demolish.interactable = tile.Claimed && tile.Building != BuildingKind.Depot &&
                (tile.Building != BuildingKind.None || tile.HasRoad);
            bool confirmRemoval = RemovalConfirmed(tile);
            demolitionLabel.text = confirmRemoval ? "Confirm removal / no refund" : "Remove building or road";
        }

        private static string ApplyText(WorldTool tool)
        {
            switch (tool)
            {
                case WorldTool.Claim: return "Claim land  /  6 timber + 1 metal";
                case WorldTool.Road: return "Build road  /  2 timber";
                case WorldTool.Forestry: return "Build forestry  /  10 timber";
                case WorldTool.Mine: return "Build mine  /  14 timber + 2 metal";
                case WorldTool.Smelter: return "Build smelter  /  20 timber + 4 metal";
                default: return "Choose a build tool";
            }
        }

        private void ConfirmNew()
        {
            if (Time.unscaledTime < confirmNewUntil)
            {
                confirmNewUntil = 0;
                game.StartNewWorld();
            }
            else confirmNewUntil = Time.unscaledTime + 4;
            Refresh();
        }

        private void ConfirmRemove()
        {
            TileState tile = game.Selected.HasValue ? game.Simulation.GetTile(game.Selected.Value) : null;
            if (tile == null) return;
            if (RemovalConfirmed(tile))
            {
                confirmRemoveUntil = 0;
                game.DemolishSelected();
            }
            else
            {
                confirmationCoord = game.Selected;
                confirmationBuilding = tile.Building;
                confirmationRoad = tile.HasRoad;
                confirmRemoveUntil = Time.unscaledTime + 4;
            }
            Refresh();
        }

        private bool RemovalConfirmed(TileState tile) => Time.unscaledTime < confirmRemoveUntil
            && confirmationCoord == tile.Coord && confirmationBuilding == tile.Building
            && confirmationRoad == tile.HasRoad;

        private void UpdateSafeArea()
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (safe == previousSafeArea && screen == previousScreen) return;
            previousSafeArea = safe;
            previousScreen = screen;
            if (Screen.width <= 0 || Screen.height <= 0) return;
            safeRoot.anchorMin = new Vector2(safe.xMin / Screen.width, safe.yMin / Screen.height);
            safeRoot.anchorMax = new Vector2(safe.xMax / Screen.width, safe.yMax / Screen.height);
            safeRoot.offsetMin = safeRoot.offsetMax = Vector2.zero;
        }

        private RectTransform MakeRect(string name, Transform parent)
        {
            var item = new GameObject(name, typeof(RectTransform));
            item.transform.SetParent(parent, false);
            return item.GetComponent<RectTransform>();
        }

        private RectTransform Panel(string name, Transform parent, Vector2 min, Vector2 max, Vector2 low, Vector2 high)
        {
            RectTransform rect = MakeRect(name, parent);
            rect.anchorMin = min;
            rect.anchorMax = max;
            rect.offsetMin = low;
            rect.offsetMax = high;
            rect.gameObject.AddComponent<Image>().color = PanelColor;
            return rect;
        }

        private Text Label(string name, Transform parent, string value, int size, Color color)
        {
            RectTransform rect = MakeRect(name, parent);
            Text text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = size;
            text.color = color;
            text.alignment = TextAnchor.MiddleLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private Button MakeButton(Transform parent, string title, Action action, out Text label)
        {
            RectTransform rect = MakeRect(title, parent);
            Image image = rect.gameObject.AddComponent<Image>();
            image.color = Color.white;
            Button button = rect.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.normalColor = ButtonColor;
            colors.highlightedColor = new Color(0.12f, 0.32f, 0.49f);
            colors.pressedColor = AccentColor;
            colors.selectedColor = ButtonColor;
            colors.disabledColor = new Color(0.08f, 0.10f, 0.13f, 0.75f);
            button.colors = colors;
            button.navigation = new Navigation { mode = Navigation.Mode.None };
            button.onClick.AddListener(() => action());
            label = Label("Label", rect, title, 14, Color.white);
            label.alignment = TextAnchor.MiddleCenter;
            Stretch(label.rectTransform, 5, 3, 5, 3);
            return button;
        }

        private static void Stretch(RectTransform rect, float left, float bottom, float right, float top)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = new Vector2(left, bottom);
            rect.offsetMax = new Vector2(-right, -top);
        }

        private static void Size(GameObject item, float width, float height, float flexibleWidth = 0)
        {
            var size = item.AddComponent<LayoutElement>();
            if (width > 0) { size.minWidth = width; size.preferredWidth = width; }
            size.minHeight = height;
            size.preferredHeight = height;
            size.flexibleWidth = flexibleWidth;
        }
    }
}
