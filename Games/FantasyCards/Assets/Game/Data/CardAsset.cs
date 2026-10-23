using FantasyCards.Core;
using UnityEngine;

namespace FantasyCards.Data
{
    [CreateAssetMenu(menuName = "별빛의 서약/카드 제작 데이터")]
    public sealed class CardAsset : ScriptableObject
    {
        public string stableId;
        public string displayName;
        [TextArea] public string description;
        public CardKind kind;
        public Element element;
        [Min(0)] public int cost;
        [Min(0)] public int power;
        [Min(1)] public int health = 1;
        [Min(0)] public int coinBonus = 1;
        public Effect effect;
        [Min(0)] public int magnitude;
        public Sprite illustration;
        public CardDefinition ToDefinition() { return new CardDefinition(stableId, displayName, description, kind, element, cost, power, health, coinBonus, effect, magnitude); }
    }
}
