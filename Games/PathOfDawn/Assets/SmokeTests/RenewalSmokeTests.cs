using System;
using System.Collections;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Actual current UI/physical input; no scene-pose or story-state injection.</summary>
    public sealed partial class RenewalSmokeTests
    {
        IDisposable storage; MemorySaveBackend backend; Keyboard keyboard; Mouse mouse;
        InputSettings previousInputs,testInputs; string previousSlot,attempt;bool hadSlot;
        GospelLuminousApp App=>UnityEngine.Object.FindFirstObjectByType<GospelLuminousApp>();
        [UnitySetUp] public IEnumerator Begin()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            attempt=Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT")??DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend=new MemorySaveBackend();storage=PersistentSaves.UseBackendForTesting(backend);
            hadSlot=PlayerPrefs.HasKey("GOSPEL.activeSlot");previousSlot=PlayerPrefs.GetString("GOSPEL.activeSlot","");PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            previousInputs=InputSystem.settings;testInputs=UnityEngine.Object.Instantiate(previousInputs);testInputs.hideFlags=HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=testInputs;
            keyboard=InputSystem.AddDevice<Keyboard>();mouse=InputSystem.AddDevice<Mouse>();yield return SmokeUi.LoadBoot();yield return new WaitForSecondsRealtime(.5f);
        }
        [UnityTearDown] public IEnumerator Finish()
        {
            if(keyboard!=null&&keyboard.added)InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            yield return SmokeUi.UnloadBoot();
            if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);if(mouse!=null&&mouse.added)InputSystem.RemoveDevice(mouse);
            if(previousInputs!=null)InputSystem.settings=previousInputs;storage?.Dispose();
            if(hadSlot)PlayerPrefs.SetString("GOSPEL.activeSlot",previousSlot);else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");PlayerPrefs.Save();
            if(Application.isPlaying)yield return new ExitPlayMode();
            // The Editor restores its play-mode InputSettings reference while exiting.
            // Destroy the isolated clone only after that restoration has completed.
            if(testInputs!=null && InputSystem.settings!=testInputs)UnityEngine.Object.DestroyImmediate(testInputs);
        }
        IEnumerator Shot(string name)=>SmokeUi.Capture("PathOfDawn","chronicle-"+attempt+"-"+name,1280,720);
        IEnumerator Key(Key key)
        {InputSystem.QueueStateEvent(keyboard,new KeyboardState(key));yield return null;InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;}
        IEnumerator MoveTo(float goal)
        {
            Assert.That(App.IsModal,Is.False,"Close actual modal before movement.");
            float deadline=Time.realtimeSinceStartup+45;
            while(Mathf.Abs(App.Player.Position.x-goal)>.8f&&Time.realtimeSinceStartup<deadline)
            {
                InputSystem.QueueStateEvent(keyboard,new KeyboardState(App.Player.Position.x<goal?UnityEngine.InputSystem.Key.D:UnityEngine.InputSystem.Key.A,UnityEngine.InputSystem.Key.LeftShift));
                yield return new WaitForSecondsRealtime(.075f);
            }
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.15f);
            Assert.That(Mathf.Abs(App.Player.Position.x-goal),Is.LessThan(1.4f),"Ordinary held run must reach the actual encounter.");
        }
        IEnumerator BeginJourney()
        {
            SmokeUi.Click("새 여정 시작");yield return null;
            Assert.That(App.Ui.CurrentView,Is.EqualTo("opening"));Assert.That(App.Journey.CodexCount,Is.Zero);
            Assert.That(GospelStudyCatalog.Opening.Count,Is.EqualTo(6));
            for(int i=0;i<6;i++)
            {SmokeUi.Click(i==5?"열두 살의 여정 시작":"이야기 계속");yield return null;}
            Assert.That(App.Journey.OpeningFinished,Is.True);Assert.That(App.Ui.CurrentView,Is.EqualTo("journey"));Assert.That(App.Journey.CodexCount,Is.Zero);
        }
        [UnityTest] public IEnumerator IllustratedOpeningAgeAndMidOpeningSaveUseRealCallbacks()
        {
            Assert.That(App,Is.Not.Null);Assert.That(App.SceneCamera.orthographic,Is.True);Assert.That(App.Ui.CurrentView,Is.EqualTo("title"));
            SmokeUi.AssertVisibleKorean("예수님의 길");yield return Shot("title");
            SmokeUi.Click("새 여정 시작");yield return null;yield return Shot("opening-birth");
            for(int i=0;i<3;i++){SmokeUi.Click("이야기 계속");yield return null;}
            Assert.That(App.Journey.OpeningPage,Is.EqualTo(3));Assert.That(App.Journey.CodexCount,Is.Zero);
            string slot=App.ActiveSaveSlot;Assert.That(backend.Exists(slot),Is.True);
            yield return SmokeUi.LoadBoot();SmokeUi.Click("이어하기");yield return null;
            Assert.That(App.Ui.CurrentView,Is.EqualTo("opening"));Assert.That(App.Journey.OpeningPage,Is.EqualTo(3));Assert.That(App.Journey.CodexCount,Is.Zero);
            SmokeUi.Click("이야기 계속");yield return null;SmokeUi.Click("이야기 계속");yield return null;yield return Shot("opening-age-twelve");
            SmokeUi.Click("열두 살의 여정 시작");yield return null;
            Assert.That(App.Ui.CurrentView,Is.EqualTo("journey"));Assert.That(App.Journey.HasStudyJourney,Is.True);Assert.That(App.Journey.CodexTotal,Is.EqualTo(15));
            yield return Shot("world-idle");LogAssert.NoUnexpectedReceived();
        }
        [UnityTest, Timeout(600000)] public IEnumerator PhysicalMotionInventoryAndCompleteFifteenReadingJourneyPersist()
        {
            yield return BeginJourney();yield return MoveTo(-20);float x=App.Player.Position.x;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D));yield return new WaitForSecondsRealtime(.25f);
            for(int i=0;i<3;i++){yield return SmokeUi.Capture("PathOfDawn","chronicle-"+attempt+"-walk-right-"+i,1280,720,settleSeconds:0f);yield return new WaitForSecondsRealtime(.13f);}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Assert.That(App.Player.Position.x-x,Is.GreaterThan(.5f));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.A));yield return new WaitForSecondsRealtime(.25f);
            for(int i=0;i<3;i++){yield return SmokeUi.Capture("PathOfDawn","chronicle-"+attempt+"-walk-left-"+i,1280,720,settleSeconds:0f);yield return new WaitForSecondsRealtime(.13f);}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D,UnityEngine.InputSystem.Key.LeftShift));yield return new WaitForSecondsRealtime(.35f);yield return Shot("run-right");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.A,UnityEngine.InputSystem.Key.LeftShift));yield return new WaitForSecondsRealtime(.35f);yield return Shot("run-left");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.2f);
            float ground=App.Player.Position.y;yield return Key(UnityEngine.InputSystem.Key.Space);yield return new WaitForSecondsRealtime(.10f);
            Assert.That(App.Player.Position.y,Is.GreaterThan(ground+.12f));yield return SmokeUi.Capture("PathOfDawn","chronicle-"+attempt+"-jump",1280,720,settleSeconds:0f);
            yield return new WaitForSecondsRealtime(1.2f);Assert.That(App.Player.Position.y,Is.EqualTo(ground).Within(.18f));yield return Shot("landed");
            for(int i=0;i<4;i++){InputSystem.QueueDeltaStateEvent(mouse.scroll,new Vector2(0,120));yield return null;}
            yield return new WaitForSecondsRealtime(.6f);Assert.That(App.Zoom,Is.EqualTo(3f).Within(.05f));yield return Shot("close-character");
            yield return Key(UnityEngine.InputSystem.Key.B);yield return null;
            // The fourth real inventory tile selects the lamp; its image and description precede equip.
            var tiles=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.gameObject.activeInHierarchy&&b.name=="Button ").ToArray();
            var lamp=SmokeUi.FindButton("등잔 장착");
            if(lamp==null||!lamp.gameObject.activeInHierarchy)
            {
                var tile=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponent<RectTransform>().anchoredPosition.x==36&&Mathf.Abs(b.GetComponent<RectTransform>().anchoredPosition.y+323)<1);
                Assert.That(tile,Is.Not.Null,"The fourth visible item tile must select the clay lamp.");tile.onClick.Invoke();yield return null;
            }
            SmokeUi.Click("등잔 장착");yield return null;Assert.That(App.Journey.LampEquipped,Is.True);yield return Shot("inventory-lamp");SmokeUi.Click("여정으로");yield return null;
            yield return Key(UnityEngine.InputSystem.Key.M);yield return Shot("codex-locked");Assert.That(App.Journey.CodexCount,Is.Zero);SmokeUi.Click("여정으로");yield return null;
            int encounters=0;bool restoredStudy=false;
            while(!App.Journey.IsComplete&&encounters<20)
            {
                yield return MoveTo(App.ObjectiveX);Assert.That(App.CanInteract,Is.True);int before=App.Journey.CodexCount;
                yield return Key(UnityEngine.InputSystem.Key.E);Assert.That(App.Ui.CurrentView,Is.EqualTo("reading"));
                if(App.Journey.StudyReadingOpen&&!restoredStudy)
                {
                    SmokeUi.Click("다음 말씀");yield return null;SmokeUi.Click("다음 말씀");yield return null;
                    int page=App.Journey.StudyDialogueIndex;Assert.That(page,Is.EqualTo(2));
                    SmokeUi.Click("질문하기");yield return null;SmokeUi.Click(GospelStudyQuestions.For(App.Journey.CurrentStudy.Reading.Id)[0]);yield return null;
                    Assert.That(App.Journey.CodexCount,Is.EqualTo(before));SmokeUi.Click("대화로 돌아가기");yield return null;yield return Shot("study-dialogue");
                    yield return Key(UnityEngine.InputSystem.Key.Escape);yield return Key(UnityEngine.InputSystem.Key.Escape);SmokeUi.Click("현재 여정 저장");yield return null;
                    float savedX=App.Player.Position.x;yield return SmokeUi.LoadBoot();SmokeUi.Click("이어하기");yield return null;
                    Assert.That(App.Journey.StudyReadingOpen,Is.True);Assert.That(App.Journey.StudyDialogueIndex,Is.EqualTo(page));Assert.That(App.Journey.CodexCount,Is.EqualTo(before));
                    Assert.That(App.Player.Position.x,Is.EqualTo(savedX).Within(.08f));Assert.That(App.Journey.LampEquipped,Is.True);yield return Shot("continued-mid-study");restoredStudy=true;
                }
                int turns=0;
                while(App.Ui.CurrentView=="reading"&&turns++<12){SmokeUi.Click("다음 말씀");yield return null;}
                Assert.That(App.Journey.CodexCount,Is.EqualTo(before+1),"A reading is acquired only after the final actual dialogue callback.");encounters++;
            }
            Assert.That(encounters,Is.EqualTo(15));Assert.That(restoredStudy,Is.True);Assert.That(App.Journey.IsComplete,Is.True);Assert.That(App.Journey.CodexCount,Is.EqualTo(15));
            yield return Shot("journey-complete");yield return Key(UnityEngine.InputSystem.Key.M);yield return Shot("codex-complete");
            Assert.That(backend.Exists(App.ActiveSaveSlot),Is.True);yield return SmokeUi.LoadBoot();SmokeUi.Click("이어하기");yield return null;
            Assert.That(App.Journey.IsComplete,Is.True);Assert.That(App.Journey.CodexCount,Is.EqualTo(15));Assert.That(App.Journey.LampEquipped,Is.True);LogAssert.NoUnexpectedReceived();
        }
    }
}
