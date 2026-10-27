using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    public sealed partial class FantasyCardsRevivalSmokeTests
    {
        [UnityTest]
        public IEnumerator FidelityGradeGuideAndRankCapabilityFiltersUseActualCards()
        {
            SmokeUi.Click("게임 시작"); yield return null;
            SmokeUi.Click("등급 / 랭크"); yield return null;
            var guide = ActiveButtons("Grade guide ");
            Assert.That(guide.Length, Is.EqualTo(12), "Every documented grade must remain visible, including explicitly unsupplied grades.");
            foreach (CardRarity rarity in Enum.GetValues(typeof(CardRarity)))
            {
                var tile = guide.Single(button => button.gameObject.name == "Grade guide " + rarity);
                var labels = tile.GetComponentsInChildren<TMP_Text>().Select(text => text.text).ToArray();
                Assert.That(labels, Does.Contain(CardCatalog.RarityName(rarity)));
                Assert.That(labels, Does.Contain(CardCatalog.RarityCode(rarity)));
                Assert.That(labels, Does.Contain("수록 " + CardCatalog.All.Count(card => card.rarity == rarity) + "종"));
            }
            yield return SmokeUi.Capture("FantasyCards", "arcana-grade-guide", 844, 390);
            ActiveButtons("Grade guide ").Single(button => button.gameObject.name == "Grade guide Rare").onClick.Invoke(); yield return null;
            Assert.That(VisibleCards(), Is.Not.Empty);
            Assert.That(VisibleCards().All(card => card.rarity == CardRarity.Rare), Is.True);
            SmokeUi.Click("검색 / 필터 초기화"); yield return null;
            SmokeUi.Click("랭크 · 전체"); yield return null;
            int normal = CardCatalog.All.Count(card => card.kind == CardKind.Unit && card.rank == CardRank.N);
            SmokeUi.Click("랭크 N · " + normal + "종"); yield return null;
            Assert.That(VisibleCards(), Is.Not.Empty);
            Assert.That(VisibleCards().All(card => card.kind == CardKind.Unit && card.rank == CardRank.N), Is.True);
            foreach (var cardButton in ActiveButtons("Card "))
            {
                var identity = cardButton.GetComponentInChildren<FmArcanaCardSurface>();
                Assert.That(CardCatalog.Get(identity.DefinitionId).rank, Is.EqualTo(CardRank.N));
                var grade = cardButton.GetComponentsInChildren<TMP_Text>().Single(text => text.gameObject.name == "Card grade code");
                var definition = CardCatalog.Get(cardButton.gameObject.name.Substring("Card ".Length));
                Assert.That(grade.text, Does.StartWith(CardCatalog.RarityCode(definition.rarity)));
            }
            SmokeUi.Click("능력 · 전체"); yield return null;
            int equipment = CardCatalog.All.Count(card => card.kind == CardKind.Unit && (card.capabilityMask & 1) != 0);
            SmokeUi.Click("장비 · " + equipment + "종"); yield return null;
            Assert.That(VisibleCards().All(card => card.kind == CardKind.Unit && (card.capabilityMask & 1) != 0), Is.True);
            SmokeUi.Click("검색 / 필터 초기화"); yield return null;
            SmokeUi.Click("대기실"); yield return null;
            SmokeUi.Click("편집 덱 분석"); yield return null;
            Assert.That(SmokeUi.FindButton("소울 곡선"), Is.Not.Null);
            SmokeUi.Click("속성 / 유지비"); yield return null;
            Assert.That(SmokeUi.HasText("유지비 합계"), Is.True);
            SmokeUi.Click("등급 / 랭크"); yield return null;
            Assert.That(SmokeUi.HasText("유닛 랭크"), Is.True);
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest]
        public IEnumerator FidelityRealDragAndOrderedPlaybackPreserveBattleAndRandomness()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var dragEngine = new BattleEngine(79, new RuleSet(), DemoCatalog.GetDeck("forest").CopyCards());
            Assert.That(dragEngine.ConfirmOpeningHand(0, out _), Is.True);
            FixtureHand(dragEngine, CardKind.Terrain, 0);
            FixtureHand(dragEngine, CardKind.Unit, 1);
            FixtureSouls(dragEngine, 0); dragEngine.State.players[0].levelPoints = 2;
            BattleEngine.Validate(dragEngine.State); InstallFidelityFixture(app, dragEngine);
            var terrain = dragEngine.State.players[0].hand.First(card => CardCatalog.Get(card.definitionId).kind == CardKind.Terrain);
            string original = JsonUtility.ToJson(dragEngine.State);
            DragHandEvent(terrain.instanceId, GameObject.Find("Lane 1 0")); yield return null;
            Assert.That(JsonUtility.ToJson(dragEngine.State), Is.EqualTo(original), "A terrain dropped on the enemy front must not consume LP or a card.");
            int lp = dragEngine.State.players[0].levelPoints;
            var terrainDefinition = CardCatalog.Get(terrain.definitionId);
            DragHandEvent(terrain.instanceId, GameObject.Find("Terrain drop band"));
            yield return SmokeUi.WaitUntil(() => !app.IsBattlePlaybackActive, 8, "Actual legal terrain drag must finish its presentation.");
            Assert.That(dragEngine.State.players[0].terrain.Any(card => card.instanceId == terrain.instanceId), Is.True);
            Assert.That(dragEngine.State.players[0].levelPoints, Is.EqualTo(lp - terrainDefinition.landPointCost));
            var backfield = GameObject.Find("Backfield 0 0");
            Assert.That(backfield, Is.Not.Null, "A played terrain must remain visible in its actual backfield slot.");
            Assert.That(backfield.GetComponentsInChildren<RawImage>().Any(image => image.texture != null
                && image.texture.name == System.IO.Path.GetFileName(terrainDefinition.artResource)), Is.True,
                "The backfield must show the deployed original terrain artwork, not just a resource pip.");
            Assert.That(backfield.GetComponent<Button>(), Is.Not.Null);
            backfield.GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(SmokeUi.HasText(terrainDefinition.name), Is.True);
            SmokeUi.Click("돌아가기"); yield return null;
            var unit = dragEngine.State.players[0].hand.First(card => CardCatalog.Get(card.definitionId).kind == CardKind.Unit);
            int soul = dragEngine.State.players[0].genericSoul;
            DragHandEvent(unit.instanceId, GameObject.Find("Lane 0 2"));
            yield return SmokeUi.WaitUntil(() => !app.IsBattlePlaybackActive, 8, "Actual legal unit drag must finish its presentation.");
            Assert.That(dragEngine.State.players[0].AtLane(2).instanceId, Is.EqualTo(unit.instanceId));
            Assert.That(dragEngine.State.players[0].genericSoul, Is.EqualTo(soul - CardCatalog.Get(unit.definitionId).cost));
            BattleEngine.Validate(dragEngine.State);
            yield return SmokeUi.Capture("FantasyCards", "arcana-critical-deployed-field", 1280, 720, AssertArcanaSurfaces);

            // Native-only legal deterministic fixture. Browser checks use ordinary
            // pointer/touch input and never inject this state.
            var engine = new BattleEngine(109, new RuleSet { baseCoins = 2 }, DemoCatalog.GetDeck("forest").CopyCards());
            Assert.That(engine.ConfirmOpeningHand(0, out _), Is.True);
            engine.State.activePlayer = 1;
            for (int side = 0; side < 2; side++) FixtureSouls(engine, side);
            FixtureUnit(engine, 0, 0, 0);
            FixtureUnit(engine, 1, 0, 30);
            FixtureUnit(engine, 1, 2, 30);
            FixtureUnit(engine, 1, 4, 30);
            Assert.That(engine.CompletePreparation(1, out _), Is.True);
            BattleEngine.Validate(engine.State); InstallFidelityFixture(app, engine);
            var expected = new BattleEngine(engine.State.Copy());
            int previousEvents = expected.State.events.Count;
            Assert.That(expected.CompletePreparation(0, out _), Is.True);
            string finalState = JsonUtility.ToJson(expected.State);
            var ready = SmokeUi.FindButton("준비 완료").onClick;
            SmokeUi.Click("준비 완료");
            Assert.That(app.IsBattlePlaybackActive, Is.True);
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(finalState), "Presentation must use the already resolved actual Core result.");
            Assert.That(JsonUtility.ToJson(app.BattlePlaybackDisplaySnapshot), Is.Not.EqualTo(finalState), "The view must begin before the damage/defeat instead of drawing the final board first.");
            ready.Invoke();
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(finalState), "A queued duplicate Ready callback during playback must not advance the game twice.");
            var speed = ActiveButtons("Battle playback speed").Single(); speed.onClick.Invoke();
            Assert.That(app.BattlePlaybackSpeed, Is.EqualTo(2));
            Assert.That(engine.State.randomState, Is.EqualTo(expected.State.randomState));
            yield return SmokeUi.WaitUntil(() => app.BattlePlaybackEventKind == "coin", 8, "The real coin event must be visible in sequence.");
            Assert.That(UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .Any(view => view.gameObject.activeInHierarchy && view.gameObject.name == "Central actual coin tray"), Is.True);
            var shownCoin = app.LastBattlePlaybackEvents.Last(item => item.kind == "coin");
            Assert.That(SmokeUi.HasText("C" + shownCoin.coinIndex + " · " + (shownCoin.coinHeads ? "앞면" : "뒷면")), Is.True,
                "The original-style horizontal tray must display this exact Core result.");
            ActiveButtons("Battle playback skip").Single().onClick.Invoke();
            yield return SmokeUi.WaitUntil(() => !app.IsBattlePlaybackActive, 8, "Skip changes only presentation time.");
            var events = app.LastBattlePlaybackEvents.ToArray();
            Assert.That(events.Length, Is.EqualTo(expected.State.events.Count - previousEvents));
            Assert.That(events.Length, Is.GreaterThan(18), "Long exchanges must not be truncated to the old18-event limit.");
            Assert.That(Array.FindIndex(events, item => item.kind == "coin"), Is.LessThan(Array.FindIndex(events, item => item.kind == "attack")));
            Assert.That(Array.FindIndex(events, item => item.kind == "attack"), Is.LessThan(Array.FindIndex(events, item => item.kind == "defeat")));
            Assert.That(events.Any(item => item.kind == "burn"), Is.True);
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(finalState));
            Assert.That(JsonUtility.ToJson(app.BattlePlaybackDisplaySnapshot), Is.EqualTo(finalState));
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadSavedBattle()), Is.EqualTo(finalState), "Save must contain the Core result, not an intermediate view.");
            BattleEngine.Validate(engine.State); SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        static void FixtureSouls(BattleEngine engine, int side)
        {
            var player = engine.State.players[side];
            foreach (Element element in Enum.GetValues(typeof(Element))) player.SetResource(element, 30);
            player.genericSoul = 30;
        }

        static void FixtureHand(BattleEngine engine, CardKind kind, int slot)
        {
            var player = engine.State.players[0];
            int hand = player.hand.FindIndex(card => CardCatalog.Get(card.definitionId).kind == kind);
            var displaced = player.hand[slot];
            if (hand >= 0) { player.hand[slot] = player.hand[hand]; player.hand[hand] = displaced; }
            else
            {
                int deck = player.deck.FindIndex(card => CardCatalog.Get(card.definitionId).kind == kind);
                Assert.That(deck, Is.GreaterThanOrEqualTo(0));
                player.hand[slot] = player.deck[deck]; player.deck[deck] = displaced;
            }
        }

        static void FixtureUnit(BattleEngine engine, int side, int lane, int bonus)
        {
            var player = engine.State.players[side];
            var card = player.deck.First(value => CardCatalog.Get(value.definitionId).kind == CardKind.Unit);
            player.deck.Remove(card);
            player.units.Add(new UnitState { instanceId = card.instanceId, definitionId = card.definitionId, lane = lane, bonusPower = bonus });
        }

        static void InstallFidelityFixture(FantasyCardsApp app, BattleEngine engine)
        {
            app.StopAllCoroutines();
            var flags = System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic;
            typeof(FantasyCardsApp).GetField("engine", flags).SetValue(app, engine);
            typeof(FantasyCardsApp).GetField("locked", flags).SetValue(app, false);
            typeof(FantasyCardsApp).GetField("battleVisualState", flags).SetValue(app, null);
            typeof(FantasyCardsApp).GetMethod("ShowBattle", flags).Invoke(app, null);
            Canvas.ForceUpdateCanvases();
        }

        static void DragHandEvent(int instanceId, GameObject target)
        {
            Assert.That(target, Is.Not.Null);
            var source = UnityEngine.Object.FindObjectsByType<FmCardDragDrop>(FindObjectsSortMode.None)
                .Single(value => value.isActiveAndEnabled && value.InstanceId == instanceId);
            var data = new PointerEventData(EventSystem.current) {
                button = PointerEventData.InputButton.Left, delta = new Vector2(0, 60),
                position = RectTransformUtility.WorldToScreenPoint(null, target.transform.position),
                pointerCurrentRaycast = new RaycastResult { gameObject = target }
            };
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.endDragHandler);
        }
    }
}
