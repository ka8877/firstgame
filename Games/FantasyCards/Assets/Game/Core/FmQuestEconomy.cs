using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FantasyCards.Core
{
    /// <summary>Local collection progression. Never mutates legacy battle/deck saves or consumes battle RNG.</summary>
    public sealed class FmQuestEconomy
    {
        public const string GameId = "starlight-oath", SaveSlot = "fantasy-quest-profile-v1", ContentVersion = "fm-quest-0.1.0";
        public const int SchemaVersion = 1, MaximumRecords = 4096, MaximumSp = 100000000;
        FmQuestProfile state;
        FmQuestEconomy(FmQuestProfile profile) { state = profile; }
        public static FmQuestEconomy New(uint seed)
        {
            seed = seed == 0 ? 1u : seed;
            return new FmQuestEconomy(new FmQuestProfile { originSeed=seed,
                packRandomState=InitialPackSeed(seed), battleSeedState=InitialBattleSeed(seed) });
        }
        public static FmQuestEconomy FromSave(FmQuestProfile profile)
        { Validate(profile); return new FmQuestEconomy(Copy(profile)); }
        public FmQuestProfile CaptureSave() => Copy(state);
        public int Sp => state.sp;
        public int PaidPoints => state.paidPoints;
        public int Gp => state.paidPoints;
        public bool HasStarter => state.starterDeckId.Length > 0;
        public string StarterDeckId => state.starterDeckId;
        public int TotalWins => state.battles.Count(b=>b.status==FmQuestBattleStatus.Won);
        public int TotalBattles => state.battles.Count(Resolved);
        public int PacksOpened => state.purchases.Count(p=>p.opened);
        public int PacksBought => state.purchases.Count;
        public string ActiveBattleTicketId => state.battles.LastOrDefault(b=>b.status==FmQuestBattleStatus.Active)?.id ?? "";
        public FmQuestBattleTicket ActiveBattle => state.battles.Where(b=>b.status==FmQuestBattleStatus.Active).Select(CopyTicket).FirstOrDefault();
        public IReadOnlyList<FmQuestOwnedCard> OwnedCards => state.ownedCards.Select(c=>new FmQuestOwnedCard {cardId=c.cardId,count=c.count}).ToArray();
        public IReadOnlyList<FmQuestPackPurchase> PackPurchases => state.purchases.Select(CopyPurchase).ToArray();
        public IReadOnlyList<string> CompletedScenarios => state.completedScenarios.ToArray();
        public IReadOnlyList<FmQuestLedgerEntry> Ledger => state.ledger.Select(CopyLedger).ToArray();
        public int OwnedCount(string cardId) => state.ownedCards.Find(c=>c.cardId==cardId)?.count ?? 0;
        public bool TrySelectStarter(string deckId,out string reason)
        {
            if(HasStarter)return Fail("무료 기본 덱은 이미 받았습니다. 다른 속성은 상점에서 SP로 구할 수 있습니다.",out reason);
            if(!FmQuestCatalog.IsStarter(deckId))return Fail("일곱 속성 기본 덱 중 하나를 고르세요.",out reason);
            var deck=DemoCatalog.GetDeck(deckId);DemoCatalog.ValidatePlayableDeck(deck.cards);
            state.starterDeckId=deckId;AddCards(state,deck.cards);
            AddLedger(state,"starter","starter","첫 기본 덱 · 환영 SP",FmQuestCatalog.WelcomeSp);
            reason="";return true;
        }
        public bool ValidateOwnedDeck(IList<string> cards,out string reason)
        {
            if(!HasStarter)return Fail("먼저 무료 기본 덱을 골라 주세요.",out reason);
            try { DemoCatalog.ValidatePlayableDeck(cards); }
            catch(ArgumentException e){return Fail(e.Message,out reason);}
            foreach(var group in cards.GroupBy(c=>c))
                if(group.Count()>OwnedCount(group.Key))return Fail(CardCatalog.Get(group.Key).name+" 보유 수량이 부족합니다 (보유 "+OwnedCount(group.Key)+"장).",out reason);
            reason="";return true;
        }
        public bool TryBeginBattle(string deckId,int encounterIndex,string scenarioId,IList<string> customDeck,
            out BattleEngine battle,out string ticketId,out string reason,string leaderId=null)
        {
            battle=null;ticketId="";
            if(!HasStarter)return Fail("먼저 무료 기본 덱을 골라 주세요.",out reason);
            if(ActiveBattleTicketId.Length>0)return Fail("진행 중인 수집전이 있습니다. 이어 하거나 포기한 뒤 새 결투를 시작하세요.",out reason);
            if(state.battles.Count>=MaximumRecords)return Fail("수집전 기록 한도에 도달했습니다. 현재 저장을 보관해 주세요.",out reason);
            StarterDeckDefinition deck; EncounterDefinition encounter;
            try { deck=DemoCatalog.GetDeck(deckId);encounter=DemoCatalog.GetEncounter(encounterIndex); }
            catch(ArgumentException e){return Fail(e.Message,out reason);}
            var cards=customDeck==null?deck.CopyCards():new List<string>(customDeck);
            if(!ValidateOwnedDeck(cards,out reason))return false;
            scenarioId=scenarioId??"";
            if(scenarioId.Length>0)
            {
                ScenarioStage stage;
                try {stage=FmScenarioCatalog.Get(scenarioId);}catch(ArgumentException e){return Fail(e.Message,out reason);}
                if(stage.encounterIndex!=encounterIndex)return Fail("시나리오와 상대가 일치하지 않습니다.",out reason);
                if(!FmScenarioCatalog.IsUnlocked(scenarioId,state.completedScenarios))return Fail("앞 시나리오를 먼저 완료해 주세요.",out reason);
            }
            else if(encounterIndex>=3)return Fail("시나리오 상대는 시나리오 경로에서 시작해 주세요.",out reason);
            leaderId=leaderId??deck.recommendedLeaderId;
            try {DemoCatalog.GetLeader(leaderId);}catch(ArgumentException e){return Fail(e.Message,out reason);}
            uint next=state.battleSeedState,seed=StableRandom.Next(ref next);
            battle=DemoCatalog.CreateBattle(seed,deckId,leaderId,encounterIndex,cards);
            ticketId=BattleId(state.nextBattle);
            var ticket=new FmQuestBattleTicket {id=ticketId,deckId=deckId,leaderId=leaderId,
                encounterIndex=encounterIndex,scenarioId=scenarioId,seed=seed,playerCards=cards,
                startEventSequence=battle.State.events.Count+1};
            battle.State.events.Add(TicketEvent(ticket));
            state.battleSeedState=next;state.nextBattle++;state.battles.Add(ticket);
            reason="";return true;
        }
        public bool TryAbandonBattle(string ticketId,out string reason)
        {
            var ticket=state.battles.Find(b=>b.id==ticketId);
            if(ticket==null||ticket.status!=FmQuestBattleStatus.Active)return Fail("진행 중인 수집전 기록이 없습니다.",out reason);
            ticket.status=FmQuestBattleStatus.Abandoned;reason="";return true;
        }
        public bool ValidateBattleBinding(string ticketId,BattleState battle,out string reason)
        {
            var ticket=state.battles.Find(b=>b.id==ticketId);
            if(ticket==null||ticket.status==FmQuestBattleStatus.Abandoned)return Fail("이 수집전에 연결된 기록이 없습니다.",out reason);
            try { ValidateBoundState(ticket,battle); }
            catch(ArgumentException e){return Fail(e.Message,out reason);}
            if(Resolved(ticket) && (battle.phase!=BattlePhase.Complete || StatusForWinner(battle.winner)!=ticket.status))
                return Fail("이미 정산한 결투 결과와 다릅니다.",out reason);
            reason="";return true;
        }
        public bool TryResolveBattle(string ticketId,BattleState battle,string calendarDay,out FmQuestRewardReceipt reward,out string reason)
        {
            reward=null;
            if(!ValidDay(calendarDay))return Fail("보상 날짜는 한국 시간 yyyy-MM-dd 형식이어야 합니다.",out reason);
            if(!ValidateBattleBinding(ticketId,battle,out reason))return false;
            var ticket=state.battles.Find(b=>b.id==ticketId);
            if(battle.phase!=BattlePhase.Complete)return Fail("끝난 결투만 정산할 수 있습니다.",out reason);
            if(Resolved(ticket)) {reward=Receipt(ticket,true);reason="";return true;}
            if(!NaturalResultOrOwnConcession(battle,out var conceded))return Fail("실제 덱 소진·턴 한도 또는 자신의 포기 결과가 필요합니다.",out reason);
            int first=ticket.scenarioId.Length>0&&battle.winner==0&&!state.completedScenarios.Contains(ticket.scenarioId)
                ?200+100*DemoCatalog.GetEncounter(ticket.encounterIndex).tier:0;
            int earned=battle.winner==0?100+50*DemoCatalog.GetEncounter(ticket.encounterIndex).tier
                :conceded||battle.turn<2?0:battle.winner==2?25:35;
            if(state.sp>MaximumSp-earned-first)return Fail("SP 보유 한도를 초과할 수 없습니다.",out reason);
            ticket.status=StatusForWinner(battle.winner);ticket.calendarDay=calendarDay;ticket.turn=battle.turn;
            ticket.rewardSp=earned+first;ticket.firstClearSp=first;ticket.conceded=conceded;
            if(first>0)state.completedScenarios.Add(ticket.scenarioId);
            AddLedger(state,ticket.id,"battle",battle.winner==0?"수집전 승리":"수집전 종료",ticket.rewardSp);
            reward=Receipt(ticket,false);reason="";return true;
        }
        FmQuestRewardReceipt Receipt(FmQuestBattleTicket ticket,bool already)
            => new FmQuestRewardReceipt {id=ticket.id,label=ticket.status==FmQuestBattleStatus.Won?"승리 보상":"결투 기록",
                spAwarded=already?0:ticket.rewardSp,originalSp=ticket.rewardSp,firstClearSp=already?0:ticket.firstClearSp,
                balance=state.sp,alreadyClaimed=already,message=already?"이미 정산한 결투입니다. SP를 다시 지급하지 않습니다.":"결투 보상이 보관되었습니다."};
        public bool CanBuyPack(string offerId,string calendarDay,out string reason)
        {
            if(!HasStarter)return Fail("먼저 무료 기본 덱을 골라 주세요.",out reason);
            FmQuestPackOffer offer;try{offer=FmQuestCatalog.GetOffer(offerId);}catch(ArgumentException e){return Fail(e.Message,out reason);}
            if(state.purchases.Count>=MaximumRecords)return Fail("구매 기록 한도에 도달했습니다.",out reason);
            if(state.sp<offer.priceSp)return Fail("SP가 부족합니다.",out reason);
            if(offer.dailyOneChoice)
            {
                if(!ValidDay(calendarDay))return Fail("무료 부스터의 한국 시간 날짜가 필요합니다.",out reason);
                if(state.purchases.Any(p=>p.calendarDay==calendarDay&&FmQuestCatalog.GetOffer(p.offerId).dailyOneChoice))
                    return Fail("오늘의 무료 부스터를 이미 받았습니다. 내일 A/B/C 중 하나를 다시 받을 수 있습니다.",out reason);
            }
            reason="";return true;
        }
        public bool TryBuyPack(string offerId,out FmQuestPackPurchase purchase,out string reason,string calendarDay=null)
        {
            purchase=null;if(!CanBuyPack(offerId,calendarDay,out reason))return false;
            var offer=FmQuestCatalog.GetOffer(offerId);uint random=state.packRandomState;
            var bought=new FmQuestPackPurchase {id=PurchaseId(state.nextPurchase),offerId=offerId,priceSp=offer.priceSp,
                calendarDay=offer.dailyOneChoice?calendarDay:"",cardIds=FmQuestCatalog.Roll(offer,ref random)};
            state.nextPurchase++;state.packRandomState=random;state.purchases.Add(bought);
            AddLedger(state,bought.id,"pack",offer.name,-offer.priceSp);
            purchase=CopyPurchase(bought);reason="";return true;
        }
        public bool TryOpenPack(string purchaseId,out FmQuestPackPurchase opened,out string reason)
        {
            opened=null;var purchase=state.purchases.Find(p=>p.id==purchaseId);
            if(purchase==null)return Fail("구매한 부스터가 없습니다.",out reason);
            if(purchase.opened)return Fail("이미 개봉해 보유 카드에 반영한 상품입니다.",out reason);
            AddCards(state,purchase.cardIds);purchase.opened=true;opened=CopyPurchase(purchase);reason="";return true;
        }
        public bool IsQuestClaimed(string objectiveId,string calendarDay)
        {
            var objective=FmQuestCatalog.GetObjective(objectiveId);
            return state.claims.Any(c=>c.objectiveId==objectiveId&&(!objective.isDaily||c.calendarDay==calendarDay));
        }
        public bool CanClaimQuest(string objectiveId,string calendarDay,out string reason)
        {
            if(!HasStarter)return Fail("무료 기본 덱을 먼저 받아 주세요.",out reason);
            FmQuestObjective objective;try{objective=FmQuestCatalog.GetObjective(objectiveId);}catch(ArgumentException e){return Fail(e.Message,out reason);}
            if(!ValidDay(calendarDay))return Fail("보상 날짜는 한국 시간 yyyy-MM-dd 형식이어야 합니다.",out reason);
            if(IsQuestClaimed(objectiveId,calendarDay))return Fail("이미 받은 목표 보상입니다.",out reason);
            if(!Eligible(state,objectiveId,calendarDay))return Fail(objective.description+" 조건을 먼저 달성해 주세요.",out reason);
            if(state.claims.Count>=MaximumRecords||state.sp>MaximumSp-objective.rewardSp)return Fail("보상 기록 또는 SP 한도를 초과할 수 없습니다.",out reason);
            reason="";return true;
        }
        public bool ClaimQuest(string objectiveId,string calendarDay,out FmQuestRewardReceipt reward,out string reason)
        {
            reward=null;if(!CanClaimQuest(objectiveId,calendarDay,out reason))return false;
            var objective=FmQuestCatalog.GetObjective(objectiveId);
            string id="quest:"+objective.id+(objective.isDaily?":"+calendarDay:"");
            state.claims.Add(new FmQuestClaim{id=id,objectiveId=objectiveId,calendarDay=objective.isDaily?calendarDay:"",rewardSp=objective.rewardSp});
            AddLedger(state,id,"quest",objective.name,objective.rewardSp);
            reward=new FmQuestRewardReceipt{id=id,label=objective.name,spAwarded=objective.rewardSp,originalSp=objective.rewardSp,balance=state.sp,message="목표 보상을 받았습니다."};
            reason="";return true;
        }
        static bool Eligible(FmQuestProfile p,string id,string day)
        {
            switch(id)
            {
                case "first-victory":return p.battles.Any(b=>b.status==FmQuestBattleStatus.Won);
                case "three-victories":return p.battles.Count(b=>b.status==FmQuestBattleStatus.Won)>=3;
                case "first-pack":return p.purchases.Any(b=>b.opened);
                case "daily-victory":return p.battles.Any(b=>b.status==FmQuestBattleStatus.Won&&b.calendarDay==day);
                default:return false;
            }
        }
        static void AddLedger(FmQuestProfile p,string id,string kind,string label,int delta)
        {
            int balance=checked(p.sp+delta);
            if(balance<0||balance>MaximumSp||p.ledger.Any(x=>x.id==id))throw new ArgumentException("중복되거나 잘못된 SP 거래입니다.");
            p.sp=balance;p.ledger.Add(new FmQuestLedgerEntry{id=id,kind=kind,label=label,deltaSp=delta,balance=balance});
        }
        static void AddCards(FmQuestProfile p,IEnumerable<string> ids)
        {
            foreach(string id in ids)
            {var owned=p.ownedCards.Find(c=>c.cardId==id);if(owned==null)p.ownedCards.Add(new FmQuestOwnedCard{cardId=id,count=1});else owned.count=checked(owned.count+1);}
        }
        static uint InitialPackSeed(uint seed){uint v=seed^0xA53C9E17u;return StableRandom.Next(ref v);}
        static uint InitialBattleSeed(uint seed){uint v=seed^0x6D2B79F5u;return StableRandom.Next(ref v);}
        static string PurchaseId(int serial)=>"pack-"+serial.ToString("D6",CultureInfo.InvariantCulture);
        static string BattleId(int serial)=>"battle-"+serial.ToString("D6",CultureInfo.InvariantCulture);
        static bool ValidDay(string day)=>DateTime.TryParseExact(day,"yyyy-MM-dd",CultureInfo.InvariantCulture,DateTimeStyles.None,out _);
        static bool Resolved(FmQuestBattleTicket b)=>b.status==FmQuestBattleStatus.Won||b.status==FmQuestBattleStatus.Lost||b.status==FmQuestBattleStatus.Drawn;
        static FmQuestBattleStatus StatusForWinner(int winner)=>winner==0?FmQuestBattleStatus.Won:winner==1?FmQuestBattleStatus.Lost:FmQuestBattleStatus.Drawn;
        static BattleEvent TicketEvent(FmQuestBattleTicket t)=>new BattleEvent {sequence=t.startEventSequence,turn=1,actor=-1,
            kind="quest-ticket",text="수집전 시작 · "+t.id,stat=t.seed.ToString("X8",CultureInfo.InvariantCulture)};
        static bool Fail(string message,out string reason){reason=message;return false;}
        static IEnumerable<CardInstance> Instances(PlayerState p)
        {
            foreach(var c in p.deck)yield return c;foreach(var c in p.hand)yield return c;
            foreach(var c in p.terrain)yield return c;foreach(var c in p.discard)yield return c;
            foreach(var u in p.units){yield return new CardInstance(u.instanceId,u.definitionId);foreach(var c in u.equipment)yield return c;}
            foreach(var c in p.preparedSpells)yield return new CardInstance(c.instanceId,c.definitionId);
        }
        static void ValidateBoundState(FmQuestBattleTicket ticket,BattleState battle)
        {
            BattleEngine.Validate(battle);
            var start=DemoCatalog.CreateBattle(ticket.seed,ticket.deckId,ticket.leaderId,ticket.encounterIndex,ticket.playerCards).State;
            if(!SameRules(start.rules,battle.rules)||battle.encounterIndex!=ticket.encounterIndex||battle.players[0].leaderId!=ticket.leaderId
                ||battle.players[0].deckId!="custom")throw new ArgumentException("수집전을 시작한 덱·상대·규칙과 저장이 다릅니다.");
            var markers=battle.events.Where(e=>e.kind=="quest-ticket").ToArray();var expected=TicketEvent(ticket);
            if(markers.Length!=1||markers[0].sequence!=ticket.startEventSequence||markers[0].text!=expected.text||markers[0].stat!=expected.stat
                ||markers[0].turn!=1||markers[0].actor!=-1)throw new ArgumentException("이 저장은 현재 수집전 거래에 연결되지 않았습니다.");
            var initial=start.players.SelectMany(Instances).ToDictionary(c=>c.instanceId,c=>c.definitionId);
            var current=battle.players.SelectMany(Instances).Where(c=>c.instanceId<=100).ToDictionary(c=>c.instanceId,c=>c.definitionId);
            if(initial.Count!=current.Count||initial.Any(c=>!current.TryGetValue(c.Key,out var id)||id!=c.Value))
                throw new ArgumentException("수집전을 시작한 원본 카드 구성이 바뀌었습니다.");
        }
        static bool SameRules(RuleSet a,RuleSet b)=>a.deckSize==b.deckSize&&a.startingHealth==b.startingHealth&&a.openingHand==b.openingHand
            &&a.handLimit==b.handLimit&&a.unitLanes==b.unitLanes&&a.terrainLimit==b.terrainLimit&&a.terrainPerTurn==b.terrainPerTurn
            &&a.maximumResource==b.maximumResource&&a.maximumTurns==b.maximumTurns&&a.guaranteeOpeningElements==b.guaranteeOpeningElements
            &&a.defenderStrikesBack==b.defenderStrikesBack&&a.baseCoins==b.baseCoins&&a.coinOrder==b.coinOrder&&a.damageOrder==b.damageOrder&&a.victoryRule==b.victoryRule;
        static bool NaturalResultOrOwnConcession(BattleState b,out bool conceded)
        {
            conceded=false;var end=b.events.LastOrDefault();if(end==null||end.kind!="result"||end.amount!=b.winner)return false;
            if(end.text.StartsWith("나 결투 포기",StringComparison.Ordinal)){conceded=true;return b.winner==1;}
            int a=b.players[0].deck.Count,z=b.players[1].deck.Count;
            if(a==0||z==0)return b.winner==(a==0&&z==0?b.activePlayer:a==0?1:0);
            return b.turn>=b.rules.maximumTurns&&b.winner==(a==z?2:a>z?0:1);
        }

        public static void Validate(FmQuestProfile p)
        {
            if(p==null||p.version!=1||p.catalogVersion!=CardCatalog.Version||p.originSeed==0||p.packRandomState==0||p.battleSeedState==0
                ||p.starterDeckId==null||p.sp<0||p.sp>MaximumSp||p.paidPoints!=0||p.ownedCards==null||p.purchases==null||p.battles==null
                ||p.claims==null||p.ledger==null||p.completedScenarios==null||p.purchases.Count>MaximumRecords||p.battles.Count>MaximumRecords
                ||p.claims.Count>MaximumRecords||p.ownedCards.Count>CardCatalog.All.Length||p.ledger.Count>MaximumRecords*3+1
                ||p.nextPurchase!=p.purchases.Count+1||p.nextBattle!=p.battles.Count+1)throw new ArgumentException("수집전 프로필 버전 또는 상태가 올바르지 않습니다. 원본 저장을 보존하세요.");
            if(p.starterDeckId.Length==0)
            {
                if(p.sp!=0||p.ownedCards.Count!=0||p.purchases.Count!=0||p.battles.Count!=0||p.claims.Count!=0||p.ledger.Count!=0||p.completedScenarios.Count!=0
                    ||p.packRandomState!=InitialPackSeed(p.originSeed)||p.battleSeedState!=InitialBattleSeed(p.originSeed))throw new ArgumentException("기본 덱을 받기 전 프로필에 보상 기록이 있습니다.");
                return;
            }
            if(!FmQuestCatalog.IsStarter(p.starterDeckId))throw new ArgumentException("받은 기본 덱을 찾을 수 없습니다.");
            var expectedOwned=new FmQuestProfile();AddCards(expectedOwned,DemoCatalog.GetDeck(p.starterDeckId).cards);
            var expectedLedger=new Dictionary<string,KeyValuePair<string,int>>{{"starter",new KeyValuePair<string,int>("starter",FmQuestCatalog.WelcomeSp)}};
            uint packRandom=InitialPackSeed(p.originSeed);var dailyPurchases=new HashSet<string>();
            for(int i=0;i<p.purchases.Count;i++)
            {
                var purchase=p.purchases[i];if(purchase==null||purchase.id!=PurchaseId(i+1)||purchase.calendarDay==null||purchase.cardIds==null)throw new ArgumentException("상품 구매 기록이 잘못되었습니다.");
                var offer=FmQuestCatalog.GetOffer(purchase.offerId);
                if(purchase.priceSp!=offer.priceSp||purchase.cardIds.Count!=offer.cardCount||!purchase.cardIds.SequenceEqual(FmQuestCatalog.Roll(offer,ref packRandom))
                    ||offer.dailyOneChoice&&(!ValidDay(purchase.calendarDay)||!dailyPurchases.Add(purchase.calendarDay))
                    ||!offer.dailyOneChoice&&purchase.calendarDay.Length!=0)throw new ArgumentException("상품의 확률·내용·무료 구매 한도 기록이 다릅니다.");
                expectedLedger.Add(purchase.id,new KeyValuePair<string,int>("pack",-offer.priceSp));
                if(purchase.opened)AddCards(expectedOwned,purchase.cardIds);
            }
            if(packRandom!=p.packRandomState)throw new ArgumentException("부스터의 독립 난수 기록이 다릅니다.");
            if(p.ownedCards.Any(c=>c==null||!CardCatalog.Contains(c.cardId)||c.count<1)||p.ownedCards.Select(c=>c.cardId).Distinct().Count()!=p.ownedCards.Count
                ||p.ownedCards.Count!=expectedOwned.ownedCards.Count||expectedOwned.ownedCards.Any(c=>!p.ownedCards.Any(d=>d.cardId==c.cardId&&d.count==c.count)))
                throw new ArgumentException("보유 카드와 기본 덱·개봉 기록이 일치하지 않습니다.");
            uint battleRandom=InitialBattleSeed(p.originSeed);int active=0;var completed=new List<string>();
            for(int i=0;i<p.battles.Count;i++)
            {
                var b=p.battles[i];if(b==null||b.id!=BattleId(i+1)||b.seed!=StableRandom.Next(ref battleRandom)||b.playerCards==null||b.scenarioId==null||b.calendarDay==null
                    ||!Enum.IsDefined(typeof(FmQuestBattleStatus),b.status))throw new ArgumentException("수집전 거래 식별자가 잘못되었습니다.");
                DemoCatalog.GetDeck(b.deckId);DemoCatalog.GetLeader(b.leaderId);DemoCatalog.GetEncounter(b.encounterIndex);DemoCatalog.ValidatePlayableDeck(b.playerCards);
                if(b.playerCards.GroupBy(c=>c).Any(g=>g.Count()>(p.ownedCards.Find(c=>c.cardId==g.Key)?.count??0)))throw new ArgumentException("보유하지 않은 카드로 수집전을 시작했습니다.");
                var initial=DemoCatalog.CreateBattle(b.seed,b.deckId,b.leaderId,b.encounterIndex,b.playerCards);
                if(b.startEventSequence!=initial.State.events.Count+1)throw new ArgumentException("수집전 시작 기록이 다릅니다.");
                if(b.scenarioId.Length>0)
                {var stage=FmScenarioCatalog.Get(b.scenarioId);if(stage.encounterIndex!=b.encounterIndex||!FmScenarioCatalog.IsUnlocked(b.scenarioId,completed))throw new ArgumentException("시나리오 진행 순서가 다릅니다.");}
                else if(b.encounterIndex>=3)throw new ArgumentException("시나리오 거래가 누락되었습니다.");
                if(!Resolved(b))
                {if(b.status==FmQuestBattleStatus.Active){active++;if(i!=p.battles.Count-1)throw new ArgumentException("진행 중인 수집전 뒤에 새 결투가 있습니다.");}
                    if(b.calendarDay.Length!=0||b.turn!=0||b.rewardSp!=0||b.firstClearSp!=0||b.conceded)throw new ArgumentException("미완료 수집전에 보상이 있습니다.");continue;}
                if(!ValidDay(b.calendarDay)||b.turn<1||b.turn>61)throw new ArgumentException("끝난 수집전 날짜·턴이 잘못되었습니다.");
                int first=b.status==FmQuestBattleStatus.Won&&b.scenarioId.Length>0&&!completed.Contains(b.scenarioId)?200+100*DemoCatalog.GetEncounter(b.encounterIndex).tier:0;
                int baseReward=b.status==FmQuestBattleStatus.Won?100+50*DemoCatalog.GetEncounter(b.encounterIndex).tier
                    :b.conceded||b.turn<2?0:b.status==FmQuestBattleStatus.Drawn?25:35;
                if(b.firstClearSp!=first || b.rewardSp!=baseReward+first || b.conceded&&b.status!=FmQuestBattleStatus.Lost)
                    throw new ArgumentException("수집전 보상 수치가 다릅니다.");
                if(first>0)completed.Add(b.scenarioId);
                expectedLedger.Add(b.id,new KeyValuePair<string,int>("battle",b.rewardSp));
            }
            if(active>1||battleRandom!=p.battleSeedState||!completed.SequenceEqual(p.completedScenarios))throw new ArgumentException("수집전 난수 또는 완료 장 목록이 다릅니다.");
            foreach(var claim in p.claims)
            {
                if(claim==null||claim.calendarDay==null)throw new ArgumentException("목표 보상 기록이 잘못되었습니다.");
                var objective=FmQuestCatalog.GetObjective(claim.objectiveId);
                string id="quest:"+objective.id+(objective.isDaily?":"+claim.calendarDay:"");
                if(claim.id!=id||claim.rewardSp!=objective.rewardSp||objective.isDaily&&!ValidDay(claim.calendarDay)
                    ||!objective.isDaily&&claim.calendarDay.Length!=0||!Eligible(p,objective.id,claim.calendarDay)||expectedLedger.ContainsKey(id))
                    throw new ArgumentException("중복되거나 조건을 달성하지 않은 목표 보상입니다.");
                expectedLedger.Add(id,new KeyValuePair<string,int>("quest",claim.rewardSp));
            }
            if(p.ledger.Count==0||p.ledger[0]==null||p.ledger[0].id!="starter")throw new ArgumentException("처음 기본 덱 지급 기록이 없습니다.");
            int balance=0;var ids=new HashSet<string>();
            foreach(var entry in p.ledger)
            {
                if(entry==null||entry.id==null||entry.kind==null||entry.label==null||entry.label.Length>160||!ids.Add(entry.id)
                    ||!expectedLedger.TryGetValue(entry.id,out var expected)||entry.kind!=expected.Key||entry.deltaSp!=expected.Value)
                    throw new ArgumentException("SP 거래 장부가 중복되거나 원인 기록과 다릅니다.");
                balance=checked(balance+entry.deltaSp);if(balance<0||balance>MaximumSp||entry.balance!=balance)throw new ArgumentException("SP 잔액 장부가 다릅니다.");
            }
            if(ids.Count!=expectedLedger.Count||balance!=p.sp)throw new ArgumentException("SP 거래가 누락되었거나 현재 잔액과 다릅니다.");
        }
        static FmQuestProfile Copy(FmQuestProfile p)=>new FmQuestProfile {version=p.version,catalogVersion=p.catalogVersion,
            starterDeckId=p.starterDeckId,originSeed=p.originSeed,packRandomState=p.packRandomState,battleSeedState=p.battleSeedState,
            sp=p.sp,paidPoints=p.paidPoints,nextPurchase=p.nextPurchase,nextBattle=p.nextBattle,
            ownedCards=p.ownedCards.Select(c=>new FmQuestOwnedCard{cardId=c.cardId,count=c.count}).ToList(),
            purchases=p.purchases.Select(CopyPurchase).ToList(),battles=p.battles.Select(CopyTicket).ToList(),
            claims=p.claims.Select(c=>new FmQuestClaim{id=c.id,objectiveId=c.objectiveId,calendarDay=c.calendarDay,rewardSp=c.rewardSp}).ToList(),
            ledger=p.ledger.Select(CopyLedger).ToList(),completedScenarios=new List<string>(p.completedScenarios)};
        static FmQuestPackPurchase CopyPurchase(FmQuestPackPurchase p)=>new FmQuestPackPurchase{id=p.id,offerId=p.offerId,calendarDay=p.calendarDay,priceSp=p.priceSp,opened=p.opened,cardIds=new List<string>(p.cardIds)};
        static FmQuestBattleTicket CopyTicket(FmQuestBattleTicket b)=>new FmQuestBattleTicket{id=b.id,deckId=b.deckId,leaderId=b.leaderId,scenarioId=b.scenarioId,calendarDay=b.calendarDay,
            encounterIndex=b.encounterIndex,startEventSequence=b.startEventSequence,turn=b.turn,rewardSp=b.rewardSp,firstClearSp=b.firstClearSp,
            seed=b.seed,status=b.status,conceded=b.conceded,playerCards=new List<string>(b.playerCards)};
        static FmQuestLedgerEntry CopyLedger(FmQuestLedgerEntry e)=>new FmQuestLedgerEntry{id=e.id,kind=e.kind,label=e.label,deltaSp=e.deltaSp,balance=e.balance};
    }
}
