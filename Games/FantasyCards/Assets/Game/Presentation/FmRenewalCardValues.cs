using System;
using System.Collections.Generic;
using FantasyCards.Core;
using TMPro;
using UnityEngine;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private TMP_Text RenewalValueText(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color tint, string name)
        {
            var label = MiniCardText(parent, value, x0, y0, x1, y1, size, tint, name);
            label.GetComponent<UiCanvasTextSize>().ConfigureCard(size);
            label.overflowMode = TextOverflowModes.Ellipsis; return label;
        }
        private readonly Dictionary<int, BattleEvent> battleCardTransitions = new Dictionary<int, BattleEvent>();
        private void DrawRenewalCardValues(RectTransform surface, CardDefinition card, UnitState unit, bool selected)
        {
            Color grade = RarityColor(card.rarity);
            Frame(surface, selected ? gold : new Color(grade.r, grade.g, grade.b, .88f), selected ? 3 : 1.4f);
            var heading = Panel(surface, .018f, .83f, .982f, .985f, new Color(.025f, .035f, .085f, .96f));
            RenewalValueText(heading, card.name, .025f, .03f, .975f, .97f, 23, ivory, "Playable card name");
            if (unit != null)
            {
                BattleEvent transition = null;
                if (battlePlaybackActive) battleCardTransitions.TryGetValue(unit.instanceId, out transition);
                int[] current = { CardCatalog.UnitAttack(unit), CardCatalog.UnitDefense(unit), Math.Max(0, CardCatalog.UnitHealth(unit)) };
                int[] before = (int[])current.Clone();
                if (transition != null && transition.hasStatTransition)
                {
                    current = new[] { transition.afterAttack, transition.afterDefense, Math.Max(0, transition.afterHealth) };
                    before = new[] { transition.beforeAttack, transition.beforeDefense, Math.Max(0, transition.beforeHealth) };
                }
                var plate = Panel(surface, .018f, .025f, .982f, .44f, new Color(.018f, .035f, .075f, .98f));
                plate.gameObject.name = "Integrated live card values " + unit.instanceId;
                string[] labels = { "AP", "DP", "HP" };
                for (int i = 0; i < 3; i++)
                {
                    float y0 = (2 - i) / 3f, y1 = (3 - i) / 3f;
                    var row = Panel(plate, .025f, y0 + .008f, .975f, y1 - .008f, i == 2 ? new Color(.065f, .16f, .17f, .95f) : new Color(.055f, .08f, .14f, .95f));
                    int delta = current[i] - before[i];
                    // Keep a single readable row on small cards: current value plus
                    // signed change. The battle banner carries before→after.
                    string value = current[i].ToString() + (delta == 0 ? "" : " " + (delta > 0 ? "+" : "−") + Math.Abs(delta));
                    RenewalValueText(row, labels[i], .025f, .01f, .30f, .99f, 21, i == 2 ? teal : gold, "Card stat label " + labels[i]);
                    RenewalValueText(row, value, .30f, .01f, .975f, .99f, 24,
                        delta > 0 ? new Color(.59f, 1, .75f) : delta < 0 ? new Color(1, .47f, .40f) : ivory,
                        "Card live " + labels[i] + " " + unit.instanceId);
                }
                if (battlePlaybackActive && battleCoinResults.TryGetValue(unit.instanceId, out var faces))
                {
                    var strip = Panel(surface, .02f, .45f, .98f, .59f, new Color(.025f, .04f, .08f, .94f));
                    strip.gameObject.name = "On-card resolved coin row " + unit.instanceId;
                    RenewalValueText(strip, string.Join(" ", faces.ConvertAll(head => head ? "●" : "○")), .03f, .02f, .97f, .98f, 23, gold, "Actual coin faces");
                }
                if (unit.equipment.Count > 0)
                    RenewalValueText(surface, "+" + unit.equipment.Count, .72f, .72f, .96f, .85f, 20, gold, "Card equipment count");
            }
            else
            {
                var footer = Panel(surface, .018f, .025f, .982f, .345f, new Color(.018f, .035f, .075f, .97f));
                string cost = card.kind == CardKind.Terrain ? "LP " + card.landPointCost : "Lv" + card.level + " · " + CardCatalog.ElementName(card.element) + card.coloredCost + "/" + card.cost;
                RenewalValueText(footer, cost, .025f, .49f, .975f, .98f, 21, gold, "Hand card playable cost");
                string lower = card.kind == CardKind.Unit ? card.attack + CardCatalog.CoinText(card.attackCoin) + " / " + card.defense + CardCatalog.CoinText(card.defenseCoin) + " / " + card.health + CardCatalog.CoinText(card.healthCoin)
                    : CardCatalog.KindName(card.kind) + " · " + CardCatalog.RarityCode(card.rarity);
                RenewalValueText(footer, lower, .025f, .02f, .975f, .49f, 20, ivory, "Hand card stats or type");
            }
        }
    }
}
