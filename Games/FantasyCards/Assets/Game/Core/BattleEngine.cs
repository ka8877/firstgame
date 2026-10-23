using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    public sealed class BattleEngine
    {
        public BattleState State { get; private set; }
        public BattleEngine(uint seed, RuleSet rules = null, IList<string> playerDeck = null, IList<string> aiDeck = null,
            string playerLeader = "lyria", string aiLeader = "serin", string playerDeckId = "custom", string aiDeckId = "custom", int encounterIndex = -1)
        {
            rules = rules ?? new RuleSet(); rules.Validate();
            DemoCatalog.GetLeader(playerLeader); DemoCatalog.GetLeader(aiLeader);
            if (encounterIndex < -1 || encounterIndex >= DemoCatalog.Encounters.Length) throw new ArgumentOutOfRangeException(nameof(encounterIndex));
            State = new BattleState { randomState = seed == 0 ? 1u : seed, rules = rules.Copy(), encounterIndex = encounterIndex };
            AddPlayer(playerDeck ?? CardCatalog.DefaultDeck(), playerLeader, playerDeckId);
            AddPlayer(aiDeck ?? CardCatalog.DefaultDeck(true), aiLeader, aiDeckId);
            for (int p = 0; p < 2; p++)
            {
                if (rules.guaranteeOpeningElements && rules.openingHand >= 2)
                {
                    DrawOpeningTerrain(p, Element.Dawn); DrawOpeningTerrain(p, Element.Tide);
                }
                while (State.players[p].hand.Count < rules.openingHand) Draw(p);
            }
            Log("start", encounterIndex < 0 ? "별빛의 서약 · 첫 결투가 시작됩니다." : DemoCatalog.GetEncounter(encounterIndex).name + " · 결투 시작");
            BeginTurn(false);
            Validate(State);
        }
        public BattleEngine(BattleState saved)
        {
            Validate(saved); State = saved.Copy();
        }
        public static void Validate(BattleState s)
        {
            if (s == null || s.schemaVersion != 2 || s.contentVersion != CardCatalog.Version || s.rules == null || s.players == null || s.players.Count != 2 || s.events == null || s.randomState == 0 || s.activePlayer < 0 || s.activePlayer > 1 || s.turn < 1 || !Enum.IsDefined(typeof(BattlePhase), s.phase)) throw new ArgumentException("이번 체험판은 전투 저장 버전 2를 사용합니다. 이전 저장은 보존한 채 새 결투를 시작해 주세요.");
            s.rules.Validate(); var ids = new HashSet<int>();
            if (s.turn > s.rules.maximumTurns + 1 || s.nextInstanceId != s.rules.deckSize * 2 + 1 || (s.phase != BattlePhase.Complete && s.winner != -1)) throw new ArgumentException("잘못된 턴 또는 결과 상태입니다.");
            if (s.encounterIndex < -1 || s.encounterIndex >= DemoCatalog.Encounters.Length) throw new ArgumentException("잘못된 체험전입니다.");
            foreach (var p in s.players)
            {
                if (p == null || p.deck == null || p.hand == null || p.terrain == null || p.discard == null || p.units == null || p.units.Count > s.rules.unitLanes || p.terrain.Count > s.rules.terrainLimit || p.hand.Count > s.rules.handLimit || p.dawn < 0 || p.tide < 0 || p.dawn > s.rules.maximumResource || p.tide > s.rules.maximumResource || p.health > s.rules.startingHealth || p.health < -10000 || p.fatigue < 0 || p.fatigue > s.rules.maximumTurns || p.terrainPlayed < 0 || p.terrainPlayed > s.rules.terrainPerTurn || (s.phase != BattlePhase.Complete && p.health <= 0)) throw new ArgumentException("잘못된 플레이어 상태입니다.");
                DemoCatalog.GetLeader(p.leaderId);
                if (p.deckId != "custom") DemoCatalog.GetDeck(p.deckId);
                if (p.lastLeaderSkillTurn < 0 || p.lastLeaderSkillTurn > s.turn || p.leaderSkillUses < 0 || p.leaderSkillUses > (s.turn + 1) / 2 || (p.leaderSkillUses == 0) != (p.lastLeaderSkillTurn == 0)) throw new ArgumentException("리더 기술 사용 정보가 올바르지 않습니다.");
                int previousIds = ids.Count;
                foreach (var c in p.deck.Concat(p.hand).Concat(p.terrain).Concat(p.discard)) CheckCard(c, ids);
                var lanes = new HashSet<int>();
                foreach (var u in p.units)
                {
                    if (u == null || !ids.Add(u.instanceId) || u.instanceId <= 0 || u.lane < 0 || u.lane >= s.rules.unitLanes || !lanes.Add(u.lane) || CardCatalog.Get(u.definitionId).kind != CardKind.Unit || u.damage < 0 || u.damage >= CardCatalog.Get(u.definitionId).health || u.equipment == null || u.equipment.Count > 2 || u.bonusPower < 0 || u.bonusPower > s.rules.deckSize * 20 || u.armor < 0 || u.armor > 40 || u.extraCoins < 0 || u.extraCoins > 40) throw new ArgumentException("잘못된 유닛 상태입니다.");
                    foreach (var equipment in u.equipment) { CheckCard(equipment, ids); if (CardCatalog.Get(equipment.definitionId).kind != CardKind.Equipment) throw new ArgumentException("잘못된 장비입니다."); }
                }
                if (p.terrain.Any(c => CardCatalog.Get(c.definitionId).kind != CardKind.Terrain)) throw new ArgumentException("잘못된 지형입니다.");
                if (ids.Count - previousIds != s.rules.deckSize) throw new ArgumentException("플레이어의 카드 수가 보존되지 않았습니다.");
                if (p.deckId != "custom")
                {
                    var actual = p.deck.Concat(p.hand).Concat(p.terrain).Concat(p.discard).Select(c => c.definitionId)
                        .Concat(p.units.Select(u => u.definitionId)).Concat(p.units.SelectMany(u => u.equipment).Select(c => c.definitionId));
                    if (!actual.OrderBy(x => x).SequenceEqual(DemoCatalog.GetDeck(p.deckId).cards.OrderBy(x => x))) throw new ArgumentException("선택한 기본 덱과 저장 카드 구성이 다릅니다.");
                }
            }
            if (ids.Count != 2 * s.rules.deckSize || ids.Any(x => x >= s.nextInstanceId)) throw new ArgumentException("카드 보존 검증에 실패했습니다.");
            if (s.phase == BattlePhase.BlockResponse && (!s.players[s.activePlayer].units.Any(x => x.instanceId == s.pendingAttackerId && x.exhausted && !x.summoningSick) || s.pendingTargetLane < -1 || s.pendingTargetLane >= s.rules.unitLanes || (s.pendingTargetLane >= 0 && s.players[1 - s.activePlayer].AtLane(s.pendingTargetLane) == null))) throw new ArgumentException("공격 선언 정보가 없습니다.");
            for (int i = 0; i < s.events.Count; i++) if (s.events[i] == null || s.events[i].sequence != i + 1 || s.events[i].turn < 1 || s.events[i].turn > s.turn || s.events[i].text == null || s.events[i].kind == null || s.events[i].actor < -1 || s.events[i].actor > 1) throw new ArgumentException("전투 기록이 잘못되었습니다.");
            for (int player = 0; player < 2; player++)
            {
                var skills = s.events.Where(e => e.kind == "leader" && e.actor == player).ToArray();
                var p = s.players[player];
                if (skills.Length != p.leaderSkillUses || skills.Select(e => e.turn).Distinct().Count() != skills.Length || skills.Any(e => e.turn % 2 != (player == 0 ? 1 : 0)) || (skills.Length > 0 && skills.Last().turn != p.lastLeaderSkillTurn)) throw new ArgumentException("리더 기술 기록과 사용 횟수가 다릅니다.");
            }
            var mulligans = s.events.Where(e => e.kind == "mulligan").ToArray();
            if (s.players[1].mulliganUsed || mulligans.Length != (s.players[0].mulliganUsed ? 1 : 0) || mulligans.Any(e => e.actor != 0 || e.turn != 1 || e.amount < 0 || e.amount > s.rules.openingHand)) throw new ArgumentException("시작 손패 교체 기록이 올바르지 않습니다.");
            if (s.openingHandOpen && (s.turn != 1 || s.activePlayer != 0 || s.phase != BattlePhase.Main || s.players[0].mulliganUsed || s.events.Any(e => e.kind == "play" || e.kind == "leader" || e.kind == "phase"))) throw new ArgumentException("시작 손패 선택 시점이 올바르지 않습니다.");
            if (s.encounterIndex >= 0)
            {
                var encounter = DemoCatalog.GetEncounter(s.encounterIndex);
                if (s.players[1].deckId != encounter.deckId || s.players[1].leaderId != encounter.leaderId) throw new ArgumentException("체험전 상대가 일치하지 않습니다.");
            }
            if (s.phase == BattlePhase.Complete && (s.winner < 0 || s.winner > 2)) throw new ArgumentException("승패 정보가 없습니다.");
        }
        private static void CheckCard(CardInstance c, HashSet<int> ids)
        {
            if (c == null || c.instanceId <= 0 || !ids.Add(c.instanceId)) throw new ArgumentException("카드 인스턴스가 잘못되었습니다."); CardCatalog.Get(c.definitionId);
        }
        private void AddPlayer(IList<string> deck, string leader, string deckId)
        {
            if (deck.Count != State.rules.deckSize) throw new ArgumentException("덱은 " + State.rules.deckSize + "장이어야 합니다.");
            if (deckId != "custom") DemoCatalog.GetDeck(deckId);
            var p = new PlayerState { health = State.rules.startingHealth, leaderId = leader, deckId = deckId };
            foreach (string id in deck) { CardCatalog.Get(id); p.deck.Add(new CardInstance(State.nextInstanceId++, id)); }
            Shuffle(p.deck);
            State.players.Add(p);
            Log("shuffle", DemoCatalog.GetLeader(leader).name + "의 덱 " + deck.Count + "장을 섞었습니다.", deck.Count, State.players.Count - 1);
        }
        private void Shuffle(List<CardInstance> deck)
        {
            for (int i = deck.Count - 1; i > 0; i--) { int j = StableRandom.Range(ref State.randomState, i + 1); var c = deck[i]; deck[i] = deck[j]; deck[j] = c; }
        }
        public bool CanMulligan(int player, out string reason)
        {
            reason = "";
            if (player != 0 || !State.openingHandOpen || State.players[0].mulliganUsed || State.turn != 1 || State.activePlayer != 0 || State.phase != BattlePhase.Main)
                return Fail("시작 손패는 첫 행동 전에 한 번만 교체할 수 있습니다.", out reason);
            return true;
        }
        public bool Mulligan(int player, IList<int> cardInstanceIds, out string reason)
        {
            if (!CanMulligan(player, out reason)) return false;
            var p = State.players[player];
            if (cardInstanceIds == null || cardInstanceIds.Distinct().Count() != cardInstanceIds.Count || cardInstanceIds.Count > p.deck.Count || cardInstanceIds.Any(id => !p.hand.Any(c => c.instanceId == id)))
                return Fail("교체할 시작 손패를 중복 없이 선택하세요.", out reason);
            var returning = p.hand.Where(c => cardInstanceIds.Contains(c.instanceId)).ToList();
            foreach (var card in returning) p.hand.Remove(card);
            // Draw before returning the rejected instances, so they cannot immediately reappear.
            for (int i = 0; i < returning.Count; i++) Draw(player);
            p.deck.AddRange(returning);
            if (returning.Count > 0) Shuffle(p.deck);
            p.mulliganUsed = true; State.openingHandOpen = false;
            Log("mulligan", returning.Count == 0 ? "시작 손패를 확정했습니다." : "시작 손패 " + returning.Count + "장을 교체하고 남은 덱을 섞었습니다.", returning.Count, player);
            return true;
        }
        private void DrawOpeningTerrain(int player, Element element)
        {
            var p = State.players[player]; var card = p.deck.Find(c => CardCatalog.Get(c.definitionId).kind == CardKind.Terrain && CardCatalog.Get(c.definitionId).element == element);
            if (card != null) { p.deck.Remove(card); p.hand.Add(card); }
        }
        private void Draw(int player)
        {
            var p = State.players[player];
            if (p.deck.Count == 0) { p.health -= ++p.fatigue; Log("fatigue", Owner(player) + " 탈진 피해 " + p.fatigue, p.fatigue, player); return; }
            var card = p.deck[0]; p.deck.RemoveAt(0);
            if (p.hand.Count >= State.rules.handLimit) { p.discard.Add(card); Log("burn", Owner(player) + " 손패 한도: " + CardCatalog.Get(card.definitionId).name + " 소멸", 0, player); }
            else p.hand.Add(card);
        }
        private void BeginTurn(bool draw)
        {
            var p = State.players[State.activePlayer]; p.terrainPlayed = 0;
            foreach (var u in p.units) { u.exhausted = false; u.summoningSick = false; }
            foreach (var terrain in p.terrain) AddResource(p, CardCatalog.Get(terrain.definitionId).element, 1);
            State.phase = BattlePhase.Main; State.pendingAttackerId = -1; State.pendingTargetLane = -1;
            if (draw) Draw(State.activePlayer);
            Log("turn", Owner(State.activePlayer) + "의 준비 단계 · 지형 자원 충전", 0, State.activePlayer); CheckVictory();
        }
        private void AddResource(PlayerState p, Element element, int value)
        {
            if (element == Element.Dawn) p.dawn = Math.Min(State.rules.maximumResource, p.dawn + value); else p.tide = Math.Min(State.rules.maximumResource, p.tide + value);
        }
        private static void Pay(PlayerState p, Element element, int cost)
        {
            if (element == Element.Dawn) p.dawn -= cost; else p.tide -= cost;
        }
        public bool CanUseLeaderSkill(int player, out string reason)
        {
            reason = "";
            if (player < 0 || player > 1 || State.activePlayer != player || State.phase != BattlePhase.Main)
                return Fail("리더 기술은 자신의 준비 단계에만 사용할 수 있습니다.", out reason);
            var p = State.players[player]; var leader = DemoCatalog.GetLeader(p.leaderId);
            if (p.lastLeaderSkillTurn == State.turn) return Fail("이번 턴에는 이미 리더 기술을 사용했습니다.", out reason);
            if (p.Resource(leader.skillElement) < leader.skillCost) return Fail(CardCatalog.ElementName(leader.skillElement) + " " + leader.skillCost + "이 필요합니다.", out reason);
            if (leader.skill == LeaderSkill.Rally && p.units.Count == 0) return Fail("강화할 아군 유닛이 필요합니다.", out reason);
            if (leader.skill == LeaderSkill.Restoration && p.health >= State.rules.startingHealth && p.units.All(u => u.damage == 0)) return Fail("회복할 피해가 없습니다.", out reason);
            if (leader.skill == LeaderSkill.Insight && (p.deck.Count == 0 || p.hand.Count >= State.rules.handLimit)) return Fail("덱의 카드와 손패 여유가 필요합니다.", out reason);
            return true;
        }
        public bool UseLeaderSkill(int player, out string reason)
        {
            if (!CanUseLeaderSkill(player, out reason)) return false;
            var p = State.players[player]; var enemy = State.players[1 - player]; var leader = DemoCatalog.GetLeader(p.leaderId);
            Pay(p, leader.skillElement, leader.skillCost); p.lastLeaderSkillTurn = State.turn; p.leaderSkillUses++;
            State.openingHandOpen = false;
            Log("leader", leader.name + " · " + leader.skillName + " 사용", leader.skillCost, player);
            switch (leader.skill)
            {
                case LeaderSkill.QuickShot:
                    var target = enemy.units.OrderBy(u => CardCatalog.Get(u.definitionId).health - u.damage).ThenBy(u => u.lane).FirstOrDefault();
                    if (target == null)
                    {
                        enemy.health--; Log("effect", "꽃바람 화살 → " + DemoCatalog.GetLeader(enemy.leaderId).name + " 생명 -1", 1, player);
                    }
                    else
                    {
                        int damage = Math.Max(0, 2 - target.armor); target.damage += damage;
                        Log("effect", "꽃바람 화살 → " + CardCatalog.Get(target.definitionId).name + " 피해 " + damage, damage, player);
                    }
                    RemoveDefeated(); break;
                case LeaderSkill.Rally:
                    foreach (var unit in p.units) unit.bonusPower++;
                    Log("effect", "모든 아군 유닛의 공격 +1", p.units.Count, player); break;
                case LeaderSkill.Restoration:
                    p.health = Math.Min(State.rules.startingHealth, p.health + 3);
                    foreach (var unit in p.units) unit.damage = Math.Max(0, unit.damage - 1);
                    Log("effect", "리더 생명 3 / 아군 유닛 피해 1 회복", 3, player); break;
                case LeaderSkill.Insight:
                    Draw(player); AddResource(p, Element.Dawn, 1);
                    Log("effect", "카드 1장 뽑기 · 여명 1 획득", 1, player); break;
            }
            CheckVictory(); return true;
        }
        public bool CanPlay(int player, int instanceId, int lane, out string reason)
        {
            reason = "";
            if (State.phase != BattlePhase.Main || State.activePlayer != player) return Fail("준비 단계에만 카드를 사용할 수 있습니다.", out reason);
            var p = State.players[player]; var card = p.hand.Find(x => x.instanceId == instanceId);
            if (card == null) return Fail("손패에서 카드를 선택하세요.", out reason);
            var d = CardCatalog.Get(card.definitionId);
            if (p.Resource(d.element) < d.cost) return Fail(CardCatalog.ElementName(d.element) + " 자원이 부족합니다.", out reason);
            if (d.kind == CardKind.Terrain)
            {
                if (p.terrainPlayed >= State.rules.terrainPerTurn || p.terrain.Count >= State.rules.terrainLimit) return Fail("지형은 턴당 " + State.rules.terrainPerTurn + "장, 최대 " + State.rules.terrainLimit + "장입니다.", out reason);
            }
            else if (d.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(d))
            {
                if (d.effect == Effect.Sweep && State.players[1 - player].units.Count == 0) return Fail("피해를 줄 적 유닛이 없습니다.", out reason);
                if (d.effect == Effect.Resource && p.Resource(d.element) >= State.rules.maximumResource) return Fail("이미 자원 한도입니다.", out reason);
            }
            else
            {
                if (lane < 0 || lane >= State.rules.unitLanes) return Fail("대상 전장 칸을 선택하세요.", out reason);
                if (d.kind == CardKind.Unit && p.AtLane(lane) != null) return Fail("비어 있는 아군 칸을 선택하세요.", out reason);
                var target = (d.effect == Effect.Damage ? State.players[1 - player] : p).AtLane(lane);
                if ((d.kind == CardKind.Spell || d.kind == CardKind.Equipment) && target == null) return Fail("효과를 받을 유닛을 선택하세요.", out reason);
                if (d.kind == CardKind.Equipment && target.equipment.Count >= 2) return Fail("유닛당 장비는 2개까지입니다.", out reason);
            }
            return true;
        }
        private static bool Fail(string message, out string reason) { reason = message; return false; }
        public bool Play(int player, int instanceId, int lane, out string reason)
        {
            if (!CanPlay(player, instanceId, lane, out reason)) return false;
            var p = State.players[player]; var card = p.hand.Find(x => x.instanceId == instanceId); var d = CardCatalog.Get(card.definitionId);
            State.openingHandOpen = false;
            p.hand.Remove(card); Pay(p, d.element, d.cost);
            Log("play", Owner(player) + " · " + d.name + " 사용", d.cost, player);
            if (d.kind == CardKind.Terrain) { p.terrain.Add(card); p.terrainPlayed++; AddResource(p, d.element, 1); }
            else if (d.kind == CardKind.Unit) p.units.Add(new UnitState { instanceId = card.instanceId, definitionId = card.definitionId, lane = lane, summoningSick = true,
                armor = d.effect == Effect.Armor ? d.magnitude : 0, extraCoins = d.effect == Effect.Coin ? d.magnitude : 0, bonusPower = d.effect == Effect.Power ? d.magnitude : 0 });
            else if (!CardCatalog.RequiresUnitTarget(d))
            {
                p.discard.Add(card);
                if (d.effect == Effect.Draw) for (int i = 0; i < d.magnitude; i++) Draw(player);
                else if (d.effect == Effect.Resource) AddResource(p, d.element, d.magnitude);
                else if (d.effect == Effect.Sweep)
                {
                    foreach (var target in State.players[1 - player].units) target.damage += Math.Max(0, d.magnitude - target.armor);
                    RemoveDefeated();
                }
                Log("effect", d.name + " · " + d.text, d.magnitude, player);
            }
            else
            {
                var target = (d.effect == Effect.Damage ? State.players[1 - player] : p).AtLane(lane);
                switch (d.effect)
                {
                    case Effect.Heal: target.damage = Math.Max(0, target.damage - d.magnitude); break;
                    case Effect.Damage: target.damage += Math.Max(0, d.magnitude - target.armor); break;
                    case Effect.Rally: case Effect.Power: target.bonusPower += d.magnitude; break;
                    case Effect.Armor: target.armor += d.magnitude; break;
                    case Effect.Coin: target.extraCoins += d.magnitude; break;
                }
                Log("effect", d.name + " → " + CardCatalog.Get(target.definitionId).name + " · " + d.text, d.magnitude, player);
                if (d.kind == CardKind.Equipment) target.equipment.Add(card); else p.discard.Add(card);
                RemoveDefeated();
            }
            CheckVictory(); return true;
        }
        public bool BeginCombat(int player, out string reason)
        {
            reason = ""; if (State.activePlayer != player || State.phase != BattlePhase.Main) return Fail("현재 준비 단계가 아닙니다.", out reason);
            State.openingHandOpen = false;
            State.phase = BattlePhase.AttackDeclaration; Log("phase", Owner(player) + " · 공격 준비", 0, player); return true;
        }
        public bool DeclareAttack(int player, int attackerId, int targetLane, out string reason)
        {
            reason = ""; if (State.activePlayer != player || State.phase != BattlePhase.AttackDeclaration) return Fail("공격 단계에 공격자를 선택하세요.", out reason);
            var unit = State.players[player].units.Find(u => u.instanceId == attackerId);
            if (unit == null || unit.exhausted || unit.summoningSick) return Fail("행동 가능한 유닛이 아닙니다. 소환한 턴에는 공격할 수 없습니다.", out reason);
            if (targetLane < -1 || targetLane >= State.rules.unitLanes || (targetLane >= 0 && State.players[1 - player].AtLane(targetLane) == null)) return Fail("적 영웅이나 유닛을 선택하세요.", out reason);
            unit.exhausted = true; State.pendingAttackerId = attackerId; State.pendingTargetLane = targetLane; State.phase = BattlePhase.BlockResponse;
            Log("declare", CardCatalog.Get(unit.definitionId).name + " → " + (targetLane < 0 ? "적 서약자" : CardCatalog.Get(State.players[1 - player].AtLane(targetLane).definitionId).name) + " 공격 선언. 방어 대응 대기.", 0, player); return true;
        }
        public bool ResolveBlock(int defender, int blockerId, out string reason)
        {
            reason = ""; if (State.phase != BattlePhase.BlockResponse || defender == State.activePlayer || defender < 0 || defender > 1) return Fail("현재 방어 대응 차례가 아닙니다.", out reason);
            var defending = State.players[defender]; UnitState blocker = null;
            if (blockerId >= 0)
            {
                blocker = defending.units.Find(u => u.instanceId == blockerId);
                if (blocker == null || blocker.exhausted || blocker.summoningSick) return Fail("준비된 방어 유닛을 선택하세요.", out reason);
            }
            var attacker = State.players[State.activePlayer].units.Find(u => u.instanceId == State.pendingAttackerId);
            var target = blocker ?? defending.AtLane(State.pendingTargetLane);
            if (blocker != null) { blocker.exhausted = true; Log("block", CardCatalog.Get(blocker.definitionId).name + " 방어 참여", 0, defender); }
            else Log("block", "가로막지 않음 · 선언된 대상으로 공격 진행", 0, defender);
            int attackPower, defensePower;
            if (State.rules.coinOrder == CoinResolutionOrder.DefenderFirst)
            {
                defensePower = target != null && State.rules.defenderStrikesBack ? RollPower(target, defender) : 0;
                attackPower = RollPower(attacker, State.activePlayer);
            }
            else
            {
                attackPower = RollPower(attacker, State.activePlayer);
                defensePower = target != null && State.rules.defenderStrikesBack ? RollPower(target, defender) : 0;
            }
            if (target == null)
            {
                defending.health -= attackPower; Log("damage", Owner(defender) + " 서약자에게 " + attackPower + " 피해", attackPower, defender);
            }
            else
            {
                int outgoing = Math.Max(0, attackPower - target.armor); int incoming = Math.Max(0, defensePower - attacker.armor);
                bool counterCanceled = State.rules.damageOrder == DamageResolutionOrder.AttackerFirst && target.damage + outgoing >= CardCatalog.Get(target.definitionId).health;
                if (counterCanceled) incoming = 0;
                target.damage += outgoing; attacker.damage += incoming;
                Log("damage", CardCatalog.Get(attacker.definitionId).name + " " + attackPower + " - 방어 " + target.armor + " = " + outgoing + " 피해 / " + (counterCanceled ? "선공 격파로 반격 취소" : "반격 " + defensePower + " - 방어 " + attacker.armor + " = " + incoming + " 피해"), outgoing, State.activePlayer);
            }
            RemoveDefeated(); State.pendingAttackerId = -1; State.pendingTargetLane = -1; State.phase = BattlePhase.AttackDeclaration; CheckVictory(); return true;
        }
        private int RollPower(UnitState unit, int player)
        {
            var d = CardCatalog.Get(unit.definitionId); int power = d.attack + unit.bonusPower;
            for (int i = 0; i < State.rules.baseCoins + unit.extraCoins; i++)
            {
                bool heads = StableRandom.Range(ref State.randomState, 2) == 1; int bonus = heads ? d.coinBonus : 0; power += bonus;
                Log("coin", d.name + " · " + (heads ? "앞면" : "뒷면") + " · 공격 +" + bonus, heads ? 1 : 0, player);
            }
            Log("power", d.name + " 최종 공격 " + power, power, player); return power;
        }
        private void RemoveDefeated()
        {
            for (int p = 0; p < 2; p++)
            {
                var player = State.players[p];
                foreach (var unit in player.units.Where(x => x.damage >= CardCatalog.Get(x.definitionId).health).OrderBy(x => x.lane).ToList())
                {
                    player.units.Remove(unit); player.discard.Add(new CardInstance(unit.instanceId, unit.definitionId)); player.discard.AddRange(unit.equipment); Log("defeat", CardCatalog.Get(unit.definitionId).name + " 전장 이탈", 0, p);
                }
            }
        }
        public bool EndTurn(int player, out string reason)
        {
            reason = ""; if (State.activePlayer != player || (State.phase != BattlePhase.Main && State.phase != BattlePhase.AttackDeclaration)) return Fail("방어 대응을 먼저 마치세요.", out reason);
            State.openingHandOpen = false;
            State.turn++;
            if (State.turn > State.rules.maximumTurns)
            {
                int a = State.players[0].health, b = State.players[1].health; Finish(a == b ? 2 : a > b ? 0 : 1, "턴 제한: 남은 생명 비교"); return true;
            }
            State.activePlayer = 1 - player; BeginTurn(true); return true;
        }
        public bool Concede(int player)
        {
            if (player < 0 || player > 1 || State.phase == BattlePhase.Complete) return false; Finish(1 - player, "서약자가 결투를 포기했습니다."); return true;
        }
        private void CheckVictory()
        {
            if (State.phase == BattlePhase.Complete) return;
            bool a = State.players[0].health <= 0, b = State.players[1].health <= 0;
            if (a || b) Finish(a && b ? 2 : a ? 1 : 0, "서약자의 생명이 소진되었습니다.");
        }
        private void Finish(int winner, string reason)
        {
            State.winner = winner; State.phase = BattlePhase.Complete; State.pendingAttackerId = -1; State.pendingTargetLane = -1; State.openingHandOpen = false;
            Log("result", reason + " " + (winner == 2 ? "무승부" : Owner(winner) + " 승리"), winner);
        }
        private static string Owner(int player) { return player == 0 ? "나" : "달의 수호자"; }
        private void Log(string kind, string message, int amount = 0, int actor = -1)
        {
            State.events.Add(new BattleEvent { sequence = State.events.Count + 1, turn = State.turn, kind = kind, text = message, amount = amount, actor = actor });
        }
    }
}
