using System;
using System.Collections.Generic;
using System.Linq;

namespace FantasyCards.Core
{
    public static class CardCatalog
    {
        public const string Version = "starlight-0.2.0";
        public static readonly CardDefinition[] All =
        {
            new CardDefinition("dawn_court", "새벽의 정원", "내 턴 시작에 여명 1. 전개 즉시 여명 1.", CardKind.Terrain, Element.Dawn, 0),
            new CardDefinition("dawn_spire", "별빛 첨탑", "내 턴 시작에 여명 1. 전개 즉시 여명 1.", CardKind.Terrain, Element.Dawn, 0),
            new CardDefinition("tide_shore", "달물결 해안", "내 턴 시작에 조류 1. 전개 즉시 조류 1.", CardKind.Terrain, Element.Tide, 0),
            new CardDefinition("tide_pool", "기억의 샘", "내 턴 시작에 조류 1. 전개 즉시 조류 1.", CardKind.Terrain, Element.Tide, 0),
            new CardDefinition("dawn_scout", "별길의 정찰자", "코인 앞면마다 공격 +1.", CardKind.Unit, Element.Dawn, 1, 2, 3, 1),
            new CardDefinition("dawn_guard", "서약의 수호자", "코인 앞면마다 공격 +1. 오래 버티는 수호자.", CardKind.Unit, Element.Dawn, 2, 2, 5, 1),
            new CardDefinition("dawn_lancer", "태양창 기사", "코인 앞면마다 공격 +2.", CardKind.Unit, Element.Dawn, 3, 4, 4, 2),
            new CardDefinition("tide_wisp", "물안개 요정", "코인 앞면마다 공격 +2.", CardKind.Unit, Element.Tide, 1, 1, 3, 2),
            new CardDefinition("tide_keeper", "샘의 기록자", "코인 앞면마다 공격 +1.", CardKind.Unit, Element.Tide, 2, 3, 4, 1),
            new CardDefinition("tide_serpent", "은비늘 해룡", "코인 앞면마다 공격 +2.", CardKind.Unit, Element.Tide, 3, 3, 6, 2),
            new CardDefinition("dawn_mend", "여명의 기도", "선택한 아군 유닛의 피해 3 회복.", CardKind.Spell, Element.Dawn, 1, effect: Effect.Heal, magnitude: 3),
            new CardDefinition("tide_bolt", "파도의 화살", "선택한 적 유닛에게 피해 2. 방어구 적용.", CardKind.Spell, Element.Tide, 1, effect: Effect.Damage, magnitude: 2),
            new CardDefinition("dawn_oath", "함께한 서약", "선택한 아군 유닛의 공격 +1. 전투 동안 지속.", CardKind.Spell, Element.Dawn, 1, effect: Effect.Rally, magnitude: 1),
            new CardDefinition("dawn_blade", "유성검", "장착 유닛의 공격 +1.", CardKind.Equipment, Element.Dawn, 1, effect: Effect.Power, magnitude: 1),
            new CardDefinition("tide_mail", "물결 갑옷", "장착 유닛이 받는 피해마다 1 감소.", CardKind.Equipment, Element.Tide, 1, effect: Effect.Armor, magnitude: 1),
            new CardDefinition("tide_charm", "달의 표식", "장착 유닛의 전투 코인 +1.", CardKind.Equipment, Element.Tide, 1, effect: Effect.Coin, magnitude: 1),
            new CardDefinition("dawn_grove", "꽃그늘 숲", "내 턴 시작에 여명 1. 전개 즉시 여명 1.", CardKind.Terrain, Element.Dawn, 0),
            new CardDefinition("dawn_sanctum", "백화 성소", "내 턴 시작에 여명 1. 전개 즉시 여명 1.", CardKind.Terrain, Element.Dawn, 0),
            new CardDefinition("tide_observatory", "월광 천문대", "내 턴 시작에 조류 1. 전개 즉시 조류 1.", CardKind.Terrain, Element.Tide, 0),
            new CardDefinition("tide_archive", "별바다 서고", "내 턴 시작에 조류 1. 전개 즉시 조류 1.", CardKind.Terrain, Element.Tide, 0),
            new CardDefinition("dawn_sprite", "꽃잎의 전령", "가벼운 전개. 코인 앞면마다 공격 +2.", CardKind.Unit, Element.Dawn, 1, 1, 2, 2),
            new CardDefinition("dawn_archer", "숲빛 사수", "날카로운 화살. 코인 앞면마다 공격 +2.", CardKind.Unit, Element.Dawn, 2, 3, 3, 2),
            new CardDefinition("dawn_falcon", "바람깃 매", "코인 앞면마다 공격 +3. 높은 변동성의 기습자.", CardKind.Unit, Element.Dawn, 2, 2, 3, 3),
            new CardDefinition("dawn_paladin", "금빛 성기사", "기본 방어 1. 코인 앞면마다 공격 +1.", CardKind.Unit, Element.Dawn, 3, 3, 6, 1, Effect.Armor, 1),
            new CardDefinition("dawn_captain", "백화의 기사단장", "튼튼한 중장 기사. 코인 앞면마다 공격 +1.", CardKind.Unit, Element.Dawn, 4, 5, 7, 1),
            new CardDefinition("tide_apprentice", "물거울 견습생", "첫 전선을 지키는 마법사. 코인 앞면마다 공격 +1.", CardKind.Unit, Element.Tide, 1, 2, 2, 1),
            new CardDefinition("tide_oracle", "달호수 예언자", "기본 방어 1. 코인 앞면마다 공격 +1.", CardKind.Unit, Element.Tide, 2, 2, 4, 1, Effect.Armor, 1),
            new CardDefinition("tide_siren", "파도노래 무희", "코인 앞면마다 공격 +2.", CardKind.Unit, Element.Tide, 2, 3, 3, 2),
            new CardDefinition("tide_dragon", "별바다 용", "별바다의 마지막 수호자. 코인 앞면마다 공격 +2.", CardKind.Unit, Element.Tide, 4, 4, 8, 2),
            new CardDefinition("tide_astrologer", "성월의 점성술사", "기본 전투 코인 +1. 코인 앞면마다 공격 +1.", CardKind.Unit, Element.Tide, 3, 3, 5, 1, Effect.Coin, 1),
            new CardDefinition("dawn_bloom", "꽃의 치유", "선택한 아군 유닛의 피해 4 회복.", CardKind.Spell, Element.Dawn, 2, effect: Effect.Heal, magnitude: 4),
            new CardDefinition("dawn_chorus", "기사단의 합창", "선택한 아군 유닛의 공격 +2. 전투 동안 지속.", CardKind.Spell, Element.Dawn, 2, effect: Effect.Rally, magnitude: 2),
            new CardDefinition("dawn_radiance", "햇살의 씨앗", "여명 자원 2 획득. 대상 없이 사용.", CardKind.Spell, Element.Dawn, 0, effect: Effect.Resource, magnitude: 2),
            new CardDefinition("tide_insight", "별자리 해독", "카드 2장 뽑기. 손패 한도 초과분은 버림. 대상 없이 사용.", CardKind.Spell, Element.Tide, 2, effect: Effect.Draw, magnitude: 2),
            new CardDefinition("tide_frost", "서리창", "선택한 적 유닛에게 피해 4. 방어구 적용.", CardKind.Spell, Element.Tide, 2, effect: Effect.Damage, magnitude: 4),
            new CardDefinition("tide_tempest", "달빛 폭풍", "모든 적 유닛에게 피해 2. 방어구 적용. 대상 없이 사용.", CardKind.Spell, Element.Tide, 3, effect: Effect.Sweep, magnitude: 2),
            new CardDefinition("dawn_longbow", "꽃가지 장궁", "장착 유닛의 공격 +2.", CardKind.Equipment, Element.Dawn, 2, effect: Effect.Power, magnitude: 2),
            new CardDefinition("dawn_aegis", "여명 방패", "장착 유닛이 받는 피해마다 1 감소.", CardKind.Equipment, Element.Dawn, 1, effect: Effect.Armor, magnitude: 1),
            new CardDefinition("tide_grimoire", "별술사의 마도서", "장착 유닛의 전투 코인 +2.", CardKind.Equipment, Element.Tide, 2, effect: Effect.Coin, magnitude: 2),
            new CardDefinition("tide_crown", "월광 왕관", "장착 유닛이 받는 피해마다 2 감소.", CardKind.Equipment, Element.Tide, 3, effect: Effect.Armor, magnitude: 2)
        };
        public static CardDefinition Get(string id)
        {
            var card = Array.Find(All, x => x.id == id);
            if (card == null) throw new ArgumentException("알 수 없는 카드 ID: " + id);
            return card;
        }
        public static void ApplyAuthoredDefinitions(IEnumerable<CardDefinition> definitions)
        {
            var authored = definitions.ToArray();
            if (authored.Length != All.Length || authored.Select(d => d.id).Distinct().Count() != All.Length || authored.Any(d => !All.Any(c => c.id == d.id))) throw new ArgumentException("제작 데이터의 안정적인 카드 ID " + All.Length + "종이 필요합니다.");
            foreach (var d in authored)
                if (string.IsNullOrWhiteSpace(d.name) || d.cost < 0 || d.cost > 100 || d.attack < 0 || d.health < 0 || d.coinBonus < 0 || d.coinBonus > 20 || d.magnitude < 0 || d.magnitude > 20 || !Enum.IsDefined(typeof(CardKind), d.kind) || !Enum.IsDefined(typeof(Element), d.element) || !Enum.IsDefined(typeof(Effect), d.effect) || (d.kind == CardKind.Unit && d.health < 1)) throw new ArgumentException("잘못된 카드 제작 데이터: " + d.id);
            for (int i = 0; i < All.Length; i++) All[i] = authored.Single(d => d.id == All[i].id);
        }
        public static List<string> DefaultDeck(bool opponent = false)
        {
            var cards = new List<string>();
            // Preserve the original tutorial deck and deterministic seed behavior.
            // The four expanded starter decks are authored separately in DemoCatalog.
            foreach (var card in All.Take(16))
            {
                int count = card.kind == CardKind.Terrain ? 2 : 1;
                for (int i = 0; i < count; i++) cards.Add(card.id);
            }
            cards.AddRange(opponent
                ? new[] { "tide_wisp", "tide_keeper", "dawn_scout", "tide_serpent" }
                : new[] { "dawn_scout", "dawn_guard", "tide_wisp", "dawn_lancer" });
            return cards;
        }
        public static string KindName(CardKind kind) { return new[] { "지형", "유닛", "마법", "장비" }[(int)kind]; }
        public static string ElementName(Element element) { return element == Element.Dawn ? "여명" : "조류"; }
        public static bool RequiresUnitTarget(CardDefinition card)
        {
            return card.kind == CardKind.Equipment || (card.kind == CardKind.Spell && card.effect != Effect.Draw && card.effect != Effect.Resource && card.effect != Effect.Sweep);
        }
    }
}
