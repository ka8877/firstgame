using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace FantasyCards.Core
{
    [Serializable] public sealed class FmQuestOwnedCard
    {
        public string cardId;
        public int count;
    }
    [Serializable] public sealed class FmQuestPackPurchase
    {
        public string id, offerId, calendarDay = "";
        public int priceSp;
        public bool opened;
        public List<string> cardIds = new List<string>();
    }
    public enum FmQuestBattleStatus { Active, Won, Lost, Drawn, Abandoned }
    [Serializable] public sealed class FmQuestBattleTicket
    {
        public string id, deckId, leaderId, scenarioId = "", calendarDay = "";
        public int encounterIndex, startEventSequence, turn, rewardSp, firstClearSp;
        public uint seed;
        public bool conceded;
        public FmQuestBattleStatus status;
        public List<string> playerCards = new List<string>();
    }
    [Serializable] public sealed class FmQuestClaim
    {
        public string id, objectiveId, calendarDay = "";
        public int rewardSp;
    }
    [Serializable] public sealed class FmQuestLedgerEntry
    {
        public string id, kind, label;
        public int deltaSp, balance;
    }
    [Serializable] public sealed class FmQuestProfile
    {
        public int version = 1;
        public string catalogVersion = CardCatalog.Version, starterDeckId = "";
        public uint originSeed, packRandomState, battleSeedState;
        public int sp, paidPoints, nextPurchase = 1, nextBattle = 1;
        public List<FmQuestOwnedCard> ownedCards = new List<FmQuestOwnedCard>();
        public List<FmQuestPackPurchase> purchases = new List<FmQuestPackPurchase>();
        public List<FmQuestBattleTicket> battles = new List<FmQuestBattleTicket>();
        public List<FmQuestClaim> claims = new List<FmQuestClaim>();
        public List<FmQuestLedgerEntry> ledger = new List<FmQuestLedgerEntry>();
        public List<string> completedScenarios = new List<string>();
    }
    public sealed class FmQuestRewardReceipt
    {
        public string id, label, message;
        public int spAwarded, originalSp, firstClearSp, balance;
        public bool alreadyClaimed;
    }
    public sealed class FmQuestObjective
    {
        public readonly string id, name, description;
        public readonly int rewardSp;
        public readonly bool isDaily;
        public FmQuestObjective(string id, string name, string description, int rewardSp, bool isDaily = false)
        { this.id = id; this.name = name; this.description = description; this.rewardSp = rewardSp; this.isDaily = isDaily; }
    }
    public sealed class FmQuestPackOffer
    {
        public readonly string id, name, description, guaranteeText, oddsText, deckId;
        public readonly int priceSp, cardCount, element, kindFilter;
        public readonly bool dailyOneChoice;
        internal readonly bool premium;
        internal FmQuestPackOffer(string id, string name, string description, int priceSp, int element = -1,
            bool premium = false, string deckId = "", bool daily = false, int kindFilter = -1)
        {
            this.id = id; this.name = name; this.description = description; this.priceSp = priceSp;
            this.element = element; this.premium = premium; this.deckId = deckId; dailyOneChoice = daily; this.kindFilter = kindFilter;
            cardCount = deckId.Length > 0 ? 50 : 5;
            guaranteeText = deckId.Length > 0 ? "표시한 기본 덱 50장 확정 · 무작위 아님"
                : daily ? "무료 5장 · A/B/C 중 한국 시간 하루 한 종류"
                : premium ? "5장 모두 레어 이상 · 마지막 한 장 스페셜 이상 확정"
                : "일반 4장 + 마지막 한 장 레어 이상 확정";
            oddsText = deckId.Length > 0 ? "기본 덱 구성 그대로 100% 지급"
                : "일반 칸: " + FmQuestCatalog.OddsText(this, false) + (daily ? "" : "\n마지막 칸: " + FmQuestCatalog.OddsText(this, true))
                    + "\n같은 등급의 대상 카드는 동일 확률 · 중복 가능 · 현재 싱글판 구성";
        }
    }

    // SP(game money), GP(paid cash) and daily free booster A/B/C names are
    // documented original features. All current prices/rewards/drop tables and
    // the seven existing fifty-card starter lists are authored solo balancing.
    public static class FmQuestCatalog
    {
        public const int WelcomeSp = 600;
        public const string DailyTimezone = "Asia/Seoul";
        static readonly string[] starterIds = { "mono-light", "mono-water", "mono-fire", "mono-forest", "mono-metal", "mono-earth", "mono-darkness" };
        public static StarterDeckDefinition[] StarterChoices => starterIds.Select(DemoCatalog.GetDeck).ToArray();
        public static readonly FmQuestObjective[] Objectives = {
            new FmQuestObjective("first-victory", "첫 승리", "보유 덱으로 수집전 또는 시나리오에서 한 번 승리", 250),
            new FmQuestObjective("three-victories", "결투의 발자취", "보유 덱으로 총 세 번 승리", 400),
            new FmQuestObjective("first-pack", "첫 부스터", "상점의 부스터 또는 기본 덱 상품 한 개를 개봉", 150),
            new FmQuestObjective("daily-victory", "오늘의 결투", "한국 시간 오늘 보유 덱으로 한 번 승리", 150, true)
        };
        static readonly Dictionary<string, CardDefinition[]> pools = new Dictionary<string, CardDefinition[]>();
        public static readonly FmQuestPackOffer[] Offers = CreateOffers();
        static FmQuestPackOffer[] CreateOffers()
        {
            var offers = new List<FmQuestPackOffer> {
                new FmQuestPackOffer("standard", "클래식 부스터", "전 속성 출전 카드로 덱을 확장합니다.", 400),
                new FmQuestPackOffer("premium", "상위 등급 부스터", "레어·스페셜·유니크·시크릿·레전드 출전 카드 구성입니다.", 1200, premium:true),
                new FmQuestPackOffer("daily-a", "어드밴스 무료 부스터 A · 전열", "이 싱글판의 A 구성은 유닛 카드입니다. 한국 시간 하루 A/B/C 중 하나를 받을 수 있습니다.", 0, daily:true, kindFilter:0),
                new FmQuestPackOffer("daily-b", "어드밴스 무료 부스터 B · 기술", "이 싱글판의 B 구성은 마법·장비 카드입니다. 한국 시간 하루 A/B/C 중 하나를 받을 수 있습니다.", 0, daily:true, kindFilter:1),
                new FmQuestPackOffer("daily-c", "어드밴스 무료 부스터 C · 지형", "이 싱글판의 C 구성은 지형 카드입니다. 한국 시간 하루 A/B/C 중 하나를 받을 수 있습니다.", 0, daily:true, kindFilter:2)
            };
            for (int i = 0; i < starterIds.Length; i++)
            {
                var deck = DemoCatalog.GetDeck(starterIds[i]);
                string elementName = CardCatalog.ElementName((Element)i);
                offers.Add(new FmQuestPackOffer("element-"+i, elementName+" 속성 부스터", elementName+" 속성 출전 카드만 나옵니다.", 400, i));
                offers.Add(new FmQuestPackOffer("starter-"+starterIds[i], deck.name+" · 기본 덱 세트", "처음 고르지 않은 속성도 SP로 확보할 수 있습니다. 구성 50장 확정 상품입니다.", 1200, deckId:starterIds[i]));
            }
            return offers.ToArray();
        }
        public static FmQuestPackOffer GetOffer(string id) => Array.Find(Offers, x => x.id == id) ?? throw new ArgumentException("알 수 없는 상품: "+id);
        public static FmQuestObjective GetObjective(string id) => Array.Find(Objectives, x => x.id == id) ?? throw new ArgumentException("알 수 없는 목표: "+id);
        public static bool IsStarter(string id) => Array.IndexOf(starterIds, id) >= 0;
        internal static IEnumerable<KeyValuePair<CardRarity, int>> Weights(FmQuestPackOffer offer, bool guaranteed)
        {
            if (offer.premium)
                return guaranteed ? Pairs(CardRarity.Special,7000,CardRarity.Unique,2400,CardRarity.Secret,500,CardRarity.Legend,100)
                    : Pairs(CardRarity.Rare,5000,CardRarity.Special,3000,CardRarity.Unique,1500,CardRarity.Secret,400,CardRarity.Legend,100);
            if (guaranteed && !offer.dailyOneChoice)
                return Pairs(CardRarity.Rare,8000,CardRarity.Special,1400,CardRarity.Unique,450,CardRarity.Secret,120,CardRarity.Legend,30);
            return Pairs(CardRarity.Free,1000,CardRarity.Common,2200,CardRarity.Uncommon,2000,CardRarity.Upgrade,2400,
                CardRarity.Rare,1500,CardRarity.Special,400,CardRarity.Unique,150,CardRarity.Event,100,
                CardRarity.Icajen,100,CardRarity.Epic,100,CardRarity.Secret,40,CardRarity.Legend,10);
        }
        static IEnumerable<KeyValuePair<CardRarity,int>> Pairs(params object[] data)
        { for (int i=0;i<data.Length;i+=2) yield return new KeyValuePair<CardRarity,int>((CardRarity)data[i],(int)data[i+1]); }
        internal static CardDefinition[] Pool(FmQuestPackOffer offer, CardRarity rarity)
        {
            string key=offer.id+":"+(int)rarity;
            if (!pools.TryGetValue(key,out var result))
            {
                result=CardCatalog.All.Where(c=>c.rarity==rarity && (offer.element<0||(int)c.element==offer.element)
                    && (offer.kindFilter<0 || offer.kindFilter==0&&c.kind==CardKind.Unit
                        || offer.kindFilter==1&&(c.kind==CardKind.Spell||c.kind==CardKind.Equipment)
                        || offer.kindFilter==2&&c.kind==CardKind.Terrain)).OrderBy(c=>c.id,StringComparer.Ordinal).ToArray();
                pools.Add(key,result);
            }
            return result;
        }
        static KeyValuePair<CardRarity,int>[] AvailableWeights(FmQuestPackOffer offer,bool guaranteed)
            => Weights(offer,guaranteed).Where(w=>Pool(offer,w.Key).Length>0).ToArray();
        public static string OddsText(FmQuestPackOffer offer,bool guaranteed)
        {
            var weights=AvailableWeights(offer,guaranteed); int total=weights.Sum(w=>w.Value);
            if(total==0)throw new InvalidOperationException("상품의 출전 카드 풀이 없습니다: "+offer.id);
            return string.Join(" · ",weights.Select(w=>CardCatalog.RarityName(w.Key)+" "+(100m*w.Value/total).ToString("0.####",CultureInfo.InvariantCulture)+"%"));
        }
        public static IReadOnlyDictionary<CardRarity,decimal> GradeOdds(string offerId,bool guaranteed)
        {
            var offer=GetOffer(offerId);if(offer.deckId.Length>0)return new Dictionary<CardRarity,decimal>();
            var weights=AvailableWeights(offer,guaranteed);int total=weights.Sum(w=>w.Value);
            return weights.ToDictionary(w=>w.Key,w=>w.Value/(decimal)total);
        }
        internal static List<string> Roll(FmQuestPackOffer offer,ref uint state)
        {
            if(offer.deckId.Length>0)return DemoCatalog.GetDeck(offer.deckId).CopyCards();
            var cards=new List<string>();
            for(int i=0;i<offer.cardCount;i++)
            {
                var weights=AvailableWeights(offer,!offer.dailyOneChoice&&i==offer.cardCount-1);
                int pick=StableRandom.Range(ref state,weights.Sum(w=>w.Value));CardRarity rarity=weights[0].Key;
                foreach(var weight in weights){if(pick<weight.Value){rarity=weight.Key;break;}pick-=weight.Value;}
                var pool=Pool(offer,rarity);cards.Add(pool[StableRandom.Range(ref state,pool.Length)].id);
            }
            return cards;
        }
    }
}
