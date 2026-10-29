using System;
using System.Linq;

namespace FantasyCards.Core
{
    public sealed partial class BattleEngine
    {
        static bool IsPolishAttackCode(int code)
        {
            switch (code)
            {
                case 15: case 402: case 209: case 453: case 43: case 775:
                case 225: case 274: case 372: case 375: case 484: case 535:
                case 639: case 391: case 654: case 701: case 13: case 4:
                case 247: case 487: return true;
                default: return false;
            }
        }

        // These are source abilities, not rarity bonuses. Damage additions stay
        // separate from AP, as the late official skill manual specifies.
        UnitState PolishNextEnemy(int owner, int afterLane, bool mustAct = false) =>
            State.players[owner].units.Where(u => u.lane > afterLane && CardCatalog.UnitHealth(u) > 0 && (!mustAct || !u.exhausted))
                .OrderBy(u => u.lane).FirstOrDefault();

        int PolishResolvedCoinCount(UnitState unit) => State.events.Count(e =>
            e.turn == State.turn && e.kind == "coin" && e.sourceInstanceId == unit.instanceId);

        static int PolishAppraisal(CardRarity rarity)
        {
            switch (rarity)
            {
                case CardRarity.Legend: case CardRarity.Epic: case CardRarity.Secret: return 1;
                case CardRarity.Event: case CardRarity.Special: case CardRarity.Rare: return 2;
                case CardRarity.Uncommon: case CardRarity.Common: return 3;
                // The original list grants no bonus to Free, Upgrade or Unique.
                default: return 0;
            }
        }

        int ApplyPolishBeforeStrike(UnitState source, UnitState target, int owner, int other)
        {
            int code = PremiumCode(source), ap = CardCatalog.UnitAttack(source);
            int dp = CardCatalog.UnitDefense(source), hp = CardCatalog.UnitHealth(source), extra = 0;
            bool active = ap >= 1;
            string notice = null;
            UnitState affected = target;
            int affectedOwner = other;
            switch (code)
            {
                case 15:
                    PremiumDefense(source, dp + 2); notice = "강한은폐 · DP +2"; break;
                case 402:
                    if (active) { extra = State.turn % 10; notice = "시간차공격 · 추가 피해 " + extra; }
                    break;
                case 209:
                    if (active && (affected = PolishNextEnemy(other, target.lane)) != null)
                    { PremiumHealth(affected, CardCatalog.UnitHealth(affected) - 1); notice = "멀티어택 · 다음 적 HP -1"; }
                    break;
                case 453:
                    if (active)
                    {
                        extra = Math.Abs(PolishResolvedCoinCount(source) - PolishResolvedCoinCount(target));
                        notice = "약탈공격 · 실제 코인 수 차이 추가 피해 " + extra;
                    }
                    break;
                case 43:
                    if (active)
                    {
                        int enemyAp = CardCatalog.UnitAttack(target);
                        extra = Math.Min(enemyAp, (ap + 1) / 2);
                        PremiumAttack(target, enemyAp - extra);
                        notice = "공격이용 · 적 AP -" + extra + " / 추가 피해 " + extra;
                    }
                    break;
                case 274:
                    if (active && CardCatalog.UnitDefense(target) >= 1)
                    { PremiumAttack(source, ap + 2); PremiumHealth(source, hp + 1); notice = "분기공격 · AP +2 / HP +1"; }
                    break;
                case 372:
                    extra = State.players[owner].genericSoul;
                    State.players[owner].genericSoul = 0;
                    notice = "솔버스트 · 남은 무소울 " + extra + " 소각 / 추가 피해 " + extra;
                    break;
                case 375:
                    extra = State.players[owner].genericSoul + State.players[other].genericSoul;
                    notice = "솔카운터 · 양측 남은 무소울 추가 피해 " + extra;
                    break;
                case 484:
                    if (active)
                    {
                        int ability = CardCatalog.Get(source.definitionId).capabilityMask;
                        extra = State.players[owner].units.Count(u => CardCatalog.UnitHealth(u) > 0 && CardCatalog.Get(u.definitionId).capabilityMask == ability);
                        notice = "연합공격 · 동일 능력 아군 수 추가 피해 " + extra;
                    }
                    break;
                case 535: case 639:
                    if (active && (affected = PolishNextEnemy(other, target.lane, code == 639)) != null)
                    {
                        int decrease = code == 639 ? 3 : 2;
                        PremiumAttack(affected, CardCatalog.UnitAttack(affected) - decrease);
                        PremiumDefense(affected, CardCatalog.UnitDefense(affected) - decrease);
                        notice = (code == 639 ? "초음파" : "음파공격") + " · 다음 적 AP/DP -" + decrease;
                    }
                    break;
                case 391:
                    if (active)
                    {
                        int count = State.players[owner].units.Count(u => u != source && CardCatalog.UnitHealth(u) > 0 && CardCatalog.Get(u.definitionId).element == Element.Forest);
                        PremiumAttack(source, ap + count); PremiumHealth(source, hp + count);
                        notice = "숲의연합 · 다른 숲 아군 수 AP/HP +" + count;
                    }
                    break;
                case 13:
                    if (active) { extra = CardCatalog.UnitDefense(target); notice = "강한방어이용 · 적 DP 수치 추가 피해 " + extra; }
                    break;
                case 4:
                    if (active) { extra = PolishAppraisal(CardCatalog.Get(target.definitionId).rarity); notice = "가치감정 · 원본 등급 추가 피해 " + extra; }
                    break;
                case 247:
                    if (active)
                    {
                        int count = State.players[owner].units.Count(u => u != source && CardCatalog.UnitHealth(u) > 0);
                        PremiumDefense(source, count); notice = "방어지원 · 다른 아군 수로 DP 변경 " + count;
                    }
                    break;
                case 487:
                    if (active && (affected = PolishNextEnemy(owner, source.lane)) != null)
                    {
                        affectedOwner = owner; PremiumAttack(affected, CardCatalog.UnitAttack(affected) + 1);
                        notice = "연환공격 · 다음 아군 AP +1";
                    }
                    break;
            }
            if (notice != null)
                Log("skill", CardCatalog.Get(source.definitionId).name + " · " + notice, extra, owner,
                    source: source, sourceOwner: owner, target: affected, targetOwner: affectedOwner,
                    hasStatTransition: true, beforeAttack: ap, beforeDefense: dp, beforeHealth: hp, additionalAttackDamage: extra);
            return extra;
        }

