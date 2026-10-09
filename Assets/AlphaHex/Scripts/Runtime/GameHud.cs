using System;
using AlphaHex.Core;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace AlphaHex.Runtime
{
    /// <summary>Code-built HUD. Resource mutations stay in validated simulation commands.</summary>
    public sealed class GameHud : MonoBehaviour
    {
        private static readonly Color PanelColor = new Color(0.022f, 0.053f, 0.088f, 0.96f);
        private static readonly Color ButtonColor = new Color(0.07f, 0.13f, 0.20f, 1);
        private static readonly Color AccentColor = new Color(0.18f, 0.61f, 1f, 1);
        private static readonly Color MutedColor = new Color(0.66f, 0.76f, 0.84f, 1);
        private GameBootstrap game;
        private Font font;
        private Canvas canvas;
        private RectTransform safeRoot, topBar, inspector, menu, noticePanel, objectivePanel, toolbar, progressFill;
        private Text title, timber, ore, metal, notice, objectiveHeading, objective, inspectorLabel, menuLabel;
        private Text tileTitle, tileInfo, tileStatus, selectedTool, applyLabel, gatherLabel, demolitionLabel;
        private Text newLabel, pauseLabel, speedLabel;
        private Button apply, gather, demolish, showObjective;
        private readonly Button[] tools = new Button[7];
        private Rect previousSafeArea;
        private Vector2Int previousScreen;
        private string previousNotice;
        private uint previousNoticeRevision;
        private float noticeUntil, confirmNewUntil, confirmRemoveUntil;
        private HexCoord? confirmationCoord;
        private BuildingKind confirmationBuilding;
        private bool confirmationRoad;

        public void Initialize(GameBootstrap controller)
        {
            game = controller;
            font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            canvas = gameObject.AddComponent<Canvas>();
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
            BuildToolbar();
            BuildObjective();
            BuildInspector();
            BuildMenu();
            inspector.gameObject.SetActive(false);
            menu.gameObject.SetActive(false);
            UpdateSafeArea();
            Refresh();
        }

        private void BuildTopBar()
        {
            topBar = Panel("Top Bar", safeRoot, Vector2.up, Vector2.one, Vector2.zero, Vector2.zero);
            var row = topBar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(12, 12, 8, 8);
            row.spacing = 8;
            row.childAlignment = TextAnchor.MiddleLeft;
            row.childControlHeight = row.childControlWidth = true;
            row.childForceExpandHeight = true;
            row.childForceExpandWidth = false;
            title = Label("Title", topBar, "ALPHA HEX\n<size=10>DEPOT STOCK</size>", 17, Color.white);
            Size(title.gameObject, 134, 48, 1);
            timber = Stat("Timber");
            ore = Stat("Iron ore");
            metal = Stat("Metal");
            Button pause = MakeButton(topBar, "Pause", game.TogglePause, out pauseLabel); Size(pause.gameObject, 66, 44);
            Button speed = MakeButton(topBar, "1x", game.CycleSpeed, out speedLabel); Size(speed.gameObject, 44, 44);
            Button site = MakeButton(topBar, "Show site", ToggleInspector, out inspectorLabel); Size(site.gameObject, 84, 44);
            Button options = MakeButton(topBar, "Menu", ToggleMenu, out menuLabel); Size(options.gameObject, 62, 44);
        }

        private Text Stat(string name)
        {
            Text text = Label(name, topBar, "", 11, MutedColor);
            Size(text.gameObject, 152, 48);
            return text;
        }

        private void BuildNotice()
        {
            noticePanel = Panel("Notice", safeRoot, Vector2.up, Vector2.up, Vector2.zero, Vector2.zero);
            notice = Label("Message", noticePanel, "", 12, Color.white);
            Stretch(notice.rectTransform, 12, 6, 12, 6);
        }

        private void BuildInspector()
        {
            inspector = Panel("Selection Panel", safeRoot, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero);
            Text heading = Label("Heading", inspector, "SITE INSPECTOR", 11, AccentColor);
            PlaceTop(heading.rectTransform, new Vector2(0, 1), 12, -8, 190, 36);
            Button close = MakeButton(inspector, "Hide", HideInspector, out _);
            PlaceTop(close.GetComponent<RectTransform>(), Vector2.one, -56, -4, 52, 40);

            RectTransform viewport = MakeRect("Viewport", inspector);
            Stretch(viewport, 12, 12, 12, 48);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = inspector.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24;
            RectTransform content = MakeRect("Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            scroll.viewport = viewport;
            scroll.content = content;
            var column = content.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 6;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            content.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            tileTitle = Label("Tile Title", content, "Choose a tile", 21, Color.white); Size(tileTitle.gameObject, 0, 30);
            tileInfo = Label("Tile Details", content, "", 13, MutedColor); Size(tileInfo.gameObject, 0, 64);
            tileStatus = Label("Production Status", content, "", 13, Color.white); Size(tileStatus.gameObject, 0, 100);
            RectTransform track = Panel("Progress", content, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            track.GetComponent<Image>().color = ButtonColor;
            Size(track.gameObject, 0, 5);
            progressFill = Panel("Fill", track, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            progressFill.GetComponent<Image>().color = AccentColor;
            progressFill.GetComponent<Image>().raycastTarget = false;
            selectedTool = Label("Selected Tool", content, "", 11, AccentColor); Size(selectedTool.gameObject, 0, 20);
            apply = MakeButton(content, "Apply", game.ApplyTool, out applyLabel); Size(apply.gameObject, 0, 44);
            gather = MakeButton(content, "Gather", game.GatherSelected, out gatherLabel); Size(gather.gameObject, 0, 44);
            demolish = MakeButton(content, "Remove", ConfirmRemove, out demolitionLabel); Size(demolish.gameObject, 0, 44);
            Text help = Label("Placement Help", content, "Choose a tool, select a site, then apply.\nI shows or hides this panel.", 11, MutedColor);
            Size(help.gameObject, 0, 32);
        }

        private void BuildMenu()
        {
            menu = Panel("Game Menu", safeRoot, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero);
            var column = menu.gameObject.AddComponent<VerticalLayoutGroup>();
            column.padding = new RectOffset(10, 10, 10, 10);
            column.spacing = 6;
            column.childControlWidth = column.childControlHeight = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            Text heading = Label("Heading", menu, "ISLAND / SAVES   [M]", 11, AccentColor); Size(heading.gameObject, 0, 22);
            Button save = MakeButton(menu, "Save   /   F5", () => { game.SaveGame(); CloseMenu(); }, out _); Size(save.gameObject, 0, 44);
            Button load = MakeButton(menu, "Load   /   F9", () => { game.LoadGame(); CloseMenu(); }, out _); Size(load.gameObject, 0, 44);
            Button previous = MakeButton(menu, "Previous island", () => { game.LoadPreviousWorld(); CloseMenu(); }, out _); Size(previous.gameObject, 0, 44);
            Button fresh = MakeButton(menu, "New island", ConfirmNew, out newLabel); Size(fresh.gameObject, 0, 44);
        }

        private void BuildToolbar()
        {
            toolbar = Panel("Build Tools", safeRoot, new Vector2(0.5f, 0), new Vector2(0.5f, 0), Vector2.zero, Vector2.zero);
            var row = toolbar.gameObject.AddComponent<HorizontalLayoutGroup>();
            row.padding = new RectOffset(8, 8, 8, 8);
            row.spacing = 6;
            row.childAlignment = TextAnchor.MiddleCenter;
            row.childControlHeight = row.childControlWidth = true;
            row.childForceExpandHeight = row.childForceExpandWidth = true;
            string[] names = {"INSPECT", "CLAIM", "ROAD", "FORESTRY", "MINE", "SMELTER", "HOME"};
            string[] costs = {"Select a site", "6 timber / 1 metal", "2 timber", "10 timber", "14 timber / 2 metal", "20 timber / 4 metal", "Depot view"};
            for (int i = 0; i < names.Length; i++)
            {
                var tool = (WorldTool)i;
                tools[i] = MakeButton(toolbar, names[i] + "\n<size=10>" + costs[i] + "</size>", () => game.SetTool(tool), out Text label);
                label.fontSize = 12;
                Size(tools[i].gameObject, 108, 52, 1);
            }
        }

        private void BuildObjective()
        {
            objectivePanel = Panel("Next Objective", safeRoot, Vector2.zero, Vector2.zero, Vector2.zero, Vector2.zero);
            objectiveHeading = Label("Heading", objectivePanel, "NEXT STEP", 11, AccentColor);
            PlaceTop(objectiveHeading.rectTransform, Vector2.up, 12, -8, 304, 32);
            showObjective = MakeButton(objectivePanel, "Show site [G]", game.FocusObjective, out Text label);
            label.fontSize = 12;
            PlaceTop(showObjective.GetComponent<RectTransform>(), Vector2.one, -94, -6, 88, 44);
            objective = Label("Objective", objectivePanel, "", 13, Color.white);
            Stretch(objective.rectTransform, 12, 10, 12, 48);
        }

        public void Refresh()
        {
            if (game == null || game.Simulation == null || timber == null) return;
            UpdateSafeArea();
            Simulation sim = game.Simulation;
            ResourceStock stock = sim.State.Stock;
            timber.text = StatText("TIMBER", stock.Timber, sim.GetInTransit(ResourceKind.Timber));
            ore.text = StatText("IRON ORE", stock.Ore, sim.GetInTransit(ResourceKind.Ore));
            metal.text = StatText("METAL", stock.Metal, sim.GetInTransit(ResourceKind.Metal));
            if (previousNotice != game.Notice || previousNoticeRevision != game.NoticeRevision)
            {
                previousNotice = game.Notice;
                previousNoticeRevision = game.NoticeRevision;
                noticeUntil = Time.unscaledTime + 8;
            }
            notice.text = game.Notice;
            noticePanel.gameObject.SetActive(Time.unscaledTime < noticeUntil);
            SettlementObjective next = SettlementGuide.Next(sim);
            objectiveHeading.text = "NEXT / " + next.Heading.ToUpperInvariant();
            objective.text = next.Detail;
            showObjective.interactable = next.Target.HasValue;
            pauseLabel.text = game.Paused ? "Resume" : "Pause";
            speedLabel.text = game.Speed + "x";
            inspectorLabel.text = inspector.gameObject.activeSelf ? "Hide site" : "Show site";
            menuLabel.text = menu.gameObject.activeSelf ? "Close" : "Menu";
            newLabel.text = Time.unscaledTime < confirmNewUntil ? "Confirm new island?" : "New island";
            for (int i = 0; i < tools.Length; i++)
            {
                ColorBlock colors = tools[i].colors;
                colors.normalColor = (int)game.Tool == i ? new Color(0.10f, 0.34f, 0.56f) : ButtonColor;
                colors.selectedColor = colors.normalColor;
                tools[i].colors = colors;
            }

            TileState tile = game.Selected.HasValue ? sim.GetTile(game.Selected.Value) : null;
            if (tile == null)
            {
                tileTitle.text = "Choose a tile";
                tileInfo.text = "Select part of the island to inspect its resources, claim it, or build.";
                tileStatus.text = selectedTool.text = "";
                apply.interactable = gather.interactable = demolish.interactable = false;
                progressFill.anchorMax = new Vector2(0, 1);
                return;
            }
            tileTitle.text = tile.Terrain + "  " + tile.Coord.Q + ", " + tile.Coord.R;
            string deposit = tile.TreesRemaining > 0 ? tile.TreesRemaining + " trees left / " + (tile.TreesRemaining * Simulation.TimberPerTree) + " timber"
                : tile.InitialOre > 0 ? tile.OreRemaining + " / " + tile.InitialOre + " ore left / quarry " + tile.ExcavationDepth + "/3"
                : "No remaining deposit";
            tileInfo.text = (tile.Claimed ? "Owned" : "Unclaimed") + " / elevation " + tile.Elevation + "\n" + deposit
                + "\n" + (tile.Building == BuildingKind.None ? "Open site" : tile.Building.ToString()) + (tile.HasRoad ? " / access road" : "");
            tileStatus.text = (game.Paused ? "SIMULATION PAUSED\n" : "") + sim.GetStatus(tile.Coord);
            progressFill.anchorMax = new Vector2(Mathf.Clamp01(sim.GetProgress(tile.Coord)), 1);
            selectedTool.text = "TOOL / " + game.Tool.ToString().ToUpperInvariant();
            applyLabel.text = ApplyText(game.Tool);
            apply.gameObject.SetActive(game.Tool != WorldTool.Inspect);
            apply.interactable = game.Tool != WorldTool.Inspect;
            gather.interactable = tile.Claimed && (tile.TreesRemaining > 0 || tile.OreRemaining > 0);
            gatherLabel.text = tile.TreesRemaining > 0 ? "Gather timber   +3" : "Hand-mine ore   +1";
            demolish.interactable = tile.Claimed && tile.Building != BuildingKind.Depot && (tile.Building != BuildingKind.None || tile.HasRoad);
            demolitionLabel.text = RemovalConfirmed(tile) ? "Confirm removal / no refund" : "Remove building or road";
        }

        private static string StatText(string resource, int available, int moving) => resource + "  <size=21><color=#FFFFFF>"
            + Count(available) + "</color></size>\n<size=11><color=#9EB7C9>" + Count(moving) + " in transit</color></size>";

        private static string Count(int amount) => amount < 10000 ? amount.ToString()
            : amount < 1000000 ? (amount / 1000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "k"
            : (amount / 1000000f).ToString("0.#", System.Globalization.CultureInfo.InvariantCulture) + "m";

        public void ToggleInspector()
        {
            if (inspector.gameObject.activeSelf) HideInspector(); else ShowInspector();
        }

        public void ShowInspector()
        {
            CloseMenu();
            inspector.gameObject.SetActive(true);
            Refresh();
        }

        public void HideInspector()
        {
            inspector.gameObject.SetActive(false);
            confirmRemoveUntil = 0;
            Refresh();
        }

        public void ToggleMenu()
        {
            if (menu.gameObject.activeSelf) CloseMenu();
            else
            {
                HideInspector();
                menu.gameObject.SetActive(true);
            }
            Refresh();
        }

        public void CloseMenu()
        {
            menu.gameObject.SetActive(false);
            menuLabel.text = "Menu";
            confirmNewUntil = 0;
        }

        private static string ApplyText(WorldTool tool)
        {
            switch (tool)
            {
                case WorldTool.Claim: return "Claim / 6 timber + 1 metal";
                case WorldTool.Road: return "Build road / 2 timber";
                case WorldTool.Forestry: return "Build forestry / 10 timber";
                case WorldTool.Mine: return "Build mine / 14 timber + 2 metal";
                case WorldTool.Smelter: return "Build smelter / 20 timber + 4 metal";
                default: return "Choose a build tool";
            }
        }

        private void ConfirmNew()
        {
            if (Time.unscaledTime < confirmNewUntil)
            {
                confirmNewUntil = 0;
                game.StartNewWorld();
                CloseMenu();
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
            && confirmationCoord == tile.Coord && confirmationBuilding == tile.Building && confirmationRoad == tile.HasRoad;

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
            Canvas.ForceUpdateCanvases();
            float width = safe.width / canvas.scaleFactor;
            float height = safe.height / canvas.scaleFactor;
            PlaceTop(topBar, Vector2.up, 12, -12, width - 24, 64);
            title.gameObject.SetActive(width >= 1100);
            PlaceTop(noticePanel, Vector2.up, 12, -84, Mathf.Min(600, width - 324), 42);
            PlaceTop(inspector, Vector2.one, -292, -84, 280, Mathf.Min(478, height - 176));
            PlaceTop(menu, Vector2.one, -236, -84, 224, 248);
            toolbar.pivot = new Vector2(0.5f, 0);
            toolbar.anchoredPosition = new Vector2(0, 12);
            toolbar.sizeDelta = new Vector2(Mathf.Min(1000, width - 24), 68);
            objectivePanel.pivot = Vector2.zero;
            objectivePanel.anchoredPosition = new Vector2(12, 92);
            objectivePanel.sizeDelta = new Vector2(Mathf.Min(430, width - 324), 126);
            objectiveHeading.rectTransform.sizeDelta = new Vector2(objectivePanel.sizeDelta.x - 118, 32);
        }

        private static void PlaceTop(RectTransform rect, Vector2 anchor, float x, float y, float width, float height)
        {
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = Vector2.up;
            rect.anchoredPosition = new Vector2(x, y);
            rect.sizeDelta = new Vector2(width, height);
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
