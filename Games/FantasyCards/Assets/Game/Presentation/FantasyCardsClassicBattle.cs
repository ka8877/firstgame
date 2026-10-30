using System;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private void ShowBattle()
        {
            QuestCurrentScreen = questBattleMode ? "quest-battle" : "practice-battle";
            battleScreen = true; redraw = ShowBattle; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("battle");
            var state = BattleDisplayState; var player = state.players[0]; var enemy = state.players[1];
            battleUnitViews.Clear(); ChronicleVisibleCoinPanels = 0;
            DrawChronicleBackdrop();
            DrawLanes(1, .660f, .910f); DrawLanes(0, .365f, .615f);
            var messagePanel = ChroniclePanel(root, .025f, .618f, .805f, .656f, new Color(.012f, .024f, .045f, .96f), false);
            ReadableText(messagePanel, battlePlaybackActive ? battlePlaybackIndex + "/" + battlePlaybackCount + " · " + battlePlaybackCaption : IsClientShuffleActive ? "손패 → 덱 회수 · 셔플 · 순서대로 받기" : locked ? "전투 준비 중" : StatusMessage(), .012f, .02f, .988f, .98f, 22, gold);
            BindTooltip(messagePanel.gameObject, "전장 안내", battlePlaybackActive ? battlePlaybackCaption : StatusMessage());
            DrawTerrainBackfield(enemy, 1, .919f, .995f);
            DrawChronicleLocalFeed();
            if (!ChronicleFocusedBattle) DrawTerrainBackfield(player, 0, .285f, .358f);
            DrawRevivalBattleRail(); StyleChronicleBattleRail();
            if (ChronicleFocusedBattle) DrawChronicleBattleTerrain(); else DrawScrollableHand();
            DrawBattlePlaybackControls();
            if (!battlePlaybackActive && state.phase == BattlePhase.Complete) ShowResult();
        }

        private static string SoulSummary(PlayerState player)
        {
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>()
                .Where(element => player.Resource(element) > 0 || player.terrain.Any(card => CardCatalog.Get(card.definitionId).element == element));
            string colored = string.Join(" · ", elements.Select(element => CardCatalog.ElementName(element) + " " + player.Resource(element)));
            return (string.IsNullOrEmpty(colored) ? "색소울 0" : colored) + " · 총소울 " + player.genericSoul;
        }
        private static string TerrainSummary(PlayerState player)
        {
            return "지형: " + (player.terrain.Count == 0 ? "아직 없음" : string.Join(" · ", player.terrain.Select(card => CardCatalog.Get(card.definitionId).name)));
        }
        private void DrawActions(float x0, float y0, float x1, float y1)
        {
            if (!engine.IsPreparing(0))
            {
                Text(root, engine.State.phase == BattlePhase.Complete ? "결투 종료" : "상대가 준비하고 있습니다", x0, y0, x1, y1, 21, teal, TextAlignmentOptions.MidlineRight);
                return;
            }
            var instance = engine.State.players[0].hand.Find(card => card.instanceId == selectedCard);
            var definition = instance == null ? null : CardCatalog.Get(instance.definitionId);
            float middle = Mathf.Lerp(x0, x1, .47f);
            if (definition != null && definition.kind == CardKind.Terrain)
                Button(root, "지형 전개", x0, y0, middle - .006f, y1, () => Play(-1), gold, 22, !locked);
            else if (definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition))
                Button(root, "마법 예약", x0, y0, middle - .006f, y1, () => Play(-1), teal, 22, !locked);
            else Button(root, "카드 상세", x0, y0, middle - .006f, y1, () => ShowDetail(CardCatalog.Get(inspectedId), ShowBattle), slate, 21, !locked);
            Button(root, "준비 완료", middle + .006f, y0, x1, y1, EndTurn, gold, 23, !locked);
        }

        private void DrawLanes(int owner, float bottom, float top)
        {
            var state = BattleDisplayState; float start = .025f, span = .78f;
            for (int lane = 0; lane < state.rules.unitLanes; lane++)
            {
                int index = lane; var unit = state.players[owner].AtLane(lane); var spell = state.players[owner].SpellAtLane(lane);
                float left = start + lane * span / state.rules.unitLanes, right = left + span / state.rules.unitLanes - .008f;
                bool target = IsLegalLane(owner, lane);
                var panel = ChroniclePanel(root, left, bottom, right, top, new Color(.005f, .013f, .055f, .28f), false);
                panel.gameObject.name = "Lane " + owner + " " + lane;
                panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                const float cardBottom = .025f, cardTop = .975f, cardRight = .985f;
                if (unit != null)
                {
                    var card = CardCatalog.Get(unit.definitionId);
                    var visual = ArcanaCardFace(panel, card, .015f, cardBottom, cardRight, cardTop, ArcanaCardMode.Field, target || selectedAttacker == unit.instanceId, unit);
                    battleUnitViews[unit.instanceId] = visual.GetComponentInChildren<FmArcanaCardSurface>().GetComponent<RectTransform>();
                    BindCardHelp(panel.gameObject, card, () => ShowDetail(card, ShowBattle));
                    var hover = panel.GetComponent<UiCardHover>();
                    if (hover != null) { hover.HoverScale = 1.055f; if (locked) hover.enabled = false; }
                }
                else if (spell != null)
                {
                    if (owner == 0 || battleRevealedSpells.Contains(spell.instanceId))
                    {
                        var card = CardCatalog.Get(spell.definitionId); ArcanaCardFace(panel, card, .015f, cardBottom, cardRight, cardTop, ArcanaCardMode.Field, selectedAttacker == spell.instanceId);
                        // Reserved magic stays a card. Its reveal marker is integrated on its face.
                        BindCardHelp(panel.gameObject, card, () => ShowDetail(card, ShowBattle));
                    }
                    else
                    {
                        ArcanaHiddenBack(panel, .015f, cardBottom, cardRight, cardTop);
                        Text(panel, "마법 예약", .04f, .03f, .96f, .17f, 23, gold, TextAlignmentOptions.Center);
                    }
                }
                else
                {
                    Text(panel, new[] { "I", "II", "III", "IV", "V" }[index], .03f, .49f, .97f, .97f, 26, target ? gold : new Color(.48f, .54f, .67f), TextAlignmentOptions.Center);
                    Text(panel, target ? "여기에 배치" : "빈 전열", .03f, .02f, .97f, .50f, 22, target ? ivory : new Color(.55f, .61f, .72f), TextAlignmentOptions.Center);
                }
                if (unit == null) Frame(panel, target ? gold : new Color(.24f, .37f, .80f, .38f), target ? 2.5f : 1);
                panel.GetComponent<Image>().raycastTarget = true;
                var button = panel.gameObject.AddComponent<Button>(); button.interactable = !locked; button.onClick.AddListener(() => LaneClick(owner, index));
                BindLaneDrop(panel, owner, lane);
            }
        }
        private bool IsLegalLane(int owner, int lane)
        {
            if (locked || !engine.IsPreparing(0)) return false;
            var instance = engine.State.players[0].hand.Find(card => card.instanceId == selectedCard); if (instance == null) return false;
            var definition = CardCatalog.Get(instance.definitionId);
            int targetOwner = TargetsEnemy(definition) ? 1 : 0;
            return definition.kind != CardKind.Terrain && owner == targetOwner && engine.CanPlay(0, selectedCard, lane, out _);
        }
        private void LaneClick(int owner, int lane)
        {
            if (locked) return;
            var unit = engine.State.players[owner].AtLane(lane);
            if (unit != null) inspectedId = unit.definitionId;
            if (!IsLegalLane(owner, lane))
            {
                if (unit != null) SelectPolishPublicCard(owner, unit.instanceId, CardCatalog.Get(unit.definitionId), "field");
                else
                {
                    var spell = engine.State.players[owner].SpellAtLane(lane);
                    if (spell != null && (owner == 0 || battleRevealedSpells.Contains(spell.instanceId)))
                        SelectPolishPublicCard(owner, spell.instanceId, CardCatalog.Get(spell.definitionId), "spell");
                    else { message = "손패에서 카드를 고른 뒤 빛나는 전열을 선택하세요."; ShowBattle(); }
                }
                return;
            }
            Play(lane);
        }
        private void Play(int lane) { ResetPolishTap(); int id = selectedCard; Act(() => engine.Play(0, id, lane, out message)); }
        private void EndTurn() { ResetPolishTap(); Act(() => engine.CompletePreparation(0, out message)); }
        private string Hint()
        {
            if (engine.State.phase == BattlePhase.Complete) return "결투가 끝났습니다.";
            if (engine.PreparingPlayer == 1) return "상대가 지형과 전열을 준비하고 있습니다.";
            return engine.State.phase == BattlePhase.Main ? "공격 준비 · 지형과 유닛을 배치하고 마법을 예약하세요." : "방어 준비 · 전열을 지키세요. 완료하면 왼쪽부터 자동 전투합니다.";
        }
        private void ShowBattleLog()
        {
            ShowPolishBattleDesk(1);
        }
    }
}
