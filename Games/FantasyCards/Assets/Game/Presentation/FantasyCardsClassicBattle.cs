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
            var state = engine.State; var player = state.players[0]; var enemy = state.players[1];
            Backdrop(false);
            var field = Panel(root, .025f, .362f, .805f, .928f, new Color(.035f, .075f, .044f));
            Frame(field, new Color(.43f, .42f, .25f), 2); Ornament(field, new Color(.56f, .58f, .36f, .55f));
            DrawLanes(1, .665f, .925f); DrawLanes(0, .382f, .600f);
            var messagePanel = Panel(root, .025f, .604f, .805f, .663f, new Color(.035f, .029f, .015f, .96f));
            ReadableText(messagePanel, locked ? "전투 판정 중" : StatusMessage(), .012f, .02f, .988f, .98f, 22, gold);
            BindTooltip(messagePanel.gameObject, "전장 안내", StatusMessage());
            var opponent = Panel(root, .025f, .930f, .805f, .995f, ink);
            var opponentText = Text(opponent, "상대 · 덱 " + enemy.deck.Count + "장 · 손패 " + enemy.hand.Count, .012f, .02f, .53f, .98f, 22, ivory);
            opponentText.textWrappingMode = TextWrappingModes.NoWrap;
            DrawTerrainPips(opponent, enemy, .55f, .07f, .985f, .93f);
            BindTooltip(opponent.gameObject, "상대 지형", TerrainSummary(enemy));
            var resources = Panel(root, .025f, .309f, .805f, .377f, ink);
            string resourceText = "내 손패 " + player.hand.Count + "장 · 지형 " + player.terrain.Count + "/" + state.rules.terrainLimit + "  |  카드 선택 → 빛나는 칸 · 길게 누르면 상세";
            ReadableText(resources, resourceText, .012f, .02f, .988f, .98f, 21, gold);
            BindTooltip(resources.gameObject, "내 소울과 레벨 포인트", "LP로 지형을 전개합니다. 색소울과 총소울로 카드를 사용합니다.\n" + TerrainSummary(player));
            DrawRevivalBattleRail(); DrawScrollableHand();
            if (state.phase == BattlePhase.Complete) ShowResult();
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
            var state = engine.State; float start = .025f, span = .78f;
            for (int lane = 0; lane < state.rules.unitLanes; lane++)
            {
                int index = lane; var unit = state.players[owner].AtLane(lane); var spell = state.players[owner].SpellAtLane(lane);
                float left = start + lane * span / state.rules.unitLanes, right = left + span / state.rules.unitLanes - .008f;
                bool target = IsLegalLane(owner, lane);
                var panel = Panel(root, left, bottom, right, top, new Color(.032f, .065f, .043f, .92f));
                panel.gameObject.name = "Lane " + owner + " " + lane;
                panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (unit != null)
                {
                    var card = CardCatalog.Get(unit.definitionId);
                    Artwork(panel, card, .02f, .23f, .98f, .76f);
                    Panel(panel, .015f, .775f, .985f, .985f, new Color(.075f, .055f, .025f));
                    Panel(panel, .015f, .015f, .985f, .215f, new Color(.025f, .025f, .015f));
                    ReadableText(panel, card.name, .04f, .785f, .96f, .98f, 23, ivory);
                    string[] values = { "AP " + CardCatalog.UnitAttack(unit), "DP " + CardCatalog.UnitDefense(unit), "HP " + CardCatalog.UnitHealth(unit) };
                    for (int stat = 0; stat < values.Length; stat++)
                        ReadableText(panel, values[stat], stat / 3f + .025f, .02f, (stat + 1) / 3f - .025f, .21f, 20, stat == 2 ? teal : gold, TextAlignmentOptions.Center);
                    Ornament(panel, new Color(.53f, .42f, .24f), true);
                    Frame(panel, target ? ivory : RarityColor(card.rarity), target ? 3 : 1.5f);
                    BindCardHelp(panel.gameObject, card, () => ShowDetail(card, ShowBattle));
                }
                else if (spell != null)
                {
                    if (owner == 0)
                    {
                        var card = CardCatalog.Get(spell.definitionId); Artwork(panel, card, 0, 0, 1, 1);
                        Gradient(panel, 0, 0, 1, 1, ink, new Color(.05f, .07f, .15f, .35f));
                        Text(panel, card.name, .05f, .51f, .95f, .98f, 24, ivory);
                        Text(panel, "마법 예약 · Lv " + card.level, .05f, .02f, .95f, .50f, 23, teal);
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
