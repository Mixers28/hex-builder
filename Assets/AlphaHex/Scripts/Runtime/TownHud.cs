using AlphaHex.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AlphaHex.Runtime
{
    public sealed partial class GameHud
    {
        private RectTransform townPanel, townBuilds, townResearch;
        private Text townLabel, townSummary, researchSummary;
        private readonly Button[] townBuildButtons = new Button[5];
        private readonly Text[] townBuildLabels = new Text[5];
        private readonly Button[] researchButtons = new Button[3];
        private readonly Text[] researchLabels = new Text[3];
        private static readonly BuildingKind[] TownBuildings = { BuildingKind.House, BuildingKind.Forager, BuildingKind.Farm, BuildingKind.Forester, BuildingKind.DeepMine };
        private static readonly WorldTool[] TownTools = { WorldTool.House, WorldTool.Forager, WorldTool.Farm, WorldTool.Forester, WorldTool.DeepMine };
        private static readonly ResearchKind[] ResearchTopics = { ResearchKind.Farming, ResearchKind.Reforestation, ResearchKind.DeepMining };
        private static readonly string[] BuildingDetails = { "4 places for new citizens", "2 food / 8 s + delivery", "6 food / 20 s + delivery", "Replants adjacent owned woodland", "2 deep ore / 7 s + delivery" };
        public bool ResearchVisible => townPanel != null && townPanel.gameObject.activeSelf && townResearch.gameObject.activeSelf;

        private void BuildTownPanel()
        {
            townPanel = Panel("Town Panel", safeRoot, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero);
            Text heading = Label("Heading", townPanel, "SETTLEMENT   [T]", 12, AccentColor);
            PlaceTop(heading.rectTransform, Vector2.up, 12, -4, 240, 40);
            Button close = MakeButton(townPanel, "Close", CloseTown, out _);
            PlaceTop(close.GetComponent<RectTransform>(), Vector2.one, -64, -4, 60, 40);
            Button builds = MakeButton(townPanel, "Town & buildings", () => SetTownTab(false), out _);
            PlaceTop(builds.GetComponent<RectTransform>(), Vector2.up, 12, -48, 182, 40);
            Button research = MakeButton(townPanel, "Research [R]", () => SetTownTab(true), out _);
            PlaceTop(research.GetComponent<RectTransform>(), Vector2.up, 202, -48, 194, 40);

            RectTransform viewport = MakeRect("Town Viewport", townPanel);
            Stretch(viewport, 12, 12, 12, 98);
            viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = townPanel.gameObject.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 30;
            RectTransform content = MakeRect("Town Content", viewport);
            content.anchorMin = new Vector2(0, 1);
            content.anchorMax = Vector2.one;
            content.pivot = new Vector2(0.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero;
            TownColumn(content);
            scroll.viewport = viewport;
            scroll.content = content;
            townSummary = Label("Town Census", content, "", 14, Color.white);
            Size(townSummary.gameObject, 0, 146);

            townBuilds = MakeRect("Town Buildings", content);
            TownColumn(townBuilds);
            Text instruction = Label("Build Help", townBuilds, "Choose a building, select its site, then apply.\nEach connected workplace needs 1 citizen.", 12, MutedColor);
            Size(instruction.gameObject, 0, 38);
            for (int i = 0; i < TownBuildings.Length; i++)
            {
                int index = i;
                townBuildButtons[i] = MakeButton(townBuilds, "", () => game.SetTool(TownTools[index]), out townBuildLabels[i]);
                Size(townBuildButtons[i].gameObject, 0, 78);
                string art = TownBuildings[i] == BuildingKind.House ? "house" : TownBuildings[i] == BuildingKind.Farm ? "farm"
                    : TownBuildings[i] == BuildingKind.Forester ? "forester" : null;
                if (art == null) continue;
                RectTransform iconRect = MakeRect("Building Illustration", townBuildButtons[i].transform);
                PlaceTop(iconRect, Vector2.up, 4, -3, 72, 72);
                RawImage icon = iconRect.gameObject.AddComponent<RawImage>();
                icon.texture = Resources.Load<Texture2D>("TownArt/" + art);
                icon.raycastTarget = false;
                townBuildLabels[i].alignment = TextAnchor.MiddleLeft;
                Stretch(townBuildLabels[i].rectTransform, 82, 3, 5, 3);
            }

            townResearch = MakeRect("Town Research", content);
            TownColumn(townResearch);
            researchSummary = Label("Research Progress", townResearch, "", 13, MutedColor);
            Size(researchSummary.gameObject, 0, 68);
            for (int i = 0; i < ResearchTopics.Length; i++)
            {
                ResearchKind topic = ResearchTopics[i];
                researchButtons[i] = MakeButton(townResearch, "", () => game.Research(topic), out researchLabels[i]);
                Size(researchButtons[i].gameObject, 0, 78);
            }
            townResearch.gameObject.SetActive(false);
            townPanel.gameObject.SetActive(false);
        }

        private static void TownColumn(RectTransform rect)
        {
            var column = rect.gameObject.AddComponent<VerticalLayoutGroup>();
            column.spacing = 8;
            column.childControlHeight = column.childControlWidth = true;
            column.childForceExpandWidth = true;
            column.childForceExpandHeight = false;
            rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }

        public void ToggleTown()
        {
            if (townPanel.gameObject.activeSelf) { CloseTown(); return; }
            OpenTown(false);
        }

        public void ShowResearch() => OpenTown(true);

        private void OpenTown(bool research)
        {
            HideInspector();
            CloseMenu();
            townPanel.gameObject.SetActive(true);
            SetTownTab(research);
        }

        private void SetTownTab(bool research)
        {
            townBuilds.gameObject.SetActive(!research);
            townResearch.gameObject.SetActive(research);
            townPanel.GetComponent<ScrollRect>().verticalNormalizedPosition = 1;
            Refresh();
        }

        public void CloseTown()
        {
            if (townPanel != null) townPanel.gameObject.SetActive(false);
        }

        private void RefreshTown()
        {
            if (townPanel == null) return;
            Simulation sim = game.Simulation;
            TownState town = sim.State.Town;
            townLabel.text = "TOWN [T]\n<size=11>" + town.Population + " citizens / " + sim.ResearchPoints + " RP</size>";
            townSummary.text = "<color=#62BEFF><b>" + sim.TownName.ToUpperInvariant() + "</b></color>\n"
                + town.Population + " citizens / " + sim.HousingCapacity + " housing places\n"
                + sim.AssignedWorkers + " working / " + sim.IdleCitizens + " available\n"
                + sim.MealDemand + " food per meal / next in " + Mathf.CeilToInt(Simulation.MealSeconds - town.MealProgress) + " s\n"
                + sim.GetImmigrationStatus();
            researchSummary.text = sim.ResearchPoints + " research point(s) available.\n"
                + "Earn 1 point at 6, 10 and 14 citizens. Each unlock costs 1. Choose the order that suits your town.";
            for (int i = 0; i < TownBuildings.Length; i++)
            {
                BuildingKind kind = TownBuildings[i];
                ResearchKind required = Simulation.RequiredResearch(kind);
                ResourceStock cost = Simulation.GetBuildCost(kind);
                bool unlocked = sim.HasResearch(required);
                townBuildButtons[i].interactable = unlocked;
                townBuildLabels[i].text = Simulation.BuildingName(kind).ToUpperInvariant()
                    + (unlocked ? "" : " / LOCKED") + "\n<size=11>" + cost.Timber + " timber"
                    + (cost.Metal > 0 ? " + " + cost.Metal + " metal" : "") + "\n"
                    + (unlocked ? BuildingDetails[i] : "Research " + Simulation.ResearchName(required)) + "</size>";
            }
            string[] effects = { "Unlock farms for a larger food supply", "Unlock foresters to plant trees (45 s growth)", "Unlock finite ore below exhausted surface mines" };
            for (int i = 0; i < ResearchTopics.Length; i++)
            {
                bool known = sim.HasResearch(ResearchTopics[i]);
                researchButtons[i].interactable = !known && sim.ResearchPoints > 0;
                researchLabels[i].text = "[" + (i + 1) + "] " + Simulation.ResearchName(ResearchTopics[i]).ToUpperInvariant()
                    + " / " + (known ? "COMPLETE" : "1 POINT") + "\n<size=11>" + effects[i] + "</size>";
            }
        }
    }
}
