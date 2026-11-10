using System;
using System.Collections;
using System.Collections.Generic;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    public sealed class GospelLivingWorldSmokeTests
    {
        IDisposable storage;
        MemorySaveBackend backend;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;
        bool hadSlot;
        string oldSlot;
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();
        void Keys(params Key[] keys) => InputSystem.QueueStateEvent(keyboard, new KeyboardState(keys));
        static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));
        IEnumerator Click(string text) { SmokeUi.Click(text); yield return null; yield return null; }
        IEnumerator Shot(string name) => SmokeUi.Capture("PathOfDawn", "living-" + name, 1280, 720, settleSeconds: .15f);
        string VisibleUiText()
        {
            var text = new List<string>();
            foreach (var label in App.Ui.CanvasView.GetComponentsInChildren<TMPro.TextMeshProUGUI>())
                if (label.gameObject.activeInHierarchy) text.Add(label.text);
            return string.Join(" | ", text);
        }

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            backend = new MemorySaveBackend(); backend.WriteAtomic("living-preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); oldSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            previousInputs = InputSystem.settings; testInputs = UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus; InputSystem.settings = testInputs;
            keyboard = InputSystem.AddDevice<Keyboard>();
            yield return SmokeUi.LoadBoot(); yield return new WaitForSecondsRealtime(.2f);
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            Keys(); yield return SmokeUi.UnloadBoot();
            if (keyboard != null && keyboard.added) InputSystem.RemoveDevice(keyboard);
            if (previousInputs != null) InputSystem.settings = previousInputs;
            Assert.That(backend.Read("living-preserved-player"), Is.EqualTo("original player bytes"));
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        IEnumerator ReachFirstField()
        {
            yield return Click("새 여정 시작");
            int turns = 0;
            while (App.Chronicle.CurrentQuest.IsPrologue && turns++ < 60) yield return Click("이야기 계속");
            Assert.That(turns, Is.EqualTo(51), "Keep the actual unchanged opening, not injected live story progress.");
            yield return Click("이야기 속으로");
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("nazareth-departure"));
            Assert.That(App.People.NamedCount, Is.EqualTo(5)); Assert.That(App.People.AmbientCount, Is.EqualTo(6));
            yield return new WaitForSecondsRealtime(.2f);
        }

        IEnumerator ApproachCurrentPerson(bool catchWhileWalking = false)
        {
            float deadline = Time.realtimeSinceStartup + 35;
            while (Time.realtimeSinceStartup < deadline)
            {
                var delta = App.ObjectivePosition - App.Player.Position;
                if (App.CanInteract && (!catchWhileWalking || App.People.CurrentPerson.IsWalking)) break;
                var keys = new List<Key>();
                if (Planar(App.ObjectivePosition, App.Player.Position) > 1.1f)
                {
                    if (Mathf.Abs(delta.x) > .12f) keys.Add(delta.x > 0 ? Key.D : Key.A);
                    if (Mathf.Abs(delta.z) > .12f) keys.Add(delta.z > 0 ? Key.W : Key.S);
                }
                Keys(keys.ToArray()); yield return new WaitForSecondsRealtime(.035f);
            }
            Keys();
            Assert.That(App.CanInteract, Is.True, "Actual input must reach the moving target and the unchanged Core landmark range.");
            if (catchWhileWalking) Assert.That(App.People.CurrentPerson.IsWalking, Is.True, "Accept during actual movement, not an already idle checkpoint.");
        }

        IEnumerator BeginConversation()
        {
            Keys(Key.E); yield return null; yield return null; Keys();
            yield return SmokeUi.WaitUntil(() => App.Chronicle.Stage == GospelQuestStage.Reading, 3, "Actual E input did not open the nearby conversation.");
            Assert.That(App.Ui.CurrentView, Is.EqualTo("story"));
        }

        IEnumerator FinishCurrentConversation()
        {
            string quest = App.Chronicle.CurrentQuest.Id;
            int guard = 0;
            while (!App.Chronicle.IsComplete && App.Chronicle.CurrentQuest.Id == quest && guard++ < 12)
                yield return Click("이야기 계속");
            Assert.That(guard, Is.LessThan(12));
            if (App.Ui.CurrentView == "scripture-acquired") yield return Click("이야기 계속");
            yield return null;
        }

        [UnityTest] public IEnumerator DialogueCompletionKeepsJosephAndPeopleAcrossBookAndReload()
        {
            yield return ReachFirstField();
            var joseph = App.People.CurrentPerson.RootTransform;
            int identity = joseph.GetInstanceID(); Vector3 before = joseph.position;
            yield return Shot("people-before-dialogue");
            yield return ApproachCurrentPerson(); yield return BeginConversation();
            yield return FinishCurrentConversation();
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("pilgrim-spring"));
            Assert.That(joseph != null && joseph.gameObject.activeInHierarchy, Is.True,
                "Finishing Joseph's conversation must retain the original actor in the live field.");
            Assert.That(joseph.GetInstanceID(), Is.EqualTo(identity));
            yield return new WaitForSecondsRealtime(1.1f);
            Assert.That(Planar(before, joseph.position), Is.GreaterThan(.35f), "The same Joseph actually walks onward.");
            Assert.That(App.People.Count, Is.EqualTo(11));
            yield return Shot("joseph-walks-after-dialogue");
            Assert.That(App.Save(false), Is.True);
            App.ShowBible(); yield return null; App.Resume(); yield return null;
            Assert.That(joseph.gameObject.activeInHierarchy && joseph.GetInstanceID() == identity, Is.True);
            App.ContinueGame(); yield return null; yield return null;
            Assert.That(App.InGame, Is.True, VisibleUiText()); Assert.That(App.People.Count, Is.EqualTo(11));
            Assert.That(App.People.CurrentPerson.Id, Is.EqualTo("mary"));
            Assert.That(joseph.gameObject.activeInHierarchy && joseph.GetInstanceID() == identity, Is.True);
            yield return Shot("people-after-reload");
        }

        [UnityTest] public IEnumerator BackgroundPilgrimsActuallyWalkOnGroundWithoutReplacingStoryActors()
        {
            yield return ReachFirstField();
            var positions = new Dictionary<string, Vector3>();
            var traveledDistance = new Dictionary<string, float>();
            var identities = new Dictionary<string, int>();
            foreach (var person in App.People.Markers)
            {
                positions.Add(person.Id, person.Position); identities.Add(person.Id, person.RootTransform.GetInstanceID());
                traveledDistance.Add(person.Id, 0);
                if (person.IsAmbient)
                {
                    var actor = person.RootTransform.GetComponentInChildren<GospelPilgrimageActor>();
                    Assert.That(actor.OriginalAtlasResource, Is.EqualTo("Art/GospelRpg/Actors/pilgrims"));
                    Assert.That(actor.UsesExpandedAnimation, Is.True);
                }
            }
            float until = Time.realtimeSinceStartup + 4;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                foreach (var person in App.People.Markers)
                {
                    traveledDistance[person.Id] += Planar(positions[person.Id], person.Position);
                    positions[person.Id] = person.Position;
                }
            }
            int traveled = 0;
            foreach (var person in App.People.Markers)
            {
                Assert.That(person.RootTransform.GetInstanceID(), Is.EqualTo(identities[person.Id]));
                Assert.That(App.World.IsInside(person.Position), Is.True);
                Assert.That(Mathf.Abs(person.Position.y - App.World.HeightAt(person.Position.x, person.Position.z)), Is.LessThan(.15f));
                if (person.IsAmbient)
                {
                    Debug.Log("GOSPEL_LIVING_ACTUAL_TRAVEL id=" + person.Id + " distance=" + traveledDistance[person.Id].ToString("F4") +
                        " end=" + person.Position.ToString("F4") + " walking=" + person.IsWalking);
                    if (traveledDistance[person.Id] > .4f) traveled++;
                }
            }
            Assert.That(traveled, Is.EqualTo(6), "Each background person must traverse their actual route, rather than being a decorative static marker.");
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("nazareth-departure"));
            Assert.That(App.People.CurrentPerson.Id, Is.EqualTo("joseph"));
            yield return Shot("ambient-pilgrims");
        }

        [UnityTest] public IEnumerator MovingJohnConversationRestoresExactAcceptedNpcPose()
        {
            // Construct a valid earlier-format save with public Core actions,
            // then load it normally. No live scene state or person is injected.
            var journey = GospelJourney.NewChronicleJourney();
            while (journey.Chronicle.CurrentQuest.Id != "john-calling")
            {
                var c = journey.Chronicle; var q = c.CurrentQuest;
                if (c.Stage == GospelQuestStage.Travel) Assert.That(c.TryArrive(q.LandmarkId, q.X, out _), Is.True);
                if (c.Stage == GospelQuestStage.Talk) Assert.That(c.TryTalk(q.NpcId, out _), Is.True);
                if (c.Stage == GospelQuestStage.Reading) Assert.That(c.AdvanceStory(out _), Is.True);
            }
            journey.SetPresentationSpace(GospelJourney.PilgrimageSpace);
            journey.SetPlayerPose(-13, .5f, 1, 90);
            Assert.That(PersistentSaves.TrySave(App.ActiveSaveSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, GospelSaveCodec.Encode(journey.CaptureSave()), out var error), Is.True, error);
            App.ContinueGame(); yield return null; yield return new WaitForSecondsRealtime(.3f);
            Assert.That(App.People.Chapter, Is.EqualTo("baptism")); Assert.That(App.People.NamedCount, Is.EqualTo(4));
            Assert.That(GameObject.Find("Encounter · 마리아"), Is.Null, "Temple people cannot remain visible in the adult Jordan chapter.");
            yield return ApproachCurrentPerson(); yield return BeginConversation(); yield return FinishCurrentConversation();
            Assert.That(App.Chronicle.CurrentQuest.Id, Is.EqualTo("baptism-request"));
            yield return ApproachCurrentPerson(true); yield return BeginConversation();
            var john = App.People.CurrentPerson.RootTransform; Vector3 accepted = john.position;
            Assert.That(Planar(accepted, App.World.PositionForQuest(App.Chronicle.CurrentQuest)), Is.GreaterThan(.2f),
                "This must be a real moving encounter, not a save made after arriving at the fixed story anchor.");
            yield return new WaitForSecondsRealtime(.3f);
            Assert.That(Vector3.Distance(john.position, accepted), Is.LessThan(.01f), "The accepted partner plants their feet throughout the conversation.");
            App.ShowBible(); yield return null; App.Resume(); yield return null;
            Assert.That(Vector3.Distance(john.position, accepted), Is.LessThan(.01f));
            App.ContinueGame(); yield return null; yield return null;
            Assert.That(App.InGame, Is.True); Assert.That(App.Chronicle.Stage, Is.EqualTo(GospelQuestStage.Reading));
            Assert.That(Vector3.Distance(App.People.CurrentPerson.Position, accepted), Is.LessThan(.01f));
            yield return Shot("moving-john-restored");

            Assert.That(PersistentSaves.TryLoad(App.ActiveSaveSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, out var payload, out error), Is.True, error);
            Assert.That(payload, Does.Contain("\"pilgrimagePeople\":"));
            string malformed = payload.Replace("\"pilgrimagePeople\":", "\"pilgrimagePeople\":null,\"preservedPreviousPeople\":");
            Assert.That(PersistentSaves.TrySave(App.ActiveSaveSlot, GospelJourney.GameId, GospelJourney.SchemaVersion,
                GospelJourney.ContentVersion, malformed, out error), Is.True, error);
            string slot = App.ActiveSaveSlot, original = backend.Read(slot);
            App.ContinueGame(); yield return null;
            Assert.That(App.InGame, Is.False); Assert.That(App.Ui.CurrentView, Is.EqualTo("title"));
            Assert.That(backend.Read(slot), Is.EqualTo(original), "Malformed optional metadata must preserve the actual original save, never overwrite it with a reset world.");
        }
    }
}
