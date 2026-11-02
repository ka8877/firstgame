using System;
using System.Collections.Generic;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private TMP_Text RenewalValueText(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color tint, string name)
        {
            var label = MiniCardText(parent, value, x0, y0, x1, y1, size, tint, name);
            label.GetComponent<UiCanvasTextSize>().ConfigureCard(size);
            label.overflowMode = TextOverflowModes.Overflow; return label;
        }
        private readonly Dictionary<int, BattleEvent> battleCardTransitions = new Dictionary<int, BattleEvent>();

        private void ChronicleValues(UnitState unit, out int[] before, out int[] current)
        {
            current = new[] { CardCatalog.UnitAttack(unit), CardCatalog.UnitDefense(unit), Math.Max(0, CardCatalog.UnitHealth(unit)) };
            before = (int[])current.Clone();
            if (battlePlaybackActive && battleCardTransitions.TryGetValue(unit.instanceId, out var change) && change.hasStatTransition)
            {
                current = new[] { change.afterAttack, change.afterDefense, Math.Max(0, change.afterHealth) };
                before = new[] { change.beforeAttack, change.beforeDefense, Math.Max(0, change.beforeHealth) };
            }
        }

        private void DrawRenewalCardValues(RectTransform surface, CardDefinition card, UnitState unit, bool selected)
        {
            // The original paper/foil can be white or gold: sampling that
            // paper underneath ivory digits made several entire sets illegible.
            // Replace only the numeral column with opaque ink plates. Names,
            // art, grade, rules and the printed stat icons remain source pixels.
            if (card.kind != CardKind.Unit) return;
            int[] before, current;
            if (unit != null) ChronicleValues(unit, out before, out current);
            else { current = new[] { card.attack, card.defense, card.health }; before = (int[])current.Clone(); }
            var container = Panel(surface, 0, 0, 1, 1, Color.clear);
            string identity = unit == null ? "base " + card.id : unit.instanceId.ToString();
            container.gameObject.name = "Integrated live card values " + identity;
            string[] labels = { "AP", "DP", "HP" };
            float[] bottoms = { .171f, .106f, .040f }, tops = { .233f, .168f, .102f };
            for (int i = 0; i < 3; i++)
            {
                var patch = Panel(container, .851f, bottoms[i], .983f, tops[i], new Color(.014f, .020f, .032f, 1));
                patch.gameObject.name = "Opaque " + labels[i] + " numeral plate " + identity;
                var plate = patch.GetComponent<UiRoundedImage>();
                plate.Radius = 1; plate.TopTint = plate.BottomTint = Color.white;
                plate.BorderWidth = .5f; plate.BorderColor = new Color(.57f, .59f, .65f, .75f);
                int delta = current[i] - before[i];
                var label = RenewalValueText(container, current[i].ToString(), .856f, bottoms[i], .978f, tops[i], 34,
                    delta > 0 ? new Color(.66f, 1, .75f) : delta < 0 ? new Color(1, .63f, .54f) : Color.white,
                    "Card live " + labels[i] + " " + identity);
                label.alignment = TextAlignmentOptions.Center; label.fontStyle = FontStyles.Bold;
                label.outlineWidth = .07f; label.outlineColor = new Color32(0, 0, 0, 255);
                label.enableAutoSizing = true; label.overflowMode = TextOverflowModes.Overflow;
                if (label.text.Length > 2) label.rectTransform.localScale = new Vector3(2.2f / label.text.Length, 1, 1);
            }
        }
    }
}
