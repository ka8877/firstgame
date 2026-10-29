using FantasyCards.Core;
using UnityEngine;

namespace FantasyCards.Data
{
    [CreateAssetMenu(menuName = "판마 체험/카드 제작 데이터")]
    public sealed class CardAsset : ScriptableObject
    {
        // The complete classic definition is serialized together, including each
        // AP/DP/HP coin expression and its original database provenance.
        public CardDefinition classicDefinition;
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
        // Streamed full-card prints stay outside the packed Resources payload.
        public string streamedIllustration;
        public void SetDefinition(CardDefinition definition)
        {
            classicDefinition = Copy(definition);
            stableId = definition.id; displayName = definition.name; description = definition.text;
            kind = definition.kind; element = definition.element; cost = definition.cost;
            power = definition.attack; health = definition.health; coinBonus = definition.coinBonus;
            effect = definition.effect; magnitude = definition.magnitude;
        }
        public CardDefinition ToDefinition()
        {
            return classicDefinition != null && !string.IsNullOrEmpty(classicDefinition.id)
                ? Copy(classicDefinition)
                : new CardDefinition(stableId, displayName, description, kind, element, cost, power, health, coinBonus, effect, magnitude);
        }
        static CardDefinition Copy(CardDefinition definition) => JsonUtility.FromJson<CardDefinition>(JsonUtility.ToJson(definition));
    }
}
