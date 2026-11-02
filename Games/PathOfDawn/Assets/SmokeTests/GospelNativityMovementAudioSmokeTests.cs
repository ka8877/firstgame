using System;
using System.Collections;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Actual Unity voices and physical movement; not a listening approval.</summary>
    public sealed class GospelNativityMovementAudioSmokeTests
    {
        const string VolumeKey = "GOSPEL.music.volume", MutedKey = "GOSPEL.music.muted";
        bool hadVolume, hadMuted;
        float previousVolume;
        int previousMuted;
        GameObject owner;
        GospelStorybookAudio audio;
        GospelPilgrimageMotor traveler;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            hadVolume = PlayerPrefs.HasKey(VolumeKey); hadMuted = PlayerPrefs.HasKey(MutedKey);
            previousVolume = PlayerPrefs.GetFloat(VolumeKey); previousMuted = PlayerPrefs.GetInt(MutedKey);
            PlayerPrefs.SetFloat(VolumeKey, .41f); PlayerPrefs.SetInt(MutedKey, 0);
            previousInputs = InputSystem.settings; testInputs = UnityEngine.Object.Instantiate(previousInputs);
            testInputs.hideFlags = HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode = InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings = testInputs; keyboard = InputSystem.AddDevice<Keyboard>();
            owner = new GameObject("Nativity audio physical integration fixture");
            var camera = new GameObject("Fixture listener", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
            camera.transform.SetParent(owner.transform); camera.transform.rotation = Quaternion.Euler(48, 0, 0);
            var floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
            floor.name = "Actual collider for grounded footstep and landing";
            floor.transform.SetParent(owner.transform); floor.transform.position = new Vector3(0, -.20f, 0);
            floor.transform.localScale = new Vector3(36, .3f, 36);
            var actor = new GameObject("Actual walking audio fixture"); actor.transform.SetParent(owner.transform);
            traveler = actor.AddComponent<GospelPilgrimageMotor>(); traveler.Initialize(camera);
            traveler.SetPose(new Vector3(0, .25f, 0));
            audio = GospelStorybookAudio.Attach(owner, traveler); audio.NotifyUserGesture();
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (keyboard != null && keyboard.added)
            { InputSystem.QueueStateEvent(keyboard, new KeyboardState()); InputSystem.RemoveDevice(keyboard); }
            if (previousInputs != null) InputSystem.settings = previousInputs;
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (hadVolume) PlayerPrefs.SetFloat(VolumeKey, previousVolume); else PlayerPrefs.DeleteKey(VolumeKey);
            if (hadMuted) PlayerPrefs.SetInt(MutedKey, previousMuted); else PlayerPrefs.DeleteKey(MutedKey);
            PlayerPrefs.Save(); yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
            if (testInputs != null && InputSystem.settings != testInputs) UnityEngine.Object.DestroyImmediate(testInputs);
        }

        AudioSource Voice(string clip) => owner.GetComponents<AudioSource>().Single(source => source.clip != null && source.clip.name == clip && source.loop);
        int Count(string cue) => audio.History.Count(entry => entry.cue == cue);
        IEnumerator WaitFor(Func<bool> ready, string failure, float seconds = 8)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, failure);
        }

        [UnityTest]
        public IEnumerator ChapterVoicesPhysicalCuesAndPreferenceMuteHaveActualSources()
        {
            foreach (string chapter in new[] { "birth", "temple", "jordan" })
            {
                foreach (string stem in new[] { "music-", "ambience-" })
                {
                    var resource = chapter == "birth" && stem == "music-" ? "Audio/GospelNativity/music-birth" : "Audio/GospelStorybook/" + stem + chapter;
                    var clip = Resources.Load<AudioClip>(resource);
                    Assert.That(clip, Is.Not.Null, "Every selected chapter stem must actually ship.");
                    Assert.That(clip.channels, Is.EqualTo(2));
                    if (stem == "music-") Assert.That(clip.length, Is.GreaterThan(30));
                    else Assert.That(clip.length, Is.EqualTo(18).Within(.05));
                    clip.LoadAudioData();
                    yield return WaitFor(() => clip.loadState == AudioDataLoadState.Loaded, "Chapter sound did not load: " + clip.name);
                }
            }
            Assert.That(audio.Volume, Is.EqualTo(.41f).Within(.001)); Assert.That(audio.Muted, Is.False);
            yield return WaitFor(() => audio.IsPlaying && audio.ActiveChapter == "birth", "Real birth arrangement must start.");
            yield return new WaitForSecondsRealtime(.20f);
            Assert.That(Voice("music-birth").isPlaying, Is.True);
            Assert.That(Voice("ambience-birth").isPlaying, Is.True);
            Assert.That(Voice("music-birth").time, Is.GreaterThan(0));
            Assert.That(Voice("ambience-birth").volume, Is.LessThan(Voice("music-birth").volume));
            int transitions = audio.ChapterTransitionCount;
            float sameRoleTime = Voice("music-birth").time;
            audio.SetChapter("birth"); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(audio.ChapterTransitionCount, Is.EqualTo(transitions));
            Assert.That(Voice("music-birth").time, Is.GreaterThan(sameRoleTime), "Same role redraw must not restart its song.");
            foreach (string chapter in new[] { "temple", "baptism" })
            {
                string expected = chapter == "baptism" ? "jordan" : chapter;
                audio.SetChapter(chapter);
                yield return WaitFor(() => audio.ActiveChapter == expected && audio.IsPlaying, "New arrangement must become active.");
                Assert.That(Voice("music-" + expected).time, Is.LessThan(.25f), "A newly selected chapter begins at its start.");
                yield return new WaitForSecondsRealtime(1.25f);
                Assert.That(Voice("ambience-" + expected).isPlaying, Is.True);
                foreach (string other in new[] { "birth", "temple", "jordan" }.Where(value => value != expected))
                { Assert.That(Voice("music-" + other).isPlaying, Is.False); Assert.That(Voice("ambience-" + other).isPlaying, Is.False); }
            }
            Assert.That(audio.ChapterTransitionCount, Is.EqualTo(transitions + 2));

            // The movement fixture is outside the cinematic opening; its optional field cues remain real.
            audio.SetOpeningCinematic(false);
            foreach (string cue in new[] { "footstep", "jump", "land", "page", "dialogue", "scripture", "ui", "dash" })
            {
                var clip = Resources.Load<AudioClip>("Audio/GospelStorybook/" + cue);
                Assert.That(clip, Is.Not.Null); Assert.That(clip.channels, Is.EqualTo(2));
                int count = audio.PlayedCount; audio.Play(cue); yield return null;
                Assert.That(audio.PlayedCount, Is.EqualTo(count + 1));
                Assert.That(audio.LastCue, Is.EqualTo(cue));
                Assert.That(owner.GetComponents<AudioSource>().Any(source => source.clip == clip && !source.loop && source.isPlaying),
                    Is.True, "Requested cue must own a real playing voice: " + cue);
            }

            yield return WaitFor(() => traveler.IsGrounded, "Traveler must reach the real collider before motion sound checks.");
            int steps = audio.FootstepCount;
            InputSystem.QueueStateEvent(keyboard, new KeyboardState(Key.W));
            Vector3 before = traveler.Position;
            yield return new WaitForSecondsRealtime(.95f);
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            Assert.That(traveler.Position.z - before.z, Is.GreaterThan(1));
            Assert.That(audio.FootstepCount, Is.GreaterThan(steps), "Real grounded displacement must produce footsteps.");
            traveler.Enabled = false; yield return new WaitForSecondsRealtime(.2f);
            int stoppedSteps = audio.FootstepCount; yield return new WaitForSecondsRealtime(.25f);
            Assert.That(audio.FootstepCount, Is.EqualTo(stoppedSteps), "A modal/disabled traveler must be silent.");
            traveler.Enabled = true; yield return null; yield return null;
            int jumps = Count("jump"), lands = Count("land");
            traveler.RequestJump(); yield return new WaitForSecondsRealtime(.20f);
            Assert.That(Count("jump"), Is.GreaterThan(jumps));
            yield return WaitFor(() => traveler.IsGrounded && Count("land") > lands, "Real jump landing must own its impact cue.", 3);
            int dashes = Count("dash"); traveler.RequestDash();
            yield return new WaitForSecondsRealtime(.06f);
            Assert.That(Count("dash"), Is.GreaterThan(dashes));

            audio.SetVolume(.23f); yield return null;
            Assert.That(audio.Volume, Is.EqualTo(.23f).Within(.001));
            Assert.That(Voice("music-jordan").volume, Is.EqualTo(.23f * .83f).Within(.005));
            float position = Voice("music-jordan").time;
            int played = audio.PlayedCount; audio.SetMuted(true); audio.Play("scripture");
            yield return new WaitForSecondsRealtime(.18f);
            Assert.That(audio.PlayedCount, Is.EqualTo(played));
            Assert.That(owner.GetComponents<AudioSource>().Any(source => source.isPlaying), Is.False);
            Assert.That(Voice("music-jordan").time, Is.EqualTo(position).Within(.05));
            audio.SetMuted(false); yield return new WaitForSecondsRealtime(.15f);
            Assert.That(audio.IsPlaying, Is.True);
            Assert.That(Voice("music-jordan").time, Is.GreaterThan(position));
            Debug.Log("GOSPEL_NATIVITY_MOVEMENT_AUDIO chapters=3 ambience=3 cues=8 actualMovementSteps=" +
                (stoppedSteps - steps) + " physicalJumpLandingDash=true settingsRestoredByTearDown=true");
        }
    }
}
