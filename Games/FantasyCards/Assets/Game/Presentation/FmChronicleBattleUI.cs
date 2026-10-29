using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private readonly Dictionary<int, int> chronicleCoinTotals = new Dictionary<int, int>();
        public int ChronicleVisibleCoinPanels { get; private set; }
        private bool ChronicleFocusedBattle => battlePlaybackActive && battleClashContext != null;

        private RectTransform ChroniclePanel(Transform parent, float x0, float y0, float x1, float y1, Color fill, bool frame = true)
        {
            var panel = Panel(parent, x0, y0, x1, y1, fill);
            var shape = panel.GetComponent<UiRoundedImage>(); shape.Radius = 0;
            shape.TopTint = shape.BottomTint = Color.white;
            if (frame)
            {
                Frame(panel, new Color(.56f, .54f, .43f, .88f), 1);
                var edge = panel.Find("Fine rounded frame").GetComponent<UiRoundedImage>();
                edge.Radius = 0; edge.BorderWidth = 1; edge.BorderColor = new Color(.56f, .54f, .43f, .88f);
            }
            return panel;
        }

        private void DrawChronicleBackdrop()
        {
            var field = ChroniclePanel(root, 0, 0, .813f, 1, new Color(.005f, .012f, .04f), false);
            var art = new GameObject("Chronicle dark blue field", typeof(RectTransform), typeof(FmBattleStoneGraphic)).GetComponent<RectTransform>();
            art.SetParent(field, false); Anchor(art, 0, 0, 1, 1);
            ChroniclePanel(root, .813f, 0, 1, 1, new Color(.015f, .023f, .035f), false);
        }

        private void StyleChronicleBattleRail()
        {
            // Clear disables old views immediately; Destroy retires them at the
            // end of the frame. Never restyle that stale, invisible rail.
            var rail = root.Cast<Transform>().LastOrDefault(child => child.gameObject.activeSelf && child.name == "Battle resource rail");
            if (rail == null) return;
            foreach (var shape in rail.GetComponentsInChildren<UiRoundedImage>())
            {
                shape.Radius = 1;
                shape.TopTint = new Color(1.06f, 1.06f, 1.06f);
                shape.BottomTint = new Color(.86f, .88f, .93f);
                if (!shape.Fill) shape.BorderColor = new Color(.55f, .56f, .56f, .7f);
                shape.SetAllDirty();
            }
        }

        private void DrawChronicleBattleBrand(Transform parent, float x0, float y0, float x1, float y1)
        {
            var header = ChroniclePanel(parent, x0, y0, x1, y1, new Color(.015f, .022f, .034f), false);
            var orb = Panel(header, .025f, .04f, .27f, .96f, Color.clear);
            RestorationTrim(orb, true);
            RenewalValueText(header, "Fantasy Masters", .30f, .04f, .98f, .96f, 25, new Color(.84f, .83f, .73f), "Client brand");
        }

        private void DrawChronicleLocalFeed()
        {
            var events = battlePlaybackActive ? battlePresentedEvents : engine.State.events;
            int rows = canvas.pixelRect.height < 600 ? 1 : 2;
            var recent = events.Skip(Mathf.Max(0, events.Count - rows)).ToArray();
            for (int i = 0; i < recent.Length; i++)
            {
                var entry = recent[i]; float top = .993f - i * .037f;
                string line = (entry.actor == 0 ? "나" : entry.actor == 1 ? "상대 AI" : "전장") + " · " + (entry.text ?? "").Replace('\n', ' ');
                var text = RenewalValueText(root, line, .027f, top - (rows == 1 ? .070f : .035f), .475f, top, 20,
                    entry.actor == 0 ? new Color(.70f, .94f, .54f) : new Color(.83f, .87f, .79f), "Chronicle local battle line " + i);
                text.overflowMode = TextOverflowModes.Ellipsis;
            }
        }

        private void DrawChronicleEnemyTerrain(PlayerState player)
        {
            var strip = ChroniclePanel(root, .49f, .919f, .805f, .995f, new Color(.01f, .022f, .04f, .92f));
            strip.gameObject.name = "Opponent terrain backfield";
            RenewalValueText(strip, "상대 지형", .01f, .51f, .22f, .98f, 18, teal, "Opponent terrain heading");
            RenewalValueText(strip, player.terrain.Count.ToString(), .01f, .02f, .22f, .49f, 19, ivory, "Opponent terrain count");
            int rows = Mathf.Max(2, (BattleDisplayState.rules.terrainLimit + 4) / 5);
            for (int i = 0; i < BattleDisplayState.rules.terrainLimit; i++)
            {
                int column = i % 5, row = i / 5;
                float x = .235f + column * .15f, top = .96f - row * .92f / rows;
                var slot = ChroniclePanel(strip, x, top - .88f / rows, x + .14f, top, new Color(.025f, .047f, .075f));
                slot.gameObject.name = "Backfield 1 " + i;
                if (i >= player.terrain.Count) continue;
                var instance = player.terrain[i]; var card = CardCatalog.Get(instance.definitionId);
                RenewalSourcePrint(slot, card.artResource, .04f, .03f, .96f, .97f, "Original enemy terrain print");
                slot.GetComponent<Image>().raycastTarget = true;
                var button = slot.gameObject.AddComponent<Button>(); button.interactable = !locked;
                button.onClick.AddListener(() => SelectPolishPublicCard(1, instance.instanceId, card, "terrain"));
                BindCardHelp(slot.gameObject, card, () => ShowDetail(card, ShowBattle));
            }
        }

        private void DrawChronicleBattleTerrain()
        {
            var lands = BattleDisplayState.players[0].terrain;
            var viewport = ChroniclePanel(root, .025f, .012f, .805f, .223f, new Color(.008f, .017f, .035f, .88f), false);
            viewport.gameObject.name = "Chronicle battle terrain strip"; viewport.GetComponent<Image>().raycastTarget = true;
            viewport.gameObject.AddComponent<RectMask2D>();
            var content = new GameObject("Battle terrain content", typeof(RectTransform)).GetComponent<RectTransform>();
            content.SetParent(viewport, false); content.pivot = new Vector2(0, .5f);
            Anchor(content, 0, 0, Mathf.Max(1, lands.Count / 10f), 1);
            for (int i = 0; i < lands.Count; i++)
            {
                float count = Mathf.Max(10, lands.Count);
                ArcanaCardFace(content, CardCatalog.Get(lands[i].definitionId), i / count, .02f, (i + .95f) / count, .98f, ArcanaCardMode.Album);
            }
            var scroll = viewport.gameObject.AddComponent<ScrollRect>(); scroll.viewport = viewport; scroll.content = content;
            scroll.horizontal = true; scroll.vertical = false; scroll.movementType = ScrollRect.MovementType.Clamped;
            RenewalValueText(root, "내 지형 · " + lands.Count, .027f, .23f, .30f, .275f, 19, teal, "Battle terrain strip count");
        }

        private RectTransform DrawChronicleCoinWindows(Transform parent, BattleEvent active)
        {
            ChronicleVisibleCoinPanels = 0; RectTransform spinning = null;
            if (battleClashContext == null) return null;
            for (int owner = 0; owner < 2; owner++)
            {
                int id = battleClashContext.sourcePlayer == owner ? battleClashContext.sourceInstanceId :
                    battleClashContext.targetPlayer == owner ? battleClashContext.targetInstanceId : -1;
                var unit = battleVisualState.players[owner].units.FirstOrDefault(value => value.instanceId == id);
                if (unit == null) continue;
                var window = DrawChronicleCoinWindow(parent, owner, unit, active);
                if (window != null) spinning = window;
                ChronicleVisibleCoinPanels++;
            }
            return spinning;
        }

        private RectTransform DrawChronicleCoinWindow(Transform parent, int owner, UnitState unit, BattleEvent active)
        {
            // Upper opponent / lower player windows follow the two real field
            // rows. They sit beside the full card, never over its printed values.
            bool mine = owner == 0;
            var panel = ChroniclePanel(parent, mine ? .025f : .493f, mine ? .365f : .660f,
                mine ? .337f : .805f, mine ? .610f : .905f, new Color(.012f, .019f, .03f, .99f));
            panel.gameObject.name = "Chronicle coin result " + owner + " " + unit.instanceId;
            int chance = OriginalEquipmentModifiers.ChancePercent(unit);
            RenewalValueText(panel, (mine ? "나" : "상대") + "  COIN", .025f, .835f, .45f, .985f, 22, new Color(.89f, .83f, .59f), "Coin owner " + owner);
            RenewalValueText(panel, "Coin : " + chance + "%", .45f, .835f, .975f, .985f, 22, ivory, "Actual coin chance " + owner).alignment = TextAlignmentOptions.MidlineRight;
            battleCoinResults.TryGetValue(unit.instanceId, out var faces);
            chronicleCoinTotals.TryGetValue(unit.instanceId, out int total);
            var definition = CardCatalog.Get(unit.definitionId);
            CoinRule[] rules = { definition.attackCoin, definition.defenseCoin, definition.healthCoin };
            OperationStat[] stats = { OperationStat.Attack, OperationStat.Defense, OperationStat.Health };
            RectTransform current = null;
            for (int slot = 0; slot < 5; slot++)
            {
                float x = .025f + slot * .19f;
                var cell = ChroniclePanel(panel, x, .425f, x + .18f, .82f, new Color(.018f, .027f, .045f, .96f));
                cell.gameObject.name = "Coin slot " + owner + " " + (slot + 1);
                if (faces == null || slot >= faces.Count)
                {
                    if (slot < total) RenewalValueText(cell, "·", .05f, .05f, .95f, .95f, 20, new Color(.45f, .46f, .43f), "Unresolved coin slot");
                    continue;
                }
                bool heads = faces[slot];
                var face = new GameObject("Resolved coin " + owner + " " + (slot + 1) + (heads ? " heads" : " tails"), typeof(RectTransform), typeof(FmChronicleCoinGraphic)).GetComponent<RectTransform>();
                face.SetParent(cell, false); Anchor(face, .07f, .28f, .93f, .97f);
                face.GetComponent<FmChronicleCoinGraphic>().Heads = heads;
                var fit = face.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 1;
                if (heads)
                {
                    var stamp = new Color(.27f, .18f, .035f);
                    RenewalValueText(face, "FANTASY", .12f, .48f, .88f, .70f, 17, stamp, "Actual heads stamp").alignment = TextAlignmentOptions.Center;
                    RenewalValueText(face, "MASTERS", .12f, .26f, .88f, .48f, 17, stamp, "Actual heads stamp").alignment = TextAlignmentOptions.Center;
                }
                else
                {
                    // Late live footage uses visibly different portrait reverses.
                    // Reuse this unit's existing original art inside a round rim;
                    // this is a reconstructed skin, not a recovered GUI texture.
                    var reverse = Panel(face, .105f, .105f, .895f, .895f, Color.black);
                    reverse.GetComponent<UiRoundedImage>().Radius = 1000;
                    reverse.gameObject.AddComponent<Mask>().showMaskGraphic = true;
                    Artwork(reverse, definition, 0, 0, 1, 1);
                }
                for (int stat = 0; stat < 3; stat++)
                {
                    int participates = BattleDisplayState.rules.baseCoins >= 0 ? total :
                        OriginalEquipmentModifiers.CoinCount(rules[stat], OriginalEquipmentModifiers.CoinDelta(unit, stats[stat]));
                    if (rules[stat].operation == CoinOperation.None || slot >= participates) continue;
                    var symbol = new GameObject("Coin slot stat " + owner + " " + (slot + 1) + " " + stats[stat], typeof(RectTransform), typeof(FmChronicleStatGraphic)).GetComponent<FmChronicleStatGraphic>();
                    symbol.transform.SetParent(cell, false); symbol.Stat = stat;
                    Anchor((RectTransform)symbol.transform, .04f + stat * .31f, .025f, .29f + stat * .31f, .27f);
                }
                if (active.kind == "coin" && active.sourceInstanceId == unit.instanceId && slot == faces.Count - 1) current = face;
            }
            ChronicleValues(unit, out var before, out var values);
            string[] labels = { "AP", "DP", "HP" };
            for (int i = 0; i < 3; i++)
            {
                float x = .025f + i * .32f;
                var stat = ChroniclePanel(panel, x, .025f, x + .31f, .405f, new Color(.025f, .037f, .055f), false);
                var icon = new GameObject("Printed stat symbol " + labels[i], typeof(RectTransform), typeof(FmChronicleStatGraphic)).GetComponent<FmChronicleStatGraphic>();
                icon.transform.SetParent(stat, false); Anchor((RectTransform)icon.transform, .025f, .52f, .20f, .95f); icon.Stat = i;
                RenewalValueText(stat, labels[i] + " " + values[i], .23f, .50f, .99f, .97f, 24, ivory, "Coin result " + owner + " " + labels[i]);
                int delta = values[i] - before[i];
                string result = delta == 0 ? "유지" : before[i] + "→" + values[i] + " " + (delta > 0 ? "+" : "−") + Mathf.Abs(delta);
                RenewalValueText(stat, result, .02f, .025f, .99f, .49f, 20,
                    delta > 0 ? new Color(.55f, .96f, .64f) : delta < 0 ? new Color(1, .46f, .36f) : new Color(.61f, .65f, .71f),
                    "Coin result change " + owner + " " + labels[i]);
            }
            return current;
        }
    }
}
