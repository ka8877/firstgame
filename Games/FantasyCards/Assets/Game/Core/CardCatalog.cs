using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    public static class CardCatalog
    {
        public const string Version = "fm-classic-0.1.0";
        public static readonly CardDefinition[] All = ClassicCardData.Create();
        public static CardDefinition Get(string id)
        {
            var card = Array.Find(All, x => x.id == id);
            if (card == null) throw new ArgumentException("알 수 없는 카드 ID: " + id);
            return card;
        }
        public static void ApplyAuthoredDefinitions(IEnumerable<CardDefinition> definitions)
        {
            var authored = definitions.ToArray();
            if (authored.Length != All.Length || authored.Select(d => d.id).Distinct().Count() != All.Length || authored.Any(d => !All.Any(c => c.id == d.id))) throw new ArgumentException("제작 데이터의 안정적인 카드 ID " + All.Length + "종이 필요합니다.");
            foreach (var d in authored) ValidateDefinition(d);
            for (int i = 0; i < All.Length; i++) All[i] = authored.Single(d => d.id == All[i].id);
        }
        public static void ValidateDefinition(CardDefinition d)
        {
            if (d == null || string.IsNullOrWhiteSpace(d.id) || string.IsNullOrWhiteSpace(d.name) || d.level < 0 || d.level > 20 || d.cost != d.coloredCost + d.genericCost || d.coloredCost < 0 || d.genericCost < 0 || d.cost > 20 || Math.Abs(d.attack) > 100 || Math.Abs(d.defense) > 100 || Math.Abs(d.health) > 100 || Math.Abs(d.magnitude) > 100 || d.landPointCost < 0 || d.landPointCost > 10 || d.producedColored < 0 || d.producedColored > 10 || d.producedGeneric < 0 || d.producedGeneric > 10 || d.deckLimit < 1 || d.deckLimit > 50 || !Enum.IsDefined(typeof(CardKind), d.kind) || !Enum.IsDefined(typeof(Element), d.element) || !Enum.IsDefined(typeof(Effect), d.effect) || !Enum.IsDefined(typeof(CardRarity), d.rarity) || !Enum.IsDefined(typeof(PlayWindow), d.playWindow) || d.capabilityMask < 0 || d.capabilityMask > 7 || d.requiredCapabilityMask < 0 || d.requiredCapabilityMask > 7 || d.firstAffectedLane < 0 || d.lastAffectedLane > 5 || d.lastAffectedLane < d.firstAffectedLane || (d.kind == CardKind.Unit && (d.health < 1 || d.attack < 0 || d.defense < 0))) throw new ArgumentException("잘못된 카드 제작 데이터: " + d?.id);
            if (!Enum.IsDefined(typeof(CardRank), d.rank)) throw new ArgumentException("잘못된 원작 랭크: " + d.id);
            if (d.discardGenericSoul < 0 || d.discardGenericSoul > 10 || (d.kind != CardKind.Unit && (d.freeEquipmentBelowLevel || d.discardGenericSoul != 0))) throw new ArgumentException("잘못된 유닛 부가 능력: " + d.id);
            if (d.operations == null) throw new ArgumentException("마법 연산이 없습니다: " + d.id);
            foreach (var operation in d.operations)
                if (operation == null || !Enum.IsDefined(typeof(OperationSide), operation.side) || !Enum.IsDefined(typeof(OperationStat), operation.stat) || !Enum.IsDefined(typeof(DeltaSource), operation.deltaSource) || operation.laneMask < 1 || operation.laneMask > 31 || Math.Abs(operation.delta) > 100 || operation.requiredCapabilityMask < 0 || operation.requiredCapabilityMask > 7 || operation.requiredElement < -1 || operation.requiredElement > 6) throw new ArgumentException("잘못된 마법 연산: " + d.id);
            foreach (var coin in new[] { d.attackCoin, d.defenseCoin, d.healthCoin })
                if (coin == null || coin.count < 0 || coin.count > 5 || coin.multiplier < 0 || coin.multiplier > 20 || !Enum.IsDefined(typeof(CoinOperation), coin.operation)) throw new ArgumentException("잘못된 코인식: " + d.id);
        }
        public static List<string> DefaultDeck(bool opponent = false)
        { return DemoCatalog.GetDeck(opponent ? "tides" : "forest").CopyCards(); }
        public static string KindName(CardKind kind) { return new[] { "지형", "유닛", "마법", "아이템" }[(int)kind]; }
        public static string ElementName(Element element) { return new[] { "빛", "물", "불", "숲", "금속", "대지", "암흑" }[(int)element]; }
        public static string RarityName(CardRarity rarity) { return new[] { "프리", "커먼", "언커먼", "레어", "스페셜", "업그레이드", "유니크", "이벤트", "레전드", "이카젠", "시크릿", "에픽" }[(int)rarity]; }
        public static string RarityCode(CardRarity rarity) { return new[] { "F", "C", "UC", "R", "SP", "UP", "U", "E", "L", "IC", "SC", "EP" }[(int)rarity]; }
        public static string RankName(CardRank rank) { return new[] { "N", "C", "B", "A", "S" }[(int)rank]; }
        public static string CoinText(CoinRule coin)
        {
            if (coin == null || coin.count == 0 || coin.operation == CoinOperation.None) return "";
            string operation = coin.operation == CoinOperation.Add ? "+" : coin.operation == CoinOperation.Subtract ? "-" : coin.operation == CoinOperation.Multiply ? "×" : "=";
            return operation + (coin.multiplier == 1 ? "" : coin.multiplier.ToString()) + "C" + coin.count;
        }
        public static string StatsText(CardDefinition card)
        { return "AP " + card.attack + CoinText(card.attackCoin) + "  DP " + card.defense + CoinText(card.defenseCoin) + "  HP " + card.health + CoinText(card.healthCoin); }
        public static string CostText(CardDefinition card)
        {
            if (card.kind == CardKind.Terrain) return "LP " + card.landPointCost + " · " + ElementName(card.element) + " " + card.producedColored + " / 총소울 " + card.producedGeneric + " 생산";
            return ElementName(card.element) + " " + card.coloredCost + " · 총소울 " + card.cost + (card.kind == CardKind.Unit ? " · 유지 " + card.genericCost : "");
        }
        public static int BaseAttack(UnitState unit) { return Get(unit.definitionId).attack + unit.bonusPower + unit.equipment.Sum(c => Get(c.definitionId).attack); }
        public static int BaseDefense(UnitState unit) { return Get(unit.definitionId).defense + unit.armor + unit.equipment.Sum(c => Get(c.definitionId).defense); }
        public static int BaseHealth(UnitState unit) { return Get(unit.definitionId).health + unit.bonusHealth + unit.equipment.Sum(c => Get(c.definitionId).health); }
        public static int UnitAttack(UnitState unit) { return Math.Max(0, unit.coinsRolled ? unit.rolledAttack : BaseAttack(unit)); }
        public static int UnitDefense(UnitState unit) { return Math.Max(0, unit.coinsRolled ? unit.rolledDefense : BaseDefense(unit)); }
        public static int MaximumHealth(UnitState unit) { return Math.Max(0, unit.coinsRolled ? unit.rolledHealth : BaseHealth(unit)); }
        public static int UnitHealth(UnitState unit) { return Math.Max(0, MaximumHealth(unit) - unit.damage); }
        public static bool RequiresUnitTarget(CardDefinition card)
        {
            return card.kind == CardKind.Equipment || (card.kind == CardKind.Spell && card.effect != Effect.Draw && card.effect != Effect.Resource && card.effect != Effect.Sweep && card.effect != Effect.HealAll && card.effect != Effect.RallyAll && card.effect != Effect.ArmorAll && card.effect != Effect.HealthAll && card.effect != Effect.DrainAll && card.effect != Effect.StatOperations);
        }
    }
}
