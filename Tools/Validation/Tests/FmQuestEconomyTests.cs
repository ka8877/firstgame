using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;

namespace FirstGame.Validation
{
    public sealed class FmQuestEconomyTests
    {
        static FmQuestEconomy Profile(uint seed=42)
        {var q=FmQuestEconomy.New(seed);Assert.True(q.TrySelectStarter("mono-forest",out var error),error);return q;}
        static void FinishLegally(BattleEngine battle)
        {
            int actions=0;
            while(battle.State.phase!=BattlePhase.Complete&&actions++<2000)
                Assert.True(BattleAi.Step(battle,battle.PreparingPlayer,out var reason),reason);
            Assert.That(battle.State.phase,Is.EqualTo(BattlePhase.Complete));
        }
        sealed class WonRoute {public FmQuestProfile profile;public BattleState battle;public string ticket;}
        static readonly Lazy<WonRoute> wonRoute=new Lazy<WonRoute>(()=>
        {
            // Standard fifty-card owned decks and actual public AI actions only;
            // no fixture resources, forged result, concede-enemy or short rules.
            for(uint seed=1;seed<=64;seed++)
            {
                var q=Profile(seed);
                Assert.True(q.TryBeginBattle("mono-forest",3,"roots",null,out var e,out var ticket,out var error),error);
                FinishLegally(e);
                if(e.State.winner==0)
                {
                    TestContext.Progress.WriteLine("FM_QUEST_NATURAL_WIN seed="+seed+" turn="+e.State.turn+" actualActionsOnly=true");
                    return new WonRoute{profile=q.CaptureSave(),battle=e.State.Copy(),ticket=ticket};
                }
            }
            throw new InvalidOperationException("No natural owned-deck victory in the deterministic bounded seed corpus.");
        });
        static WonRoute WinCopy()=>new WonRoute{profile=FieldJson.Decode<FmQuestProfile>(FieldJson.Encode(wonRoute.Value.profile)),
            battle=wonRoute.Value.battle.Copy(),ticket=wonRoute.Value.ticket};

