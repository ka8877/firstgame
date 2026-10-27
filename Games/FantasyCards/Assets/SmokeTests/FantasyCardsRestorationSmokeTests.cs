using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    public sealed partial class FantasyCardsRevivalSmokeTests
    {
        [UnityTest]
        public IEnumerator RestorationEffectsPlayThroughVisibleControlsIndependentlyFromMusic()
        {
            FmOriginalMusic.SetMuted(false);FmBattleAudio.SetMuted(false);FmBattleAudio.SetVolume(.82f);
            yield return new WaitForSecondsRealtime(.5f);
            var host=UnityEngine.Object.FindFirstObjectByType<FmSfxHost>();
            Assert.That(host,Is.Not.Null);Assert.That(host.LoadedCueCount,Is.EqualTo(25));
            int before=host.PlayedCount;
            SmokeUi.Click("소리 설정");yield return null;
            Assert.That(host.PlayedCount,Is.GreaterThan(before));
            SmokeUi.Click("배경 음악 설정");yield return null;
            SmokeUi.Click("음악 끄기");yield return null;
            Assert.That(FmOriginalMusic.Muted,Is.True);Assert.That(FmBattleAudio.Muted,Is.False);
            SmokeUi.Click("돌아가기");yield return null;
            SmokeUi.Click("전투 효과 듣기");yield return null;
            Assert.That(FmBattleAudio.LastCue,Is.EqualTo("spell"));
            Assert.That(host.ActiveVoiceCount,Is.GreaterThan(0));
            Assert.That(FmOriginalMusic.Muted,Is.True,"Effect controls cannot unmute background music.");
            var voices=host.GetComponents<AudioSource>();
            Assert.That(voices.Length,Is.EqualTo(12));Assert.That(voices.All(v=>v.spatialBlend==0&&!v.loop),Is.True);
            yield return SmokeUi.Capture("FantasyCards","restoration-sfx-controls",1280,720);
            SmokeUi.Click("효과음 끄기");yield return null;
            Assert.That(FmBattleAudio.Muted,Is.True);Assert.That(host.ActiveVoiceCount,Is.Zero);
            Assert.That(SmokeUi.FindButton("전투 효과 듣기").interactable,Is.False);
            int mutedCount=host.PlayedCount;
            FmBattleAudio.Play("attack");FmBattleAudio.Play("hit");
            Assert.That(host.PlayedCount,Is.EqualTo(mutedCount),"The public effects facade must reject muted cues.");
            SmokeUi.Click("효과음 켜기");yield return null;
            Assert.That(FmBattleAudio.Muted,Is.False);Assert.That(host.History.Last().cue,Is.EqualTo("confirm"));
            float volume=FmBattleAudio.Volume;
            SmokeUi.Click("효과음 −");yield return null;
            Assert.That(FmBattleAudio.Volume,Is.EqualTo(volume-.1f).Within(.001f));
            Assert.That(FmOriginalMusic.Muted,Is.True);
            SmokeUi.Click("효과음 +");yield return null;
            Assert.That(FmBattleAudio.Volume,Is.EqualTo(volume).Within(.001f));
            var path=System.IO.Path.GetFullPath("../../Artifacts/Validation/Restoration/native-sfx.json");
            System.IO.File.WriteAllText(path,JsonUtility.ToJson(new SfxLedger{cues=host.History},true));
            TestContext.WriteLine("FM_RESTORATION_SFX_OK loaded25=true independentSettings=true activeVoicesStoppedOnMute=true staleCuesNotReplayed=true");
            LogAssert.NoUnexpectedReceived();
        }
        [System.Serializable] sealed class SfxLedger { public FmSfxHost.CueObservation[] cues; }

        [UnityTest]
        public IEnumerator RestorationScenarioModesAndHighGradeArchiveUseActualContent()
        {
            foreach(var stage in FmScenarioCatalog.Stages)PlayerPrefs.DeleteKey("FC.scenario.won."+stage.id);
            SmokeUi.Click("게임 시작");yield return null;
            var app=UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            SmokeUi.Click("SCENARIO · 시나리오");yield return null;
            Assert.That(app.RestorationLobbyMode,Is.EqualTo(1));
            var stages=ActiveButtons("Scenario stage ");Assert.That(stages.Length,Is.EqualTo(4));
            Assert.That(stages.Count(b=>b.interactable),Is.EqualTo(1));
            Assert.That(app.CompletedScenarioCount,Is.Zero);
            yield return SmokeUi.Capture("FantasyCards","restoration-scenario-list",1280,720);
            SmokeUi.Click("MULTI · 멀티플레이");yield return null;
            Assert.That(app.RestorationLobbyMode,Is.EqualTo(2));
            Assert.That(SmokeUi.HasText("준비 중"),Is.True);
            Assert.That(SmokeUi.HasText("온라인 방 만들기·친구 초대·매칭은 추후 제공됩니다."),Is.True);
            Assert.That(ActiveButtons("Practice room "),Is.Empty);
            yield return SmokeUi.Capture("FantasyCards","restoration-multiplayer-preparation",1280,720);
            SmokeUi.Click("상위 등급");yield return null;
            foreach(var grade in new[]{CardRarity.Unique,CardRarity.Legend,CardRarity.Secret})
                Assert.That(CardCatalog.All.Any(c=>c.rarity==grade),Is.True);
            AssertArcanaSurfaces();
            yield return SmokeUi.Capture("FantasyCards","restoration-playable-high-grades",1280,720);
            SmokeUi.Click("상위 등급 원본도감 · "+OriginalCardArchive.All.Length+"종 감상 전용");yield return null;
            Assert.That(OriginalCardArchive.All.Length,Is.EqualTo(30));
            Assert.That(SmokeUi.HasText("감상 전용이며 덱에 추가되지 않습니다."),Is.True);
            var seen=new System.Collections.Generic.HashSet<string>();
            do
            {
                foreach(var b in ActiveButtons("Archive original card "))
                {
                    string id=b.name.Substring("Archive original card ".Length);
                    Assert.That(seen.Add(id),Is.True);var card=OriginalCardArchive.Get(id);
                    var image=b.GetComponentsInChildren<RawImage>().Single();
                    Assert.That(image.texture,Is.SameAs(Resources.Load<Texture2D>(card.artResource)));
                    Assert.That(image.uvRect,Is.EqualTo(new Rect(0,0,1,1)));
                }
                if(!SmokeUi.FindButton("다음").interactable)break;
                SmokeUi.Click("다음");yield return null;
            }while(true);
            Assert.That(seen,Is.EquivalentTo(OriginalCardArchive.All.Select(c=>c.id)));
            yield return SmokeUi.Capture("FantasyCards","restoration-archive-originals",1280,720);
            SmokeUi.Click("대기실");yield return null;
            SmokeUi.Click("SCENARIO · 시나리오");yield return null;
            ActiveButtons("Scenario stage ").Single(b=>b.name=="Scenario stage roots").onClick.Invoke();yield return null;
            SmokeUi.Click("추천 덱으로 시작");
            yield return SmokeUi.WaitUntil(()=>SmokeUi.FindButton("이대로 시작")?.interactable==true,15,"The first scenario must deal an actual playable opening hand.");
            Assert.That(app.ActiveScenarioId,Is.EqualTo("roots"));
            Assert.That(SmokeUi.ReadEngine().State.encounterIndex,Is.EqualTo(FmScenarioCatalog.Get("roots").encounterIndex));
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(()=>SmokeUi.FindButton("준비 완료")?.interactable==true,15,"Scenario opening confirmation must reach the real battle engine.");
            yield return SaveThroughMenu();
            yield return SmokeUi.LoadBoot();
            SmokeUi.Click("이어하기");
            yield return SmokeUi.WaitUntil(()=>SmokeUi.FindButton("준비 완료")?.interactable==true,15,"Continue must restore the saved scenario battle.");
            Assert.That(UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>().ActiveScenarioId,Is.EqualTo("roots"));
            // The restored profile route must actually export/import an existing
            // saved battle and preserve its bytes after a rejected payload.
            SmokeUi.Click("기록 / 메뉴");yield return null;
            SmokeUi.Click("대기실");yield return null;
            SmokeUi.Click("프로필");yield return null;
            SmokeUi.Click("저장 옮기기");yield return null;
            Assert.That(SmokeUi.HasText("전투 저장 옮기기"),Is.True);
            SmokeUi.Click("내보내기");yield return null;
            var transfer=UnityEngine.Object.FindObjectsByType<TMPro.TMP_InputField>(FindObjectsSortMode.None)
                .Single(field=>field.gameObject.activeInHierarchy);
            string exported=transfer.text,originalBytes=backend.Read(SmokeUi.BattleSlot);
            Assert.That(exported.Length,Is.GreaterThan(100));
            transfer.text="invalid-transfer-payload";SmokeUi.Click("검증 후 가져오기");yield return null;
            Assert.That(SmokeUi.HasText("가져오기 실패"),Is.True);
            Assert.That(backend.Read(SmokeUi.BattleSlot),Is.EqualTo(originalBytes));
            transfer.text=exported;SmokeUi.Click("검증 후 가져오기");yield return null;
            Assert.That(SmokeUi.HasText("가져오기 완료"),Is.True);
            Assert.That(backend.Read(SmokeUi.BattleSlot),Is.EqualTo(originalBytes));
            yield return SmokeUi.Capture("FantasyCards","restoration-actual-save-transfer",1280,720);
            SmokeUi.Click("돌아가기");yield return null;
            Assert.That(GameObject.Find("Profile nickname input"),Is.Not.Null,"Transfer cancel must return to the same profile page.");
            SmokeUi.Click("돌아가기");yield return null;
            TestContext.WriteLine("FM_RESTORATION_TRANSFER_OK visibleProfileEntry=true validExportImport=true rejectedBytesPreserved=true cancelReturnsProfile=true");

            // Native-only legal Core fixture: resolve complete genuine matches,
            // never set winner, delete a deck or write a completion preference.
            BattleEngine actualWin=null;int legalActions=0;uint winningSeed=0;
            var deck=DemoCatalog.GetDeck(FmScenarioCatalog.Get("roots").recommendedDeckId);
            for(uint seed=1;seed<=32&&actualWin==null;seed++)
            {
                var candidate=FmScenarioCatalog.CreateBattle("roots",seed,deck.id,deck.recommendedLeaderId);
                int actions=0;
                while(candidate.State.phase!=BattlePhase.Complete&&actions++<2000)
                    Assert.That(BattleAi.Step(candidate,candidate.PreparingPlayer,out var reason),Is.True,reason);
                BattleEngine.Validate(candidate.State);
                if(candidate.State.phase==BattlePhase.Complete&&candidate.State.winner==0)
                {actualWin=candidate;legalActions=actions;winningSeed=seed;}
            }
            Assert.That(actualWin,Is.Not.Null,"A genuine legally resolved first scenario victory is required to exercise the reward callback.");
            app=UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            int winsBefore=app.LocalProfileWins;
            InstallFidelityFixture(app,actualWin);yield return null;
            Assert.That(app.CompletedScenarioCount,Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("FC.scenario.won.roots",0),Is.EqualTo(1));
            Assert.That(PlayerPrefs.GetInt("FC.scenario.won.tides",0),Is.EqualTo(0));
            Assert.That(app.LocalProfileWins,Is.EqualTo(winsBefore+1));
            InstallFidelityFixture(app,actualWin);yield return null;
            Assert.That(app.LocalProfileWins,Is.EqualTo(winsBefore+1),"Repeated genuine scenario result draw cannot duplicate the win.");
            yield return SmokeUi.Capture("FantasyCards","restoration-genuine-scenario-victory",1280,720);
            SmokeUi.Click("시나리오 계속");yield return null;
            Assert.That(GameObject.Find("Scenario briefing tides"),Is.Not.Null);
            SmokeUi.Click("목록");yield return null;
            stages=ActiveButtons("Scenario stage ");
            Assert.That(stages.Single(b=>b.name=="Scenario stage tides").interactable,Is.True);
            Assert.That(stages.Single(b=>b.name=="Scenario stage forge").interactable,Is.False);
            yield return SmokeUi.LoadBoot();
            SmokeUi.Click("게임 시작");yield return null;SmokeUi.Click("SCENARIO · 시나리오");yield return null;
            Assert.That(ActiveButtons("Scenario stage ").Single(b=>b.name=="Scenario stage tides").interactable,Is.True,
                "Actual first-win unlock must survive a real scene reload.");
            TestContext.WriteLine("FM_RESTORATION_SCENARIO_TRUE_WIN seed="+winningSeed+" legalActions="+legalActions+" stage2UnlockedAfterReload=true duplicateWinRejected=true");
            TestContext.WriteLine("FM_RESTORATION_MODES_OK scenarios4=true realScenarioSaved=true multiplayerHonest=true archive30CompletePrints=true playablePremiumGrades=true");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
