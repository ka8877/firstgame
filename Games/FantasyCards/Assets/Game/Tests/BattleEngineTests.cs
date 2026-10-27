using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;

namespace FantasyCards.Tests
{
    public static class ClassicFixtures
    {
        public static string Id(string name) => CardCatalog.All.Single(c => c.name == name).id;
        public static BattleEngine Battle(uint seed = 42, RuleSet rules = null)
        {
            rules = rules ?? new RuleSet();
            var pool = CardCatalog.All.Where(c => c.kind == CardKind.Terrain).Select(c => c.id)
                .Concat(CardCatalog.All.Where(c => c.kind == CardKind.Unit).OrderBy(c => c.level).Select(c => c.id)).ToList();
            var deck = pool.Take(rules.deckSize).ToList();
            var e = new BattleEngine(seed, rules, deck, deck); e.ConfirmOpeningHand(0, out _); return e;
        }
        public static CardInstance Extract(BattleEngine engine, int player, string name)
        {
            var p = engine.State.players[player]; string id = Id(name);
            var card = p.deck.Concat(p.hand).FirstOrDefault(c => c.definitionId == id);
            if (card == null)
            {
                // Build a legal custom fixture by replacing one unplayed card, preserving its stable instance.
                card = p.deck.Last(c => p.deck.Concat(p.hand).Count(x => x.definitionId == c.definitionId) == 1);
                card.definitionId = id;
            }
            p.deck.Remove(card); p.hand.Remove(card); return card;
        }
        public static UnitState Unit(BattleEngine e, int player, string name, int lane)
        {
            var c = Extract(e, player, name); var unit = new UnitState { instanceId = c.instanceId, definitionId = c.definitionId, lane = lane };
            e.State.players[player].units.Add(unit); return unit;
        }
        public static CardInstance Hand(BattleEngine e, int player, string name)
        {
            var c = Extract(e, player, name); var p = e.State.players[player];
            if (p.hand.Count >= e.State.rules.handLimit) { p.deck.Add(p.hand[0]); p.hand.RemoveAt(0); }
            p.hand.Add(c); return c;
        }
        public static void Souls(BattleEngine e, int player, int count = 30)
        { var p = e.State.players[player]; foreach (Element element in Enum.GetValues(typeof(Element))) p.SetResource(element, count); p.genericSoul = count; }
        public static void Resolve(BattleEngine e)
        { Assert.True(e.CompletePreparation(e.PreparingPlayer, out var error), error); Assert.True(e.CompletePreparation(e.PreparingPlayer, out error), error); }
        public static int AllCards(PlayerState p) => p.deck.Count + p.hand.Count + p.terrain.Count + p.discard.Count + p.units.Count + p.units.Sum(u => u.equipment.Count) + p.preparedSpells.Count;
    }

    public sealed class BattleEngineTests
    {
        [Test] public void PlaybackCaptureDoesNotChangeRulesRandomnessOrSavedBattle()
        {
            var a = ClassicFixtures.Battle(109);
            ClassicFixtures.Unit(a, 0, "임프", 2); ClassicFixtures.Unit(a, 1, "크라켄", 4);
            var b = new BattleEngine(a.State.Copy());
            a.BeginPlaybackCapture(); ClassicFixtures.Resolve(a); ClassicFixtures.Resolve(b);
            var frames = a.EndPlaybackCapture();
            Assert.That(frames.Count, Is.GreaterThan(5));
            Assert.That(a.State.randomState, Is.EqualTo(b.State.randomState));
            Assert.That(a.State.events.Select(e => e.sequence + ":" + e.kind + ":" + e.text),
                Is.EqualTo(b.State.events.Select(e => e.sequence + ":" + e.kind + ":" + e.text)));
            Assert.That(a.State.players.Select(p => p.deck.Count), Is.EqualTo(b.State.players.Select(p => p.deck.Count)));
            Assert.That(a.State.players.Select(p => p.units.Count), Is.EqualTo(b.State.players.Select(p => p.units.Count)));
            BattleEngine.Validate(a.State); BattleEngine.Validate(b.State);
            frames[0].players[0].hand.Clear(); frames[0].item.text = "changed copy";
            Assert.That(a.State.players[0].hand.Count, Is.EqualTo(6));
            Assert.That(a.State.events.Any(e => e.text == "changed copy"), Is.False);
            Assert.That(a.EndPlaybackCapture(), Is.Empty);
            Assert.That(b.EndPlaybackCapture(), Is.Empty);
            a.BeginPlaybackCapture(); Assert.Throws<InvalidOperationException>(() => a.BeginPlaybackCapture());
            Assert.That(a.EndPlaybackCapture(), Is.Empty);
        }

