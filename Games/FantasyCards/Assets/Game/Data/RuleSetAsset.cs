using FantasyCards.Core;
using UnityEngine;

namespace FantasyCards.Data
{
    [CreateAssetMenu(menuName = "별빛의 서약/규칙 제작 데이터")]
    public sealed class RuleSetAsset : ScriptableObject
    {
        public RuleSet rules = new RuleSet();
        public string contentVersion;
        public RuleSet CreateRuntimeRules() { rules.Validate(); return rules.Copy(); }
    }
}
