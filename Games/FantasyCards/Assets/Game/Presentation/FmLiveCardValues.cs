using TMPro;
using UnityEngine;

namespace FantasyCards.Presentation
{
    // A view of one Core snapshot. This component never changes a UnitState,
    // resolves an effect or rolls a coin. Both field and duel copies bind to
    // the same stable instance, including during an event's impact animation.
    public sealed class FmLiveCardValues : MonoBehaviour
    {
        public int InstanceId { get; private set; }
        public int Attack { get; private set; }
        public int Defense { get; private set; }
        public int Health { get; private set; }
        public int BeforeAttack { get; private set; }
        public int BeforeDefense { get; private set; }
        public int BeforeHealth { get; private set; }
        public bool IsCardFace { get; private set; }
        TMP_Text[] labels, changes;
        string[] prefixes;
        int[] printedValues;
        static readonly Color Increased = new Color(.66f, 1, .75f);
        static readonly Color Decreased = new Color(1, .63f, .54f);

        public void Configure(int instanceId, TMP_Text[] values, TMP_Text[] deltas, bool cardFace, string[] valuePrefixes = null, int[] printed = null)
        {
            InstanceId = instanceId; labels = values; changes = deltas;
            IsCardFace = cardFace; prefixes = valuePrefixes; printedValues = printed;
        }

        public void Present(int beforeAttack, int beforeDefense, int beforeHealth, int attack, int defense, int health)
        {
            BeforeAttack = beforeAttack; BeforeDefense = beforeDefense; BeforeHealth = Mathf.Max(0, beforeHealth);
            Attack = attack; Defense = defense; Health = Mathf.Max(0, health);
            int[] before = { BeforeAttack, BeforeDefense, BeforeHealth }, current = { Attack, Defense, Health };
            for (int i = 0; i < 3; i++)
            {
                int delta = current[i] - before[i];
                int tintDelta = delta != 0 || printedValues == null ? delta : current[i] - printedValues[i];
                var tint = tintDelta > 0 ? Increased : tintDelta < 0 ? Decreased : Color.white;
                labels[i].text = (prefixes == null ? "" : prefixes[i]) + current[i];
                labels[i].color = tint;
                if (changes == null || changes[i] == null) continue;
                changes[i].text = IsCardFace ? delta == 0 ? "" : (delta > 0 ? "+" : "−") + Mathf.Abs(delta)
                    : delta == 0 ? "유지" : before[i] + "→" + current[i] + " " + (delta > 0 ? "+" : "−") + Mathf.Abs(delta);
                changes[i].color = tint;
            }
        }
    }
}
