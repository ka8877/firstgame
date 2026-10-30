using System;
using System.Collections;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.Rendering.Universal;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    public sealed class QuestSmokeTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs,testInputs;
        bool hadSlot;string previousSlot,attempt;
        GospelPilgrimageApp App=>UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();
        IEnumerator Click(string label){SmokeUi.Click(label);yield return null;yield return null;}
        IEnumerator Shot(string name,int width=1280,int height=720,float settle=.15f)=>
            SmokeUi.Capture("PathOfDawn","quest-"+attempt+"-"+name,width,height,settleSeconds:settle);
        void Keys(params Key[] keys)=>InputSystem.QueueStateEvent(keyboard,new KeyboardState(keys));
        static float Planar(Vector3 a,Vector3 b)=>Vector2.Distance(new Vector2(a.x,a.z),new Vector2(b.x,b.z));
        void AssertCarriedLampBounds()
        {
            var carried=GameObject.Find("Equipped travel lamp · preserved authored mesh and light");
            Assert.That(carried,Is.Not.Null);Assert.That(carried.activeInHierarchy,Is.True);
            var meshes=carried.GetComponentsInChildren<MeshRenderer>();Assert.That(meshes.Length,Is.GreaterThan(0));
            var bounds=meshes[0].bounds;foreach(var renderer in meshes.Skip(1))bounds.Encapsulate(renderer.bounds);
            float longest=Mathf.Max(bounds.size.x,Mathf.Max(bounds.size.y,bounds.size.z));
            var body=App.Actor.GetComponentInChildren<SpriteRenderer>();Assert.That(body,Is.Not.Null);
            Assert.That(longest,Is.InRange(.15f,.23f),"The real carried model must be a hand-sized oil lamp, not a torso-sized disk.");
            Assert.That(longest,Is.LessThan(body.bounds.size.y*.25f),"Actual lamp geometry must stay much smaller than the rendered person.");
            Assert.That(Vector3.Dot(carried.transform.up,Vector3.up),Is.GreaterThan(.999f));
            var flame=carried.GetComponentInChildren<Light>();Assert.That(flame,Is.Not.Null);
            Assert.That(flame.transform.localPosition.y,Is.InRange(.03f,.12f),"The flame light must sit just above the shallow authored bowl.");
            Debug.Log($"GOSPEL_QUEST_LAMP_BOUNDS adult={App.Actor.IsAdult} longestMetres={longest:F3} bodyHeightMetres={body.bounds.size.y:F3}");
        }
        [UnitySetUp]public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            attempt=Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT")??DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend=new MemorySaveBackend();backend.WriteAtomic("preserved-player","original player bytes");
            storage=PersistentSaves.UseBackendForTesting(backend);
            hadSlot=PlayerPrefs.HasKey("GOSPEL.activeSlot");previousSlot=PlayerPrefs.GetString("GOSPEL.activeSlot","");PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            previousInputs=InputSystem.settings;testInputs=UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags=HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;InputSystem.settings=testInputs;
            keyboard=InputSystem.AddDevice<Keyboard>();yield return SmokeUi.LoadBoot();yield return new WaitForSecondsRealtime(.25f);
        }
        [UnityTearDown]public IEnumerator TearDown()
        {
            Keys();yield return SmokeUi.UnloadBoot();if(keyboard!=null&&keyboard.added)InputSystem.RemoveDevice(keyboard);
            if(previousInputs!=null)InputSystem.settings=previousInputs;storage?.Dispose();
            if(hadSlot)PlayerPrefs.SetString("GOSPEL.activeSlot",previousSlot);else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");PlayerPrefs.Save();
            if(Application.isPlaying)yield return new ExitPlayMode();
            if(testInputs!=null&&InputSystem.settings!=testInputs)UnityEngine.Object.DestroyImmediate(testInputs);
        }
        IEnumerator FinishReading()
        {
            int index=App.Chronicle.QuestIndex,found=App.Chronicle.DiscoveredCount,guard=0;
            bool earns=App.Chronicle.CurrentQuest.Reading!=null;
            while(!App.Chronicle.IsComplete&&App.Chronicle.QuestIndex==index&&guard++<12)
            {
                Assert.That(App.Ui.CurrentView,Is.EqualTo("reading"));
                bool last=App.Chronicle.DialogueIndex+1==App.Chronicle.DialogueCount;
                yield return Click("다음 말씀");
                Assert.That(App.Chronicle.DiscoveredCount,Is.EqualTo(found+(last&&earns?1:0)),"Only the final actual reading callback discovers a passage.");
            }
            Assert.That(App.Chronicle.QuestIndex,Is.EqualTo(index+1));
        }
        IEnumerator MoveTo(Vector3 target)
        {
            Assert.That(App.IsModal,Is.False);yield return null;
            float deadline=Time.realtimeSinceStartup+35,stuck=Time.realtimeSinceStartup;
            Vector3 previous=App.Player.Position;
            while(Planar(App.Player.Position,target)>.78f&&Time.realtimeSinceStartup<deadline)
            {
                Vector3 d=target-App.Player.Position;
                var keys=new System.Collections.Generic.List<Key>{Key.LeftShift};
                if(Mathf.Abs(d.x)>.32f)keys.Add(d.x>0?Key.D:Key.A);
                if(Mathf.Abs(d.z)>.32f)keys.Add(d.z>0?Key.W:Key.S);
                Keys(keys.ToArray());yield return new WaitForSecondsRealtime(.075f);
                if(Planar(previous,App.Player.Position)>.1f){stuck=Time.realtimeSinceStartup;previous=App.Player.Position;}
                else if(Time.realtimeSinceStartup-stuck>1.3f)
                {
                    // Normal physical keyboard steering around a collider; no
                    // pose teleport, controller disable or story mutation.
                    Keys(Key.LeftShift,Mathf.Abs(d.x)>Mathf.Abs(d.z)?Key.W:Key.D);
                    yield return new WaitForSecondsRealtime(.45f);Keys();yield return null;stuck=Time.realtimeSinceStartup;
                }
            }
            Keys();yield return new WaitForSecondsRealtime(.14f);
            Assert.That(Planar(App.Player.Position,target),Is.LessThan(1.5f),"Actual free XZ movement did not reach the physical NPC.");
        }
        IEnumerator Hold(Key key,string face,string name)
        {
            var before=App.Player.Position;Keys(key);yield return new WaitForSecondsRealtime(.65f);
            Assert.That(Planar(before,App.Player.Position),Is.GreaterThan(.6f));
            Assert.That(App.Actor.CurrentFacing,Is.EqualTo(face));Assert.That(App.Actor.CurrentState,Is.EqualTo("Walk"));
            yield return Shot(name,1280,720,0);Keys();yield return new WaitForSecondsRealtime(.16f);
        }
        [UnityTest]public IEnumerator FreeFieldFourFacingsActualNpcSequenceNineteenBookAndXyzResume()
        {
            Assert.That(App,Is.Not.Null);SmokeUi.AssertNoMissingScripts();
            Assert.That(App.SceneCamera.GetUniversalAdditionalCameraData().scriptableRenderer.GetType().Name,Is.EqualTo("UniversalRenderer"));
            Assert.That(App.World.GroundTriangleCount,Is.GreaterThan(1000));Assert.That(App.World.AuthoredPropCount,Is.GreaterThan(12));
            Assert.That(App.World.ArchitecturalMeshCount,Is.GreaterThan(12));
            Assert.That(App.Actor.AnimationFrameCount,Is.EqualTo(12));Assert.That(App.Actor.UsesMeasuredLayout,Is.True);
            yield return Shot("title");yield return Click("새 여정 시작");
            App.ShowBible();yield return null;Assert.That(App.Ui.CurrentView,Is.EqualTo("bible"));
            Assert.That(App.Chronicle.DiscoveredCount,Is.Zero);
            string visible=string.Join("\n",UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None).Where(t=>t.gameObject.activeInHierarchy).Select(t=>t.text));
            foreach(var reading in GospelChronicleCatalog.Readings){Assert.That(visible,Does.Not.Contain(reading.Title));Assert.That(visible,Does.Not.Contain(reading.Reference));Assert.That(visible,Does.Not.Contain(reading.Text));}
            yield return Shot("bible-hidden");yield return Click("여정으로");
            while(App.Chronicle.CurrentQuest.IsPrologue)yield return FinishReading();
            Assert.That(App.Ui.CurrentView,Is.EqualTo("chapter"));yield return Shot("child-chapter");yield return Click("이야기 속으로");
            Assert.That(App.Actor.IsAdult,Is.False);Assert.That(App.Journey.PresentationSpace,Is.EqualTo(GospelJourney.PilgrimageSpace));
            yield return Shot("child-field");yield return Shot("child-field-mobile",844,390);
            yield return Hold(Key.D,"Right","child-walk-right");yield return Hold(Key.A,"Left","child-walk-left");
            yield return Hold(Key.W,"Back","child-walk-back");yield return Hold(Key.S,"Front","child-walk-front");
            Vector3 diagonal=App.Player.Position;Keys(Key.W,Key.D,Key.LeftShift);yield return new WaitForSecondsRealtime(.6f);
            Assert.That(App.Player.Position.x-diagonal.x,Is.GreaterThan(.5f));Assert.That(App.Player.Position.z-diagonal.z,Is.GreaterThan(.5f));
            Assert.That(App.Actor.CurrentState,Is.EqualTo("Run"));yield return Shot("child-diagonal-run",1280,720,0);Keys();yield return new WaitForSecondsRealtime(.2f);
            var goal=App.ObjectivePosition;var wrongDepth=new Vector3(goal.x,goal.y,goal.z-5.5f);
            yield return MoveTo(wrongDepth);int originalQuest=App.Chronicle.QuestIndex;
            Assert.That(Mathf.Abs(App.Player.Position.x-goal.x),Is.LessThan(1.5f));Assert.That(App.CanInteract,Is.False);
            App.Interact();yield return null;Assert.That(App.Chronicle.QuestIndex,Is.EqualTo(originalQuest));Assert.That(App.Ui.CurrentView,Is.EqualTo("journey"));
            App.ShowStatus();yield return null;Assert.That(SmokeUi.HasText("발견한 말씀"),Is.True);yield return Shot("actual-character-status");yield return Shot("actual-character-status-mobile",844,390);
            App.Resume();yield return null;App.ShowInventory();yield return null;yield return Click("등잔 장착");
            Assert.That(App.Actor.LampEquipped,Is.True);yield return Shot("inventory-equipped");yield return Click("여정으로");
            var lamp=GameObject.Find("Equipped travel lamp · preserved authored mesh and light");
            Assert.That(lamp,Is.Not.Null);Assert.That(Vector3.Dot(lamp.transform.up,Vector3.up),Is.GreaterThan(.999f));
            AssertCarriedLampBounds();yield return Shot("lamp-equipped-field");yield return Shot("lamp-equipped-field-mobile",844,390);
            bool adultCaptured=false,readingResumed=false;int guard=0;
            while(!App.Chronicle.IsComplete&&guard++<18)
            {
                if(App.Ui.CurrentView=="chapter")
                {
                    Assert.That(App.Actor.IsAdult,Is.True);yield return Shot("adult-chapter");yield return Click("이야기 속으로");
                }
                yield return MoveTo(App.ObjectivePosition);Assert.That(App.CanInteract,Is.True);
                if(App.Actor.IsAdult&&!adultCaptured)
                {adultCaptured=true;yield return Shot("adult-field");yield return Shot("adult-field-mobile",844,390);}
                App.Interact();yield return null;Assert.That(App.Ui.CurrentView,Is.EqualTo("reading"));
                if(!readingResumed)
                {
                    readingResumed=true;int q=App.Chronicle.QuestIndex,page=App.Chronicle.DialogueIndex;
                    Vector3 savedPosition=App.Player.Position;Assert.That(App.Save(false),Is.True);string slot=App.ActiveSaveSlot;
                    yield return SmokeUi.LoadBoot();yield return Click("이어하기");
                    Assert.That(App.ActiveSaveSlot,Is.EqualTo(slot));Assert.That(App.Chronicle.QuestIndex,Is.EqualTo(q));Assert.That(App.Chronicle.DialogueIndex,Is.EqualTo(page));
                    Assert.That(Planar(App.Player.Position,savedPosition),Is.LessThan(.1f));yield return Shot("actual-dialogue-xyz-continued");
                }
                if(App.Chronicle.CurrentQuest.NpcId=="john-baptist")yield return Shot("john-scripture-dialogue-"+App.Chronicle.QuestIndex);
                yield return FinishReading();
            }
            Assert.That(App.Chronicle.IsComplete,Is.True);Assert.That(App.Chronicle.DiscoveredCount,Is.EqualTo(19));Assert.That(App.Actor.IsAdult,Is.True);
            AssertCarriedLampBounds();yield return Shot("completed-adult-field");yield return Shot("adult-lamp-equipped-field");
            Assert.That(App.Save(false),Is.True);var pose=App.Player.Position;string finalSlot=App.ActiveSaveSlot;
            yield return SmokeUi.LoadBoot();yield return Click("이어하기");
            Assert.That(App.ActiveSaveSlot,Is.EqualTo(finalSlot));Assert.That(App.Chronicle.DiscoveredCount,Is.EqualTo(19));Assert.That(App.Actor.IsAdult,Is.True);
            Assert.That(Planar(App.Player.Position,pose),Is.LessThan(.12f));Assert.That(App.Journey.LampEquipped,Is.True);
            yield return Shot("completed-xyz-continued");App.ShowBible();yield return null;yield return Shot("bible-nineteen-discovered");
            Assert.That(backend.Read("preserved-player"),Is.EqualTo("original player bytes"));
        }

        [UnityTest]public IEnumerator RealPreviousLateralSaveForksWithoutTouchingItsOriginalBytes()
        {
            var legacy=GospelJourney.NewChronicleJourney();
            while(legacy.Chronicle.CurrentQuest.IsPrologue)legacy.Chronicle.AdvanceDialogue();
            legacy.SetPresentationSpace(GospelJourney.LuminousSpace);legacy.SetPlayerPose(-27,.1f,0,180);
            const string oldSlot="old-chronicle-player";
            Assert.That(PersistentSaves.TrySave(oldSlot,GospelJourney.GameId,GospelJourney.SchemaVersion,GospelJourney.ContentVersion,
                GospelSaveCodec.Encode(legacy.CaptureSave()),out var error),Is.True,error);
            string original=backend.Read(oldSlot);int found=legacy.CodexCount;
            PlayerPrefs.SetString("GOSPEL.activeSlot",oldSlot);PlayerPrefs.Save();yield return SmokeUi.LoadBoot();yield return Click("이어하기");
            Assert.That(App.MigratedSourceSlot,Is.EqualTo(oldSlot));Assert.That(App.ActiveSaveSlot,Is.Not.EqualTo(oldSlot));
            Assert.That(backend.Read(oldSlot),Is.EqualTo(original));Assert.That(App.Journey.CodexCount,Is.EqualTo(found));
            Assert.That(App.Journey.PresentationSpace,Is.EqualTo(GospelJourney.PilgrimageSpace));
            Assert.That(App.World.IsInside(App.Player.Position),Is.True);Assert.That(Mathf.Abs(App.Player.Position.z),Is.GreaterThan(1));
            yield return Shot("legacy-save-preserved-new-free-field");
            Assert.That(App.Save(false),Is.True);Assert.That(backend.Read(oldSlot),Is.EqualTo(original));
        }
    }
}
