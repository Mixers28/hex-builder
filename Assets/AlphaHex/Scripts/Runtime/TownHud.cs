using System;
using System.Collections.Generic;
using AlphaHex.Core;
using UnityEngine;
using UnityEngine.UI;

namespace AlphaHex.Runtime
{
    public sealed partial class GameHud
    {
        private RectTransform townPanel, townBuilds, townResearch, townWorkers;
        private readonly List<WorkerRow> workerRows = new List<WorkerRow>();
        private string workerSignature = "";
        private sealed class WorkerRow { public HexCoord Coord; public Text Label, Count; public Button Add, Remove; }
        private Text townLabel, townSummary, researchSummary;
        private readonly Button[] townBuildButtons = new Button[Simulation.Buildable.Length];
        private readonly Text[] townBuildLabels = new Text[Simulation.Buildable.Length];
        private readonly Button[] researchButtons = new Button[Simulation.ResearchTopics.Length];
        private readonly Text[] researchLabels = new Text[Simulation.ResearchTopics.Length];
        public bool ResearchVisible => townPanel != null && townPanel.gameObject.activeSelf && townResearch.gameObject.activeSelf;
        private void BuildTownPanel()
        {
            townPanel = Panel("Town Panel", safeRoot, Vector2.one, Vector2.one, Vector2.zero, Vector2.zero);
            var heading = Label("Heading", townPanel, "SETTLEMENT   [T / B]", 14, AccentColor);
            PlaceTop(heading.rectTransform, Vector2.up, 12, -4, 240, 40);
            var close = MakeButton(townPanel, "Close", CloseTown, out _); PlaceTop(close.GetComponent<RectTransform>(), Vector2.one, -70, -4, 64, 40);
            var builds = MakeButton(townPanel, "Buildings", () => SetTownTab(0), out _); PlaceTop(builds.GetComponent<RectTransform>(), Vector2.up, 12, -48, 122, 40);
            var research = MakeButton(townPanel, "Research [R]", () => SetTownTab(2), out _); PlaceTop(research.GetComponent<RectTransform>(), Vector2.up, 274, -48, 122, 40);
            var workers = MakeButton(townPanel, "Workers [T]", () => SetTownTab(1), out _); PlaceTop(workers.GetComponent<RectTransform>(), Vector2.up, 143, -48, 122, 40);
            var viewport = MakeRect("Town Viewport", townPanel); Stretch(viewport, 12, 12, 12, 98); viewport.gameObject.AddComponent<RectMask2D>();
            var scroll = townPanel.gameObject.AddComponent<ScrollRect>(); scroll.horizontal = false; scroll.movementType = ScrollRect.MovementType.Clamped; scroll.scrollSensitivity = 36;
            var content = MakeRect("Town Content", viewport); content.anchorMin = new Vector2(0, 1); content.anchorMax = Vector2.one; content.pivot = new Vector2(.5f, 1);
            content.offsetMin = content.offsetMax = Vector2.zero; TownColumn(content); scroll.viewport = viewport; scroll.content = content;
            townSummary = Label("Town Census", content, "", 14, Color.white); Size(townSummary.gameObject, 0, 132);
            townBuilds = MakeRect("Buildings", content); TownColumn(townBuilds);
            var help = Label("Build Help", townBuilds, "Choose a building, select its site, then build.\nAssign workers in the site inspector.", 13, MutedColor); Size(help.gameObject, 0, 42);
            for (int i = 0; i < Simulation.Buildable.Length; i++)
            {
                BuildingKind kind = Simulation.Buildable[i];
                townBuildButtons[i] = MakeButton(townBuilds, "", () => game.SetTool((WorldTool)Enum.Parse(typeof(WorldTool), kind.ToString())), out townBuildLabels[i]);
                townBuildLabels[i].alignment = TextAnchor.MiddleLeft; Stretch(townBuildLabels[i].rectTransform, 12, 6, 12, 6); Size(townBuildButtons[i].gameObject, 0, 92);
            }
            townWorkers = MakeRect("Workplaces", content); TownColumn(townWorkers); townWorkers.gameObject.SetActive(false);
            townResearch = MakeRect("Research", content); TownColumn(townResearch);
            researchSummary = Label("Research Progress", townResearch, "", 14, MutedColor); Size(researchSummary.gameObject, 0, 70);
            for (int i = 0; i < Simulation.ResearchTopics.Length; i++)
            {
                ResearchKind topic = Simulation.ResearchTopics[i]; researchButtons[i] = MakeButton(townResearch, "", () => game.Research(topic), out researchLabels[i]);
                researchLabels[i].alignment = TextAnchor.MiddleLeft; Stretch(researchLabels[i].rectTransform, 12, 6, 12, 6); Size(researchButtons[i].gameObject, 0, 120);
            }
            townResearch.gameObject.SetActive(false); townPanel.gameObject.SetActive(false);
        }
        private static void TownColumn(RectTransform rect)
        {
            var column = rect.gameObject.AddComponent<VerticalLayoutGroup>(); column.spacing = 8; column.childControlHeight = column.childControlWidth = true;
            column.childForceExpandWidth = true; column.childForceExpandHeight = false;
            rect.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
        }
        public void ToggleTown() { if (townPanel.gameObject.activeSelf) CloseTown(); else OpenTown(1); }
        public void ToggleBuild() { if (townPanel.gameObject.activeSelf && townBuilds.gameObject.activeSelf) CloseTown(); else OpenTown(0); }
        public void ShowResearch() => OpenTown(2);
        private void OpenTown(int tab) { HideInspector(); CloseMenu(); townPanel.gameObject.SetActive(true); SetTownTab(tab); }
        private void SetTownTab(int tab)
        {
            townBuilds.gameObject.SetActive(tab == 0); townWorkers.gameObject.SetActive(tab == 1); townResearch.gameObject.SetActive(tab == 2);
            townPanel.GetComponent<ScrollRect>().verticalNormalizedPosition = 1; Refresh();
        }
        public void CloseTown() { if (townPanel != null) townPanel.gameObject.SetActive(false); }
        private void RefreshTown()
        {
            if (townPanel == null) return;
            var sim = game.Simulation; var town = sim.State.Town;
            townLabel.text = "WORKERS [T]\n<size=12>" + town.Population + " citizens</size>";
            townSummary.text = "<b>" + sim.TownName.ToUpperInvariant() + "</b>\n" + town.Population + " citizens / " + sim.HousingCapacity + " places\n"
                + sim.AssignedWorkers + " assigned / " + sim.IdleCitizens + " available\n" + sim.MealDemand + " food each 30 seconds\n" + sim.GetImmigrationStatus();
            RefreshWorkers(sim);
            researchSummary.text = town.ResearchJob == ResearchKind.None ? "Choose one project. Its materials are spent when research begins."
                : Simulation.ResearchName(town.ResearchJob) + " · " + Mathf.CeilToInt(Simulation.ResearchSeconds(town.ResearchJob) - town.ResearchProgress) + " seconds left.";
            if (sim.ResearchPoints > 0) researchSummary.text += "\n" + sim.ResearchPoints + " legacy credits remain for original technologies.";
            for (int i = 0; i < Simulation.Buildable.Length; i++)
            {
                var kind = Simulation.Buildable[i]; var required = Simulation.RequiredResearch(kind); bool unlocked = sim.HasResearch(required);
                townBuildButtons[i].interactable = unlocked;
                townBuildLabels[i].text = "<b>" + Simulation.BuildingName(kind) + "</b>\n<size=13>" + Simulation.CostText(Simulation.GetBuildCost(kind)) + "\n"
                    + (unlocked ? BuildingDetail(kind) : "Requires " + Simulation.ResearchName(required)) + "</size>";
            }
            for (int i = 0; i < Simulation.ResearchTopics.Length; i++)
            {
                var kind = Simulation.ResearchTopics[i]; bool known = sim.HasResearch(kind); string error = sim.ResearchError(kind);
                researchButtons[i].interactable = error == "";
                researchLabels[i].text = "<b>" + (i < 9 ? "[" + (i + 1) + "] " : "") + Simulation.ResearchName(kind) + (known ? " · COMPLETE" : "") + "</b>\n<size=12>"
                    + Simulation.ResearchEffect(kind) + "\n" + (known ? "" : Simulation.CostText(Simulation.ResearchCost(kind)) + " · " + Simulation.ResearchSeconds(kind) + " s\n"
                    + (error == "" ? "Ready to begin" : error)) + "</size>";
            }
        }
        private void RefreshWorkers(Simulation sim)
        {
            string signature = "";
            foreach (var tile in sim.State.Tiles) if (Simulation.NeedsWorker(tile.Building)) signature += tile.Coord + ":" + tile.Building + ";";
            if (signature != workerSignature)
            {
                workerSignature = signature;
                foreach (Transform child in townWorkers) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
                workerRows.Clear();
                foreach (var tile in sim.State.Tiles)
                {
                    if (!Simulation.NeedsWorker(tile.Building)) continue;
                    var coord = tile.Coord; var row = MakeRect("Crew " + coord, townWorkers); TownColumn(row);
                    var label = Label("Workplace", row, "", 14, Color.white); Size(label.gameObject, 0, 28);
                    var controls = MakeRect("Allocation", row); Size(controls.gameObject, 0, 44);
                    var layout = controls.gameObject.AddComponent<HorizontalLayoutGroup>(); layout.spacing = 6;
                    layout.childControlWidth = layout.childControlHeight = true; layout.childForceExpandWidth = false;
                    var show = MakeButton(controls, "Show site", () => game.Select(coord), out _); Size(show.gameObject, 124, 44);
                    var remove = MakeButton(controls, "−", () => game.AssignAt(coord, -1), out _); Size(remove.gameObject, 44, 44);
                    var count = Label("Crew count", controls, "", 14, Color.white); count.alignment = TextAnchor.MiddleCenter; Size(count.gameObject, 80, 44, 1);
                    var add = MakeButton(controls, "+", () => game.AssignAt(coord, 1), out _); Size(add.gameObject, 44, 44);
                    workerRows.Add(new WorkerRow { Coord = coord, Label = label, Count = count, Add = add, Remove = remove });
                }
            }
            foreach (var row in workerRows)
            {
                var tile = sim.GetTile(row.Coord); int capacity = Simulation.CrewCapacity(tile.Building);
                row.Label.text = Simulation.BuildingName(tile.Building) + " " + row.Coord + (sim.GetPathToDepot(tile.Coord) == null ? " · disconnected" : "");
                row.Count.text = tile.Workers + " / " + capacity; row.Add.interactable = tile.Workers < capacity && sim.IdleCitizens > 0; row.Remove.interactable = tile.Workers > 0;
            }
        }
        private static string BuildingDetail(BuildingKind kind) => kind == BuildingKind.House ? "Four places; newcomers arrive over time."
            : kind == BuildingKind.Watchtower ? "Connected towers reveal a wider frontier."
            : kind == BuildingKind.Forester ? "Replants neighbouring woodland; 90 s growth."
            : kind == BuildingKind.Bloomery ? "Manual batch: 3 ore + 3 timber → 1 metal / 90 s."
            : kind == BuildingKind.Kiln ? "4 clay + 2 timber → 2 bricks / 45 s."
            : kind == BuildingKind.Smelter ? "2 ore + 1 timber → 1 metal / 18 s."
            : kind == BuildingKind.ClayPit ? "3 clay / 24 s + delivery; finite deposit."
            : kind == BuildingKind.Forestry ? "1 tree → 3 timber / 12 s + delivery."
            : kind == BuildingKind.Forager ? "3 food / 12 s + delivery."
            : kind == BuildingKind.Farm ? "6 food / 24 s + delivery."
            : "Assign up to " + Simulation.CrewCapacity(kind) + " workers; " + Simulation.JobSeconds(kind) + " s base job + delivery.";
    }
}