        void AfterPolishStrike(UnitState source, UnitState target, int owner, int other, int targetHpBefore, int damage)
        {
            int code = PremiumCode(source), ap = CardCatalog.UnitAttack(source);
            if (code == 775 && ap >= 1 && damage > 0)
            {
                PremiumDefense(target, CardCatalog.UnitDefense(target) - 1);
                if (CardCatalog.UnitHealth(target) > 0) PremiumHealth(target, CardCatalog.UnitHealth(target) - 1);
                PremiumNotice(source, owner, target, other, "화염공격 · 피해 후 적 DP/HP -1", 1);
            }
            if (code == 225 && ap >= 1 && (CardCatalog.Get(target.definitionId).capabilityMask & 2) != 0)
            {
                PremiumAttack(target, 0); PremiumDefense(target, 0);
                if (CardCatalog.UnitHealth(target) > 0) PremiumHealth(target, 1);
                PremiumNotice(source, owner, target, other, "무기제압 · 공격 후 장비 능력 적 AP/DP=0, HP=1");
            }
            if (code == 13 && ap >= 1)
            {
                PremiumDefense(target, 0);
                PremiumNotice(source, owner, target, other, "강한방어이용 · 피해 후 적 DP=0");
            }
            if (targetHpBefore <= 0 || CardCatalog.UnitHealth(target) > 0) return;
            if (code == 654 && CardCatalog.Get(target.definitionId).level <= 2)
            {
                PremiumHealth(source, CardCatalog.UnitHealth(source) + targetHpBefore);
                PremiumNotice(source, owner, source, owner, "컨슘 · 파괴 전 적 HP 흡수 " + targetHpBefore, targetHpBefore);
            }
            if (code == 701)
            {
                var next = PolishNextEnemy(other, target.lane, true);
                if (next != null)
                {
                    PremiumAttack(next, CardCatalog.UnitAttack(next) - 1);
                    PremiumDefense(next, CardCatalog.UnitDefense(next) - 1);
                    PremiumHealth(next, CardCatalog.UnitHealth(next) - 1);
                    PremiumNotice(source, owner, next, other, "투척공격 · 다음 행동가능 적 AP/DP/HP -1", 1);
                }
            }
        }
    }
}