        [Test] public void PlaybackSeparatesActualFirstStrikeCounterAndRemovalWithExactLanes()
        {
            var e = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0 });
            var attacker = ClassicFixtures.Unit(e, 0, "임프", 2);
            var defender = ClassicFixtures.Unit(e, 1, "크라켄", 4);
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var strike = frames.Single(f => f.item.kind == "attack");
            var counter = frames.Single(f => f.item.kind == "counter");
            var defeat = frames.Single(f => f.item.kind == "defeat" && f.item.sourceInstanceId == attacker.instanceId);
            Assert.That(new[] { strike.item.sourcePlayer, strike.item.sourceLane, strike.item.targetPlayer, strike.item.targetLane }, Is.EqualTo(new[] { 0, 2, 1, 4 }));
            Assert.That(strike.item.amount, Is.EqualTo(3)); Assert.That(strike.item.targetHealth, Is.EqualTo(5));
            Assert.That(CardCatalog.UnitHealth(strike.players[0].AtLane(2)), Is.EqualTo(2));
            Assert.That(counter.item.sourceInstanceId, Is.EqualTo(defender.instanceId));
            Assert.That(counter.item.targetHealth, Is.Zero);
            Assert.That(counter.players[0].AtLane(2), Is.Not.Null);
            Assert.That(defeat.players[0].AtLane(2), Is.Null);
            Assert.That(strike.item.sequence, Is.LessThan(counter.item.sequence));
            Assert.That(counter.item.sequence, Is.LessThan(defeat.item.sequence));
        }

        [Test] public void PlaybackBurnsActualCardsOneAtATimeAndRevealsMagicBeforeItsEffect()
        {
            var e = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0 });
            var unit = ClassicFixtures.Unit(e, 0, "데몬", 3); int before = e.State.players[1].deck.Count;
            e.BeginPlaybackCapture(); ClassicFixtures.Resolve(e); var frames = e.EndPlaybackCapture();
            var burns = frames.Where(f => f.item.kind == "burn" && f.item.sourceInstanceId == unit.instanceId).ToArray();
            Assert.That(burns.Length, Is.EqualTo(5));
            for (int i = 0; i < burns.Length; i++)
            {
                Assert.That(burns[i].players[1].deck.Count, Is.EqualTo(before - i - 1));
                Assert.That(burns[i].players[1].discard.Any(c => c.instanceId == burns[i].item.targetInstanceId), Is.True);
                Assert.That(burns[i].item.targetPlayer, Is.EqualTo(1));
            }
            var magic = ClassicFixtures.Battle(rules: new RuleSet { baseCoins = 0 }); ClassicFixtures.Souls(magic, 0);
            var target = ClassicFixtures.Unit(magic, 1, "파이어버그", 0);
            var spell = ClassicFixtures.Hand(magic, 0, "파이어볼");
            magic.BeginPlaybackCapture(); Assert.True(magic.Play(0, spell.instanceId, -1, out _)); ClassicFixtures.Resolve(magic);
            var spellFrames = magic.EndPlaybackCapture(); var reveal = spellFrames.Single(f => f.item.kind == "spell");
            var hit = spellFrames.Single(f => f.item.kind == "spell-target" && f.item.targetInstanceId == target.instanceId);
            Assert.That(reveal.item.sourceInstanceId, Is.EqualTo(spell.instanceId));
            Assert.That(CardCatalog.UnitHealth(reveal.players[1].AtLane(0)), Is.EqualTo(1));
            Assert.That(hit.item.sourceDefinitionId, Is.EqualTo(spell.definitionId));
            Assert.That(hit.item.targetPlayer, Is.EqualTo(1)); Assert.That(hit.item.targetLane, Is.Zero);
            Assert.That(hit.item.amount, Is.EqualTo(-2)); Assert.That(hit.item.targetHealth, Is.Zero);
            Assert.That(reveal.item.sequence, Is.LessThan(hit.item.sequence));
        }

        [Test] public void OriginalRankIsSeparateFromRarityAndLegacyEventMetadataIsOptional()
        {
            var card = CardCatalog.Get(ClassicFixtures.Id("크라켄"));
            Assert.That(card.rarity, Is.EqualTo(CardRarity.Common)); Assert.That(card.rank, Is.EqualTo(CardRank.N));
            Assert.That(Enum.GetValues(typeof(CardRarity)).Length, Is.EqualTo(12));
            Assert.That(Enum.GetValues(typeof(CardRank)).Cast<CardRank>().Select(CardCatalog.RankName), Is.EqualTo(new[] { "N", "C", "B", "A", "S" }));
            var e = ClassicFixtures.Battle();
            foreach (var item in e.State.events) { item.hasPresentationData = false; item.sourcePlayer = 0; item.sourceLane = 0; item.sourceInstanceId = 0; }
            BattleEngine.Validate(e.State);
            var old = new BattleEngine(e.State); Assert.That(old.State.schemaVersion, Is.EqualTo(3));
        }

        [Test] public void MagicReservationSlotIsIndependentFromItsEffectTargetAndLegacyPlayStillWorks()
        {
            var e = ClassicFixtures.Battle(); ClassicFixtures.Souls(e, 0);
            var spell = ClassicFixtures.Hand(e, 0, "파이어볼");
            Assert.True(e.PlayInSlot(0, spell.instanceId, 4, -1, out var error), error);
            Assert.That(e.State.players[0].SpellAtLane(4).instanceId, Is.EqualTo(spell.instanceId));
            Assert.That(e.State.players[0].SpellAtLane(0), Is.Null);
            Assert.That(e.State.events.Last().sourceLane, Is.EqualTo(4));
            var other = ClassicFixtures.Hand(e, 0, "홀리레인"); uint random = e.State.randomState;
            Assert.False(e.PlayInSlot(0, other.instanceId, 4, -1, out _));
            Assert.That(e.State.randomState, Is.EqualTo(random));
            Assert.That(e.State.players[0].hand.Contains(other), Is.True);
            Assert.True(e.Play(0, other.instanceId, -1, out error), error);
            Assert.That(e.State.players[0].SpellAtLane(0).instanceId, Is.EqualTo(other.instanceId));
            BattleEngine.Validate(e.State);
        }

        [Test] public void HigherGradeOriginalsKeepActualTwoCopyLimitsAndHaveAPlayableFifthDeck()
        {
            var extra = CardCatalog.All.Skip(161).Take(17).ToArray();
            Assert.That(extra.Length, Is.EqualTo(17));
            Assert.That(extra.All(c => c.deckLimit == 2), Is.True);
            Assert.That(extra.Count(c => c.rarity == CardRarity.Upgrade), Is.EqualTo(3));
            Assert.That(extra.Count(c => c.rarity == CardRarity.Unique), Is.EqualTo(1));
            Assert.That(extra.Count(c => c.rarity == CardRarity.Event), Is.EqualTo(13));
            Assert.That(CardCatalog.All.Select(c => c.rarity).Distinct().Count(), Is.EqualTo(10));
            Assert.That(DemoCatalog.StarterDecks.Take(5).Select(d => d.id), Is.EqualTo(new[] { "forest", "knights", "tides", "stars", "fidelity" }));
            var deck = DemoCatalog.GetDeck("fidelity"); DemoCatalog.ValidatePlayableDeck(deck.cards);
            Assert.That(deck.cards.Count(id => CardCatalog.Get(id).rarity == CardRarity.Upgrade), Is.EqualTo(4));
            Assert.That(deck.cards.Count(id => CardCatalog.Get(id).rarity == CardRarity.Unique), Is.EqualTo(2));
            Assert.That(deck.cards.Count(id => CardCatalog.Get(id).rarity == CardRarity.Event), Is.EqualTo(8));
            var battle = DemoCatalog.CreateBattle(109, "fidelity", "noa"); BattleEngine.Validate(battle.State);
        }

        [Test] public void UpgradeHandDiscardProducesOnlyNeutralSoulAndConservesCardsWithoutRng()
        {
            var e = ClassicFixtures.Battle(); var p = e.State.players[0];
            var card = ClassicFixtures.Hand(e, 0, "다크핸드"); uint random = e.State.randomState;
            e.BeginPlaybackCapture(); Assert.True(e.DiscardHand(0, card.instanceId, out var error), error); var frames = e.EndPlaybackCapture();
            Assert.That(p.hand.Contains(card), Is.False); Assert.That(p.discard.Contains(card), Is.True);
            Assert.That(p.genericSoul, Is.EqualTo(1));
            Assert.That(Enum.GetValues(typeof(Element)).Cast<Element>().All(el => p.Resource(el) == 0), Is.True);
            Assert.That(e.State.randomState, Is.EqualTo(random)); Assert.That(ClassicFixtures.AllCards(p), Is.EqualTo(50));
            Assert.That(frames.Select(f => f.item.kind), Is.EqualTo(new[] { "discard", "resource" }));
            Assert.That(frames[1].item.amount, Is.EqualTo(1));
            Assert.That(p.playedThisPreparation, Is.False); // Discard is not a card deployment.
            Assert.True(e.CanShuffleHand(0, out _));
            var normal = ClassicFixtures.Hand(e, 0, "좀비"); Assert.True(e.DiscardHand(0, normal.instanceId, out _));
            Assert.That(p.genericSoul, Is.EqualTo(1));
            Assert.False(e.DiscardHand(1, normal.instanceId, out _)); Assert.False(e.DiscardHand(0, normal.instanceId, out _));
            BattleEngine.Validate(e.State);
        }

        [Test] public void KingpinWaivesBelowLevelEquipmentSoulButPreservesAttachmentLimit()
        {
            var e = ClassicFixtures.Battle(); var p = e.State.players[0];
            var kingpin = ClassicFixtures.Unit(e, 0, "킹핀사이클롭", 2);
            var onyx = ClassicFixtures.Hand(e, 0, "오닉스");
            Assert.True(e.IsFreeEquipment(0, onyx.instanceId, 2));
            Assert.True(e.Play(0, onyx.instanceId, 2, out var error), error);
            Assert.That(p.genericSoul, Is.Zero);
            Assert.That(Enum.GetValues(typeof(Element)).Cast<Element>().All(el => p.Resource(el) == 0), Is.True);
            var second = ClassicFixtures.Hand(e, 0, "거인의곤봉"); Assert.True(e.Play(0, second.instanceId, 2, out error), error);
            var third = ClassicFixtures.Hand(e, 0, "거인의팔찌"); Assert.False(e.Play(0, third.instanceId, 2, out _));
            Assert.That(kingpin.equipment.Count, Is.EqualTo(2));
            var normal = ClassicFixtures.Unit(e, 0, "드워프", 3); Assert.False(e.IsFreeEquipment(0, third.instanceId, 3));
            Assert.False(e.Play(0, third.instanceId, 3, out _)); Assert.That(normal.equipment, Is.Empty);
            BattleEngine.Validate(e.State);
        }

        [Test] public void PreparedEnemyMagicHidesItsNameAndLevelUntilResolution()
        {
            var e=ClassicFixtures.Battle();ClassicFixtures.Souls(e,1);
            var spell=ClassicFixtures.Hand(e,1,"죽음의물결");
            Assert.True(e.CompletePreparation(0,out _));
            Assert.True(e.Play(1,spell.instanceId,-1,out var error),error);
            var announcement=e.State.events.Last();
            Assert.That(announcement.text,Does.Not.Contain("죽음의물결"));
            Assert.That(announcement.amount,Is.Zero);
            Assert.True(e.CompletePreparation(1,out error),error);
            Assert.True(e.State.events.Any(ev=>ev.kind=="spell"&&ev.text.Contains("죽음의물결 공개")));
        }
        [Test] public void OriginalCatalogHasExactKindsSevenElementsAndSeparateSoulCosts()
        {
            Assert.That(CardCatalog.All.Length, Is.EqualTo(605));
            Assert.That(CardCatalog.All.GroupBy(c => c.kind).ToDictionary(g => g.Key, g => g.Count()), Is.EquivalentTo(new Dictionary<CardKind,int> { {CardKind.Terrain,29},{CardKind.Unit,453},{CardKind.Spell,26},{CardKind.Equipment,97} }));
            foreach (Element element in Enum.GetValues(typeof(Element)))
            { Assert.That(CardCatalog.All.Count(c => c.element == element && c.kind == CardKind.Unit), Is.GreaterThanOrEqualTo(50)); Assert.That(CardCatalog.All.Count(c => c.element == element && c.kind == CardKind.Terrain), Is.EqualTo(element == Element.Forest ? 5 : 4)); }
            foreach (var card in CardCatalog.All) { CardCatalog.ValidateDefinition(card); Assert.That(card.id, Is.EqualTo("fm_" + card.sourceId)); }
            var deathKnight = CardCatalog.Get(ClassicFixtures.Id("듀라한"));
            Assert.That(new[] { deathKnight.level,deathKnight.coloredCost,deathKnight.genericCost,deathKnight.cost }, Is.EqualTo(new[] {4,2,2,4}));
            Assert.That(CardCatalog.All.Where(c=>c.kind==CardKind.Unit).Select(c=>c.level).Distinct(), Is.EquivalentTo(Enumerable.Range(1,8)));
        }
        [Test] public void NewOriginalEquipmentKeepsSignedStatsAndBeastRequirement()
        {
            var e=ClassicFixtures.Battle();ClassicFixtures.Souls(e,0);
            var gear=ClassicFixtures.Unit(e,0,"농민",0);var beast=ClassicFixtures.Unit(e,0,"레드스콜피온",1);
            int defense=CardCatalog.UnitDefense(beast),health=CardCatalog.MaximumHealth(beast);
            var item=ClassicFixtures.Hand(e,0,"박쥐날개");
            Assert.False(e.Play(0,item.instanceId,0,out _));
            Assert.True(e.Play(0,item.instanceId,1,out var error),error);
            Assert.That(CardCatalog.UnitDefense(beast),Is.EqualTo(Math.Max(0,defense-1)));
            Assert.That(CardCatalog.MaximumHealth(beast),Is.EqualTo(health+3));
            Assert.That(gear.equipment,Is.Empty);BattleEngine.Validate(e.State);
        }
        [TestCase("스네이크스펠", "E0:Health:-2;A0:Health:2")]
        [TestCase("파이어월", "A2:Defense:1;E2:Health:-2")]
        [TestCase("함정", "E0:Health:-1;E2:Health:-1;E4:Health:-1;E0:Health:-2")]
        [TestCase("파이어실드", "E0:Health:-2;A0:Defense:1")]
        [TestCase("적장목베기", "A0:Attack:1;A2:Attack:1;A4:Attack:1;A0:Defense:1;A2:Defense:1;A4:Defense:1;A0:Health:1;A2:Health:1;A4:Health:1")]
        [TestCase("승리의함성", "E0:Attack:-1;E2:Attack:-1;E4:Attack:-1;A0:Attack:1;A2:Attack:1;A4:Attack:1;A0:Health:1;A2:Health:1;A4:Health:1")]
        [TestCase("프로즌실드", "A0:Defense:2;E0:Attack:-1")]
        [TestCase("황소힘겨루기", "A0:Attack:1;A0:Health:1")]
        [TestCase("도깨비불", "E0:Health:-4")]
        [TestCase("개미지옥", "E0:Attack:-1;E2:Attack:-1;E4:Attack:-1;E0:Health:-2;E2:Health:-2;E4:Health:-2")]
        public void ArcanaSpellsResolvePrintedSidesFixedSlotsAndCapabilityFilters(string name,string expected)
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});
            var definition=CardCatalog.Get(ClassicFixtures.Id(name));
            int actor=definition.playWindow==PlayWindow.DefenseOnly?1:0;
            for(int player=0;player<2;player++)
            {
                ClassicFixtures.Unit(e,player,"크라켄",0); // beast, fixed slot1
                ClassicFixtures.Unit(e,player,"농민",2); // gear, fixed slot3
                ClassicFixtures.Unit(e,player,"타락천사",4); // magic, fixed slot5
            }
            ClassicFixtures.Souls(e,actor);
            if(actor==1)Assert.True(e.CompletePreparation(0,out _));
            var spell=ClassicFixtures.Hand(e,actor,name);
            // Slot2 is empty: reserve there without changing the original
            // numbered targets or renumbering the sparse occupied unit slots.
            Assert.True(e.PlayInSlot(actor,spell.instanceId,1,-1,out var error),error);
            if(actor==0)Assert.True(e.CompletePreparation(0,out error),error);
            Assert.True(e.CompletePreparation(1,out error),error);
            var changes=e.State.events.Where(ev=>ev.kind=="spell-target"&&ev.sourceDefinitionId==definition.id)
                .Select(ev=>(ev.targetPlayer==actor?"A":"E")+ev.targetLane+":"+ev.stat+":"+ev.amount);
            Assert.That(string.Join(";",changes),Is.EqualTo(expected));
            Assert.That(e.State.players[actor].preparedSpells,Is.Empty);
            Assert.That(e.State.players[actor].discard.Any(c=>c.instanceId==spell.instanceId),Is.True);
            BattleEngine.Validate(e.State);
        }
        [Test] public void NewOriginalGearSpellFiltersCapabilitiesAndResetsAfterBattle()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});ClassicFixtures.Souls(e,0);
            var gear=ClassicFixtures.Unit(e,0,"농민",0);var beast=ClassicFixtures.Unit(e,0,"레드스콜피온",1);
            int gearAttack=CardCatalog.UnitAttack(gear),beastAttack=CardCatalog.UnitAttack(beast);
            var spell=ClassicFixtures.Hand(e,0,"거인의힘");
            Assert.True(e.Play(0,spell.instanceId,-1,out var error),error);ClassicFixtures.Resolve(e);
            var powers=e.State.events.Where(ev=>ev.kind=="power").Select(ev=>ev.text).ToArray();
            Assert.That(powers.Any(text=>text.StartsWith("농민")&&text.Contains("AP "+(gearAttack+2))),Is.True);
            Assert.That(powers.Any(text=>text.StartsWith("레드스콜피온")&&text.Contains("AP "+beastAttack)),Is.True);
            Assert.That(CardCatalog.UnitAttack(gear),Is.EqualTo(gearAttack));
            Assert.That(CardCatalog.UnitAttack(beast),Is.EqualTo(beastAttack));BattleEngine.Validate(e.State);
        }
        [Test] public void NewOriginalSetCoinUsesFourSharedTossesAndRestoresDeterministically()
        {
            var a=ClassicFixtures.Battle(103);ClassicFixtures.Unit(a,0,"모르토카토템",0);
            var card=CardCatalog.Get(ClassicFixtures.Id("모르토카토템"));
            Assert.That(card.attackCoin.operation,Is.EqualTo(CoinOperation.Set));
            Assert.That(new[]{card.attack,card.attackCoin.count,card.attackCoin.multiplier},Is.EqualTo(new[]{0,4,3}));
            uint expected=a.State.randomState;int heads=0;
            for(int i=0;i<4;i++)heads+=StableRandom.Range(ref expected,2);
            var b=new BattleEngine(a.State.Copy());ClassicFixtures.Resolve(a);ClassicFixtures.Resolve(b);
            Assert.That(a.State.randomState,Is.EqualTo(expected));
            Assert.That(a.State.events.Count(ev=>ev.kind=="coin"),Is.EqualTo(4));
            Assert.That(a.State.events.Single(ev=>ev.kind=="power").text,Does.Contain("AP "+(3*heads)));
            Assert.That(b.State.events.Select(ev=>ev.sequence+":"+ev.kind+":"+ev.text),Is.EqualTo(a.State.events.Select(ev=>ev.sequence+":"+ev.kind+":"+ev.text)));
        }
        [TestCase("forest","967e29107e2cbdff9f32a8bc24a9ebd85e861dc165124922440e42fa98fa2a47")]
        [TestCase("knights","78d36dc32881c1c4f78064fccb2edf4b75a5be363523ac1ac200ce672ff6dd8b")]
        [TestCase("tides","2357372f3b4bc02016b23f0b6f232520c1ea06ee79c620a67e32f962f124ff67")]
        [TestCase("stars","6b254e330dd56dd78eac38711a5ef18d99f99712a4bba8cab3bf9ab2e6f42029")]
        public void CatalogExpansionPreservesEveryPresetCardAndItsOrder(string deckId,string expected)
        {
            using(var sha=System.Security.Cryptography.SHA256.Create())
            {
                var cards=DemoCatalog.GetDeck(deckId).cards;
                DemoCatalog.ValidatePlayableDeck(cards);
                string actual=BitConverter.ToString(sha.ComputeHash(System.Text.Encoding.UTF8.GetBytes(string.Join(",",cards)))).Replace("-","").ToLowerInvariant();
                Assert.That(actual,Is.EqualTo(expected));
            }
            Assert.That(CardCatalog.Version,Is.EqualTo("fm-classic-0.1.0"));
        }
        [Test] public void RoundStartsWithSixRealRandomCardsBothLpAndNoLeaderSkill()
        {
            var e = new BattleEngine(7);
            Assert.That(e.State.players.Select(p=>p.hand.Count), Is.EqualTo(new[]{6,6}));
            Assert.That(e.State.players.Select(p=>p.deck.Count), Is.EqualTo(new[]{44,44}));
            Assert.That(e.State.players.Select(p=>p.levelPoints), Is.EqualTo(new[]{1,1}));
            Assert.That(e.PreparingPlayer, Is.Zero); Assert.False(e.UseLeaderSkill(0,out _));
            Assert.False(e.CompletePreparation(0,out _)); Assert.True(e.ConfirmOpeningHand(0,out _));
            Assert.True(e.CompletePreparation(0,out _)); Assert.That(e.PreparingPlayer, Is.EqualTo(1));
            Assert.That(e.State.phase, Is.EqualTo(BattlePhase.DefensePreparation));
            BattleEngine.Validate(e.State);
        }
        [Test] public void LandPaysLpAndProducesColoredAndTotalSoulsImmediately()
        {
            var e = ClassicFixtures.Battle(); var p=e.State.players[0];
            var land=ClassicFixtures.Hand(e,0,"묘지");
            Assert.True(e.Play(0,land.instanceId,-1,out var error),error);
            Assert.That(p.levelPoints, Is.Zero); Assert.That(p.darkness,Is.EqualTo(1)); Assert.That(p.genericSoul,Is.EqualTo(1));
            var other=ClassicFixtures.Hand(e,0,"요정의숲");
            Assert.False(e.Play(0,other.instanceId,-1,out _)); Assert.That(p.hand.Contains(other),Is.True);
            Assert.False(e.CanShuffleHand(0,out _));
        }
        [Test] public void SummoningConsumesColoredRequirementAndWholeLevelOfTotalSoul()
        {
            var e=ClassicFixtures.Battle();var p=e.State.players[0];ClassicFixtures.Souls(e,0,10);
            var card=ClassicFixtures.Hand(e,0,"듀라한"); Assert.True(e.Play(0,card.instanceId,0,out var error),error);
            Assert.That(p.darkness,Is.EqualTo(8)); Assert.That(p.genericSoul,Is.EqualTo(6));
            Assert.False(p.AtLane(0).summoningSick);
        }
        [Test] public void InvalidPaymentAndWrongPreparationHaveNoSideEffects()
        {
            var e=ClassicFixtures.Battle();var card=ClassicFixtures.Hand(e,0,"듀라한");
            var p=e.State.players[0];p.darkness=2;p.genericSoul=3;
            uint random=e.State.randomState;int events=e.State.events.Count;
            Assert.False(e.Play(0,card.instanceId,0,out _)); Assert.False(e.Play(1,card.instanceId,0,out _));
            Assert.That(p.darkness,Is.EqualTo(2));Assert.That(p.genericSoul,Is.EqualTo(3));Assert.That(p.hand.Contains(card),Is.True);
            Assert.That(e.State.randomState,Is.EqualTo(random));Assert.That(e.State.events.Count,Is.EqualTo(events));
        }
        [Test] public void WholeHandShuffleWorksOnceBeforeAnyPlayInEveryPreparation()
        {
            var e=new BattleEngine(91);var p=e.State.players[0];uint rng=e.State.randomState;
            Assert.True(e.MulliganWholeHand(0,out _));Assert.True(e.State.openingHandOpen);Assert.That(p.lastShuffleTurn,Is.EqualTo(1));
            Assert.False(e.MulliganWholeHand(0,out _));Assert.That(e.State.randomState,Is.Not.EqualTo(rng));
            Assert.True(e.ConfirmOpeningHand(0,out _));ClassicFixtures.Resolve(e);
            Assert.That(e.PreparingPlayer,Is.EqualTo(1));Assert.True(e.CompletePreparation(1,out _));
            Assert.That(e.PreparingPlayer,Is.Zero);Assert.True(e.ShuffleHand(0,out _));Assert.False(e.ShuffleHand(0,out _));
            Assert.That(p.hand.Count,Is.EqualTo(6));Assert.That(ClassicFixtures.AllCards(p),Is.EqualTo(50));BattleEngine.Validate(e.State);
        }
        [Test] public void PartialMulliganIsRejectedAndKeepingHandDoesNotConsumeRandomness()
        {
            var e=new BattleEngine(11);uint rng=e.State.randomState;
            Assert.False(e.Mulligan(0,new[]{e.State.players[0].hand[0].instanceId},out _));
            Assert.True(e.ConfirmOpeningHand(0,out _));Assert.That(e.State.randomState,Is.EqualTo(rng));
        }
        [Test] public void DirectAttackBurnsLevelInsteadOfApAndNewlyDeployedUnitsFight()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});ClassicFixtures.Souls(e,0);
            var c=ClassicFixtures.Hand(e,0,"데몬");Assert.True(e.Play(0,c.instanceId,0,out _));
            int before=e.State.players[1].deck.Count;ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].deck.Count,Is.EqualTo(before-5));
            Assert.That(e.State.events.Any(x=>x.kind=="damage" && x.text.Contains("직접 공격 · 레벨 5")),Is.True);
            BattleEngine.Validate(e.State);
        }
        [Test] public void FirstStrikeKillPreventsRetaliationAndSurvivorsResetEachRound()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});
            var a=ClassicFixtures.Unit(e,0,"데몬",0);ClassicFixtures.Unit(e,1,"파이어버그",0);
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].units,Is.Empty);Assert.That(e.State.players[0].units.Single(),Is.SameAs(a));
            Assert.That(a.damage,Is.Zero);Assert.False(a.coinsRolled);Assert.That(e.State.events.Any(x=>x.kind=="damage" && x.text.Contains("반격 없음")),Is.True);
        }
        [Test] public void SurvivingDefenderBlocksMultipleAttackersInLaneOrder()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});
            ClassicFixtures.Unit(e,0,"파이어버그",3);ClassicFixtures.Unit(e,0,"임프",1);ClassicFixtures.Unit(e,1,"골드드래곤",4);
            int deck=e.State.players[1].deck.Count;ClassicFixtures.Resolve(e);
            var hits=e.State.events.Where(x=>x.kind=="damage"&&x.text.Contains("→")).Select(x=>x.text).ToArray();
            Assert.That(hits.Length,Is.EqualTo(2));Assert.That(hits[0],Does.StartWith("임프"));Assert.That(hits[1],Does.StartWith("파이어버그"));
            Assert.That(hits.All(x=>x.Contains("골드드래곤")),Is.True);Assert.That(e.State.players[1].deck.Count,Is.EqualTo(deck-4)); // Unit's upkeep, never direct damage.
            Assert.That(e.State.events.Any(x=>x.text.Contains("직접 공격")),Is.False);
        }
        [Test] public void CoinsShareOneOrderedSequenceAndSetMultiplyOperatorsArePreserved()
        {
            var e=ClassicFixtures.Battle(103);ClassicFixtures.Unit(e,0,"아이언타워",0);
            uint expected=e.State.randomState;int head=StableRandom.Range(ref expected,2);
            ClassicFixtures.Resolve(e);
            var coins=e.State.events.Where(x=>x.kind=="coin").ToArray();Assert.That(coins.Length,Is.EqualTo(1));Assert.That(coins[0].amount,Is.EqualTo(head));
            Assert.That(e.State.randomState,Is.EqualTo(expected));
            Assert.That(e.State.events.Single(x=>x.kind=="power").text,Does.Contain("DP "+(2*head)));
            Assert.That(CardCatalog.Get(ClassicFixtures.Id("실프")).defenseCoin.operation,Is.EqualTo(CoinOperation.Multiply));
        }
        [Test] public void FireballIsReservedOccupiesFrontAndHitsOnlySpecifiedEnemySlots()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});ClassicFixtures.Souls(e,0);
            ClassicFixtures.Unit(e,1,"파이어버그",0);var rear=ClassicFixtures.Unit(e,1,"임프",4);
            var spell=ClassicFixtures.Hand(e,0,"파이어볼");Assert.True(e.Play(0,spell.instanceId,-1,out _));
            Assert.That(e.State.players[1].units.Count,Is.EqualTo(2));Assert.That(e.State.players[0].SpellAtLane(0),Is.Not.Null);
            var unit=ClassicFixtures.Hand(e,0,"농민");Assert.False(e.Play(0,unit.instanceId,0,out _));
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].units.Single(),Is.SameAs(rear));Assert.That(e.State.players[0].preparedSpells,Is.Empty);
            Assert.That(e.State.players[0].discard.Any(c=>c.instanceId==spell.instanceId),Is.True);BattleEngine.Validate(e.State);
        }
        [Test] public void PhysicalSpellHonorsDpWhereHpReductionDoesNot()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});ClassicFixtures.Souls(e,0);
            ClassicFixtures.Unit(e,1,"파이어버그",0);var protectedUnit=ClassicFixtures.Unit(e,1,"스캘럽",1);
            var spell=ClassicFixtures.Hand(e,0,"낙석");Assert.True(e.Play(0,spell.instanceId,-1,out _));ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].units.Single(),Is.SameAs(protectedUnit));
        }
        [Test] public void AttackDefenseSpellRestrictionsAndLowerLevelOrderAreEnforced()
        {
            var e=ClassicFixtures.Battle();ClassicFixtures.Souls(e,0);ClassicFixtures.Souls(e,1);
            var defense=ClassicFixtures.Hand(e,0,"겨울잠");Assert.False(e.Play(0,defense.instanceId,-1,out _));
            var high=ClassicFixtures.Hand(e,0,"메테오");Assert.True(e.Play(0,high.instanceId,-1,out _));
            Assert.True(e.CompletePreparation(0,out _));
            var attack=ClassicFixtures.Hand(e,1,"파이어볼");Assert.False(e.Play(1,attack.instanceId,-1,out _));
            var low=ClassicFixtures.Hand(e,1,"겨울잠");Assert.True(e.Play(1,low.instanceId,-1,out _));Assert.True(e.CompletePreparation(1,out _));
            Assert.That(e.State.events.Where(x=>x.kind=="spell").Select(x=>x.text.Split(' ')[0]),Is.EqualTo(new[]{"겨울잠","메테오"}));
        }
        [Test] public void EquipmentRequiresMatchingAbilityAndAppliesSignedStats()
        {
            var e=ClassicFixtures.Battle();ClassicFixtures.Souls(e,0);
            var beast=ClassicFixtures.Unit(e,0,"스웜",0);var gear=ClassicFixtures.Unit(e,0,"농민",1);
            var item=ClassicFixtures.Hand(e,0,"아이언실드");
            Assert.False(e.Play(0,item.instanceId,0,out _));Assert.True(e.Play(0,item.instanceId,1,out var error),error);
            Assert.That(CardCatalog.UnitAttack(gear),Is.EqualTo(1));Assert.That(CardCatalog.UnitDefense(gear),Is.EqualTo(3));Assert.That(beast.equipment,Is.Empty);
            BattleEngine.Validate(e.State);
        }
        [Test] public void UnitsPayUpkeepAfterSoulRefreshButItemsDoNot()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});var p=e.State.players[0];
            ClassicFixtures.Unit(e,0,"듀라한",0);var land=ClassicFixtures.Extract(e,0,"묘지");p.terrain.Add(land);
            p.genericSoul=40;int before=p.discard.Count;ClassicFixtures.Resolve(e);
            Assert.That(p.genericSoul,Is.Zero);Assert.That(p.discard.Count,Is.EqualTo(before+1));
            Assert.That(e.State.events.Any(x=>x.kind=="upkeep"&&x.actor==0&&x.amount==2),Is.True);
        }
        [Test] public void SavedDefensePreparationRestoresNextCoinsAndAllCards()
        {
            var a=ClassicFixtures.Battle(77);ClassicFixtures.Unit(a,0,"아이언타워",0);ClassicFixtures.Unit(a,1,"골드드래곤",0);
            Assert.True(a.CompletePreparation(0,out _));var b=new BattleEngine(a.State.Copy());
            Assert.True(a.CompletePreparation(1,out _));Assert.True(b.CompletePreparation(1,out _));
            Assert.That(a.State.randomState,Is.EqualTo(b.State.randomState));
            Assert.That(a.State.events.Select(x=>x.sequence+":"+x.kind+":"+x.text),Is.EqualTo(b.State.events.Select(x=>x.sequence+":"+x.kind+":"+x.text)));
            foreach(var p in b.State.players)Assert.That(ClassicFixtures.AllCards(p),Is.EqualTo(50));BattleEngine.Validate(b.State);
        }
        [Test] public void PreviousVersionAndDuplicateInstancesAreRejectedWithoutMutatingSource()
        {
            var e=ClassicFixtures.Battle();var old=e.State.Copy();old.schemaVersion=2;old.contentVersion="starlight-0.2.0";
            Assert.Throws<ArgumentException>(()=>new BattleEngine(old));Assert.That(old.schemaVersion,Is.EqualTo(2));
            var corrupt=e.State.Copy();corrupt.players[0].deck[0].instanceId=corrupt.players[1].deck[0].instanceId;
            Assert.Throws<ArgumentException>(()=>new BattleEngine(corrupt));BattleEngine.Validate(e.State);
        }
        [Test] public void TurnLimitResultPreservesLastPreparationRecordsAndCanBeSaved()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{maximumTurns=2});
            ClassicFixtures.Resolve(e);
            var land=ClassicFixtures.Hand(e,1,"묘지");Assert.True(e.Play(1,land.instanceId,-1,out _));
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete));
            Assert.That(e.State.turn,Is.EqualTo(2));
            Assert.DoesNotThrow(()=>new BattleEngine(e.State.Copy()));
        }
        [Test] public void LifeModeAndConcessionReachExplicitResults()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0,victoryRule=VictoryRule.Life,startingHealth=4});ClassicFixtures.Unit(e,0,"데몬",0);ClassicFixtures.Resolve(e);
            Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete));Assert.That(e.State.winner,Is.Zero);BattleEngine.Validate(e.State);
            var other=ClassicFixtures.Battle();Assert.True(other.Concede(0));Assert.That(other.State.winner,Is.EqualTo(1));BattleEngine.Validate(other.State);
        }
