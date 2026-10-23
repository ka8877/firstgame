using System;
using System.Collections.Generic;

namespace FantasyCards.Core
{
    public enum Element { Dawn, Tide }
    public enum CardKind { Terrain, Unit, Spell, Equipment }
    public enum Effect { None, Heal, Damage, Rally, Power, Armor, Coin, Draw, Resource, Sweep }
    public enum LeaderSkill { QuickShot, Rally, Restoration, Insight }
    public enum BattlePhase { Main, AttackDeclaration, BlockResponse, Complete }
    public enum CoinResolutionOrder { AttackerFirst, DefenderFirst }
    public enum DamageResolutionOrder { Simultaneous, AttackerFirst }

    [Serializable] public sealed class RuleSet
    {
        public int deckSize = 24;
        public int startingHealth = 18;
        public int openingHand = 5;
        public int handLimit = 9;
        public int unitLanes = 3;
        public int terrainLimit = 6;
        public int terrainPerTurn = 1;
        public int maximumResource = 12;
        public int maximumTurns = 60;
        public bool guaranteeOpeningElements = true;
        public bool defenderStrikesBack = true;
        public int baseCoins = 1;
        public CoinResolutionOrder coinOrder = CoinResolutionOrder.AttackerFirst;
        public DamageResolutionOrder damageOrder = DamageResolutionOrder.Simultaneous;
        public void Validate()
        {
            if (deckSize < 5 || deckSize > 256 || startingHealth < 1 || startingHealth > 1000 || openingHand < 1 || openingHand > handLimit || openingHand > deckSize || handLimit > 30 || unitLanes < 1 || unitLanes > 6 || terrainLimit < 1 || terrainLimit > 24 || terrainPerTurn < 1 || terrainPerTurn > terrainLimit || maximumResource < 1 || maximumResource > 100 || maximumTurns < 2 || maximumTurns > 10000 || baseCoins < 0 || baseCoins > 4 || !Enum.IsDefined(typeof(CoinResolutionOrder), coinOrder) || !Enum.IsDefined(typeof(DamageResolutionOrder), damageOrder))
                throw new ArgumentException("유효하지 않은 RuleSet입니다.");
        }
        public RuleSet Copy() { return (RuleSet)MemberwiseClone(); }
    }

    [Serializable] public sealed class CardDefinition
    {
        public string id;
        public string name;
        public string text;
        public CardKind kind;
        public Element element;
        public int cost;
        public int attack;
        public int health;
        public int coinBonus;
        public Effect effect;
        public int magnitude;
        public string artResource;
        public CardDefinition(string id, string name, string text, CardKind kind, Element element, int cost, int attack = 0, int health = 0, int coinBonus = 1, Effect effect = Effect.None, int magnitude = 0)
        {
            this.id = id; this.name = name; this.text = text; this.kind = kind; this.element = element; this.cost = cost; this.attack = attack; this.health = health; this.coinBonus = coinBonus; this.effect = effect; this.magnitude = magnitude; artResource = "Art/Cards/" + id;
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
        public int lane;
        public int damage;
        public int bonusPower;
        public int armor;
        public int extraCoins;
        public bool exhausted;
        public bool summoningSick;
        public List<CardInstance> equipment = new List<CardInstance>();
        public UnitState Copy()
        {
            var c = (UnitState)MemberwiseClone(); c.equipment = equipment.ConvertAll(x => x.Copy()); return c;
        }
    }

    [Serializable] public sealed class PlayerState
    {
        public string leaderId = "lyria";
        public string deckId = "custom";
        public int lastLeaderSkillTurn;
        public int leaderSkillUses;
        public bool mulliganUsed;
        public int health;
        public int dawn;
        public int tide;
        public int fatigue;
        public int terrainPlayed;
        public List<CardInstance> deck = new List<CardInstance>();
        public List<CardInstance> hand = new List<CardInstance>();
        public List<CardInstance> terrain = new List<CardInstance>();
        public List<CardInstance> discard = new List<CardInstance>();
        public List<UnitState> units = new List<UnitState>();
        public int Resource(Element element) { return element == Element.Dawn ? dawn : tide; }
        public UnitState AtLane(int lane) { return units.Find(x => x.lane == lane); }
        public PlayerState Copy()
        {
            var c = (PlayerState)MemberwiseClone(); c.deck = deck.ConvertAll(x => x.Copy()); c.hand = hand.ConvertAll(x => x.Copy()); c.terrain = terrain.ConvertAll(x => x.Copy()); c.discard = discard.ConvertAll(x => x.Copy()); c.units = units.ConvertAll(x => x.Copy()); return c;
        }
    }

    [Serializable] public sealed class BattleEvent
    {
        public int sequence;
        public int turn;
        public string kind;
        public string text;
        public int amount;
        public int actor;
        public BattleEvent Copy() { return (BattleEvent)MemberwiseClone(); }
    }

    [Serializable] public sealed class BattleState
    {
        public int schemaVersion = 2;
        public string contentVersion = CardCatalog.Version;
        public RuleSet rules = new RuleSet();
        public uint randomState;
        public int nextInstanceId = 1;
        public int turn = 1;
        public int activePlayer;
        public BattlePhase phase;
        public int winner = -1;
        public int pendingAttackerId = -1;
        public int pendingTargetLane = -1;
        public int encounterIndex = -1;
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
            // Xorshift32 visits 1..uint.MaxValue. Shift to a zero-based domain before rejection.
            uint value; do { value = Next(ref state) - 1u; } while (value >= limit);
            return (int)(value % (uint)maximum);
        }
    }
}
