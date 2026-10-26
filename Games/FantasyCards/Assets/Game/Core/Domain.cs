using System;
using System.Collections.Generic;

namespace FantasyCards.Core
{
    // Preserve the first two numeric values for editor references; display names are light/water.
    public enum Element { Dawn, Tide, Fire, Forest, Metal, Earth, Darkness }
    public enum CardKind { Terrain, Unit, Spell, Equipment }
    public enum CardRarity { Free, Common, Uncommon, Rare, Special, Upgrade, Unique, Event, Legend, Icajen, Secret, Epic }
    public enum Effect { None, Heal, Damage, Rally, Power, Armor, Coin, Draw, Resource, Sweep, Health, Weaken, DefenseBreak, HealAll, RallyAll, ArmorAll, HealthAll, DrainAll, StatOperations }
    public enum LeaderSkill { QuickShot, Rally, Restoration, Insight }
    public enum BattlePhase { Main, AttackDeclaration, BlockResponse, Complete, DefensePreparation }
    public enum CoinResolutionOrder { AttackerFirst, DefenderFirst }
    public enum DamageResolutionOrder { Simultaneous, AttackerFirst }
    public enum VictoryRule { Deck, Life }
    public enum PlayWindow { Any, AttackOnly, DefenseOnly }
    public enum CoinOperation { None = 1, Add = 2, Subtract = 3, Multiply = 4, Set = 5 }

    [Serializable] public sealed class CoinRule
    {
        public CoinOperation operation = CoinOperation.None;
        public int count;
        public int multiplier = 1;
        public CoinRule() { }
        public CoinRule(CoinOperation operation, int count, int multiplier = 1)
        { this.operation = operation; this.count = count; this.multiplier = multiplier; }
        public CoinRule Copy() { return (CoinRule)MemberwiseClone(); }
    }

    public enum OperationSide { Ally, Enemy, Both }
    public enum OperationStat { Attack, Defense, Health, PhysicalDamage }
    public enum DeltaSource { Constant, OwnDefense, OneBasedSlot, NegativeOneBasedSlot, NegativeEmptySlots }
    [Serializable] public sealed class SpellOperation
    {
        public OperationSide side;
        public OperationStat stat;
        public DeltaSource deltaSource;
        public int delta;
        public int laneMask = 31;
        public int requiredCapabilityMask;
        public int requiredElement = -1;
    }

    [Serializable] public sealed class RuleSet
    {
        public int deckSize = 50;
        public int startingHealth = 50;
        public int openingHand = 6;
        public int handLimit = 6;
        public int unitLanes = 5;
        public int terrainLimit = 10;
        public int terrainPerTurn = 10;
        public int maximumResource = 50;
        public int maximumTurns = 60;
        public bool guaranteeOpeningElements = false;
        public bool defenderStrikesBack = true;
        // -1 uses each original card's coin specification. Nonnegative overrides are for rules experiments.
        public int baseCoins = -1;
        public CoinResolutionOrder coinOrder = CoinResolutionOrder.AttackerFirst;
        public DamageResolutionOrder damageOrder = DamageResolutionOrder.AttackerFirst;
        public VictoryRule victoryRule = VictoryRule.Deck;
        public void Validate()
        {
            if (deckSize < 6 || deckSize > 256 || startingHealth < 1 || startingHealth > 1000 || openingHand < 1 || openingHand > handLimit || openingHand > deckSize || handLimit > 30 || unitLanes < 1 || unitLanes > 6 || terrainLimit < 1 || terrainLimit > 24 || terrainPerTurn < 1 || terrainPerTurn > terrainLimit || maximumResource < 1 || maximumResource > 100 || maximumTurns < 2 || maximumTurns > 10000 || baseCoins < -1 || baseCoins > 5 || !Enum.IsDefined(typeof(CoinResolutionOrder), coinOrder) || !Enum.IsDefined(typeof(DamageResolutionOrder), damageOrder) || !Enum.IsDefined(typeof(VictoryRule), victoryRule))
                throw new ArgumentException("유효하지 않은 RuleSet입니다.");
        }
        public RuleSet Copy() { return (RuleSet)MemberwiseClone(); }
    }

    [Serializable] public sealed class CardDefinition
    {
        public string id, name, text;
        public CardKind kind;
        public Element element;
        public CardRarity rarity;
        public int level, coloredCost, genericCost, cost;
        public int landPointCost = 1, producedColored = 1, producedGeneric;
        public int attack, defense, health, coinBonus;
        public CoinRule attackCoin = new CoinRule(), defenseCoin = new CoinRule(), healthCoin = new CoinRule();
        public SpellOperation[] operations = new SpellOperation[0];
        public Effect effect;
        public int magnitude;
        public int deckLimit = 4;
        public int capabilityMask = 7, requiredCapabilityMask;
        public PlayWindow playWindow = PlayWindow.Any;
        public int firstAffectedLane = 0, lastAffectedLane = 4;
        public string artResource, sourceId, sourceUrl;
        public CardDefinition(string id, string name, string text, CardKind kind, Element element, int cost,
            int attack = 0, int health = 0, int coinBonus = 1, Effect effect = Effect.None, int magnitude = 0,
            CardRarity rarity = CardRarity.Common, int level = -1, int coloredCost = -1, int genericCost = 0, int defense = 0)
        {
            this.id = id; this.name = name; this.text = text; this.kind = kind; this.element = element;
            this.coloredCost = coloredCost < 0 ? cost : coloredCost; this.genericCost = genericCost;
            this.cost = this.coloredCost + this.genericCost; this.level = level < 0 ? cost : level;
            this.attack = attack; this.defense = defense; this.health = health; this.coinBonus = coinBonus;
            this.effect = effect; this.magnitude = magnitude; this.rarity = rarity;
            attackCoin = new CoinRule(coinBonus < 0 ? CoinOperation.Subtract : CoinOperation.Add, 1, Math.Abs(coinBonus));
            artResource = "Art/Cards/" + id;
        }
    }