#if UNITY_EDITOR
        [TestCase("forest")] [TestCase("knights")] [TestCase("tides")] [TestCase("stars")]
        public void UnityJsonPreservesClassicPreparationShuffleAndEntireContinuation(string deckId)
        {
            var a=DemoCatalog.CreateBattle(31,deckId,DemoCatalog.GetDeck(deckId).recommendedLeaderId);
            Assert.True(a.MulliganWholeHand(0,out _));Assert.True(a.ConfirmOpeningHand(0,out _));
            for(int i=0;i<4 && a.State.phase!=BattlePhase.Complete;i++)Assert.True(BattleAi.Step(a,a.PreparingPlayer,out _));
            var json=UnityEngine.JsonUtility.ToJson(a.State);var b=new BattleEngine(UnityEngine.JsonUtility.FromJson<BattleState>(json));
            Assert.That(UnityEngine.JsonUtility.ToJson(b.State),Is.EqualTo(json));
            int actions=0;
            while(a.State.phase!=BattlePhase.Complete && actions++<2000)
            { int actor=a.PreparingPlayer;Assert.True(BattleAi.Step(a,actor,out _));Assert.True(BattleAi.Step(b,actor,out _));BattleEngine.Validate(b.State);Assert.That(UnityEngine.JsonUtility.ToJson(b.State),Is.EqualTo(UnityEngine.JsonUtility.ToJson(a.State))); }
            Assert.That(a.State.phase,Is.EqualTo(BattlePhase.Complete));
        }
#endif
    }
}
