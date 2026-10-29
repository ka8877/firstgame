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
            // Hands retain the entire original print. Live units replace only
            // the three printed numeral areas; no name/art/rules/stat bars.
            if (unit == null) return;
            ChronicleValues(unit, out var before, out var current);
            var container = Panel(surface, 0, 0, 1, 1, Color.clear);
            container.gameObject.name = "Integrated live card values " + unit.instanceId;
            string[] labels = { "AP", "DP", "HP" };
            float[] bottoms = { .1753f, .1136f, .0469f }, tops = { .2272f, .1605f, .0988f };
            for (int i = 0; i < 3; i++)
            {
                // The blank 3px strip alongside each source numeral preserves
                // both legacy paper and later dark slots. Source bytes stay intact.
                var patch = new GameObject("Printed " + labels[i] + " numeral background", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                patch.transform.SetParent(container, false);
                Anchor((RectTransform)patch.transform, .8632f, bottoms[i], .9649f, tops[i]);
                patch.uvRect = new Rect(246f / 285f, bottoms[i], 3f / 285f, tops[i] - bottoms[i]);
                patch.texture = OriginalCardTexture(card); patch.raycastTarget = false;
                FmStreamingCardArt.Bind(patch, card.artResource);
                int delta = current[i] - before[i];
                var label = RenewalValueText(container, current[i].ToString(), .862f, bottoms[i], .969f, tops[i], 32,
                    delta > 0 ? new Color(.60f, 1, .66f) : delta < 0 ? new Color(1, .40f, .32f) : ivory,
                    "Card live " + labels[i] + " " + unit.instanceId);
                label.alignment = TextAlignmentOptions.MidlineRight;
                label.fontStyle = FontStyles.Bold | FontStyles.Italic;
                label.outlineWidth = .14f; label.outlineColor = new Color32(8, 12, 18, 255);
                if (current[i].ToString().Length > 2)
                    label.transform.localScale = new Vector3(2.2f / current[i].ToString().Length, 1, 1);
            }
        }
    }
}
