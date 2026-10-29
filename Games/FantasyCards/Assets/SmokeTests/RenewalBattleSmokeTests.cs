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
    public sealed partial class RenewalSmokeTests
    {
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
            backfield.GetComponent<Button>().onClick.Invoke();
            Assert.That(SmokeUi.FindButton("돌아가기"), Is.Null, "A single public-card click now selects instead of opening detail.");
            GameObject.Find("Backfield 0 0").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(SmokeUi.HasText(terrainDefinition.name), Is.True);
            SmokeUi.Click("돌아가기"); yield return null;
            var unit = dragEngine.State.players[0].hand.First(card => CardCatalog.Get(card.definitionId).kind == CardKind.Unit);
            int soul = dragEngine.State.players[0].genericSoul;
            DragHandEvent(unit.instanceId, GameObject.Find("Lane 0 2"));
            yield return SmokeUi.WaitUntil(() => !app.IsBattlePlaybackActive, 8, "Actual legal unit drag must finish its presentation.");
            Assert.That(dragEngine.State.players[0].AtLane(2).instanceId, Is.EqualTo(unit.instanceId));
            Assert.That(dragEngine.State.players[0].genericSoul, Is.EqualTo(soul - CardCatalog.Get(unit.definitionId).cost));
            BattleEngine.Validate(dragEngine.State);
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-critical-deployed-field", 1280, 720);

            // Native-only legal deterministic fixture. Browser checks use ordinary
            // pointer/touch input and never inject this state.
            var engine = new BattleEngine(109, new RuleSet { baseCoins = 2 }, DemoCatalog.GetDeck("forest").CopyCards());
            Assert.That(engine.ConfirmOpeningHand(0, out _), Is.True);
            engine.State.activePlayer = 1;
            for (int side = 0; side < 2; side++) FixtureSouls(engine, side);
            for (int side = 0; side < 2; side++)
            for (int land = 0; land < 3; land++)
            {
                var actualTerrain = engine.State.players[side].deck.First(c => CardCatalog.Get(c.definitionId).kind == CardKind.Terrain);
                engine.State.players[side].deck.Remove(actualTerrain);
                engine.State.players[side].terrain.Add(actualTerrain);
            }
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
            var expectedChangedCoin=expected.State.events.Skip(previousEvents).First(e=>e.kind=="coin"&&e.hasStatTransition&&(e.beforeAttack!=e.afterAttack||e.beforeDefense!=e.afterDefense||e.beforeHealth!=e.afterHealth));
            yield return SmokeUi.WaitUntil(() => app.BattlePlaybackEventKind == "coin" && app.LastBattlePlaybackEvents.LastOrDefault(e=>e.kind=="coin") is BattleEvent e && e.sourceInstanceId==expectedChangedCoin.sourceInstanceId && e.coinIndex==expectedChangedCoin.coinIndex,
                20, "A real coin that changes this card's stats must be visible in sequence.");
            Assert.That(app.ChronicleVisibleCoinPanels, Is.EqualTo(2), "An actual two-unit clash must show both source-style result windows.");
            var shownCoin = app.LastBattlePlaybackEvents.Last(item => item.kind == "coin");
            string[] statLabels = { "AP", "DP", "HP" };
            int[] expectedValues = { shownCoin.afterAttack, shownCoin.afterDefense, Math.Max(0, shownCoin.afterHealth) };
            for (int stat = 0; stat < 3; stat++)
            {
                var printed = GameObject.Find("Card live " + statLabels[stat] + " " + shownCoin.sourceInstanceId);
                Assert.That(printed, Is.Not.Null);
                Assert.That(printed.GetComponent<TMP_Text>().text, Is.EqualTo(expectedValues[stat].ToString()),
                    "The actual source numeral area must update to the real transition; no extra AP/DP/HP bars.");
                var rect = printed.GetComponent<RectTransform>();
                Assert.That(rect.anchorMin.x, Is.GreaterThan(.85f));
                Assert.That(rect.anchorMax.y, Is.LessThan(.24f));
            }
            for (int owner = 0; owner < 2; owner++)
            {
                var slots = UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                    .Where(view => view.gameObject.activeInHierarchy && view.name.StartsWith("Coin slot " + owner + " ")).ToArray();
                Assert.That(slots.Length, Is.EqualTo(5), "Each real side window retains all five outlined source slots.");
                Assert.That(GameObject.Find("Actual coin chance " + owner), Is.Not.Null);
            }
            var face = GameObject.Find("Resolved coin " + shownCoin.sourcePlayer + " " + shownCoin.coinIndex + (shownCoin.coinHeads ? " heads" : " tails"));
            Assert.That(face, Is.Not.Null, "The round token must show this exact resolved face from Core.");
            Assert.That(GameObject.Find("Chronicle battle terrain strip"), Is.Not.Null, "Focused battle shows actual own full terrain instead of the preparation hand.");
            Action inspectLiveLayers = () =>
            {
                Assert.That(app.ChronicleVisibleCoinPanels, Is.EqualTo(2));
                Assert.That(UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                    .Count(v => v.gameObject.activeInHierarchy && v.name.StartsWith("Chronicle coin result ")), Is.EqualTo(2),
                    "Both actual coin windows must survive viewport setup through camera submission.");
                Assert.That(GameObject.Find("Chronicle battle terrain strip").GetComponentsInChildren<RawImage>().Length,
                    Is.GreaterThanOrEqualTo(3), "The fixture has three actual full terrain prints, not an empty placeholder.");
            };
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-on-card-coin-result",1280,720,inspect:inspectLiveLayers,settleSeconds:0f);
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-on-card-coin-result-mobile",844,390,inspect:inspectLiveLayers,settleSeconds:0f);
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
