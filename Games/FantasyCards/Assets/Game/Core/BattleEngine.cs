using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    /// <summary>Classic preparation, ordered combat and soul-cost rules. No presentation randomness.</summary>
    public sealed class BattleEngine
    {
        public BattleState State { get; private set; }
        public int PreparingPlayer => State.phase == BattlePhase.Main ? State.activePlayer : State.phase == BattlePhase.DefensePreparation ? 1 - State.activePlayer : -1;
        public int ActivePreparationPlayer => PreparingPlayer;
        public bool IsPreparing(int player) => player >= 0 && player <= 1 && PreparingPlayer == player;

        public BattleEngine(uint seed, RuleSet rules = null, IList<string> playerDeck = null, IList<string> aiDeck = null,
            string playerLeader = "lyria", string aiLeader = "serin", string playerDeckId = "custom", string aiDeckId = "custom", int encounterIndex = -1)
        {
            rules = rules ?? new RuleSet(); rules.Validate();
            DemoCatalog.GetLeader(playerLeader); DemoCatalog.GetLeader(aiLeader);
            if (encounterIndex < -1 || encounterIndex >= DemoCatalog.Encounters.Length) throw new ArgumentOutOfRangeException(nameof(encounterIndex));
            State = new BattleState { randomState = seed == 0 ? 1u : seed, rules = rules.Copy(), encounterIndex = encounterIndex };
            AddPlayer(playerDeck ?? CardCatalog.DefaultDeck(), playerLeader, playerDeckId);
            AddPlayer(aiDeck ?? CardCatalog.DefaultDeck(true), aiLeader, aiDeckId);
            for (int player = 0; player < 2; player++)
                while (State.players[player].hand.Count < rules.openingHand && State.players[player].deck.Count > 0) Draw(player);
            Log("start", "클래식 체험 결투 · 공격 준비 → 방어 준비 → 전투 → 소울 코스트");
            RefreshSouls(); BeginRound(false); Validate(State);
        }
        public BattleEngine(BattleState saved) { Validate(saved); State = saved.Copy(); }

        public static void Validate(BattleState s)
        {
            if (s == null || s.schemaVersion != 3 || s.contentVersion != CardCatalog.Version || s.rules == null || s.players == null || s.players.Count != 2 || s.events == null || s.randomState == 0 || s.activePlayer < 0 || s.activePlayer > 1 || s.turn < 1 || (s.phase != BattlePhase.Main && s.phase != BattlePhase.DefensePreparation && s.phase != BattlePhase.Complete)) throw new ArgumentException("클래식 전투 저장 버전 3이 필요합니다. 이전 저장은 보존하고 새 결투를 시작하세요.");
            s.rules.Validate(); var ids = new HashSet<int>();
            if (s.turn > s.rules.maximumTurns + 1 || s.nextInstanceId != s.rules.deckSize * 2 + 1 || (s.phase != BattlePhase.Complete && s.winner != -1) || s.pendingAttackerId != -1 || s.pendingTargetLane != -1 || s.encounterIndex < -1 || s.encounterIndex >= DemoCatalog.Encounters.Length) throw new ArgumentException("잘못된 턴 또는 결과 상태입니다.");
            for (int player = 0; player < 2; player++)
            {
                var p = s.players[player];
                if (p == null || p.deck == null || p.hand == null || p.terrain == null || p.discard == null || p.units == null || p.preparedSpells == null || p.units.Count + p.preparedSpells.Count > s.rules.unitLanes || p.terrain.Count > s.rules.terrainLimit || p.hand.Count > s.rules.handLimit || p.levelPoints < 0 || p.levelPoints > s.rules.maximumTurns + 1 || p.genericSoul < 0 || p.genericSoul > s.rules.maximumResource || p.health > s.rules.startingHealth || p.health < -10000 || p.fatigue < 0 || p.terrainPlayed < 0 || p.terrainPlayed > s.rules.terrainPerTurn || p.leaderSkillUses != 0 || p.lastLeaderSkillTurn != 0 || p.lastShuffleTurn < 0 || p.lastShuffleTurn > s.turn) throw new ArgumentException("잘못된 플레이어 상태입니다.");
                foreach (Element element in Enum.GetValues(typeof(Element))) if (p.Resource(element) < 0 || p.Resource(element) > s.rules.maximumResource) throw new ArgumentException("잘못된 소울 상태입니다.");
                if (s.phase != BattlePhase.Complete && (s.rules.victoryRule == VictoryRule.Deck ? p.deck.Count == 0 : p.health <= 0)) throw new ArgumentException("패배한 전투를 진행 상태로 저장할 수 없습니다.");
                DemoCatalog.GetLeader(p.leaderId); if (p.deckId != "custom") DemoCatalog.GetDeck(p.deckId);
                if (p.playedThisPreparation != s.events.Any(e => e.kind == "play" && e.actor == player && e.turn == s.turn)) throw new ArgumentException("이번 준비의 카드 사용 기록이 다릅니다.");
                bool expectedReady = s.phase == BattlePhase.DefensePreparation && player == s.activePlayer;
                if (s.phase != BattlePhase.Complete && p.preparationComplete != expectedReady) throw new ArgumentException("준비 완료 상태와 단계가 다릅니다.");
                int previousCount = ids.Count; var lanes = new HashSet<int>();
                foreach (var card in p.deck.Concat(p.hand).Concat(p.terrain).Concat(p.discard)) CheckCard(card, ids);
                foreach (var unit in p.units)
                {
                    if (unit == null || !ids.Add(unit.instanceId) || unit.instanceId <= 0 || unit.lane < 0 || unit.lane >= s.rules.unitLanes || !lanes.Add(unit.lane) || CardCatalog.Get(unit.definitionId).kind != CardKind.Unit || unit.equipment == null || unit.equipment.Count > 2 || unit.damage < 0 || CardCatalog.UnitHealth(unit) <= 0 || Math.Abs(unit.bonusPower) > 100 || Math.Abs(unit.armor) > 100 || Math.Abs(unit.bonusHealth) > 100 || unit.extraCoins < 0 || unit.extraCoins > 5 || Math.Abs(unit.rolledAttack) > 1000 || Math.Abs(unit.rolledDefense) > 1000 || Math.Abs(unit.rolledHealth) > 1000) throw new ArgumentException("잘못된 전열 유닛입니다.");
                    foreach (var equipment in unit.equipment) { CheckCard(equipment, ids); if (CardCatalog.Get(equipment.definitionId).kind != CardKind.Equipment || (CardCatalog.Get(equipment.definitionId).requiredCapabilityMask != 0 && (CardCatalog.Get(unit.definitionId).capabilityMask & CardCatalog.Get(equipment.definitionId).requiredCapabilityMask) == 0)) throw new ArgumentException("잘못된 아이템입니다."); }
                }
                foreach (var spell in p.preparedSpells)
                    if (spell == null || !ids.Add(spell.instanceId) || spell.instanceId <= 0 || spell.lane < 0 || spell.lane >= s.rules.unitLanes || !lanes.Add(spell.lane) || spell.targetLane < -1 || spell.targetLane >= s.rules.unitLanes || spell.targetPlayer < -1 || spell.targetPlayer > 1 || CardCatalog.Get(spell.definitionId).kind != CardKind.Spell) throw new ArgumentException("잘못된 예고 마법입니다.");
                var definitionsInPlay = p.deck.Concat(p.hand).Concat(p.terrain).Concat(p.discard).Select(c => c.definitionId).Concat(p.units.Select(u => u.definitionId)).Concat(p.units.SelectMany(u => u.equipment).Select(c => c.definitionId)).Concat(p.preparedSpells.Select(c => c.definitionId));
                if (definitionsInPlay.GroupBy(id => id).Any(group => group.Count() > CardCatalog.Get(group.Key).deckLimit)) throw new ArgumentException("동일 카드 한도를 초과했습니다.");
                if (p.terrain.Any(c => CardCatalog.Get(c.definitionId).kind != CardKind.Terrain) || ids.Count - previousCount != s.rules.deckSize) throw new ArgumentException("플레이어의 카드 수가 보존되지 않았습니다.");
                if (p.deckId != "custom")
                {
                    var actual = p.deck.Concat(p.hand).Concat(p.terrain).Concat(p.discard).Select(c => c.definitionId).Concat(p.units.Select(u => u.definitionId)).Concat(p.units.SelectMany(u => u.equipment).Select(c => c.definitionId)).Concat(p.preparedSpells.Select(c => c.definitionId));
                    if (!actual.OrderBy(x => x).SequenceEqual(DemoCatalog.GetDeck(p.deckId).cards.OrderBy(x => x))) throw new ArgumentException("선택한 기본 덱과 저장 카드 구성이 다릅니다.");
                }
            }
            if (ids.Count != 2 * s.rules.deckSize || ids.Any(x => x >= s.nextInstanceId)) throw new ArgumentException("카드 보존 검증에 실패했습니다.");
            for (int i = 0; i < s.events.Count; i++) if (s.events[i] == null || s.events[i].sequence != i + 1 || s.events[i].turn < 1 || s.events[i].turn > s.turn || s.events[i].text == null || s.events[i].kind == null || s.events[i].actor < -1 || s.events[i].actor > 1) throw new ArgumentException("전투 기록이 잘못되었습니다.");
            for (int player = 0; player < 2; player++)
            {
                var shuffles = s.events.Where(e => e.kind == "shuffle-hand" && e.actor == player).ToArray();
                int last = shuffles.Length == 0 ? 0 : shuffles.Last().turn;
                if (shuffles.Select(e => e.turn).Distinct().Count() != shuffles.Length || shuffles.Any(e => e.amount < 1 || e.amount > s.rules.handLimit) || s.players[player].lastShuffleTurn != last) throw new ArgumentException("손패 셔플 기록이 올바르지 않습니다.");
                if (s.players[player].mulliganUsed != (player == 0 && shuffles.Any(e => e.turn == 1))) throw new ArgumentException("시작 손패 교체 기록이 올바르지 않습니다.");
            }
            if (s.openingHandOpen && (s.turn != 1 || s.activePlayer != 0 || s.phase != BattlePhase.Main || s.events.Any(e => e.kind == "play" || e.kind == "ready"))) throw new ArgumentException("시작 손패 선택 시점이 올바르지 않습니다.");
            if (s.encounterIndex >= 0)
            {
                var encounter = DemoCatalog.GetEncounter(s.encounterIndex);
                if (s.players[1].deckId != encounter.deckId || s.players[1].leaderId != encounter.leaderId) throw new ArgumentException("체험전 상대가 일치하지 않습니다.");
            }
            if (s.phase == BattlePhase.Complete && (s.winner < 0 || s.winner > 2)) throw new ArgumentException("승패 정보가 없습니다.");
        }
        static void CheckCard(CardInstance card, HashSet<int> ids)
        { if (card == null || card.instanceId <= 0 || !ids.Add(card.instanceId)) throw new ArgumentException("카드 인스턴스가 잘못되었습니다."); CardCatalog.Get(card.definitionId); }
        void AddPlayer(IList<string> deck, string leader, string deckId)
        {
            if (deck == null || deck.Count != State.rules.deckSize) throw new ArgumentException("덱은 " + State.rules.deckSize + "장이어야 합니다.");
            if (deckId != "custom") DemoCatalog.GetDeck(deckId);
            var p = new PlayerState { health = State.rules.startingHealth, leaderId = leader, deckId = deckId };
            foreach (string id in deck) { CardCatalog.Get(id); p.deck.Add(new CardInstance(State.nextInstanceId++, id)); }
            Shuffle(p.deck); State.players.Add(p);
            Log("shuffle", DemoCatalog.GetLeader(leader).name + " · 덱 " + deck.Count + "장 셔플", deck.Count, State.players.Count - 1);
        }
        void Shuffle(List<CardInstance> deck)
        { for (int i = deck.Count - 1; i > 0; i--) { int j = StableRandom.Range(ref State.randomState, i + 1); var card = deck[i]; deck[i] = deck[j]; deck[j] = card; } }
        public bool CanShuffleHand(int player, out string reason)
        {
            reason = "";
            if (!IsPreparing(player)) return Fail("자신의 준비 단계에서만 손패를 섞을 수 있습니다.", out reason);
            var p = State.players[player];
            if (p.playedThisPreparation) return Fail("이번 준비에서 카드를 사용하기 전에만 손패를 섞을 수 있습니다.", out reason);
            if (p.lastShuffleTurn == State.turn) return Fail("이번 준비에서는 이미 손패를 섞었습니다.", out reason);
            if (p.hand.Count == 0) return Fail("섞을 손패가 없습니다.", out reason);
            return true;
        }
        public bool ShuffleHand(int player, out string reason)
        {
            if (!CanShuffleHand(player, out reason)) return false;
            var p = State.players[player]; int count = p.hand.Count;
            p.deck.AddRange(p.hand); p.hand.Clear(); Shuffle(p.deck);
            for (int i = 0; i < count; i++) Draw(player);
            p.lastShuffleTurn = State.turn;
            if (player == 0 && State.turn == 1) p.mulliganUsed = true;
            Log("shuffle-hand", "손패 " + count + "장 전체를 덱에 넣고 섞어 다시 뽑았습니다.", count, player); return true;
        }
        public bool CanMulligan(int player, out string reason)
        {
            if (player != 0 || !State.openingHandOpen || State.turn != 1) return Fail("시작 손패를 선택하는 중이 아닙니다.", out reason);
            return CanShuffleHand(player, out reason);
        }
        public bool MulliganWholeHand(int player, out string reason)
        { if (!CanMulligan(player, out reason)) return false; return ShuffleHand(player, out reason); }
        public bool ConfirmOpeningHand(int player, out string reason)
        {
            reason = "";
            if (player != 0 || !State.openingHandOpen || State.turn != 1 || !IsPreparing(player)) return Fail("확정할 시작 손패가 없습니다.", out reason);
            State.openingHandOpen = false; Log("hand-confirm", "시작 손패 확정", State.players[player].hand.Count, player); return true;
        }
        public bool Mulligan(int player, IList<int> ids, out string reason)
        {
            if (ids != null && ids.Count == 0) return ConfirmOpeningHand(player, out reason);
            if (!CanMulligan(player, out reason)) return false;
            var hand = State.players[player].hand;
            if (ids == null || ids.Count != hand.Count || ids.Distinct().Count() != ids.Count || hand.Any(c => !ids.Contains(c.instanceId))) return Fail("클래식 셔플은 손패 전체를 교체합니다.", out reason);
            return MulliganWholeHand(player, out reason);
        }
        bool Draw(int player)
        {
            var p = State.players[player];
            if (p.deck.Count == 0) { Finish(1 - player, Owner(player) + "의 덱에서 보충할 카드가 없습니다."); return false; }
            var card = p.deck[0]; p.deck.RemoveAt(0);
            if (p.hand.Count < State.rules.handLimit) p.hand.Add(card); else p.discard.Add(card);
            return true;
        }
        void BeginRound(bool refill)
        {
            State.phase = BattlePhase.Main; State.openingHandOpen = !refill;
            for (int player = 0; player < 2; player++)
            {
                var p = State.players[player]; p.preparationComplete = false; p.playedThisPreparation = false; p.terrainPlayed = 0; p.levelPoints++;
                foreach (var unit in p.units)
                { unit.damage = unit.bonusPower = unit.armor = unit.bonusHealth = 0; unit.coinsRolled = unit.exhausted = unit.summoningSick = false; unit.rolledAttack = unit.rolledDefense = unit.rolledHealth = 0; }
            }
            RemoveDefeated();
            if (refill)
                for (int i = 0; i < 2; i++)
                {
                    int player = i == 0 ? State.activePlayer : 1 - State.activePlayer;
                    while (State.players[player].hand.Count < State.rules.handLimit && State.phase != BattlePhase.Complete) if (!Draw(player)) break;
                }
            Log("turn", Owner(State.activePlayer) + " 공격 준비 · 양측 LP +1 · 손패 " + State.rules.handLimit + "장 보충", 0, State.activePlayer);
            CheckVictory();
        }
        void AddProduction(PlayerState p, CardDefinition terrain)
        {
            p.SetResource(terrain.element, Math.Min(State.rules.maximumResource, p.Resource(terrain.element) + terrain.producedColored));
            p.genericSoul = Math.Min(State.rules.maximumResource, p.genericSoul + terrain.producedGeneric);
        }
        void RefreshSouls()
        {
            foreach (var p in State.players)
            {
                foreach (Element element in Enum.GetValues(typeof(Element))) p.SetResource(element, 0);
                p.genericSoul = 0;
                foreach (var card in p.terrain) AddProduction(p, CardCatalog.Get(card.definitionId));
            }
        }
        public bool CanUseLeaderSkill(int player, out string reason) => Fail("클래식 모드의 아바타에는 리더 기술이 없습니다.", out reason);
        public bool UseLeaderSkill(int player, out string reason) => CanUseLeaderSkill(player, out reason);
        int FirstEmptyLane(PlayerState p) { for (int lane = 0; lane < State.rules.unitLanes; lane++) if (p.AtLane(lane) == null && p.SpellAtLane(lane) == null) return lane; return -1; }
        static int TargetPlayer(CardDefinition card, int player) => card.effect == Effect.Damage || card.effect == Effect.Weaken || card.effect == Effect.DefenseBreak ? 1 - player : player;
        public bool CanPlay(int player, int instanceId, int lane, out string reason)
        {
            reason = "";
            if (!IsPreparing(player)) return Fail("자신의 준비 단계에만 카드를 배치할 수 있습니다.", out reason);
            if (State.openingHandOpen) return Fail("시작 손패를 먼저 확정하세요.", out reason);
            var p = State.players[player]; var card = p.hand.Find(x => x.instanceId == instanceId);
            if (card == null) return Fail("손패에서 카드를 선택하세요.", out reason);
            var d = CardCatalog.Get(card.definitionId);
            if ((d.playWindow == PlayWindow.AttackOnly && player != State.activePlayer) || (d.playWindow == PlayWindow.DefenseOnly && player == State.activePlayer)) return Fail(d.playWindow == PlayWindow.AttackOnly ? "공격 준비 전용 카드입니다." : "방어 준비 전용 카드입니다.", out reason);
            if (d.kind == CardKind.Terrain)
            {
                if (p.levelPoints < d.landPointCost) return Fail("LP " + d.landPointCost + "이 필요합니다.", out reason);
                if (p.terrain.Count >= State.rules.terrainLimit || p.terrainPlayed >= State.rules.terrainPerTurn) return Fail("전장 지형은 최대 " + State.rules.terrainLimit + "장입니다.", out reason);
                return true;
            }
            if (p.Resource(d.element) < d.coloredCost || p.genericSoul < d.cost) return Fail(CardCatalog.CostText(d) + "이 필요합니다.", out reason);
            if (d.kind == CardKind.Unit)
            {
                if (lane < 0 || lane >= State.rules.unitLanes || p.AtLane(lane) != null || p.SpellAtLane(lane) != null) return Fail("유닛과 예고 마법이 없는 전열 칸을 선택하세요.", out reason);
            }
            else if (d.kind == CardKind.Spell)
            {
                if (FirstEmptyLane(p) < 0) return Fail("마법을 예고할 빈 전열 칸이 필요합니다.", out reason);
                if (CardCatalog.RequiresUnitTarget(d) && (lane < 0 || lane >= State.rules.unitLanes || State.players[TargetPlayer(d, player)].AtLane(lane) == null)) return Fail("마법의 대상 유닛을 선택하세요.", out reason);
            }
            else
            {
                var target = lane < 0 || lane >= State.rules.unitLanes ? null : p.AtLane(lane);
                if (target == null) return Fail("아이템을 장착할 아군 유닛을 선택하세요.", out reason);
                if (d.requiredCapabilityMask != 0 && (CardCatalog.Get(target.definitionId).capabilityMask & d.requiredCapabilityMask) == 0) return Fail("이 유닛은 아이템의 장비·마법·야수 조건을 충족하지 않습니다.", out reason);
                if (target.equipment.Count >= 2) return Fail("유닛당 아이템은 2장까지입니다.", out reason);
            }
            return true;
        }
        public bool Play(int player, int instanceId, int lane, out string reason)
        {
            if (!CanPlay(player, instanceId, lane, out reason)) return false;
            var p = State.players[player]; var card = p.hand.Find(x => x.instanceId == instanceId); var d = CardCatalog.Get(card.definitionId);
            p.hand.Remove(card); p.playedThisPreparation = true;
            if (d.kind == CardKind.Terrain)
            { p.levelPoints -= d.landPointCost; p.terrain.Add(card); p.terrainPlayed++; AddProduction(p, d); }
            else
            {
                p.SetResource(d.element, p.Resource(d.element) - d.coloredCost); p.genericSoul -= d.cost;
                if (d.kind == CardKind.Unit) p.units.Add(new UnitState { instanceId = card.instanceId, definitionId = d.id, lane = lane });
                else if (d.kind == CardKind.Equipment) p.AtLane(lane).equipment.Add(card);
                else p.preparedSpells.Add(new PreparedSpellState { instanceId = card.instanceId, definitionId = d.id, lane = FirstEmptyLane(p), targetLane = CardCatalog.RequiresUnitTarget(d) ? lane : -1, targetPlayer = CardCatalog.RequiresUnitTarget(d) ? TargetPlayer(d, player) : -1 });
            }
            Log("play", Owner(player) + " · " + (d.kind == CardKind.Spell ? "마법 1장 예고" : d.name + " 배치"), d.kind == CardKind.Spell ? 0 : d.level, player);
            RemoveDefeated(); CheckVictory(); return true;
        }
        public bool CompletePreparation(int player, out string reason)
        {
            reason = "";
            if (!IsPreparing(player)) return Fail("현재 자신의 준비 단계가 아닙니다.", out reason);
            if (State.openingHandOpen) return Fail("시작 손패를 먼저 확정하세요.", out reason);
            State.players[player].preparationComplete = true; Log("ready", Owner(player) + " 준비 완료", 0, player);
            if (State.phase == BattlePhase.Main)
            { State.phase = BattlePhase.DefensePreparation; Log("phase", Owner(1 - player) + " 방어 준비", 0, 1 - player); }
            else ResolveCombat();
            return true;
        }
        public bool ReadyPreparation(int player, out string reason) => CompletePreparation(player, out reason);
        public bool BeginCombat(int player, out string reason) => CompletePreparation(player, out reason);
        public bool DeclareAttack(int player, int attackerId, int targetLane, out string reason) => Fail("클래식 전투는 양측 준비 완료 후 전열 순서로 진행합니다.", out reason);
        public bool ResolveBlock(int defender, int blockerId, out string reason) => Fail("클래식 방어는 전열 순서로 자동 진행합니다.", out reason);
        public bool EndTurn(int player, out string reason) => CompletePreparation(player, out reason);
        void ResolveCombat()
        {
            int attacker = State.activePlayer, defender = 1 - attacker;
            Log("phase", "전투 · 낮은 레벨 마법부터 공개", 0, attacker);
            var spells = State.players.SelectMany((p, owner) => p.preparedSpells.Select(spell => new { owner, spell }))
                .OrderBy(x => CardCatalog.Get(x.spell.definitionId).level).ThenBy(x => x.owner == attacker ? 0 : 1).ThenBy(x => x.spell.lane).ToArray();
            foreach (var entry in spells)
            {
                var p = State.players[entry.owner];
                ResolveSpell(entry.owner, entry.spell); p.preparedSpells.Remove(entry.spell); p.discard.Add(new CardInstance(entry.spell.instanceId, entry.spell.definitionId));
                RemoveDefeated(); CheckVictory();
                if (State.phase == BattlePhase.Complete) return;
            }
            foreach (var unit in State.players[attacker].units.OrderBy(u => u.lane).ToArray())
            {
                if (!State.players[attacker].units.Contains(unit)) continue;
                unit.exhausted = true;
                var blocker = State.players[defender].units.OrderBy(u => u.lane).FirstOrDefault();
                if (State.rules.coinOrder == CoinResolutionOrder.DefenderFirst && blocker != null) RollUnit(blocker, defender);
                RollUnit(unit, attacker);
                if (State.rules.coinOrder == CoinResolutionOrder.AttackerFirst && blocker != null) RollUnit(blocker, defender);
                RemoveDefeated();
                if (!State.players[attacker].units.Contains(unit)) continue;
                while (blocker != null && !State.players[defender].units.Contains(blocker))
                {
                    blocker = State.players[defender].units.OrderBy(u => u.lane).FirstOrDefault();
                    if (blocker != null) { RollUnit(blocker, defender); RemoveDefeated(); }
                }
                if (blocker == null)
                {
                    int amount = CardCatalog.Get(unit.definitionId).level;
                    DamagePlayer(defender, amount, CardCatalog.Get(unit.definitionId).name + " 직접 공격 · 레벨 " + amount);
                }
                else
                {
                    int outgoing = Math.Max(0, CardCatalog.UnitAttack(unit) - CardCatalog.UnitDefense(blocker));
                    int incoming = Math.Max(0, CardCatalog.UnitAttack(blocker) - CardCatalog.UnitDefense(unit));
                    blocker.damage += outgoing;
                    bool retaliate = State.rules.defenderStrikesBack && (State.rules.damageOrder == DamageResolutionOrder.Simultaneous || CardCatalog.UnitHealth(blocker) > 0);
                    if (retaliate) unit.damage += incoming;
                    Log("damage", CardCatalog.Get(unit.definitionId).name + " → " + CardCatalog.Get(blocker.definitionId).name + " 피해 " + outgoing + (retaliate ? " · 생존 반격 " + incoming : " · 반격 없음"), outgoing, attacker);
                }
                RemoveDefeated(); CheckVictory();
                if (State.phase == BattlePhase.Complete) return;
            }
            RefreshSouls();
            for (int player = 0; player < 2; player++)
            {
                var p = State.players[player]; int upkeep = p.units.Sum(u => CardCatalog.Get(u.definitionId).genericCost);
                int deficit = Math.Max(0, upkeep - p.genericSoul); p.genericSoul = Math.Max(0, p.genericSoul - upkeep);
                Log("upkeep", Owner(player) + " · 소울 회복 후 유지비 " + upkeep + (deficit > 0 ? " · 부족 " + deficit : " 지불"), upkeep, player);
                if (deficit > 0) DamagePlayer(player, deficit, "유지비 부족");
            }
            CheckVictory(); if (State.phase == BattlePhase.Complete) return;
            if (State.turn >= State.rules.maximumTurns)
            {
                int a = RemainingLife(0), b = RemainingLife(1); Finish(a == b ? 2 : a > b ? 0 : 1, "체험 결투 턴 한도 · 남은 " + (State.rules.victoryRule == VictoryRule.Deck ? "덱" : "라이프") + " 비교"); return;
            }
            State.turn++; State.activePlayer = defender; BeginRound(true);
        }
        void RollUnit(UnitState unit, int player)
        {
            if (unit.coinsRolled) return;
            var d = CardCatalog.Get(unit.definitionId);
            int count = State.rules.baseCoins >= 0 ? State.rules.baseCoins : Math.Max(d.attackCoin.count, Math.Max(d.defenseCoin.count, d.healthCoin.count));
            count = Math.Min(5, count + unit.extraCoins); var heads = new bool[count];
            for (int i = 0; i < count; i++)
            { heads[i] = StableRandom.Range(ref State.randomState, 2) == 1; Log("coin", d.name + " · C" + (i + 1) + " " + (heads[i] ? "앞면" : "뒷면"), heads[i] ? 1 : 0, player); }
            unit.rolledAttack = ApplyCoins(CardCatalog.BaseAttack(unit), d.attackCoin, heads);
            unit.rolledDefense = ApplyCoins(CardCatalog.BaseDefense(unit), d.defenseCoin, heads);
            unit.rolledHealth = ApplyCoins(CardCatalog.BaseHealth(unit), d.healthCoin, heads);
            unit.coinsRolled = true;
            Log("power", d.name + " 최종 AP " + unit.rolledAttack + " / DP " + unit.rolledDefense + " / HP " + unit.rolledHealth, unit.rolledAttack, player);
        }
        static int ApplyCoins(int basis, CoinRule spec, bool[] heads)
        {
            if (heads.Length == 0 || spec.operation == CoinOperation.None || spec.count == 0) return Math.Max(0, basis);
            int value = heads.Take(Math.Min(heads.Length, spec.count)).Count(h => h) * spec.multiplier;
            switch (spec.operation)
            { case CoinOperation.Add: basis += value; break; case CoinOperation.Subtract: basis -= value; break; case CoinOperation.Multiply: basis *= value; break; case CoinOperation.Set: basis = value; break; }
            return Math.Max(0, basis);
        }
        void ResolveSpell(int player, PreparedSpellState spell)
        {
            var d = CardCatalog.Get(spell.definitionId); var p = State.players[player]; var enemies = State.players[1 - player];
            var target = spell.targetPlayer < 0 ? null : State.players[spell.targetPlayer].AtLane(spell.targetLane);
            if (d.effect == Effect.StatOperations)
            {
                // Snapshot every delta before applying the card, including its still-occupied spell slot.
                int emptySlots = State.rules.unitLanes * 2 - State.players.Sum(owner => owner.units.Count + owner.preparedSpells.Count);
                var changes = new List<Tuple<UnitState, OperationStat, int>>();
                foreach (var operation in d.operations)
                    for (int side = 0; side < 2; side++)
                    {
                        if ((operation.side == OperationSide.Ally && side != player) || (operation.side == OperationSide.Enemy && side == player)) continue;
                        foreach (var unit in State.players[side].units)
                        {
                            var definition = CardCatalog.Get(unit.definitionId);
                            if ((operation.laneMask & (1 << unit.lane)) == 0 || (operation.requiredCapabilityMask != 0 && (definition.capabilityMask & operation.requiredCapabilityMask) == 0) || (operation.requiredElement >= 0 && (int)definition.element != operation.requiredElement)) continue;
                            int delta = operation.delta;
                            switch (operation.deltaSource)
                            {
                                case DeltaSource.OwnDefense: delta = CardCatalog.UnitDefense(unit); break;
                                case DeltaSource.OneBasedSlot: delta = unit.lane + 1; break;
                                case DeltaSource.NegativeOneBasedSlot: delta = -(unit.lane + 1); break;
                                case DeltaSource.NegativeEmptySlots: delta = -emptySlots; break;
                            }
                            if (operation.stat == OperationStat.PhysicalDamage) delta = Math.Max(0, delta - CardCatalog.UnitDefense(unit));
                            changes.Add(Tuple.Create(unit, operation.stat, delta));
                        }
                    }
                foreach (var change in changes)
                    switch (change.Item2)
                    {
                        case OperationStat.Attack: change.Item1.bonusPower += change.Item3; break;
                        case OperationStat.Defense: change.Item1.armor += change.Item3; break;
                        case OperationStat.Health: change.Item1.bonusHealth += change.Item3; break;
                        case OperationStat.PhysicalDamage: change.Item1.damage += change.Item3; break;
                    }
            }
            switch (d.effect)
            {
                case Effect.Heal: if (target != null) target.damage = Math.Max(0, target.damage - d.magnitude); break;
                case Effect.Damage: if (target != null) target.damage += Math.Max(0, d.magnitude - CardCatalog.UnitDefense(target)); break;
                case Effect.Rally: case Effect.Power: if (target != null) target.bonusPower += d.magnitude; break;
                case Effect.Armor: if (target != null) target.armor += d.magnitude; break;
                case Effect.Health: if (target != null) target.bonusHealth += d.magnitude; break;
                case Effect.Weaken: if (target != null) target.bonusPower -= d.magnitude; break;
                case Effect.DefenseBreak: if (target != null) target.armor -= d.magnitude; break;
                case Effect.Coin: if (target != null) target.extraCoins = Math.Min(5, target.extraCoins + d.magnitude); break;
                case Effect.Draw: for (int i = 0; i < d.magnitude && State.phase != BattlePhase.Complete; i++) Draw(player); break;
                case Effect.Resource: p.SetResource(d.element, Math.Min(State.rules.maximumResource, p.Resource(d.element) + d.magnitude)); p.genericSoul = Math.Min(State.rules.maximumResource, p.genericSoul + d.magnitude); break;
                case Effect.Sweep: foreach (var u in enemies.units) u.damage += Math.Max(0, d.magnitude - CardCatalog.UnitDefense(u)); break;
                case Effect.HealAll: foreach (var u in p.units) u.damage = Math.Max(0, u.damage - d.magnitude); break;
                case Effect.RallyAll: foreach (var u in p.units) u.bonusPower += d.magnitude; break;
                case Effect.ArmorAll: foreach (var u in p.units) u.armor += d.magnitude; break;
                case Effect.HealthAll: foreach (var u in p.units) u.bonusHealth += d.magnitude; break;
                case Effect.DrainAll: foreach (var u in p.units) u.bonusHealth += d.magnitude; foreach (var u in enemies.units) u.bonusHealth -= d.magnitude; break;
            }
            Log("spell", d.name + " 공개 · " + d.text + (CardCatalog.RequiresUnitTarget(d) && target == null ? " (대상 부재)" : ""), d.magnitude, player);
        }
        void RemoveDefeated()
        {
            for (int player = 0; player < 2; player++)
            {
                var p = State.players[player];
                foreach (var unit in p.units.Where(u => CardCatalog.UnitHealth(u) <= 0).OrderBy(u => u.lane).ToArray())
                { p.units.Remove(unit); p.discard.Add(new CardInstance(unit.instanceId, unit.definitionId)); p.discard.AddRange(unit.equipment); Log("defeat", CardCatalog.Get(unit.definitionId).name + " 전장 이탈", 0, player); }
            }
        }
        void DamagePlayer(int player, int amount, string cause)
        {
            var p = State.players[player];
            if (State.rules.victoryRule == VictoryRule.Life) p.health -= amount;
            else for (int i = 0; i < amount && p.deck.Count > 0; i++) { var card = p.deck[0]; p.deck.RemoveAt(0); p.discard.Add(card); }
            Log("damage", cause + " · " + Owner(player) + (State.rules.victoryRule == VictoryRule.Deck ? " 덱 " : " 라이프 ") + "-" + amount, amount, player);
        }
        int RemainingLife(int player) => State.rules.victoryRule == VictoryRule.Deck ? State.players[player].deck.Count : State.players[player].health;
        void CheckVictory()
        {
            if (State.phase == BattlePhase.Complete) return;
            bool a = RemainingLife(0) <= 0, b = RemainingLife(1) <= 0;
            if (a || b) Finish(a && b ? State.activePlayer : a ? 1 : 0, a && b ? "양측 소진 · 공격측 승리" : (State.rules.victoryRule == VictoryRule.Deck ? "덱" : "라이프") + " 소진");
        }
        public bool Concede(int player)
        { if (player < 0 || player > 1 || State.phase == BattlePhase.Complete) return false; Finish(1 - player, Owner(player) + " 결투 포기"); return true; }
        void Finish(int winner, string reason)
        { State.winner = winner; State.phase = BattlePhase.Complete; State.openingHandOpen = false; Log("result", reason + " · " + (winner == 2 ? "무승부" : Owner(winner) + " 승리"), winner); }
        static bool Fail(string message, out string reason) { reason = message; return false; }
        static string Owner(int player) => player == 0 ? "나" : "상대";
        void Log(string kind, string text, int amount = 0, int actor = -1)
        { State.events.Add(new BattleEvent { sequence = State.events.Count + 1, turn = State.turn, kind = kind, text = text, amount = amount, actor = actor }); }
    }
}
