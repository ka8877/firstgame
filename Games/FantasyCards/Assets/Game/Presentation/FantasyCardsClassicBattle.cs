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
            battleScreen = true; redraw = ShowBattle; Clear(); UpdateWorld();
            var state = engine.State; var player = state.players[0]; var enemy = state.players[1];
            Backdrop(false);
            Gradient(root, 0, 0, 1, .44f, new Color(.016f, .020f, .045f, .99f), Color.clear);
            Gradient(root, 0, .67f, 1, 1, Color.clear, new Color(.016f, .020f, .045f, .97f));
            var header = Panel(root, .018f, .88f, .982f, .993f, ink);
            Text(header, "FANTASY MASTERS", .015f, .50f, .38f, .97f, 25, gold);
            Text(header, state.turn + "턴 · " + (state.activePlayer == 0 ? "내 공격" : "상대 공격") + " / " + PhaseName(state.phase), .015f, .03f, .63f, .52f, 23, ivory);
            Button(root, "안내", .65f, .880f, .727f, .993f, () => ShowRulesHelp(ShowBattle), slate, 22, !locked);
            Button(root, "음악", .735f, .880f, .812f, .993f, () => ShowMusicSettings(ShowBattle), slate, 22, !locked);
            Button(root, "저장", .82f, .880f, .897f, .993f, () => { if (Save()) message = "전장과 다음 코인 상태를 저장했습니다."; ShowBattle(); }, slate, 22, !locked);
            Button(root, "타이틀", .905f, .880f, .977f, .993f, () => LeaveBattle(ShowTitle), slate, 21, !locked);

            var opponent = Panel(root, .025f, .755f, .975f, .872f, new Color(.085f, .09f, .17f, .96f));
            Portrait(opponent, enemy.leaderId, .008f, .04f, .082f, .96f);
            Text(opponent, DemoCatalog.GetLeader(enemy.leaderId).name + " · 덱 " + enemy.deck.Count + "장", .10f, .46f, .51f, .97f, 28, ivory);
            Text(opponent, "LP " + enemy.levelPoints + " · 손패 " + enemy.hand.Count + " · 지형 " + enemy.terrain.Count + "/" + state.rules.terrainLimit, .10f, .03f, .51f, .48f, 22, gold);
            Text(opponent, SoulSummary(enemy), .53f, .48f, .98f, .97f, 23, teal, TextAlignmentOptions.MidlineRight);
            DrawTerrainPips(opponent, enemy, .53f, .07f, .98f, .43f);
            BindTooltip(opponent.gameObject, "상대 · 덱이 생명입니다", "직접 공격은 유닛 레벨만큼 덱을 파괴합니다. 상대 손패와 예약 마법의 내용은 공개되지 않습니다.\n" + TerrainSummary(enemy));

            DrawLanes(1, .600f, .745f);
            DrawLanes(0, .325f, .470f);
            var middle = Panel(root, .025f, .475f, .975f, .598f, new Color(.065f, .065f, .14f, .97f));
            Text(middle, locked ? "전투 판정 중" : StatusMessage(), .018f, .50f, .59f, .97f, 22, ivory);
            Text(middle, "AP 공격 / DP 방어 / HP 생명 · 전열 왼쪽부터 판정", .018f, .015f, .60f, .50f, 19, new Color(.75f, .79f, .88f));
            DrawActions(.61f, .480f, .970f, .593f);

            var resources = Panel(root, .025f, .270f, .975f, .318f, ink);
            Text(resources, "나 · 덱 " + player.deck.Count + "장   손패 " + player.hand.Count + "   LP " + player.levelPoints + "   " + SoulSummary(player) + "   지형 " + player.terrain.Count + "/" + state.rules.terrainLimit,
                .012f, .02f, .988f, .98f, 23, gold);
            BindTooltip(resources.gameObject, "내 소울과 레벨 포인트", "LP는 지형 전개에 사용합니다. 지형에서 얻은 색소울과 총소울로 유닛·마법·아이템을 사용합니다.\n" + TerrainSummary(player));
            DrawPlayerLeader();
            if (player.hand.Count > 5) DrawScrollableHand();
            else
            {
                float width = Mathf.Min(.16f, .79f / Mathf.Max(1, player.hand.Count)); float left = .59f - player.hand.Count * width * .5f;
                for (int i = 0; i < player.hand.Count; i++)
                {
                    var instance = player.hand[i]; var card = CardCatalog.Get(instance.definitionId); int id = instance.instanceId;
                    DrawCard(root, card, left + i * width, .022f, left + (i + 1) * width - .008f, .255f, selectedCard == id,
                        () => SelectHandCard(id, card), !locked);
                }
            }
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
            var state = engine.State; float start = .025f, span = .95f;
            for (int lane = 0; lane < state.rules.unitLanes; lane++)
            {
                int index = lane; var unit = state.players[owner].AtLane(lane); var spell = state.players[owner].SpellAtLane(lane);
                float left = start + lane * span / state.rules.unitLanes, right = left + span / state.rules.unitLanes - .008f;
                bool target = IsLegalLane(owner, lane);
                var panel = Panel(root, left, bottom, right, top, new Color(.075f, .095f, .17f, .96f));
                panel.gameObject.name = "Lane " + owner + " " + lane;
                panel.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                if (unit != null)
                {
                    var card = CardCatalog.Get(unit.definitionId);
                    Artwork(panel, card, 0, 0, 1, 1);
                    Gradient(panel, 0, 0, 1, 1, new Color(.01f, .015f, .035f, .98f), new Color(.01f, .015f, .035f, .38f));
                    Text(panel, card.name, .045f, .51f, .955f, .98f, 25, ivory);
                    Text(panel, "AP " + CardCatalog.UnitAttack(unit) + "  DP " + CardCatalog.UnitDefense(unit) + "  HP " + CardCatalog.UnitHealth(unit),
                        .045f, .02f, .955f, .50f, 24, gold);
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
