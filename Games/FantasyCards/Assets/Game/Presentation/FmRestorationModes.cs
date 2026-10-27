using System;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        public int RestorationLobbyMode => restorationLobbyMode;
        public string ActiveScenarioId => engine == null ? null : FmScenarioCatalog.StageForBattle(engine.State)?.id;
        public int CompletedScenarioCount => FmScenarioCatalog.Stages.Count(stage => PlayerPrefs.GetInt("FC.scenario.won." + stage.id, 0) != 0);

        private void ShowRestorationScenario(string id)
        {
            var stage = FmScenarioCatalog.Get(id);
            battleScreen = false; redraw = () => ShowRestorationScenario(id); Clear(); FmOriginalMusic.SetScene("lobby"); RestorationBackdrop();
            var page = RestorationPanel(root, .048f, .18f, .744f, .91f, RestorationLeather, "Scenario briefing " + stage.id);
            Text(page, "SCENARIO  /  " + stage.tier.ToString("00"), .05f, .84f, .95f, .95f, 23, gold);
            Text(page, stage.name, .05f, .67f, .95f, .85f, 43, ivory);
            Text(page, stage.description, .05f, .46f, .95f, .67f, 27, new Color(.82f, .77f, .66f));
            Text(page, "이번 과제  ·  " + stage.objective, .05f, .29f, .95f, .46f, 28, gold);
            Text(page, "추천  " + DemoCatalog.GetDeck(stage.recommendedDeckId).name + "\n선택  " + (useCustomDeck ? "내 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name), .05f, .08f, .95f, .28f, 25, ivory);
            var completed = FmScenarioCatalog.Stages.Where(value => PlayerPrefs.GetInt("FC.scenario.won." + value.id, 0) != 0).Select(value => value.id);
            bool unlocked = FmScenarioCatalog.IsUnlocked(stage.id, completed);
            RestorationButton(root, "추천 덱으로 시작", .048f, .065f, .30f, .158f, () =>
            {
                var deck = DemoCatalog.GetDeck(stage.recommendedDeckId);
                selectedDeckId = deck.id; selectedLeaderId = deck.recommendedLeaderId; useCustomDeck = false;
                selectedEncounter = stage.encounterIndex; BeginSelectedBattle();
            }, true, unlocked);
            RestorationButton(root, "선택한 덱으로 시작", .315f, .065f, .565f, .158f, () =>
            { selectedEncounter = stage.encounterIndex; BeginSelectedBattle(); }, false, unlocked && (!useCustomDeck || ValidDeck(customDeck)));
            RestorationButton(root, "목록", .58f, .065f, .744f, .158f, () => { restorationLobbyMode = 1; ShowLobby(); });
            DrawRestorationProfileRail(() => { restorationLobbyMode = 1; ShowLobby(); }, "시나리오");
        }

        private void RecordRestorationScenarioVictory()
        {
            if (engine == null || engine.State.phase != BattlePhase.Complete || engine.State.winner != 0) return;
            var stage = FmScenarioCatalog.StageForBattle(engine.State);
            if (stage == null) return;
            PlayerPrefs.SetInt("FC.scenario.won." + stage.id, 1); PlayerPrefs.Save();
        }

        private void RestorationNextBattle()
        {
            var stage = FmScenarioCatalog.StageForBattle(engine.State);
            if (stage != null)
            {
                string id = stage.id;
                if (engine.State.winner == 0)
                {
                    var index = Array.FindIndex(FmScenarioCatalog.Stages, value => value.id == stage.id);
                    if (index + 1 < FmScenarioCatalog.Stages.Length) id = FmScenarioCatalog.Stages[index + 1].id;
                    else { restorationLobbyMode = 1; ShowLobby(); return; }
                }
                ShowRestorationScenario(id); return;
            }
            var state = engine.State; RestoreDemoSelection();
            if (state.winner == 0) selectedEncounter = Mathf.Min(2, state.encounterIndex + 1);
            ShowEncounterSelection();
        }
    }
}
