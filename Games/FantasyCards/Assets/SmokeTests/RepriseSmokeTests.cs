using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    public sealed class RepriseSmokeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        IDisposable storage, preferences;
        MemorySaveBackend backend;
        string attempt;
        FantasyCardsApp App => UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
        void Call(string method, params object[] args) => typeof(FantasyCardsApp).GetMethod(method, Private).Invoke(App, args);
        void Set(string field, object value) => typeof(FantasyCardsApp).GetField(field, Private).SetValue(App, value);
        IEnumerator Shot(string name, int width = 1280, int height = 720, Action inspect = null) =>
            SmokeUi.Capture("FantasyCards", "reprise-" + attempt + "-" + name, width, height, inspect: inspect);
        static Button NamedButton(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button => button.gameObject.activeInHierarchy && button.name == name);

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt = Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT") ?? DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend(); backend.WriteAtomic("preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend); preferences = SmokeUi.PreserveDemoPreferences();
            yield return SmokeUi.LoadBoot();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return SmokeUi.UnloadBoot(); preferences?.Dispose(); storage?.Dispose();
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator OneAlbumRetainsEveryOriginalAndFiltersRealGradesAndSupport()
        {
            Assert.That(SmokeUi.FindButton("상위 등급"), Is.Null);
            SmokeUi.Click("카드 앨범"); yield return null;
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("album"));
            Assert.That(App.OriginalArchiveFilteredCount, Is.EqualTo(15350));
            yield return Shot("complete-card-album", inspect: () => Assert.That(App.OriginalArchiveVisibleCards, Is.EqualTo(12)));
            yield return Shot("complete-card-album-touch", 616, 346, () =>
            {
                Assert.That(App.OriginalArchiveVisibleCards, Is.EqualTo(4), "Touch defaults to large readable original cards.");
                AssertAlbumTextGeometry();
            });
            yield return SmokeUi.Capture("FantasyCards", "reprise-" + attempt + "-compact-card-album-touch", 616, 346,
                beforeCapture: () => SmokeUi.Click("앨범 보기"), inspect: () =>
                {
                    Assert.That(App.OriginalArchiveVisibleCards, Is.EqualTo(6));
                    AssertAlbumTextGeometry();
                });
            var gradeButton = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.gameObject.activeInHierarchy && button.GetComponentsInChildren<TMP_Text>().Any(text => text.text == "등급 · 전체"));
            gradeButton.onClick.Invoke(); yield return null;
            var unique = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.gameObject.activeInHierarchy && button.GetComponentsInChildren<TMP_Text>().Any(text => text.text.StartsWith("유니크 · ")));
            unique.onClick.Invoke(); yield return null;
            Assert.That(App.OriginalArchiveGrade, Is.EqualTo((int)CardRarity.Unique));
            Assert.That(App.OriginalArchiveFilteredCount, Is.EqualTo(OriginalCardArchive.All.Count(card => card.rarity == CardRarity.Unique)));
            yield return Shot("unified-unique-album");
            SmokeUi.Click("초기화"); yield return null;
            SmokeUi.Click("전체 원본 카드"); yield return null;
            Assert.That(App.OriginalArchiveFilteredCount, Is.EqualTo(CardCatalog.All.Length), "The support filter must use actual supported definitions, never call all15350 playable.");
            SmokeUi.Click("큰 카드 보기"); yield return null;
            yield return Shot("supported-large-cards", inspect: () => Assert.That(App.OriginalArchiveVisibleCards, Is.EqualTo(4)));
            var first = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .First(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Album card "));
            first.onClick.Invoke(); yield return null;
            Assert.That(SmokeUi.FindButton("도감으로"), Is.Not.Null);
            Assert.That(SmokeUi.FindButton("덱에서 보기"), Is.Not.Null);
            Assert.That(backend.Read("preserved-player"), Is.EqualTo("original player bytes"));
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        static void AssertAlbumTextGeometry()
        {
            var labels = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Where(text => text.gameObject.activeInHierarchy && (text.name.StartsWith("Album card name ") || text.name.StartsWith("Album card grade "))).ToArray();
            Assert.That(labels.Length, Is.GreaterThan(0));
            foreach (var text in labels)
            {
                text.ForceMeshUpdate();
                Assert.That(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(character => character.isVisible), Is.True,
                    "An album name or grade must have actual visible glyph geometry: " + text.name);
            }
        }

        // Native-only deterministic input fixture: two real printed definitions
        // are dealt from a legal fifty-card deck, then installed on the field.
        // This isolates white-paper and gold-foil contrast; it is not a claimed
        // naturally drawn battle or a browser pointer acceptance test.
        BattleEngine PrintedFixture()
        {
            var ids = CardCatalog.DefaultDeck();
            string[] prints = { "fm_3042520487", "fm_11101925983" };
            foreach (string id in prints)
                if (!ids.Contains(id)) ids[ids.FindIndex(value => CardCatalog.Get(value).kind == CardKind.Unit && !prints.Contains(value))] = id;
            var engine = new BattleEngine(713, playerDeck: ids);
            Assert.That(engine.ConfirmOpeningHand(0, out _), Is.True);
            for (int lane = 0; lane < prints.Length; lane++)
            {
                var p = engine.State.players[0];
                var instance = p.deck.Concat(p.hand).First(card => card.definitionId == prints[lane]);
                p.deck.Remove(instance); p.hand.Remove(instance);
                p.units.Add(new UnitState { instanceId = instance.instanceId, definitionId = instance.definitionId, lane = lane, damage = lane });
            }
            BattleEngine.Validate(engine.State); return engine;
        }
        void Install(BattleEngine engine)
        {
            App.StopAllCoroutines(); Set("engine", engine); Set("locked", false); Set("battleVisualState", null);
            Set("battlePlaybackActive", false); Set("questBattleMode", false); Set("selectedCard", -1); Set("selectedAttacker", -1);
            PlayerPrefs.SetInt("FC.quest.lastBattleMode", 0);
            Call("ShowBattle"); Canvas.ForceUpdateCanvases();
        }
        void AssertNumerals(BattleEngine engine)
        {
            foreach (var unit in engine.State.players[0].units)
            {
                string[] names = { "AP", "DP", "HP" };
                int[] values = { CardCatalog.UnitAttack(unit), CardCatalog.UnitDefense(unit), Math.Max(0, CardCatalog.UnitHealth(unit)) };
                for (int i = 0; i < names.Length; i++)
                {
                    var plate = GameObject.Find("Opaque " + names[i] + " numeral plate " + unit.instanceId).GetComponent<UiRoundedImage>();
                    Assert.That(plate.color.a, Is.EqualTo(1));
                    Assert.That(Math.Max(plate.color.r, Math.Max(plate.color.g, plate.color.b)), Is.LessThan(.04f));
                    var text = GameObject.Find("Card live " + names[i] + " " + unit.instanceId).GetComponent<TMP_Text>();
                    Assert.That(text.text, Is.EqualTo(values[i].ToString()));
                    Assert.That(text.color.r, Is.GreaterThan(.95f));
                    text.ForceMeshUpdate();
                    Assert.That(text.textInfo.characterInfo.Take(text.textInfo.characterCount).Any(character => character.isVisible), Is.True,
                        "The actual scaled card numeral must have visible TMP geometry: " + text.name);
                    Assert.That(text.rectTransform.anchorMin.x, Is.GreaterThan(.85f));
                    Assert.That(text.rectTransform.anchorMax.y, Is.LessThan(.24f));
                }
            }
        }
        [UnityTest] public IEnumerator WhitePaperAndGoldFoilKeepReadableActualOnCardValues()
        {
            var engine = PrintedFixture(); Install(engine); string original = JsonUtility.ToJson(engine.State);
            yield return Shot("white-gold-actual-stat-plates", inspect: () => AssertNumerals(engine));
            yield return Shot("white-gold-actual-stat-plates-touch", 616, 346, () => AssertNumerals(engine));
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(original), "Rendering contrast must not mutate a card, a coin or Core RNG.");
            Assert.That(GameObject.Find("Card live HP " + engine.State.players[0].AtLane(1).instanceId).GetComponent<TMP_Text>().text, Is.EqualTo("6"));
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ActualSummonAndDuelEffectsDoNotOwnDamageOrRng()
        {
            TestContext.Out.WriteLine("REPRISE_FX_STAGE create-fixture");
            var engine = new BattleEngine(142, playerDeck: DemoCatalog.GetDeck("forest").CopyCards());
            Assert.That(engine.ConfirmOpeningHand(0, out _), Is.True);
            var p = engine.State.players[0];
            foreach (Element element in Enum.GetValues(typeof(Element))) p.SetResource(element, 30);
            p.genericSoul = 30;
            var unit = p.hand.FirstOrDefault(card => CardCatalog.Get(card.definitionId).kind == CardKind.Unit);
            if (unit == null)
            {
                unit = p.deck.First(card => CardCatalog.Get(card.definitionId).kind == CardKind.Unit);
                var displaced = p.hand[0]; p.hand[0] = unit; p.deck.Remove(unit); p.deck.Add(displaced);
            }
            TestContext.Out.WriteLine("REPRISE_FX_STAGE install unit=" + unit.instanceId + " definition=" + unit.definitionId);
            Install(engine);
            TestContext.Out.WriteLine("REPRISE_FX_STAGE drag-start");
            Drag(unit.instanceId, GameObject.Find("Lane 0 0"));
            TestContext.Out.WriteLine("REPRISE_FX_STAGE drag-complete playback=" + App.IsBattlePlaybackActive + " kind=" + App.BattlePlaybackEventKind);
            string resolvedSummon = JsonUtility.ToJson(engine.State);
            Assert.That(p.AtLane(0).instanceId, Is.EqualTo(unit.instanceId));
            yield return SmokeUi.WaitUntil(() => UnityEngine.Object.FindObjectsByType<FmBattleFxGraphic>(FindObjectsSortMode.None)
                .Any(fx => fx.isActiveAndEnabled && fx.Style == FmBattleFxGraphic.EffectStyle.Summon && fx.Progress > .12f), 3,
                "A real played unit must have a live summon effect and flying original card.");
            TestContext.Out.WriteLine("REPRISE_FX_STAGE summon-visible");
            yield return SmokeUi.Capture("FantasyCards", "reprise-" + attempt + "-actual-unit-summon-effect", 1280, 720, settleSeconds: 0);
            TestContext.Out.WriteLine("REPRISE_FX_STAGE summon-captured");
            yield return SmokeUi.WaitUntil(() => !App.IsBattlePlaybackActive, 8, "The actual unit summon did not finish.");
            TestContext.Out.WriteLine("REPRISE_FX_STAGE summon-finished");
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(resolvedSummon));

            // Continue the real summoned card with one native-only enemy unit
            // fixture so a complete Core duel reliably occurs in this test.
            var enemy = engine.State.players[1];
            foreach (Element element in Enum.GetValues(typeof(Element))) enemy.SetResource(element, 30);
            enemy.genericSoul = 30;
            var instance = enemy.deck.First(card => CardCatalog.Get(card.definitionId).kind == CardKind.Unit);
            enemy.deck.Remove(instance);
            enemy.units.Add(new UnitState { instanceId = instance.instanceId, definitionId = instance.definitionId, lane = 0, bonusPower = 30 });
            engine.State.activePlayer = 1;
            Assert.That(engine.CompletePreparation(1, out _), Is.True);
            BattleEngine.Validate(engine.State); Install(engine);
            var expected = new BattleEngine(engine.State.Copy());
            Assert.That(expected.CompletePreparation(0, out _), Is.True);
            SmokeUi.Click("준비 완료");
            string resolvedDuel = JsonUtility.ToJson(engine.State);
            Assert.That(resolvedDuel, Is.EqualTo(JsonUtility.ToJson(expected.State)));
            yield return SmokeUi.WaitUntil(() => (App.BattlePlaybackEventKind == "attack" || App.BattlePlaybackEventKind == "counter") &&
                UnityEngine.Object.FindObjectsByType<FmBattleFxGraphic>(FindObjectsSortMode.None).Any(fx => fx.isActiveAndEnabled &&
                    (fx.Style == FmBattleFxGraphic.EffectStyle.Slash || fx.Style == FmBattleFxGraphic.EffectStyle.Shield) && fx.Progress > .12f),
                30, "The actual Core attack did not show its visible damage/shield effect.");
            var attack = App.LastBattlePlaybackEvents.Last(item => item.kind == "attack" || item.kind == "counter");
            Assert.That(expected.State.events.Any(item => item.kind == attack.kind && item.amount == attack.amount && item.sourceInstanceId == attack.sourceInstanceId), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "reprise-" + attempt + "-actual-duel-hit-effect", 1280, 720, settleSeconds: 0);
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(resolvedDuel), "Drawing particles and damage text must not change the already resolved battle.");
            NamedButton("Battle playback skip").onClick.Invoke();
            yield return SmokeUi.WaitUntil(() => !App.IsBattlePlaybackActive, 8, "Skipping duel effects did not finish.");
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(resolvedDuel));
            Assert.That(engine.State.randomState, Is.EqualTo(expected.State.randomState));
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator DamageAudioLoadsAndPlaysRealDistinctVoicesAndHonorsMute()
        {
            var engine = PrintedFixture(); Install(engine); string original = JsonUtility.ToJson(engine.State);
            FmBattleAudio.Initialize(); FmBattleAudio.SetMuted(false); FmBattleAudio.SetVolume(.7f);
            var host = UnityEngine.Object.FindFirstObjectByType<FmSfxHost>();
            Assert.That(host, Is.Not.Null); Assert.That(host.LoadedCueCount, Is.EqualTo(32));
            foreach (string cue in new[] { "shield", "hit", "hit-heavy" })
                yield return SmokeUi.WaitUntil(() =>
                {
                    var clip = Resources.Load<AudioClip>("Audio/FmReprise/" + cue);
                    return clip != null && clip.loadState == AudioDataLoadState.Loaded;
                }, 8, "Current authored effect has not actually loaded: " + cue);
            int[] damage = { 0, 5, 6 }; string[] cues = { "shield", "hit", "hit-heavy" };
            for (int i = 0; i < damage.Length; i++)
            {
                yield return new WaitForSecondsRealtime(.03f);
                int count = FmBattleAudio.PlayedCount;
                FmBattleAudio.PlayImpact(damage[i], .2f);
                Assert.That(FmBattleAudio.PlayedCount, Is.EqualTo(count + 1));
                Assert.That(FmBattleAudio.LastCue, Is.EqualTo(cues[i]));
                var observation = FmBattleAudio.History.Last();
                Assert.That(observation.clip, Is.EqualTo(cues[i])); Assert.That(observation.volume, Is.GreaterThan(0));
                Assert.That(observation.length, Is.GreaterThan(.1f));
                var voice = host.GetComponents<AudioSource>()[observation.voice];
                Assert.That(voice.clip, Is.Not.Null); Assert.That(voice.clip.name, Is.EqualTo(cues[i]));
                Assert.That(voice.isPlaying, Is.True, "The cue observation must correspond to an actually started AudioSource.");
            }
            FmBattleAudio.SetMuted(true); int beforeMute = FmBattleAudio.PlayedCount;
            FmBattleAudio.PlayImpact(9); Assert.That(FmBattleAudio.PlayedCount, Is.EqualTo(beforeMute));
            Assert.That(host.ActiveVoiceCount, Is.Zero);
            FmBattleAudio.SetMuted(false);
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(original), "Audio cannot consume a battle roll or alter any card.");
            LogAssert.NoUnexpectedReceived();
        }

        static void Drag(int instance, GameObject destination)
        {
            Assert.That(destination, Is.Not.Null);
            var source = UnityEngine.Object.FindObjectsByType<FmCardDragDrop>(FindObjectsSortMode.None)
                .Single(drag => drag.isActiveAndEnabled && drag.InstanceId == instance);
            var data = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left,
                delta = new Vector2(0, 60), position = RectTransformUtility.WorldToScreenPoint(null, destination.transform.position),
                pointerCurrentRaycast = new RaycastResult { gameObject = destination } };
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.initializePotentialDrag);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.beginDragHandler);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(source.gameObject, data, ExecuteEvents.endDragHandler);
        }
        [UnityTest] public IEnumerator ActualFieldDragClickSwapAndDisposalPreserveRngAndSave()
        {
            var engine = PrintedFixture(); Install(engine);
            int angel = engine.State.players[0].AtLane(0).instanceId, gold = engine.State.players[0].AtLane(1).instanceId;
            uint random = engine.State.randomState; int soul = engine.State.players[0].genericSoul;
            string original = JsonUtility.ToJson(engine.State);
            Drag(angel, GameObject.Find("Lane 1 0")); yield return null;
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(original), "Dragging an own field card into an enemy lane must not act.");
            Drag(angel, GameObject.Find("Lane 0 2"));
            Assert.That(App.BattlePlaybackEventKind, Is.EqualTo("reposition"));
            yield return SmokeUi.WaitUntil(() => !App.IsBattlePlaybackActive, 8, "Field drag did not finish.");
            Assert.That(engine.State.players[0].AtLane(2).instanceId, Is.EqualTo(angel));
            GameObject.Find("Lane 0 2").GetComponent<Button>().onClick.Invoke(); yield return null;
            Assert.That(SmokeUi.FindButton("선택 카드 소각"), Is.Not.Null);
            string beforeCancelledHandDrag = JsonUtility.ToJson(engine.State);
            var handCard = engine.State.players[0].hand.First();
            Drag(handCard.instanceId, GameObject.Find("Battle resource rail")); yield return null;
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(beforeCancelledHandDrag));
            Assert.That(SmokeUi.FindButton("선택 카드 소각"), Is.Null,
                "After selecting a hand card through a cancelled drag, the action must never dispose of the previously selected field card.");
            Call("HandlePolishBack"); yield return null;
            GameObject.Find("Lane 0 2").GetComponent<Button>().onClick.Invoke(); yield return null;
            GameObject.Find("Lane 0 1").GetComponent<Button>().onClick.Invoke();
            yield return SmokeUi.WaitUntil(() => !App.IsBattlePlaybackActive, 8, "Actual occupied-lane swap did not finish.");
            Assert.That(engine.State.players[0].AtLane(1).instanceId, Is.EqualTo(angel));
            Assert.That(engine.State.players[0].AtLane(2).instanceId, Is.EqualTo(gold));
            Assert.That(engine.State.randomState, Is.EqualTo(random)); Assert.That(engine.State.players[0].genericSoul, Is.EqualTo(soul));
            yield return Shot("actual-reordered-field");
            GameObject.Find("Lane 0 2").GetComponent<Button>().onClick.Invoke(); yield return null;
            SmokeUi.Click("선택 카드 소각");
            Assert.That(App.IsBattlePlaybackActive, Is.True);
            Assert.That(App.BattlePlaybackEventKind, Is.EqualTo("discard-field"));
            yield return SmokeUi.WaitUntil(() => UnityEngine.Object.FindObjectsByType<FmBattleFxGraphic>(FindObjectsSortMode.None)
                .Any(fx => fx.isActiveAndEnabled && fx.Style == FmBattleFxGraphic.EffectStyle.Defeat && fx.Progress > .10f), 3, "Real field disposal has no live particle effect.");
            yield return SmokeUi.Capture("FantasyCards", "reprise-" + attempt + "-actual-disposal-effect", 1280, 720, settleSeconds: 0);
            NamedButton("Battle playback skip").onClick.Invoke();
            yield return SmokeUi.WaitUntil(() => !App.IsBattlePlaybackActive, 8, "Skipping actual disposal did not finish.");
            Assert.That(engine.State.players[0].units.Any(unit => unit.instanceId == gold), Is.False);
            Assert.That(engine.State.players[0].discard.Any(card => card.instanceId == gold), Is.True);
            Assert.That(engine.State.randomState, Is.EqualTo(random)); Assert.That(engine.State.players[0].genericSoul, Is.EqualTo(soul));
            Assert.That(engine.State.events.Count(item => item.kind == "reposition"), Is.EqualTo(2));
            Assert.That(engine.State.events.Count(item => item.kind == "discard-field"), Is.EqualTo(1));
            Call("Save"); string final = JsonUtility.ToJson(engine.State);
            yield return SmokeUi.LoadBoot(); SmokeUi.Click("전투 이어하기");
            yield return SmokeUi.WaitUntil(() => !App.IsBattlePlaybackActive, 8, "Saved reordered battle failed to continue.");
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadEngine().State), Is.EqualTo(final));
            Assert.That(backend.Read("preserved-player"), Is.EqualTo("original player bytes"));
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }
    }
}
