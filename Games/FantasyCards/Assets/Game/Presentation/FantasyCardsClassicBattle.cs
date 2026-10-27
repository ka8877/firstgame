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
            var field = Panel(root, .025f, .362f, .805f, .928f, new Color(.065f, .14f, .115f, .99f));
            var stone = new GameObject("Remastered green stone field", typeof(RectTransform), typeof(FmBattleStoneGraphic)).GetComponent<RectTransform>();
            stone.SetParent(field, false); Anchor(stone, 0, 0, 1, 1);
            Frame(field, new Color(.59f, .49f, .29f, .85f), 2); Ornament(field, gold);
            DrawLanes(1, .665f, .925f); DrawLanes(0, .382f, .600f);
            var messagePanel = Panel(root, .025f, .604f, .805f, .663f, new Color(.06f, .10f, .15f, .96f));
            ReadableText(messagePanel, battlePlaybackActive ? battlePlaybackIndex + "/" + battlePlaybackCount + " · " + battlePlaybackCaption : locked ? "전투 준비 중" : StatusMessage(), .012f, .02f, .988f, .98f, 22, gold);
            BindTooltip(messagePanel.gameObject, "전장 안내", battlePlaybackActive ? battlePlaybackCaption : StatusMessage());
            var opponent = Panel(root, .025f, .930f, .805f, .995f, ink);
            var opponentText = Text(opponent, "상대 · " + (state.activePlayer == 1 ? "공격" : "방어") + " · 덱 " + enemy.deck.Count + " · 손패 " + enemy.hand.Count, .012f, .02f, .53f, .98f, 22, ivory);
            opponentText.textWrappingMode = TextWrappingModes.NoWrap;
            DrawTerrainPips(opponent, enemy, .55f, .07f, .985f, .93f);
            BindTooltip(opponent.gameObject, "상대 지형", TerrainSummary(enemy));
            var resources = Panel(root, .025f, .309f, .805f, .377f, ink);
            resources.gameObject.name = "Terrain drop band";
            string resourceText = "내 손패 " + player.hand.Count + "장 · 지형 " + player.terrain.Count + "/" + state.rules.terrainLimit + "  |  카드 선택 → 빛나는 칸 · 길게 누르면 상세";
            if (canvas.pixelRect.height < 600) resourceText = "손패 " + player.hand.Count + "장 · AP/DP/HP · 선택→전열 · 길게 눌러 상세";
            ReadableText(resources, resourceText, .012f, .02f, .988f, .98f, 21, gold);
            BindTooltip(resources.gameObject, "내 소울과 레벨 포인트", "LP로 지형을 전개합니다. 색소울과 총소울로 카드를 사용합니다.\n" + TerrainSummary(player));
            BindTerrainDrop(resources);
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
        private void DrawTerrainPips(Transform parent, PlayerState player, float x0, float y0, float x1, float y1)
        {
            for (int i = 0; i < engine.State.rules.terrainLimit; i++)
            {
                var card = i < player.terrain.Count ? CardCatalog.Get(player.terrain[i].definitionId) : null;
                float width = (x1 - x0) / engine.State.rules.terrainLimit, x = x0 + i * width;
                var pip = Panel(parent, x, y0, x + width - .005f, y1, card == null ? new Color(.18f, .20f, .29f) : ElementColor(card.element) * new Color(.65f, .65f, .65f, 1));
                Text(pip, card == null ? "·" : CardCatalog.ElementName(card.element).Substring(0, 1), .03f, .02f, .97f, .98f, 18, ivory, TextAlignmentOptions.Center);
            }
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
                var panel = Panel(root, left, bottom, right, top, new Color(.035f, .07f, .055f, .68f));
                panel.gameObject.name = "Lane " + owner + " " + lane;
                panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (unit != null)
                {
                    var card = CardCatalog.Get(unit.definitionId);
                    battleUnitViews[unit.instanceId] = panel;
                    var printed = Resources.Load<Texture2D>(card.artResource);
                    if (printed != null) FittedImage(panel, printed, new Rect(0, 0, 1, 1), .015f, .02f, .985f, .98f, "Original printed battlefield card");
                    else Artwork(panel, card, .02f, .23f, .98f, .76f);
                    Panel(panel, .015f, .775f, .985f, .985f, new Color(.09f, .145f, .19f));
                    Panel(panel, .015f, .015f, .985f, .215f, new Color(.045f, .075f, .11f));
                    ReadableText(panel, card.name, .04f, .785f, .96f, .98f, 23, ivory);
                    string[] values = { "AP " + CardCatalog.UnitAttack(unit), "DP " + CardCatalog.UnitDefense(unit), "HP " + Math.Max(0, CardCatalog.UnitHealth(unit)) };
                    for (int stat = 0; stat < values.Length; stat++)
                        ReadableText(panel, values[stat], stat / 3f + .025f, .02f, (stat + 1) / 3f - .025f, .21f, 20, stat == 2 && unit.damage > 0 ? new Color(1, .59f, .39f) : stat == 2 ? teal : gold, TextAlignmentOptions.Center);
                    DrawCardGradeBadges(panel, card, .23f, .76f, canvas.pixelRect.height < 600);
                    if (unit.equipment.Count > 0) Text(panel, "장비 " + unit.equipment.Count, .05f, .61f, .95f, .77f, 18, gold, TextAlignmentOptions.MidlineRight);
                    if (battlePlaybackActive && battleCoinResults.TryGetValue(unit.instanceId, out var faces))
                    {
                        var coins = Panel(panel, .18f, .52f, .82f, .68f, new Color(.025f, .045f, .035f, .9f));
                        coins.gameObject.name = "Resolved coin row " + unit.instanceId;
                        Text(coins, string.Join(" ", faces.Select(head => head ? "●" : "○")), .02f, .02f, .98f, .98f, 25, gold, TextAlignmentOptions.Center);
                    }
                    Ornament(panel, new Color(.55f, .73f, .80f, .40f), true);
                    Frame(panel, target ? ivory : RarityColor(card.rarity), target ? 3 : 1.5f);
                    BindCardHelp(panel.gameObject, card, () => ShowDetail(card, ShowBattle));
                    var hover = panel.GetComponent<UiCardHover>();
                    if (hover != null && locked) hover.enabled = false;
                }
                else if (spell != null)
                {
                    if (owner == 0 || battleRevealedSpells.Contains(spell.instanceId))
                    {
                        var card = CardCatalog.Get(spell.definitionId); Artwork(panel, card, 0, 0, 1, 1);
                        Gradient(panel, 0, 0, 1, 1, ink, new Color(.05f, .07f, .15f, .35f));
                        Text(panel, card.name, .05f, .51f, .95f, .98f, 24, ivory);
                        Text(panel, (battleRevealedSpells.Contains(spell.instanceId) ? "마법 공개 · Lv " : "마법 예약 · Lv ") + card.level, .05f, .02f, .95f, .50f, 23, teal);
                        BindCardHelp(panel.gameObject, card, () => ShowDetail(card, ShowBattle));
                    }
                    else
                    {
                        var back = Resources.Load<Texture2D>("Art/ClassicCards/Decor/card-back");
                        if (back != null) ImageFrame(panel, back, new Rect(0, 0, 1, 1), 0, 0, 1, 1, new Vector2(.5f, .5f));
                        Panel(panel, 0, 0, 1, 1, new Color(.025f, .02f, .045f, .72f));
                        Text(panel, "마법 예약", .05f, .30f, .95f, .85f, 25, gold, TextAlignmentOptions.Center);
                        Text(panel, "공방 시 공개", .05f, .03f, .95f, .40f, 20, ivory, TextAlignmentOptions.Center);
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