        [Test] public void FirstChoiceGrantsExactlyOneFiftyCardDeckAndOneWelcomeWallet()
        {
            foreach(var starter in FmQuestCatalog.StarterChoices)
            {
                var q=FmQuestEconomy.New(5);Assert.That(q.Sp,Is.Zero);Assert.That(q.Gp,Is.Zero);
                Assert.That(q.OwnedCards,Is.Empty);Assert.False(q.TryBuyPack("standard",out _,out _));
                Assert.True(q.TrySelectStarter(starter.id,out var error),error);
                Assert.That(q.OwnedCards.Sum(c=>c.count),Is.EqualTo(50));Assert.That(q.Sp,Is.EqualTo(600));
                Assert.That(q.OwnedCards.SelectMany(c=>Enumerable.Repeat(c.cardId,c.count)).OrderBy(x=>x),Is.EqualTo(starter.cards.OrderBy(x=>x)));
                string before=FieldJson.Encode(q.CaptureSave());Assert.False(q.TrySelectStarter("mono-light",out _));
                Assert.That(FieldJson.Encode(q.CaptureSave()),Is.EqualTo(before));FmQuestEconomy.Validate(q.CaptureSave());
            }
        }
        [Test] public void OwnershipAndOriginalDeckLimitsAreBothRequiredForCollectionBattles()
        {
            var q=Profile();Assert.True(q.ValidateOwnedDeck(DemoCatalog.GetDeck("mono-forest").cards,out _));
            Assert.False(q.ValidateOwnedDeck(DemoCatalog.GetDeck("mono-fire").cards,out _));
            var illegal=DemoCatalog.GetDeck("mono-forest").CopyCards();for(int i=0;i<5;i++)illegal[i]=illegal[16];
            Assert.False(q.ValidateOwnedDeck(illegal,out _));
            string before=FieldJson.Encode(q.CaptureSave());
            Assert.False(q.TryBeginBattle("mono-fire",0,"",null,out _,out _,out _));
            Assert.False(q.TryBeginBattle("mono-forest",4,"tides",null,out _,out _,out _));
            Assert.That(FieldJson.Encode(q.CaptureSave()),Is.EqualTo(before));
        }
        [Test] public void PurchasesDebitOnceAndUnopenedResultsPersistAcrossJsonReload()
        {
            var q=Profile(812);Assert.True(q.TryBuyPack("standard",out var purchase,out _));
            Assert.That(q.Sp,Is.EqualTo(200));Assert.That(q.OwnedCards.Sum(c=>c.count),Is.EqualTo(50));
            var restored=FmQuestEconomy.FromSave(FieldJson.Decode<FmQuestProfile>(FieldJson.Encode(q.CaptureSave())));
            Assert.That(restored.PackPurchases.Single().cardIds,Is.EqualTo(purchase.cardIds));
            Assert.True(restored.TryOpenPack(purchase.id,out var result,out _));Assert.That(result.cardIds,Is.EqualTo(purchase.cardIds));
            Assert.That(restored.OwnedCards.Sum(c=>c.count),Is.EqualTo(55));
            string before=FieldJson.Encode(restored.CaptureSave());Assert.False(restored.TryOpenPack(purchase.id,out _,out _));
            Assert.False(restored.TryBuyPack("premium",out _,out _));
            Assert.That(FieldJson.Encode(restored.CaptureSave()),Is.EqualTo(before));FmQuestEconomy.Validate(restored.CaptureSave());
        }
        [Test] public void PackAndBattleRandomStreamsDoNotInfluenceEachOther()
        {
            var a=Profile(900);var b=Profile(900);
            Assert.True(a.TryBeginBattle("mono-forest",0,"",null,out var ea,out _,out _));
            string initial=FieldJson.Encode(ea.State);
            Assert.True(a.TryBuyPack("standard",out var pa,out _));
            Assert.That(FieldJson.Encode(ea.State),Is.EqualTo(initial));
            Assert.True(b.TryBuyPack("standard",out var pb,out _));
            Assert.True(b.TryBeginBattle("mono-forest",0,"",null,out var eb,out _,out _));
            Assert.That(pa.cardIds,Is.EqualTo(pb.cardIds));Assert.That(FieldJson.Encode(eb.State),Is.EqualTo(initial));
        }
        [Test] public void EveryOfferHasAnActualSupportedPoolAndTruthfulNormalizedGradeOdds()
        {
            foreach(var offer in FmQuestCatalog.Offers.Where(o=>o.deckId.Length==0))
            {
                foreach(bool guarantee in new[]{false,true})
                {
                    var odds=FmQuestCatalog.GradeOdds(offer.id,guarantee);
                    Assert.That(odds.Values.Sum(),Is.EqualTo(1m).Within(0.000000000000000000001m));
                    Assert.That(odds.Values.All(x=>x>0m),Is.True);
                }
                uint random=18; // Pure catalogue draw, not a wallet bypass.
                for(int k=0;k<8;k++)
                {
                    var cards=FmQuestCatalog.Roll(offer,ref random);Assert.That(cards.Count,Is.EqualTo(5));
                    Assert.That(cards.All(CardCatalog.Contains),Is.True);
                    if(offer.element>=0)Assert.That(cards.All(id=>(int)CardCatalog.Get(id).element==offer.element),Is.True);
                    if(!offer.dailyOneChoice)
                    {
                        var last=CardCatalog.Get(cards.Last()).rarity;
                        Assert.That(new[]{CardRarity.Rare,CardRarity.Special,CardRarity.Unique,CardRarity.Secret,CardRarity.Legend}.Contains(last),Is.True);
                        if(offer.id=="premium")Assert.That(last,Is.Not.EqualTo(CardRarity.Rare));
                    }
                }
            }
            var all=FmQuestCatalog.GradeOdds("standard",false);
            foreach(var grade in new[]{CardRarity.Rare,CardRarity.Special,CardRarity.Unique,CardRarity.Secret,CardRarity.Legend})
                Assert.That(all.ContainsKey(grade),Is.True,grade.ToString());
        }
        [Test] public void DailyAorBorCIsOneChoicePerKoreanDayAndDoesNotChargeSp()
        {
            var q=Profile();Assert.False(q.TryBuyPack("daily-a",out _,out _));
            Assert.True(q.TryBuyPack("daily-a",out var a,out _,"2026-10-30"));
            Assert.That(q.Sp,Is.EqualTo(600));Assert.False(q.TryBuyPack("daily-b",out _,out _,"2026-10-30"));
            q=FmQuestEconomy.FromSave(FieldJson.Decode<FmQuestProfile>(FieldJson.Encode(q.CaptureSave())));
            Assert.False(q.TryBuyPack("daily-c",out _,out _,"2026-10-30"));
            Assert.True(q.TryBuyPack("daily-c",out var c,out _,"2026-10-31"));
            Assert.True(q.TryOpenPack(c.id,out _,out _));Assert.True(q.TryOpenPack(a.id,out _,out _));
            Assert.That(q.OwnedCards.Sum(x=>x.count),Is.EqualTo(60));FmQuestEconomy.Validate(q.CaptureSave());
        }
        [Test] public void FirstPackObjectiveNeedsActualOpeningAndCanOnlyBeClaimedOnce()
        {
            var q=Profile();Assert.False(q.ClaimQuest("first-pack","2026-10-30",out _,out _));
            Assert.True(q.TryBuyPack("standard",out var purchase,out _));
            Assert.False(q.ClaimQuest("first-pack","2026-10-30",out _,out _));
            Assert.True(q.TryOpenPack(purchase.id,out _,out _));
            Assert.True(q.ClaimQuest("first-pack","2026-10-30",out var receipt,out _));Assert.That(receipt.spAwarded,Is.EqualTo(150));
            Assert.That(q.Sp,Is.EqualTo(350));Assert.False(q.ClaimQuest("first-pack","2026-10-31",out _,out _));
            FmQuestEconomy.Validate(q.CaptureSave());
        }
        [Test] public void ARealScenarioWinAwardsReplayAndFirstClearOnceAcrossFullReload()
        {
            var route=WinCopy();var q=FmQuestEconomy.FromSave(route.profile);
            Assert.True(q.TryResolveBattle(route.ticket,route.battle,"2026-10-30",out var reward,out var reason),reason);
            Assert.That(reward.spAwarded,Is.EqualTo(450));Assert.That(reward.firstClearSp,Is.EqualTo(300));
            Assert.That(q.CompletedScenarios,Is.EqualTo(new[]{"roots"}));Assert.That(q.Sp,Is.EqualTo(1050));
            q=FmQuestEconomy.FromSave(FieldJson.Decode<FmQuestProfile>(FieldJson.Encode(q.CaptureSave())));
            Assert.True(q.TryResolveBattle(route.ticket,route.battle,"2026-10-31",out reward,out reason),reason);
            Assert.That(reward.alreadyClaimed,Is.True);Assert.That(reward.spAwarded,Is.Zero);Assert.That(q.Sp,Is.EqualTo(1050));
            Assert.True(q.TryBeginBattle("mono-forest",4,"tides",null,out _,out _,out reason),reason);
        }
        [Test] public void VictoryAndDailyObjectivesRequireAnActualResultOnThatDay()
        {
            var route=WinCopy();var q=FmQuestEconomy.FromSave(route.profile);
            Assert.False(q.ClaimQuest("first-victory","2026-10-30",out _,out _));
            Assert.True(q.TryResolveBattle(route.ticket,route.battle,"2026-10-30",out _,out _));
            Assert.True(q.ClaimQuest("first-victory","2026-10-30",out _,out _));
            Assert.True(q.ClaimQuest("daily-victory","2026-10-30",out var today,out _));Assert.That(today.spAwarded,Is.EqualTo(150));
            Assert.False(q.ClaimQuest("daily-victory","2026-10-30",out _,out _));
            Assert.False(q.ClaimQuest("daily-victory","2026-10-31",out _,out _));
            Assert.False(q.ClaimQuest("three-victories","2026-10-30",out _,out _));
            FmQuestEconomy.Validate(q.CaptureSave());
        }
        [Test] public void ForeignLegacyBattleCannotBeAttachedAndPracticeStateIsUnchanged()
        {
            var legacy=DemoCatalog.CreateBattle(33,"forest","lyria");string original=FieldJson.Encode(legacy.State);
            var q=Profile();Assert.True(q.TryBeginBattle("mono-forest",0,"",null,out var owned,out var ticket,out _));
            Assert.False(q.ValidateBattleBinding(ticket,legacy.State,out _));
            Assert.False(q.TryResolveBattle(ticket,legacy.State,"2026-10-30",out _,out _));
            Assert.That(FieldJson.Encode(legacy.State),Is.EqualTo(original));
            var loaded=new BattleEngine(FieldJson.Decode<BattleState>(FieldJson.Encode(owned.State)));
            Assert.True(q.ValidateBattleBinding(ticket,loaded.State,out var error),error);
            Assert.That(FmQuestEconomy.SaveSlot,Is.Not.EqualTo("fantasy-battle-classic-v3"));
        }
        [Test] public void EnemyConcedeCannotMintVictoryRewardsAndOwnConcedePaysNothing()
        {
            var q=Profile();Assert.True(q.TryBeginBattle("mono-forest",0,"",null,out var e,out var ticket,out _));
            Assert.True(e.Concede(1));Assert.False(q.TryResolveBattle(ticket,e.State,"2026-10-30",out _,out _));Assert.That(q.Sp,Is.EqualTo(600));
            Assert.True(q.TryAbandonBattle(ticket,out _));
            Assert.True(q.TryBeginBattle("mono-forest",0,"",null,out e,out ticket,out _));Assert.True(e.Concede(0));
            Assert.True(q.TryResolveBattle(ticket,e.State,"2026-10-30",out var lost,out _));Assert.That(lost.spAwarded,Is.Zero);
            Assert.That(q.TotalWins,Is.Zero);FmQuestEconomy.Validate(q.CaptureSave());
        }
        [Test] public void AbandonmentReleasesOrphanTicketWithoutMoneyOrRngCrossTalk()
        {
            var q=Profile();Assert.True(q.TryBeginBattle("mono-forest",0,"",null,out _,out var first,out _));
            Assert.False(q.TryBeginBattle("mono-forest",0,"",null,out _,out _,out _));
            q=FmQuestEconomy.FromSave(FieldJson.Decode<FmQuestProfile>(FieldJson.Encode(q.CaptureSave())));
            Assert.That(q.ActiveBattleTicketId,Is.EqualTo(first));Assert.True(q.TryAbandonBattle(first,out _));
            Assert.False(q.TryAbandonBattle(first,out _));Assert.That(q.Sp,Is.EqualTo(600));
            Assert.True(q.TryBeginBattle("mono-forest",0,"",null,out _,out var second,out _));Assert.That(second,Is.Not.EqualTo(first));
            FmQuestEconomy.Validate(q.CaptureSave());
        }
        [Test] public void DamagedInventoryWalletResultsRngAndDuplicateTransactionsAreRejected()
        {
            var q=Profile();Assert.True(q.TryBuyPack("standard",out _,out _));
            var p=q.CaptureSave();p.sp++;Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.paidPoints=1;Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.ownedCards[0].count++;Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.packRandomState++;Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.purchases[0].cardIds[0]="unknown";Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.purchases.Add(p.purchases[0]);p.nextPurchase++;Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.ledger.Add(p.ledger[0]);Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.ownedCards[0].cardId="unknown";Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            p=q.CaptureSave();p.version=2;Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(p));
            Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(new FmQuestProfile()));
            Assert.Throws<ArgumentException>(()=>FmQuestEconomy.FromSave(null));
        }
        [Test] public void ReturnedSnapshotsCannotMutateTheLiveProfileOrPackResults()
        {
            var q=Profile();var snapshot=q.CaptureSave();snapshot.sp=12345;snapshot.ownedCards[0].count=999;
            Assert.That(q.Sp,Is.EqualTo(600));Assert.That(q.OwnedCards.Sum(c=>c.count),Is.EqualTo(50));
            Assert.True(q.TryBuyPack("standard",out var purchase,out _));var real=q.PackPurchases.Single().cardIds.ToArray();
            purchase.cardIds[0]="unknown";var exposed=q.PackPurchases.Single();exposed.cardIds.Clear();
            Assert.That(q.PackPurchases.Single().cardIds,Is.EqualTo(real));FmQuestEconomy.Validate(q.CaptureSave());
        }
    }
}
