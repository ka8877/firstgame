using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    /// <summary>Current remaster callbacks and catalog browsing; retired avatar fixtures are historical.</summary>
    public sealed partial class RenewalSmokeTests
    {
        IDisposable storageScope, preferenceScope;
        MemorySaveBackend backend;
        string attempt;

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            attempt=Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT")??DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend = new MemorySaveBackend();
            backend.WriteAtomic("preserved-previous-player-slot", "previous-player-save-bytes");
            storageScope = PersistentSaves.UseBackendForTesting(backend);
            preferenceScope = SmokeUi.PreserveDemoPreferences();
            yield return SmokeUi.LoadBoot();
        }

        [UnityTearDown]
        public IEnumerator LeaveGame()
        {
            yield return SmokeUi.UnloadBoot();
            preferenceScope?.Dispose(); preferenceScope = null;
            storageScope?.Dispose(); storageScope = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator RemasterTitleLocalRoomsDeckChoiceShuffleAndContinueUseLiveCallbacks()
        {
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>(), Is.Not.Null);
            var listeners = UnityEngine.Object.FindObjectsByType<AudioListener>(FindObjectsSortMode.None)
                .Where(listener => listener.isActiveAndEnabled).ToArray();
            Assert.That(listeners.Length, Is.EqualTo(1), "Native bundled music requires exactly one active output AudioListener.");
            var title = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .FirstOrDefault(text => text.gameObject.activeInHierarchy && string.Concat(text.text.Where(character => !char.IsWhiteSpace(character))) == "판타지마스터즈");
            Assert.That(title, Is.Not.Null, "The modern title must retain the complete Korean game name across its designed line break.");
            SmokeUi.AssertVisibleKorean(title.text);
            Assert.That(SmokeUi.FindButton("게임 시작"), Is.Not.Null);
            Assert.That(SmokeUi.FindButton("새로운 결투"), Is.Null, "The retired anime-avatar entry must not remain the primary flow.");
            Assert.That(ActiveButtons("Leader ").Length, Is.Zero);
            Assert.That(SmokeUi.FindButton("이어하기").interactable, Is.False);
            SmokeUi.AssertNoMissingScripts();
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-01-title", 1280, 720);
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-01-title-mobile", 844, 390);

            SmokeUi.Click("게임 시작"); yield return null;
            if (ActiveButtons("Practice room ").Length == 0)
                yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-diagnostic-title-start-state", 1280,720);
            yield return SmokeUi.WaitUntil(() => ActiveButtons("Practice room ").Length == 3, 5, "The actual title callback must enter the local room list.");
            Assert.That(ActiveButtons("Practice room ").Length, Is.EqualTo(3));
            Assert.That(SmokeUi.HasText("AI 상대"), Is.True, "Rooms must identify their actual single-player opponent.");
            Assert.That(ActiveButtons("Leader ").Length, Is.Zero);
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-02-local-lobby", 1280, 720);
            foreach(var brand in UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Where(text=>text.gameObject.activeInHierarchy && text.text=="Fantasy Masters"))
            {
                brand.ForceMeshUpdate();
                Assert.That(brand.isTextTruncated,Is.False,"The narrow lobby rail must show the complete Fantasy Masters name.");
                Assert.That(brand.textInfo.characterCount,Is.EqualTo("Fantasy Masters".Length));
            }
            SmokeUi.Click("상위 등급");yield return null;
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-premium-hub",1280,720);
            SmokeUi.Click("레어 · "+CardCatalog.All.Count(c=>c.rarity==CardRarity.Rare)+"종");yield return null;
            Assert.That(SmokeUi.HasText("검색 결과 49종"),Is.True);
            foreach(var cardButton in ActiveButtons("Card "))Assert.That(CardCatalog.Get(cardButton.name.Substring(5)).rarity,Is.EqualTo(CardRarity.Rare));
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-rare-filter",1280,720);
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-rare-filter-mobile",844,390);
            SmokeUi.Click("대기실");yield return null;
            ActiveButtons("Practice room ").Single(button => button.gameObject.name == "Practice room 0").onClick.Invoke();
            yield return null;
            Assert.That(SmokeUi.FindButton("START · 대전 시작"), Is.Not.Null);
            SmokeUi.Click("덱 선택"); yield return null;
            Assert.That(ActiveButtons("Starter deck ").Length, Is.EqualTo(4));
            var decksSeen = new System.Collections.Generic.HashSet<string>();
            do
            {
                foreach (var tile in ActiveButtons("Starter deck "))
                    Assert.That(decksSeen.Add(tile.gameObject.name.Substring("Starter deck ".Length)), Is.True);
                if (!SmokeUi.FindButton("다음").interactable) break;
                SmokeUi.Click("다음"); yield return null;
            } while (true);
            Assert.That(decksSeen, Is.EquivalentTo(DemoCatalog.StarterDecks.Select(deck => deck.id)));
            Assert.That(decksSeen.Count, Is.EqualTo(DemoCatalog.StarterDecks.Length));
            SmokeUi.Click("단일 속성"); yield return null;
            var monoSeen = new System.Collections.Generic.HashSet<string>();
            do
            {
                foreach (var tile in ActiveButtons("Starter deck ")) monoSeen.Add(tile.gameObject.name.Substring("Starter deck ".Length));
                if (!SmokeUi.FindButton("다음").interactable) break;
                SmokeUi.Click("다음"); yield return null;
            } while (true);
            Assert.That(monoSeen, Is.EquivalentTo(DemoCatalog.StarterDecks.Where(deck=>deck.cards.Select(CardCatalog.Get).Where(c=>c.kind==CardKind.Terrain).Select(c=>c.element).Distinct().Count()==1).Select(deck=>deck.id)));
            SmokeUi.Click("듀얼 속성"); yield return null;
            var dualSeen = new System.Collections.Generic.HashSet<string>();
            do
            {
                foreach (var tile in ActiveButtons("Starter deck ")) dualSeen.Add(tile.gameObject.name.Substring("Starter deck ".Length));
                if (!SmokeUi.FindButton("다음").interactable) break;
                SmokeUi.Click("다음"); yield return null;
            } while (true);
            Assert.That(dualSeen, Is.EquivalentTo(DemoCatalog.StarterDecks.Where(deck=>deck.cards.Select(CardCatalog.Get).Where(c=>c.kind==CardKind.Terrain).Select(c=>c.element).Distinct().Count()>1).Select(deck=>deck.id)));
            SmokeUi.TraceUiState("chronicle-" + attempt + "-decks-before-all-filter");
            SmokeUi.Click("전체"); SmokeUi.TraceUiState("chronicle-" + attempt + "-decks-immediate-all-filter"); yield return null;
            SmokeUi.TraceUiState("chronicle-" + attempt + "-decks-after-all-filter");
            while (!ActiveButtons("Starter deck ").Any(tile => tile.gameObject.name == "Starter deck forest"))
            { SmokeUi.TraceUiState("chronicle-" + attempt + "-decks-before-find-forest-next"); SmokeUi.Click("다음"); yield return null; SmokeUi.TraceUiState("chronicle-" + attempt + "-decks-after-find-forest-next"); }
            ActiveButtons("Starter deck ").Single(tile => tile.gameObject.name == "Starter deck forest").onClick.Invoke(); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-03-four-starter-decks", 1280, 720);
            SmokeUi.Click("대전실로"); yield return null;
            Assert.That(SmokeUi.HasText(DemoCatalog.GetDeck("forest").name), Is.True);
            Assert.That(SmokeUi.HasText("컴퓨터"), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-04-local-game-room", 844, 390, () =>
            {
                var compactBrand = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Single(text => text.gameObject.activeInHierarchy && text.text == "Fantasy\nMasters");
                compactBrand.ForceMeshUpdate();
                Assert.That(string.Concat(compactBrand.text.Where(c=>!char.IsWhiteSpace(c))), Is.EqualTo("FantasyMasters"), "The classic rail keeps both complete title words across its deliberate line break.");
                Assert.That(compactBrand.isTextTruncated, Is.False);
            });
            SmokeUi.Click("START · 대전 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null && SmokeUi.FindButton("이대로 시작").interactable,
                15, "The actual room start callback must shuffle and deal an opening hand.");
            SmokeUi.TraceUiState("fidelity-opening-before-read");
            var engine = SmokeUi.ReadEngine();
            Assert.That(engine.State.players[0].hand.Count, Is.EqualTo(6));
            uint beforeRng = engine.State.randomState;
            SmokeUi.Click("전체 손패 교체");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("전체 손패 교체") != null &&
                SmokeUi.FindButton("이대로 시작") != null, 5, "The actual mulligan callback must redraw the opening controls.");
            Assert.That(SmokeUi.ReadEngine(), Is.SameAs(engine), "Mulligan must keep the actual battle engine.");
            Assert.That(engine.State, Is.Not.Null, "Mulligan must retain its live state.");
            Assert.That(engine.State.players[0].hand, Is.Not.Null, "Mulligan must retain its live hand.");
            Assert.That(engine.State.players[0].hand.Count, Is.EqualTo(6));
            Assert.That(engine.State.randomState, Is.Not.EqualTo(beforeRng), "Whole-hand replacement must run the core shuffle.");
            Assert.That(SmokeUi.FindButton("전체 손패 교체").interactable, Is.False);
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-05-shuffled-opening", 1280, 720);
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("준비 완료") != null && SmokeUi.FindButton("준비 완료").interactable,
                15, "The opening confirmation must enter the current battle board.");
            Assert.That(SmokeUi.ReadEngine().State.players[0].hand.Count, Is.EqualTo(6));
            Assert.That(SmokeUi.HasText("공격 준비"), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-06-battle-desktop", 1280, 720);
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-06-battle-mobile", 844, 390);
            var app=UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            string unmodified=JsonUtility.ToJson(SmokeUi.ReadEngine().State);
            var info=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Single(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("Button 상대 정보 ·"));
            info.onClick.Invoke();yield return null;Assert.That(app.IsBattleDeskOpen,Is.True);Assert.That(app.BattleDeskTab,Is.Zero);
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-opponent-info",1280,720);
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-opponent-info-mobile",844,390);
            SmokeUi.Click("전투 기록");yield return null;Assert.That(app.BattleDeskTab,Is.EqualTo(1));
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-battle-event-feed",1280,720);
            SmokeUi.Click("연습 메모");yield return null;
            var note=UnityEngine.Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).Single(v=>v.gameObject.activeInHierarchy&&v.name=="Local practice note input");
            note.text="공격 전에 지형 확인";SmokeUi.Click("기록");yield return null;
            Assert.That(SmokeUi.HasText("공격 전에 지형 확인"),Is.True);
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-local-practice-note",1280,720);
            SmokeUi.Click("전장으로");yield return null;Assert.That(app.IsBattleDeskOpen,Is.False);
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadEngine().State),Is.EqualTo(unmodified),"Opponent/feed/localnote UI must not alter Core state or consume RNG.");
            string detailId=SmokeUi.ReadEngine().State.players[0].hand[0].definitionId;
            for(int tap=0;tap<2;tap++)
            {var cardButton=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).First(b=>b.gameObject.activeInHierarchy&&b.name=="Card "+detailId);cardButton.onClick.Invoke();}
            yield return null;
            Assert.That(SmokeUi.FindButton("돌아가기"),Is.Not.Null,"Actual doubletap after the first redraw must open originalcard detail.");
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-double-tap-original-detail",1280,720);
            SmokeUi.Click("돌아가기");yield return null;
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadEngine().State),Is.EqualTo(unmodified));
            yield return SaveThroughMenu();
            string saved = JsonUtility.ToJson(SmokeUi.ReadSavedBattle());
            int appId = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>().GetInstanceID();
            yield return SmokeUi.LoadBoot();
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>().GetInstanceID(), Is.Not.EqualTo(appId));
            SmokeUi.Click("이어하기");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("준비 완료") != null && SmokeUi.FindButton("준비 완료").interactable,
                15, "Continue must reconstruct the live classic battle in the new client flow.");
            yield return SaveThroughMenu();
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadSavedBattle()), Is.EqualTo(saved));
            Assert.That(backend.Read("preserved-previous-player-slot"), Is.EqualTo("previous-player-save-bytes"));
            yield return SmokeUi.Capture("FantasyCards", "chronicle-" + attempt + "-07-continued-battle", 1280, 720);
            SmokeUi.AssertNoMissingScripts();
            LogAssert.NoUnexpectedReceived();
        }

        static Button[] ActiveButtons(string prefix)=>UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith(prefix,StringComparison.Ordinal)).ToArray();
        static IEnumerator SaveThroughMenu(){SmokeUi.Click("기록 / 메뉴");yield return null;SmokeUi.Click("저장");yield return null;SmokeUi.Click("전투로");yield return null;}
        [UnityTest] public IEnumerator KnownLateEraSecretAndLegendOriginalsLoadThroughSearch()
        {
            SmokeUi.Click("전체 원본 도감");yield return null;
            foreach(string id in new[]{"fm_16051128346","fm_16113028641"})
            {
                var card=OriginalCardArchive.Get(id);
                Assert.That(card.rarity==CardRarity.Secret||card.rarity==CardRarity.Legend,Is.True);
                var input=UnityEngine.Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None).Single(v=>v.gameObject.activeInHierarchy&&v.name=="Original archive search input");
                input.text=id;SmokeUi.Click("검색");yield return null;
                var first=ActiveButtons("Archive original card ").Single();Assert.That(first.name,Is.EqualTo("Archive original card "+id));
                first.onClick.Invoke();yield return null;
                yield return SmokeUi.WaitUntil(()=>UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Any(v=>v.gameObject.activeInHierarchy&&v.texture!=null&&v.texture.name==id),20,"Exact cached original must load through the real streamed image path.");
                var original=UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).First(v=>v.gameObject.activeInHierarchy&&v.texture!=null&&v.texture.name==id);
                Assert.That(original.texture.width,Is.EqualTo(285));Assert.That(original.texture.height,Is.EqualTo(405));
                yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-cached-original-"+id,1280,720);
                yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-cached-original-mobile-"+id,844,390);
                SmokeUi.Click("도감으로");yield return null;
            }
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator FullOriginalCataloguePremiumPrintsAndIndependentAudioControls()
        {
            Assert.That(OriginalCardArchive.All.Length,Is.EqualTo(15350));Assert.That(CardCatalog.All.Length,Is.GreaterThan(2000));
            foreach(var grade in new[]{CardRarity.Special,CardRarity.Unique,CardRarity.Legend,CardRarity.Secret})Assert.That(CardCatalog.All.Any(c=>c.rarity==grade),Is.True);
            SmokeUi.Click("전체 원본 도감");yield return null;
            Assert.That(ActiveButtons("Archive original card ").Length,Is.EqualTo(4));
            yield return new WaitForSecondsRealtime(.8f);
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-full-original-archive",1280,720);
            SmokeUi.Click("등급 · 전체");yield return null;
            int count=OriginalCardArchive.All.Count(c=>c.rarity==CardRarity.Secret);
            SmokeUi.Click("시크릿 · "+count);yield return null;
            var first=ActiveButtons("Archive original card ").First();string id=first.name.Substring("Archive original card ".Length);
            Assert.That(OriginalCardArchive.All.First(c=>c.id==id).rarity,Is.EqualTo(CardRarity.Secret));
            first.onClick.Invoke();yield return null;
            yield return SmokeUi.WaitUntil(()=>UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Any(i=>i.gameObject.activeInHierarchy&&i.texture!=null&&i.texture.name==System.IO.Path.GetFileName(OriginalCardArchive.All.First(c=>c.id==id).artResource)),20,"Streamed original secret image must finish loading from the actual shipped JPG.");
            yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-secret-original-detail",844,390);
            SmokeUi.Click("도감으로");yield return null;SmokeUi.Click("대기실");yield return null;
            SmokeUi.Click("음악 / 효과음");yield return null;
            var host=UnityEngine.Object.FindFirstObjectByType<FmSfxHost>();Assert.That(host.LoadedCueCount,Is.EqualTo(31));Assert.That(host.GetComponents<AudioSource>().Length,Is.EqualTo(16));
            if(FmOriginalMusic.Muted){SmokeUi.Click("배경 음악 설정");yield return null;SmokeUi.Click("음악 켜기");yield return null;SmokeUi.Click("돌아가기");yield return null;}
            if(FmBattleAudio.Muted){SmokeUi.Click("효과음 켜기");yield return null;}
            SmokeUi.Click("배경 음악 설정");yield return null;SmokeUi.Click("음악 끄기");yield return null;SmokeUi.Click("돌아가기");yield return null;
            int before=host.PlayedCount;SmokeUi.Click("전투 효과 듣기");yield return null;Assert.That(host.PlayedCount,Is.GreaterThan(before));Assert.That(FmOriginalMusic.Muted,Is.True);Assert.That(FmBattleAudio.Muted,Is.False);
            SmokeUi.Click("효과음 끄기");yield return null;Assert.That(host.ActiveVoiceCount,Is.Zero);Assert.That(FmOriginalMusic.Muted,Is.True);
            SmokeUi.Click("효과음 켜기");yield return null;Assert.That(FmBattleAudio.Muted,Is.False);yield return SmokeUi.Capture("FantasyCards","chronicle-"+attempt+"-sound-settings",1280,720);LogAssert.NoUnexpectedReceived();
        }
    }
}