    [Serializable] public sealed class CardInstance
    {
        public int instanceId;
        public string definitionId;
        public CardInstance(int instanceId, string definitionId) { this.instanceId = instanceId; this.definitionId = definitionId; }
        public CardInstance Copy() { return new CardInstance(instanceId, definitionId); }
    }

    [Serializable] public sealed class UnitState
    {
        public int instanceId;
        public string definitionId;
        public int lane, damage, bonusPower, armor, bonusHealth, extraCoins;
        public bool exhausted, summoningSick, coinsRolled;
        public int rolledAttack, rolledDefense, rolledHealth;
        public List<CardInstance> equipment = new List<CardInstance>();
        public UnitState Copy()
        {
            var c = (UnitState)MemberwiseClone(); c.equipment = equipment.ConvertAll(x => x.Copy()); return c;
        }
    }

    [Serializable] public sealed class PreparedSpellState
    {
        public int instanceId;
        public string definitionId;
        public int lane, targetLane = -1, targetPlayer = -1;
        public PreparedSpellState Copy() { return (PreparedSpellState)MemberwiseClone(); }
    }

    [Serializable] public sealed class PlayerState
    {
        public string leaderId = "lyria", deckId = "custom";
        public int lastLeaderSkillTurn, leaderSkillUses;
        public bool mulliganUsed, preparationComplete, playedThisPreparation;
        public int lastShuffleTurn;
        public int health, levelPoints, dawn, tide, fire, forest, metal, earth, darkness, genericSoul;
        public int fatigue, terrainPlayed;
        public List<CardInstance> deck = new List<CardInstance>(), hand = new List<CardInstance>(), terrain = new List<CardInstance>(), discard = new List<CardInstance>();
        public List<UnitState> units = new List<UnitState>();
        public List<PreparedSpellState> preparedSpells = new List<PreparedSpellState>();
        public int Resource(Element element)
        {
            switch (element) { case Element.Dawn: return dawn; case Element.Tide: return tide; case Element.Fire: return fire; case Element.Forest: return forest; case Element.Metal: return metal; case Element.Earth: return earth; case Element.Darkness: return darkness; default: throw new ArgumentOutOfRangeException(nameof(element)); }
        }
        public void SetResource(Element element, int value)
        {
            switch (element) { case Element.Dawn: dawn = value; break; case Element.Tide: tide = value; break; case Element.Fire: fire = value; break; case Element.Forest: forest = value; break; case Element.Metal: metal = value; break; case Element.Earth: earth = value; break; case Element.Darkness: darkness = value; break; default: throw new ArgumentOutOfRangeException(nameof(element)); }
        }
        public UnitState AtLane(int lane) { return units.Find(x => x.lane == lane); }
        public PreparedSpellState SpellAtLane(int lane) { return preparedSpells.Find(x => x.lane == lane); }
        public PlayerState Copy()
        {
            var c = (PlayerState)MemberwiseClone(); c.deck = deck.ConvertAll(x => x.Copy()); c.hand = hand.ConvertAll(x => x.Copy()); c.terrain = terrain.ConvertAll(x => x.Copy()); c.discard = discard.ConvertAll(x => x.Copy()); c.units = units.ConvertAll(x => x.Copy()); c.preparedSpells = preparedSpells.ConvertAll(x => x.Copy()); return c;
        }
    }

    [Serializable] public sealed class BattleEvent
    {
        public int sequence, turn, amount, actor;
        public string kind, text;
        public BattleEvent Copy() { return (BattleEvent)MemberwiseClone(); }
    }

    [Serializable] public sealed class BattleState
    {
        public int schemaVersion = 3;
        public string contentVersion = CardCatalog.Version;
        public RuleSet rules = new RuleSet();
        public uint randomState;
        public int nextInstanceId = 1, turn = 1, activePlayer;
        public BattlePhase phase;
        public int winner = -1, pendingAttackerId = -1, pendingTargetLane = -1, encounterIndex = -1;
        public bool openingHandOpen = true;
        public List<PlayerState> players = new List<PlayerState>();
        public List<BattleEvent> events = new List<BattleEvent>();
        public BattleState Copy()
        {
            var c = (BattleState)MemberwiseClone(); c.rules = rules.Copy(); c.players = players.ConvertAll(x => x.Copy()); c.events = events.ConvertAll(x => x.Copy()); return c;
        }
    }

    public static class StableRandom
    {
        public static uint Next(ref uint state)
        {
            if (state == 0) state = 0xA341316Cu;
            state ^= state << 13; state ^= state >> 17; state ^= state << 5; return state;
        }
        public static int Range(ref uint state, int maximum)
        {
            if (maximum <= 0) throw new ArgumentOutOfRangeException(nameof(maximum));
            uint limit = uint.MaxValue - uint.MaxValue % (uint)maximum;
            uint value; do { value = Next(ref state) - 1u; } while (value >= limit);
            return (int)(value % (uint)maximum);
        }
    }
}
