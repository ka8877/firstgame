using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Actual imported narration/music sources; independent of human listening approval.</summary>
    public sealed class GospelNativityAudioSmokeTests
    {
        const string VolumeKey = "GOSPEL.music.volume", MutedKey = "GOSPEL.music.muted", VoiceKey = "GOSPEL.voice.enabled";
        bool hadVolume, hadMuted, hadVoice;
        float oldVolume;
        int oldMuted, oldVoice;
        GameObject owner;
        GospelStorybookAudio audio;
        GospelNativityVoice voice;

        [Serializable] sealed class VoiceProduction { public VoiceRecording[] lines; }
        [Serializable] sealed class VoiceRecording
        {
            public string key, resourcePath, file, sha256;
            public float seconds;
            public int sampleRate, channels;
        }

        static string FileSha256(string path)
        {
            using (var stream = File.OpenRead(path))
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            hadVolume = PlayerPrefs.HasKey(VolumeKey); oldVolume = PlayerPrefs.GetFloat(VolumeKey);
            hadMuted = PlayerPrefs.HasKey(MutedKey); oldMuted = PlayerPrefs.GetInt(MutedKey);
            hadVoice = PlayerPrefs.HasKey(VoiceKey); oldVoice = PlayerPrefs.GetInt(VoiceKey);
            PlayerPrefs.SetFloat(VolumeKey, .43f); PlayerPrefs.SetInt(MutedKey, 0); PlayerPrefs.SetInt(VoiceKey, 1);
            owner = new GameObject("Nativity real imported narration and music fixture");
            new GameObject("Nativity fixture listener", typeof(AudioListener)).transform.SetParent(owner.transform);
            audio = GospelStorybookAudio.Attach(owner, null); audio.NotifyUserGesture();
            voice = GospelNativityVoice.Attach(owner, audio);
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (owner != null) UnityEngine.Object.Destroy(owner);
            if (hadVolume) PlayerPrefs.SetFloat(VolumeKey, oldVolume); else PlayerPrefs.DeleteKey(VolumeKey);
            if (hadMuted) PlayerPrefs.SetInt(MutedKey, oldMuted); else PlayerPrefs.DeleteKey(MutedKey);
            if (hadVoice) PlayerPrefs.SetInt(VoiceKey, oldVoice); else PlayerPrefs.DeleteKey(VoiceKey);
            PlayerPrefs.Save(); yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        IEnumerator WaitFor(Func<bool> ready, string failure, float seconds = 8)
        {
            float deadline = Time.realtimeSinceStartup + seconds;
            while (!ready() && Time.realtimeSinceStartup < deadline) yield return null;
            Assert.That(ready(), Is.True, failure);
        }

        static GospelDialogueTurn ActualTurn(GospelNativityVoice.Line line)
        {
            var quest = GospelChronicleCatalog.Quests.Single(item => item.Id == line.questId);
            for (int legacy = 0; legacy < quest.Dialogue.Count; legacy++)
                for (int detail = 0; detail < GospelStorybookCatalog.GetDetails(quest, legacy).Count; detail++)
                    if (GospelStorybookCatalog.GetStoryBeatIndex(quest, legacy, detail) == line.beatIndex)
                        return GospelStorybookCatalog.GetDetail(quest, legacy, detail).Turn;
            Assert.Fail("Voice entry is not an actual current story beat: " + line.key);
            return null;
        }

        [UnityTest]
        public IEnumerator ExactNarrationSourcesRejectChangedTextStaleLoadsAndMutedOrDisabledPlayback()
        {
            Assert.That(voice.ManifestLineCount, Is.EqualTo(28));
            string repository = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", ".."));
            string productionFile = Path.Combine(repository, "Tools/Voice/Nativity/production.json");
            Assert.That(File.Exists(productionFile), Is.True, "The preserved production binds exact source rates, durations and recording hashes.");
            var production = JsonUtility.FromJson<VoiceProduction>(File.ReadAllText(productionFile));
            Assert.That(production.lines.Length, Is.EqualTo(28));
            Debug.Log("GOSPEL_NATIVITY_IMPORTED_VOICE productionSha256=" + FileSha256(productionFile) +
                " dspOutputHz=" + AudioSettings.outputSampleRate);
            foreach (var line in voice.ManifestLines)
            {
                var recording = production.lines.Single(item => item.key == line.key);
                var turn = ActualTurn(line);
                Assert.That(line.key, Is.EqualTo(line.questId + ":" + line.beatIndex));
                Assert.That(line.text, Is.EqualTo(turn.Text), line.key);
                Assert.That(line.speaker, Is.EqualTo(turn.Speaker), line.key);
                Assert.That(line.speechText, Is.EqualTo(turn.Text), "Narration cannot quietly rewrite its visible text.");
                var clip = Resources.Load<AudioClip>(line.resourcePath);
                Assert.That(clip, Is.Not.Null, line.key);
                Assert.That(clip.channels, Is.EqualTo(1));
                Assert.That(recording.resourcePath, Is.EqualTo(line.resourcePath));
                Assert.That(recording.channels, Is.EqualTo(1));
                Assert.That(recording.sampleRate, Is.EqualTo(32000), "The independently checked source OGG remains32kHz mono.");
                var assetPath = AssetDatabase.GetAssetPath(clip);
                Assert.That(assetPath.StartsWith("Assets/", StringComparison.Ordinal), Is.True, "The resource must resolve to its actual authored project asset.");
                string sourceFile = Path.GetFullPath(Path.Combine(Application.dataPath, assetPath.Substring("Assets/".Length)));
                Assert.That(sourceFile, Is.EqualTo(Path.GetFullPath(Path.Combine(repository, recording.file))));
                Assert.That(FileSha256(sourceFile), Is.EqualTo(recording.sha256), "The imported clip must be the exact independently checked source recording.");
                var importer = AssetImporter.GetAtPath(assetPath) as AudioImporter;
                Assert.That(importer, Is.Not.Null); Assert.That(importer.forceToMono, Is.True);
                var settings = importer.defaultSampleSettings;
                // A source OGG's32kHz rate is not the native imported clip's rate
                // or the DSP device rate. Native01 observed44.1kHz imports with
                // a48kHz DSP. Check resampling quality and unchanged duration.
                Assert.That(clip.samples, Is.GreaterThan(0));
                Assert.That(clip.frequency, Is.EqualTo(32000).Or.EqualTo(44100).Or.EqualTo(48000));
                Assert.That(clip.frequency, Is.GreaterThanOrEqualTo(recording.sampleRate));
                double importedSeconds = clip.samples / (double)clip.frequency;
                Assert.That(importedSeconds, Is.EqualTo(recording.seconds).Within(.15), "Import resampling must preserve the source sentence length: " + line.key);
                Assert.That(clip.length, Is.EqualTo(recording.seconds).Within(.15));
                Assert.That(clip.length, Is.InRange(1, 60), line.key);
                Debug.Log("GOSPEL_NATIVITY_IMPORTED_VOICE key=" + line.key + " sourceHz=" + recording.sampleRate +
                    " importedHz=" + clip.frequency + " importPolicy=" + settings.sampleRateSetting +
                    " configuredOverrideHz=" + settings.sampleRateOverride + " sourceSeconds=" + recording.seconds +
                    " importedSeconds=" + importedSeconds + " mono=true sourceSha256=" + recording.sha256);
            }
            var first = voice.ManifestLines.First();
            var second = voice.ManifestLines.Skip(1).First();
            int rejected = voice.RejectedTextCount;
            Assert.That(voice.PlayForTurn(first.key, new GospelDialogueTurn(first.speaker, "mary", first.text + " 변경")), Is.False);
            Assert.That(voice.RejectedTextCount, Is.EqualTo(rejected + 1));
            Assert.That(voice.IsPlaying || voice.IsPending, Is.False);
            Assert.That(voice.PlayForTurn(first.key, ActualTurn(first)), Is.True);
            yield return WaitFor(() => voice.IsPlaying, "The exact current dialogue must own a real playing imported AudioSource.");
            Assert.That(voice.Source.clip, Is.EqualTo(Resources.Load<AudioClip>(first.resourcePath)));
            Assert.That(voice.Source.loop, Is.False); Assert.That(voice.Source.pitch, Is.EqualTo(1));
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(voice.Source.time, Is.GreaterThan(.02f));
            Assert.That(voice.ObservedPlaybackCount, Is.GreaterThan(0));
            Assert.That(voice.LastObservedKey, Is.EqualTo(first.key));
            int started = voice.StartedCount;
            Assert.That(voice.PlayForTurn(first.key, ActualTurn(first)), Is.True);
            Assert.That(voice.StartedCount, Is.EqualTo(started), "Redrawing the same playing line must not restart narration.");

            voice.Stop("native-real-navigation");
            Assert.That(voice.IsPlaying || voice.IsPending, Is.False);
            Assert.That(voice.Source.clip, Is.Null);
            Assert.That(voice.PlayForTurn(second.key, ActualTurn(second)), Is.True);
            voice.Stop("native-navigation-before-resource-completed");
            yield return new WaitForSecondsRealtime(.20f);
            Assert.That(voice.IsPlaying || voice.IsPending, Is.False, "A cancelled asynchronous resource cannot later start its old sentence.");
            Assert.That(voice.StartedCount, Is.EqualTo(started));
            Assert.That(voice.PlayForTurn(second.key, ActualTurn(second)), Is.True);
            yield return WaitFor(() => voice.IsPlaying, "The newly requested sentence must start after cancellation.");
            audio.SetMuted(true);
            Assert.That(voice.IsPlaying || voice.IsPending, Is.False, "Master mute must stop voice immediately, including its pending load.");
            audio.SetMuted(false); yield return new WaitForSecondsRealtime(.12f);
            Assert.That(voice.IsPlaying || voice.IsPending, Is.False, "Unmuting must not resurrect a stale sentence.");
            voice.SetVoiceEnabled(false);
            Assert.That(voice.PlayForTurn(first.key, ActualTurn(first)), Is.False);
            voice.SetVoiceEnabled(true);
            Assert.That(voice.PlayForTurn(first.key, ActualTurn(first)), Is.True);
            yield return WaitFor(() => voice.IsPlaying, "Re-enabled voice must accept a new exact request.");
            audio.SetVolume(0);
            Assert.That(voice.IsPlaying || voice.IsPending, Is.False);
            audio.SetVolume(.43f); yield return null;
            Assert.That(voice.IsPlaying || voice.IsPending, Is.False);
            Debug.Log("GOSPEL_NATIVITY_VOICE exact-current-lines=28 real-playing-source=true stale-text-rejected=true navigation-cancel=true master-mute-zero-stop=true no-stale-unmute=true generic-single-speaker-pitch=1");
        }

        [UnityTest]
        public IEnumerator NewBirthArrangementAndSilentOpeningCuesDuckOnlyForActualNarration()
        {
            var newBirth = Resources.Load<AudioClip>("Audio/GospelNativity/music-birth");
            Assert.That(newBirth, Is.Not.Null); Assert.That(newBirth.channels, Is.EqualTo(2));
            Assert.That(newBirth.length, Is.EqualTo(84.70588f).Within(.06f));
            yield return WaitFor(() => audio.IsPlaying && audio.ActiveChapter == "birth", "The real new Nativity arrangement must play.");
            Assert.That(audio.Clip, Is.EqualTo(newBirth), "The old recording is a fallback, not the new birth selection.");
            yield return new WaitForSecondsRealtime(1.3f);
            var musicSource = owner.GetComponents<AudioSource>().Single(item => item.clip == newBirth && item.loop);
            Assert.That(musicSource.time, Is.GreaterThan(0));
            audio.SetOpeningCinematic(true);
            int played = audio.PlayedCount, suppressed = audio.SuppressedCinematicCueCount;
            foreach (string cue in new[] { "ui", "dialogue", "scripture", "page" }) audio.Play(cue);
            Assert.That(audio.PlayedCount, Is.EqualTo(played));
            Assert.That(audio.SuppressedCinematicCueCount, Is.EqualTo(suppressed + 4));
            Assert.That(owner.GetComponents<AudioSource>().Any(item => !item.loop && item.isPlaying), Is.False);
            Assert.That(audio.NarrationMusicGain, Is.EqualTo(1).Within(.01));
            float fullLevel = musicSource.volume;
            var line = voice.ManifestLines.First();
            Assert.That(voice.PlayForTurn(line.key, ActualTurn(line)), Is.True);
            yield return WaitFor(() => voice.IsPlaying, "Cinematic silence must not silence its exact key narration.");
            yield return new WaitForSecondsRealtime(.20f);
            Assert.That(audio.NarrationMusicGain, Is.EqualTo(.32f).Within(.02));
            Assert.That(musicSource.volume, Is.LessThan(fullLevel * .40f));
            Assert.That(voice.Source.volume, Is.EqualTo(.43f * .98f).Within(.005));
            voice.Stop("native-end-of-visible-line");
            yield return new WaitForSecondsRealtime(.70f);
            Assert.That(audio.NarrationMusicGain, Is.EqualTo(1).Within(.01));
            Assert.That(musicSource.volume, Is.EqualTo(fullLevel).Within(.005));
            audio.SetOpeningCinematic(false); audio.Play("scripture"); yield return null;
            Assert.That(audio.PlayedCount, Is.EqualTo(played + 1), "Exploration retains the existing optional discovery cue.");
            Assert.That(owner.GetComponents<AudioSource>().Any(item => !item.loop && item.isPlaying && item.clip != null && item.clip.name == "scripture"), Is.True);
            Debug.Log("GOSPEL_NATIVITY_MIX new-birth-seconds=" + newBirth.length + " opening-ui-dialogue-scripture-page-cues=0 actual-voice-duck=.32 recovered-gain=1 exploration-cue-retained=true");
        }
    }
}
