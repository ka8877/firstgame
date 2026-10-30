using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    public sealed class QuestSmokeTests
    {
        IDisposable storage, preferences;
        MemorySaveBackend backend;
        string attempt;
        FantasyCardsApp App => UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        void Call(string method, params object[] args) => typeof(FantasyCardsApp).GetMethod(method, Private).Invoke(App, args);
        IEnumerator Click(string label) { SmokeUi.Click(label); yield return null; yield return null; }
        IEnumerator Shot(string name, int width=1280, int height=720, float settle=.1f) =>
            SmokeUi.Capture("FantasyCards", "quest-"+attempt+"-"+name, width, height, settleSeconds:settle);
        void Named(string name)
        {
            var button=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(b=>b.gameObject.activeInHierarchy&&b.gameObject.name==name);
            Assert.That(button.interactable,Is.True);button.onClick.Invoke();
        }
        FmQuestProfile AssertUnityProfile()
        {
            Assert.That(PersistentSaves.TryLoad(FmQuestEconomy.SaveSlot,FmQuestEconomy.GameId,FmQuestEconomy.SchemaVersion,
                FmQuestEconomy.ContentVersion,out var payload,out var error),Is.True,error);
            var parsed=JsonUtility.FromJson<FmQuestProfile>(payload);
            var restored=FmQuestEconomy.FromSave(parsed);
            Assert.That(restored.Sp,Is.EqualTo(App.QuestSp));
            Assert.That(restored.OwnedCards.Sum(c=>c.count),Is.EqualTo(App.QuestProfileSnapshot.ownedCards.Sum(c=>c.count)));
            Assert.That(JsonUtility.ToJson(restored.CaptureSave()),Is.EqualTo(JsonUtility.ToJson(parsed)),"Actual Unity JSON must replay the complete profile exactly.");
            return parsed;
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt=Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT")??DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend=new MemorySaveBackend();backend.WriteAtomic("preserved-player","original player bytes");
            storage=PersistentSaves.UseBackendForTesting(backend);preferences=SmokeUi.PreserveDemoPreferences();
            yield return SmokeUi.LoadBoot();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return SmokeUi.UnloadBoot();preferences?.Dispose();storage?.Dispose();
            if(Application.isPlaying)yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator ActualStarterShopSealedPackOwnershipAndUnityReload()
        {
            Assert.That(App,Is.Not.Null);Assert.That(App.QuestHasStarter,Is.False);
            SmokeUi.AssertNoMissingScripts();yield return Shot("title");
            yield return Click("게임 시작");Assert.That(App.QuestCurrentScreen,Is.EqualTo("starter"));
            Assert.That(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Count(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("Quest starter choice ")),Is.EqualTo(4));
            yield return Shot("starter-choice");
            Named("Quest starter choice mono-light");yield return null;
            yield return Click("이 덱을 무료로 받기");
            Assert.That(App.QuestSp,Is.EqualTo(600));Assert.That(App.QuestGp,Is.Zero);
            Assert.That(App.QuestDeckCards.Count,Is.EqualTo(50));
            Assert.That(AssertUnityProfile().ownedCards.Sum(c=>c.count),Is.EqualTo(50));
            yield return Shot("starter-wallet");yield return Shot("starter-wallet-mobile",844,390);
            yield return Click("카드 상점");Named("Quest shop offer standard");yield return null;
            Assert.That(App.QuestCurrentScreen,Is.EqualTo("offer"));
            yield return Shot("pack-rules-odds");yield return Click("400 SP로 구입");
            Assert.That(App.QuestSp,Is.EqualTo(200));Assert.That(App.QuestUnopenedPackCount,Is.EqualTo(1));
            var sealedProfile=AssertUnityProfile();var purchased=sealedProfile.purchases.Single();
            Assert.That(purchased.opened,Is.False);Assert.That(sealedProfile.ownedCards.Sum(c=>c.count),Is.EqualTo(50));
            var results=purchased.cardIds.ToArray();yield return Shot("sealed-purchased-pack");
            yield return SmokeUi.LoadBoot();yield return Click("게임 시작");
            Assert.That(App.QuestSp,Is.EqualTo(200));Assert.That(App.QuestUnopenedPackCount,Is.EqualTo(1));
            Assert.That(App.QuestProfileSnapshot.purchases.Single().cardIds,Is.EqualTo(results));
            Call("ShowQuestSealedPack",purchased.id);yield return null;
            yield return Click("카드팩 개봉");
            yield return SmokeUi.WaitUntil(()=>App.QuestCurrentScreen=="pack-reveal",6,"Actual pack animation did not finish.");
            Assert.That(App.QuestUnopenedPackCount,Is.Zero);Assert.That(App.QuestSp,Is.EqualTo(200));
            Assert.That(AssertUnityProfile().ownedCards.Sum(c=>c.count),Is.EqualTo(55));
            yield return Shot("five-acquired-cards");yield return Shot("five-acquired-cards-mobile",844,390);
            yield return Click("보유 카드 보기");Assert.That(App.QuestCurrentScreen,Is.EqualTo("collection"));
            yield return Shot("owned-collection");yield return Click("덱 편집");
            Assert.That(App.QuestDeckCards.Count,Is.EqualTo(50));
            Assert.That(UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b=>b.gameObject.activeInHierarchy&&b.GetComponentsInChildren<TMPro.TMP_Text>().Any(t=>t.text=="+")).All(b=>!b.interactable),Is.True,
                "A full50-card deck must not permit extra additions.");
            yield return Shot("owned-deck-editor");
            yield return SmokeUi.Capture("FantasyCards","quest-"+attempt+"-owned-deck-editor-mobile",616,346,inspect:()=>
            {
                var labels=UnityEngine.Object.FindObjectsByType<TMPro.TMP_Text>(FindObjectsSortMode.None)
                    .Where(t=>t.gameObject.activeInHierarchy&&(t.text=="+"||t.text=="−")&&t.GetComponentInParent<Button>()!=null).ToArray();
                Assert.That(labels.Length,Is.EqualTo(8),"Four visible owned cards must each show both quantity controls.");
                foreach(var label in labels)
                {
                    label.ForceMeshUpdate();
                    Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c=>c.isVisible),Is.True,
                        "The actual mobile quantity label has no rendered glyph geometry: "+label.text);
                }
            });
            yield return Click("덱 저장");
            string saved=JsonUtility.ToJson(App.QuestProfileSnapshot);
            yield return SmokeUi.LoadBoot();yield return Click("게임 시작");
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot),Is.EqualTo(saved));
            Assert.That(App.QuestDeckCards.Count,Is.EqualTo(50));
            Named("Quest scenario roots");yield return null;yield return Click("대전 시작");
            yield return SmokeUi.WaitUntil(()=>SmokeUi.FindButton("이대로 시작")!=null,8,"Opening cards missing.");
            yield return Click("이대로 시작");
            yield return SmokeUi.WaitUntil(()=>!App.IsBattlePlaybackActive&&SmokeUi.FindButton("손패 셔플")!=null&&SmokeUi.FindButton("손패 셔플").interactable,8,"Actual full-hand shuffle must be available before playing a card.");
            SmokeUi.Click("손패 셔플");
            Assert.That(App.IsClientShuffleActive,Is.True);
            Assert.That(GameObject.Find("Original hand-to-deck shuffle presentation"),Is.Not.Null);
            Assert.That(UnityEngine.Object.FindObjectsByType<RectTransform>(FindObjectsSortMode.None)
                .Count(t=>t.gameObject.activeInHierarchy&&t.name.StartsWith("Hand shuffle original back ")),Is.EqualTo(6));
            uint resolvedShuffle=SmokeUi.ReadEngine().State.randomState;
            yield return Shot("actual-on-field-hand-shuffle",1280,720,0);
            yield return SmokeUi.WaitUntil(()=>!App.IsClientShuffleActive,5,"Actual hand-to-deck shuffle did not finish.");
            Assert.That(SmokeUi.ReadEngine().State.randomState,Is.EqualTo(resolvedShuffle),"Drawing the shuffle animation must not consume Core RNG.");
            var battleBefore=SmokeUi.ReadEngine().State.Copy();string ticket=App.QuestCurrentTicketId;
            Call("Save");yield return null;
            yield return SmokeUi.LoadBoot();yield return Click("전투 이어하기");
            yield return SmokeUi.WaitUntil(()=>App.IsQuestBattle&&!App.IsBattlePlaybackActive,8,"Actual saved collection battle did not resume.");
            Assert.That(App.QuestCurrentTicketId,Is.EqualTo(ticket));
            Assert.That(SmokeUi.ReadEngine().State.randomState,Is.EqualTo(battleBefore.randomState));
            Assert.That(App.QuestSp,Is.EqualTo(200));AssertUnityProfile();
            yield return Shot("continued-owned-battle");
            Call("ShowQuestLobby");yield return null;
            yield return Click("멀티플레이");Assert.That(SmokeUi.HasText("추후 추가"),Is.True);
            Assert.That(backend.Read("preserved-player"),Is.EqualTo("original player bytes"));
        }

        [UnityTest] public IEnumerator StandardOwnedDeckNaturalVictoryCreditsOnceAndPreservesBattle()
        {
            // Native-only deterministic origin seed. No cards, souls, result or
            // enemy concede are fabricated; the same public Core AI actions
            // complete standard fifty-card decks from the actual UI start.
            var fresh=FmQuestEconomy.New(4);
            Assert.That(PersistentSaves.TrySave(FmQuestEconomy.SaveSlot,FmQuestEconomy.GameId,1,FmQuestEconomy.ContentVersion,
                JsonUtility.ToJson(fresh.CaptureSave()),out var error),Is.True,error);
            yield return SmokeUi.LoadBoot();yield return Click("게임 시작");
            Named("Quest starter choice mono-forest");yield return null;yield return Click("이 덱을 무료로 받기");
            Named("Quest scenario roots");yield return null;yield return Click("대전 시작");
            yield return SmokeUi.WaitUntil(()=>SmokeUi.FindButton("이대로 시작")!=null,8,"Actual initial hand was not dealt.");
            yield return Click("이대로 시작");
            yield return SmokeUi.WaitUntil(()=>!App.IsBattlePlaybackActive&&SmokeUi.FindButton("준비 완료")!=null&&SmokeUi.FindButton("준비 완료").interactable,8,"Real board did not unlock.");
            Assert.That(App.IsQuestBattle,Is.True);Assert.That(App.QuestCurrentTicketId,Is.Not.Empty);
            AssertUnityProfile();yield return Shot("owned-single-board");
            var engine=SmokeUi.ReadEngine();int actions=0;
            while(engine.State.phase!=BattlePhase.Complete&&actions++<2000)
                Assert.That(BattleAi.Step(engine,engine.PreparingPlayer,out var reason),Is.True,reason);
            Assert.That(engine.State.phase,Is.EqualTo(BattlePhase.Complete));Assert.That(engine.State.winner,Is.EqualTo(0));
            TestContext.Out.WriteLine("QUEST_NATIVE_STANDARD_WIN originSeed=4 turn="+engine.State.turn+" legalAiActions="+actions+" noResultInjection=true");
            Call("ShowBattle");yield return null;
            Assert.That(App.QuestSp,Is.EqualTo(1050));Assert.That(SmokeUi.HasText("+450 SP"),Is.True);
            Assert.That(AssertUnityProfile().completedScenarios,Does.Contain("roots"));
            yield return Shot("natural-win-sp-reward");yield return Shot("natural-win-sp-reward-mobile",844,390);
            string settled=JsonUtility.ToJson(App.QuestProfileSnapshot);
            Call("ShowBattle");yield return null;Assert.That(App.QuestSp,Is.EqualTo(1050));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot),Is.EqualTo(settled),"Redrawing an actual settled result cannot mint SP twice.");
            yield return Click("카드 상점");Assert.That(App.QuestCurrentScreen,Is.EqualTo("shop"));
            yield return SmokeUi.LoadBoot();yield return Click("게임 시작");
            Assert.That(App.QuestSp,Is.EqualTo(1050));Assert.That(App.QuestProfileSnapshot.completedScenarios,Does.Contain("roots"));
            Assert.That(backend.Read("preserved-player"),Is.EqualTo("original player bytes"));
        }
    }
}
