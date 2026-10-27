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
            battleScreen = true; redraw = ShowBattle; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("battle");
            var state = BattleDisplayState; var player = state.players[0]; var enemy = state.players[1];
            battleUnitViews.Clear();
            Backdrop(false);
            var field = Panel(root, .025f, .442f, .805f, .912f, new Color(.065f, .14f, .115f, .99f));
            var stone = new GameObject("Remastered green stone field", typeof(RectTransform), typeof(FmBattleStoneGraphic)).GetComponent<RectTransform>();
            stone.SetParent(field, false); Anchor(stone, 0, 0, 1, 1);
            Frame(field, new Color(.59f, .49f, .29f, .85f), 2); Ornament(field, gold);
            DrawLanes(1, .706f, .912f); DrawLanes(0, .442f, .648f);
            var messagePanel = Panel(root, .025f, .652f, .805f, .700f, new Color(.06f, .10f, .15f, .96f));
            ReadableText(messagePanel, battlePlaybackActive ? battlePlaybackIndex + "/" + battlePlaybackCount + " · " + battlePlaybackCaption : locked ? "전투 준비 중" : StatusMessage(), .012f, .02f, .988f, .98f, 22, gold);
            BindTooltip(messagePanel.gameObject, "전장 안내", battlePlaybackActive ? battlePlaybackCaption : StatusMessage());
            DrawTerrainBackfield(enemy, 1, .919f, .995f);
            DrawTerrainBackfield(player, 0, .360f, .435f);
            DrawRevivalBattleRail(); DrawScrollableHand();
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
                var panel = Panel(root, left, bottom, right, top, new Color(.035f, .055f, .095f, .48f));
                panel.gameObject.name = "Lane " + owner + " " + lane;
                panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                float visibleHeight = Mathf.Max(1, canvas.pixelRect.height * (top - bottom));
                bool compactField = canvas.pixelRect.height < 600;
                float cardBottom = Mathf.Clamp(22f / visibleHeight, .02f, .32f), cardTop = 1 - cardBottom;
                float nameBottom = 1 - 20f / visibleHeight, nameTop = 1 - 2f / visibleHeight;
                float statsBottom = 2f / visibleHeight, statsTop = 20f / visibleHeight;
                float cardRight = .985f;
                if (compactField) { cardBottom = .045f; cardTop = nameBottom - .025f; cardRight = .545f; }
                if (unit != null)
                {
                    var card = CardCatalog.Get(unit.definitionId);
                    var visual = ArcanaCardFace(panel, card, .015f, cardBottom, cardRight, cardTop, ArcanaCardMode.Field, target, unit);
                    battleUnitViews[unit.instanceId] = visual.GetComponentInChildren<FmArcanaCardSurface>().GetComponent<RectTransform>();
                    var caption = Panel(panel, .015f, nameBottom, .985f, nameTop, new Color(.025f, .04f, .075f, .95f));
                    var title = Text(caption, card.name + " · " + CardCatalog.RarityCode(card.rarity)
                        + (compactField && unit.equipment.Count > 0 ? " · 장비" + unit.equipment.Count : ""), .025f, 0, .975f, 1, 23, ivory, TextAlignmentOptions.Center);
                    title.gameObject.name = "Field name and grade caption";
                    var liveStats = Panel(panel, compactField ? .555f : .015f, compactField ? cardBottom : statsBottom,
                        .985f, compactField ? cardTop : statsTop, new Color(.025f, .04f, .075f, .95f));
                    liveStats.gameObject.name = "Field live stats caption";
                    string[] values = { "AP" + CardCatalog.UnitAttack(unit), "DP" + CardCatalog.UnitDefense(unit), "HP" + Math.Max(0, CardCatalog.UnitHealth(unit)) };
                    for (int stat = 0; stat < values.Length; stat++)
                        ArcanaText(liveStats, values[stat], compactField ? .01f : stat / 3f + .005f,
                            compactField ? (2 - stat) / 3f : 0, compactField ? .99f : (stat + 1) / 3f - .005f,
                            compactField ? (3 - stat) / 3f : 1, 20,
                            stat == 2 && unit.damage > 0 ? new Color(1, .59f, .39f) : stat == 2 ? teal : ivory, "Field live " + stat);
                    if (unit.equipment.Count > 0 && !compactField) Text(panel, "장비 " + unit.equipment.Count, .78f, .38f, .985f, .56f, 18, gold, TextAlignmentOptions.Center);
                    if (battlePlaybackActive && battleCoinResults.TryGetValue(unit.instanceId, out var faces))
                    {
                        var coins = Panel(panel, .18f, .52f, .82f, .68f, new Color(.025f, .045f, .035f, .9f));
                        coins.gameObject.name = "Resolved coin row " + unit.instanceId;
                        Text(coins, string.Join(" ", faces.Select(head => head ? "●" : "○")), .02f, .02f, .98f, .98f, 25, gold, TextAlignmentOptions.Center);
                    }
                    Ornament(panel, new Color(.55f, .73f, .80f, .40f), true);
                    Frame(panel, target ? ivory : new Color(.25f, .33f, .50f, .25f), target ? 3 : 1);
                    BindCardHelp(panel.gameObject, card, () => ShowDetail(card, ShowBattle));
                    var hover = panel.GetComponent<UiCardHover>();
                    if (hover != null && locked) hover.enabled = false;
                }
                else if (spell != null)
                {
                    if (owner == 0 || battleRevealedSpells.Contains(spell.instanceId))
                    {
                        var card = CardCatalog.Get(spell.definitionId); ArcanaCardFace(panel, card, .015f, cardBottom, cardRight, cardTop, ArcanaCardMode.Field);
                        Text(panel, card.name + " · " + CardCatalog.RarityCode(card.rarity), .025f, nameBottom, .975f, nameTop, 23, ivory, TextAlignmentOptions.Center);
                        Text(panel, (battleRevealedSpells.Contains(spell.instanceId) ? "공개 · Lv" : "예약 · Lv") + card.level,
                            compactField ? .555f : .025f, compactField ? cardBottom : statsBottom, .975f, compactField ? cardTop : statsTop, 20, teal);
                        BindCardHelp(panel.gameObject, card, () => ShowDetail(card, ShowBattle));
                    }
                    else
                    {
                        ArcanaHiddenBack(panel, .015f, cardBottom, cardRight, cardTop);
                        Text(panel, "마법 예약", .025f, nameBottom, .975f, nameTop, 23, gold, TextAlignmentOptions.Center);
                        Text(panel, "공방 시 공개", compactField ? .555f : .025f, compactField ? cardBottom : statsBottom,
                            .975f, compactField ? cardTop : statsTop, 20, ivory, TextAlignmentOptions.Center);
                    }
                }
                else
                {
                    Text(panel, (index + 1).ToString("00"), .03f, .49f, .97f, .97f, 26, target ? gold : new Color(.48f, .54f, .67f), TextAlignmentOptions.Center);
                    Text(panel, target ? "여기에 배치" : "빈 전열", .03f, .02f, .97f, .50f, 22, target ? ivory : new Color(.55f, .61f, .72f), TextAlignmentOptions.Center);
                }
                if (unit == null) Frame(panel, target ? gold : new Color(.47f, .52f, .68f, .58f), target ? 2.5f : 1);
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
                if (unit != null) ShowDetail(CardCatalog.Get(unit.definitionId), ShowBattle);
                else { message = "손패에서 카드를 고른 뒤 빛나는 전열을 선택하세요."; ShowBattle(); }
                return;
            }
            Play(lane);
        }
        private void Play(int lane) { int id = selectedCard; Act(() => engine.Play(0, id, lane, out message)); }
        private void EndTurn() { Act(() => engine.CompletePreparation(0, out message)); }
        private string Hint()
        {
            if (engine.State.phase == BattlePhase.Complete) return "결투가 끝났습니다.";
            if (engine.PreparingPlayer == 1) return "상대가 지형과 전열을 준비하고 있습니다.";
            return engine.State.phase == BattlePhase.Main ? "공격 준비 · 지형과 유닛을 배치하고 마법을 예약하세요." : "방어 준비 · 전열을 지키세요. 완료하면 왼쪽부터 자동 전투합니다.";
        }
        private void ShowBattleLog()
        {
            redraw = ShowBattleLog; Clear(); Backdrop(true);
            Text(root, "전투 기록", .055f, .84f, .945f, .96f, 42, gold);
            Text(root, string.Join("\n", engine.State.events.Skip(Math.Max(0, engine.State.events.Count - 12)).Select(item => item.text)), .06f, .19f, .94f, .82f, 23, ivory, TextAlignmentOptions.TopLeft);
            Button(root, "돌아가기", .35f, .025f, .65f, .145f, ShowBattle, gold, 26);
        }
    }
}
