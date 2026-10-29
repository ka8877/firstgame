using System;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Tests;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class RestorationCardsTests
    {
        [Test] public void CompleteOriginalArchiveSeparatesSupportedRulesFromUnsupportedSources()
        {
            Assert.That(CardCatalog.All.Length, Is.EqualTo(2571));
            Assert.That(CardCatalog.All.Count(c=>c.rarity==CardRarity.Unique), Is.EqualTo(23));
            Assert.That(CardCatalog.All.Count(c=>c.rarity==CardRarity.Legend), Is.EqualTo(4));
            Assert.That(CardCatalog.All.Count(c=>c.rarity==CardRarity.Secret), Is.EqualTo(15));
            Assert.That(OriginalCardArchive.All.Length, Is.EqualTo(15350));
            Assert.That(OriginalCardArchive.All.Select(c=>c.id).Distinct().Count(), Is.EqualTo(15350));
            Assert.That(OriginalCardArchive.All.Count(c=>c.rarity==CardRarity.Uncatalogued), Is.EqualTo(23));
            var supported = new System.Collections.Generic.HashSet<string>(CardCatalog.All.Select(c=>c.id));
            foreach(var card in OriginalCardArchive.All)
            {
                Assert.That(card.sourceRules, Is.Not.Empty, card.id);
                Assert.That(card.playable, Is.EqualTo(supported.Contains(card.id)),card.id);
                if (card.playable) Assert.That(card.unsupportedReason, Is.Empty,card.id);
                else { Assert.That(card.unsupportedReason, Is.Not.Empty,card.id); Assert.Throws<ArgumentException>(()=>CardCatalog.Get(card.id)); }
                Assert.That(card.sourceUrl, Does.EndWith(card.id.Substring(3)));
            }
        }
        [Test] public void StrongPoisonAppliesBeforeDamageWithoutExtraRandomnessOrPermanentChanges()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0,defenderStrikesBack=false});
            var a=ClassicFixtures.Unit(e,0,"정글의독침수",0);
            var b=ClassicFixtures.Unit(e,1,"크라켄",2);
            uint random=e.State.randomState;
            e.BeginPlaybackCapture();ClassicFixtures.Resolve(e);var frames=e.EndPlaybackCapture();
            var skill=frames.Single(f=>f.item.kind=="skill"&&f.item.sourceInstanceId==a.instanceId);
            Assert.That(skill.players[1].AtLane(2).rolledAttack,Is.EqualTo(1));
            Assert.That(skill.players[1].AtLane(2).rolledHealth,Is.EqualTo(1));
            Assert.That(skill.item.sequence,Is.LessThan(frames.Single(f=>f.item.kind=="attack").item.sequence));
            Assert.That(e.State.players[1].discard.Any(c=>c.instanceId==b.instanceId),Is.True);
            Assert.That(e.State.randomState,Is.EqualTo(random));
            BattleEngine.Validate(e.State);
        }
        [Test] public void PoisonDoesNotTriggerWhenAttackIsZero()
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});
            var a=ClassicFixtures.Unit(e,0,"정글의독침수",0);a.bonusPower=-CardCatalog.Get(a.definitionId).attack;
            ClassicFixtures.Unit(e,1,"크라켄",2);ClassicFixtures.Resolve(e);
            Assert.That(e.State.events.Any(x=>x.kind=="skill"&&x.sourceInstanceId==a.instanceId),Is.False);
            BattleEngine.Validate(e.State);
        }
        [TestCase("크라켄",true)] [TestCase("레드스콜피온",false)] [TestCase("농민",false)]
        public void BullfighterEvadesOnlyBeastsAtLeastItsLevel(string attackingName,bool shouldEvade)
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0});
            var attacker=ClassicFixtures.Unit(e,0,attackingName,0);
            var bull=ClassicFixtures.Unit(e,1,"투우사",2);
            Assert.That(RestorationCardSkills.EvadesBeast(bull,attacker),Is.EqualTo(shouldEvade));
            ClassicFixtures.Resolve(e);
            var strike=e.State.events.Single(x=>x.kind=="attack"&&x.sourceInstanceId==attacker.instanceId);
            if(shouldEvade)Assert.That(strike.amount,Is.Zero);
            else Assert.That(strike.amount,Is.EqualTo(Math.Max(0,CardCatalog.Get(attacker.definitionId).attack-CardCatalog.Get(bull.definitionId).defense)));
            BattleEngine.Validate(e.State);
        }
        [TestCase(0)] [TestCase(2)]
        public void LandRuinCannotMakeOpponentLpNegativeAndOccursOnce(int lp)
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0,defenderStrikesBack=false});
            ClassicFixtures.Unit(e,0,"데몬",0);var trap=ClassicFixtures.Unit(e,1,"랜드트랩퍼",3);
            e.State.players[0].levelPoints=lp;ClassicFixtures.Resolve(e);
            Assert.That(e.State.events.Count(x=>x.kind=="skill"&&x.sourceInstanceId==trap.instanceId),Is.EqualTo(1));
            Assert.That(e.State.players[0].levelPoints,Is.EqualTo(Math.Max(0,lp-1)+1)); // next round LP +1
            BattleEngine.Validate(e.State);
        }
        static BattleEngine Shockwave(bool enemyMagic)
        {
            var e=ClassicFixtures.Battle(rules:new RuleSet{baseCoins=0,defenderStrikesBack=false});
            ClassicFixtures.Souls(e,0);ClassicFixtures.Souls(e,1);
            // Eight real terrain cards produce eight neutral soul; the two
            // surviving units pay seven upkeep, exposing a real +1 difference.
            foreach(var land in CardCatalog.All.Where(c=>c.kind==CardKind.Terrain&&c.producedGeneric==1).Take(8))
                e.State.players[1].terrain.Add(ClassicFixtures.Extract(e,1,land.name));
            ClassicFixtures.Unit(e,1,"크라켄",0); // HP8 ->2
            ClassicFixtures.Unit(e,1,"농민",1); // HP3 ->0, removed before combat
            ClassicFixtures.Unit(e,1,"타락천사",4); // fixed slot5 unaffected
            var spell=ClassicFixtures.Hand(e,0,"차원의충격파");
            Assert.True(e.PlayInSlot(0,spell.instanceId,2,-1,out var error),error);
            Assert.True(e.CompletePreparation(0,out error),error);
            if(enemyMagic)
            {
                var next=ClassicFixtures.Hand(e,1,"신의은총");
                Assert.True(e.PlayInSlot(1,next.instanceId,3,-1,out error),error);
            }
            return e;
        }
        [TestCase(false)] [TestCase(true)]
        public void SecretQuartersExactFixedSlotsFloorsAtZeroAndCompensatesOnlyWhenEnemyMagicExists(bool enemyMagic)
        {
            var e=Shockwave(enemyMagic);e.BeginPlaybackCapture();
            Assert.True(e.CompletePreparation(1,out var error),error);var frames=e.EndPlaybackCapture();
            var effects=frames.Where(f=>f.item.kind=="spell-target"&&f.item.sourceDefinitionId==RestorationCardSkills.DimensionShockwave).ToArray();
            Assert.That(effects.Select(f=>f.item.targetLane),Is.EqualTo(new[]{0,1}));
            Assert.That(effects[0].players[1].AtLane(0).bonusHealth,Is.EqualTo(-6));
            Assert.That(effects[1].players[1].AtLane(1).bonusHealth,Is.EqualTo(-3));
            Assert.That(effects[1].players[1].AtLane(4).bonusHealth,Is.Zero);
            Assert.That(e.State.events.Any(x=>x.kind=="soul-bonus"),Is.EqualTo(enemyMagic));
            Assert.That(e.State.players[1].genericSoul,Is.EqualTo(enemyMagic?2:1));
            Assert.That(e.State.players[1].nextGenericSoulBonus,Is.Zero);
            ClassicFixtures.Resolve(e);
            Assert.That(e.State.players[1].genericSoul,Is.EqualTo(1),"next-soul-cost compensation must not repeat");
            BattleEngine.Validate(e.State);
        }
        [Test] public void PendingSoulBonusSurvivesJsonRestoreAndOldMissingFieldDefaultsToZero()
        {
            var a=ClassicFixtures.Battle();a.State.players[0].nextGenericSoulBonus=1;
            var b=new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(a.State)));
            Assert.That(b.State.players[0].nextGenericSoulBonus,Is.EqualTo(1));
            ClassicFixtures.Resolve(a);ClassicFixtures.Resolve(b);
            Assert.That(FieldJson.Encode(a.State),Is.EqualTo(FieldJson.Encode(b.State)));
            Assert.That(a.State.players[0].nextGenericSoulBonus,Is.Zero);
            var old=FieldJson.Encode(a.State).Replace("\"nextGenericSoulBonus\":0,","");
            var restored=new BattleEngine(FieldJson.Decode<BattleState>(old));
            Assert.That(restored.State.players.All(p=>p.nextGenericSoulBonus==0),Is.True);
            restored.State.players[0].nextGenericSoulBonus=-1;
            Assert.Throws<ArgumentException>(()=>BattleEngine.Validate(restored.State));
            restored.State.players[0].nextGenericSoulBonus=restored.State.rules.maximumResource+1;
            Assert.Throws<ArgumentException>(()=>BattleEngine.Validate(restored.State));
        }
        [Test] public void OriginalLegendCoinBonusChangesTheActualRollAndEveryPrintedStat()
        {
            var e=ClassicFixtures.Battle();var unit=ClassicFixtures.Unit(e,0,"임프",0);
            unit.equipment.Add(ClassicFixtures.Extract(e,0,"용의보주"));
            e.State.randomState=42;
            e.BeginPlaybackCapture();ClassicFixtures.Resolve(e);var frames=e.EndPlaybackCapture();
            var coins=frames.Where(f=>f.item.kind=="coin"&&f.item.sourceInstanceId==unit.instanceId).ToArray();
            var power=frames.Single(f=>f.item.kind=="power"&&f.item.sourceInstanceId==unit.instanceId).players[0].AtLane(0);
            Assert.That(coins.Length,Is.EqualTo(2),"original C+1 changes Imp AC1 to AC2");
            int first=coins[0].item.coinHeads?1:0,second=coins[1].item.coinHeads?1:0;
            Assert.That(power.rolledAttack,Is.EqualTo(4+first+second),"Imp AP3 + relic AP1 + C2");
            Assert.That(power.rolledDefense,Is.EqualTo(1+first),"relic adds C1 to the former DP without coins");
            Assert.That(power.rolledHealth,Is.EqualTo(3+first),"relic adds C1 to the former HP without coins");
            BattleEngine.Validate(e.State);
        }
        [Test] public void PerStatEquipmentCoinsRetainSetOperatorAndRespectSignedCounts()
        {
            var e=ClassicFixtures.Battle();var unit=ClassicFixtures.Unit(e,0,"레오나이트",0);
            unit.equipment.Add(ClassicFixtures.Extract(e,0,"미스릴방패"));
            e.State.randomState=42;
            e.BeginPlaybackCapture();ClassicFixtures.Resolve(e);var frames=e.EndPlaybackCapture();
            var coins=frames.Where(f=>f.item.kind=="coin"&&f.item.sourceInstanceId==unit.instanceId).ToArray();
            var power=frames.Single(f=>f.item.kind=="power"&&f.item.sourceInstanceId==unit.instanceId).players[0].AtLane(0);
            Assert.That(coins.Length,Is.EqualTo(3),"DC+2 changes the knight's =2C1 into =2C3");
            Assert.That(power.rolledDefense,Is.EqualTo(coins.Count(f=>f.item.coinHeads)*2),"= remains a set operator, not an additive defence bonus");
            Assert.That(OriginalEquipmentModifiers.CoinDelta(unit,OperationStat.Attack),Is.Zero);
            Assert.That(OriginalEquipmentModifiers.CoinCount(new CoinRule(CoinOperation.Add,1),-2),Is.Zero);
            Assert.That(OriginalEquipmentModifiers.CoinCount(new CoinRule(CoinOperation.Add,4),3),Is.EqualTo(5));
            BattleEngine.Validate(e.State);
        }
        [Test] public void EquipmentChanceUsesExactOriginalPercentagePointsAndFiftyPercentKeepsLegacyRng()
        {
            var e=ClassicFixtures.Battle();var unit=ClassicFixtures.Unit(e,0,"임프",0);
            Assert.That(OriginalEquipmentModifiers.ChancePercent(unit),Is.EqualTo(50));
            uint before=e.State.randomState,expected=before;bool legacy=StableRandom.Range(ref expected,2)==1;
            e.BeginPlaybackCapture();ClassicFixtures.Resolve(e);var frames=e.EndPlaybackCapture();
            Assert.That(frames.Single(f=>f.item.kind=="coin"&&f.item.sourceInstanceId==unit.instanceId).item.coinHeads,Is.EqualTo(legacy));
            Assert.That(e.State.randomState,Is.EqualTo(expected),"unchanged 50% unit consumes the exact legacy RNG path");
            var adjusted=ClassicFixtures.Battle();var gear=ClassicFixtures.Unit(adjusted,0,"임프",0);
            gear.equipment.Add(ClassicFixtures.Extract(adjusted,0,"허수아비"));
            Assert.That(OriginalEquipmentModifiers.ChancePercent(gear),Is.EqualTo(55));
            expected=adjusted.State.randomState;bool sourceChance=StableRandom.Range(ref expected,100)<55;
            adjusted.BeginPlaybackCapture();ClassicFixtures.Resolve(adjusted);var changed=adjusted.EndPlaybackCapture();
            Assert.That(changed.Single(f=>f.item.kind=="coin"&&f.item.sourceInstanceId==gear.instanceId).item.coinHeads,Is.EqualTo(sourceChance));
            Assert.That(adjusted.State.randomState,Is.EqualTo(expected));
            BattleEngine.Validate(adjusted.State);
        }
        [Test] public void AlreadyResolvedLowerLevelEnemyMagicDoesNotTriggerSecretCompensation()
        {
            var e=Shockwave(false);var low=ClassicFixtures.Hand(e,1,"겨울잠");
            Assert.True(e.PlayInSlot(1,low.instanceId,3,-1,out var error),error);
            Assert.True(e.CompletePreparation(1,out error),error);
            Assert.That(e.State.events.Any(x=>x.kind=="soul-bonus"),Is.False);
            BattleEngine.Validate(e.State);
        }
        [Test] public void FourScenarioStagesHaveBoundSavedIdentitiesAndCannotBeMistakenForPractice()
        {
            Assert.That(FmScenarioCatalog.Stages.Length,Is.EqualTo(4));
            var first=FmScenarioCatalog.Stages[0];
            Assert.True(FmScenarioCatalog.IsUnlocked(first.id,null));
            Assert.False(FmScenarioCatalog.IsUnlocked(FmScenarioCatalog.Stages[3].id,new[]{FmScenarioCatalog.Stages[2].id}));
            Assert.True(FmScenarioCatalog.IsUnlocked(FmScenarioCatalog.Stages[3].id,FmScenarioCatalog.Stages.Take(3).Select(x=>x.id)));
            foreach(var stage in FmScenarioCatalog.Stages)
            {
                var deck=DemoCatalog.GetDeck(stage.recommendedDeckId);
                var a=FmScenarioCatalog.CreateBattle(stage.id,17,deck.id,deck.recommendedLeaderId);
                var b=new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(a.State)));
                Assert.That(FmScenarioCatalog.StageForBattle(b.State),Is.SameAs(stage));
                int actions=0;
                while(a.State.phase!=BattlePhase.Complete&&actions++<2000)
                {
                    Assert.True(BattleAi.Step(a,a.PreparingPlayer,out var error),error);
                    BattleEngine.Validate(a.State);
                }
                Assert.That(a.State.phase,Is.EqualTo(BattlePhase.Complete));
                Assert.That(actions,Is.LessThan(2000));
            }
            Assert.That(FmScenarioCatalog.StageForBattle(DemoCatalog.CreateBattle(17,"forest","lyria",0).State),Is.Null);
        }
        [TestCase("premium-grove")] [TestCase("premium-relic")]
        public void PremiumDecksUseActualOriginalTopGradesAndCompleteRealSeededBattles(string id)
        {
            var deck=DemoCatalog.GetDeck(id);DemoCatalog.ValidatePlayableDeck(deck.cards);
            Assert.That(deck.cards.Select(CardCatalog.Get).Any(c=>c.rarity==CardRarity.Unique),Is.True);
            if(id=="premium-relic")
            {
                Assert.That(deck.cards,Does.Contain(RestorationCardSkills.DimensionShockwave));
                Assert.That(deck.cards.Select(CardCatalog.Get).Any(c=>c.rarity==CardRarity.Legend),Is.True);
            }
            for(uint seed=1;seed<=3;seed++)
            {
                var e=DemoCatalog.CreateBattle(seed,id,deck.recommendedLeaderId,(int)(seed%3));int actions=0;
                while(e.State.phase!=BattlePhase.Complete&&actions++<2000)
                {Assert.True(BattleAi.Step(e,e.PreparingPlayer,out var error),error);BattleEngine.Validate(e.State);}
                Assert.That(e.State.phase,Is.EqualTo(BattlePhase.Complete));
            }
        }
    }
}
