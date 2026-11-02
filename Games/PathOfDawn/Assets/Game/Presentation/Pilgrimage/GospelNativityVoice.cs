using System;
using System.Collections.Generic;
using System.Globalization;
using PathOfDawn.Core;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>One generic Korean AI narrator, bound to the exact currently visible dialogue.</summary>
    [DisallowMultipleComponent]
    public sealed class GospelNativityVoice : MonoBehaviour
    {
        const string PreferenceKey = "GOSPEL.voice.enabled";
        const string ManifestPath = "Voice/GospelNativity/nativity-voice";
        readonly Dictionary<string, Line> lines = new Dictionary<string, Line>(StringComparer.Ordinal);
        readonly Dictionary<string, AudioClip> loaded = new Dictionary<string, AudioClip>(StringComparer.Ordinal);
        GospelStorybookAudio music;
        AudioSource source;
        ResourceRequest pending;
        Line requested;
        string currentKey = "";
        int generation, pendingGeneration, observedGeneration = -1;
        bool applicationPaused, initialized;

        [Serializable] public sealed class Manifest
        {
            public string version, provider, speakerDescription;
            public Line[] lines;
        }

        [Serializable] public sealed class Line
        {
            public string key, questId, speaker, text, speechText, resourcePath, textSha256;
            public int beatIndex;
        }

        public bool VoiceEnabled { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying;
        public bool IsPending => pending != null;
        public string CurrentKey => currentKey;
        public string LastStartedKey { get; private set; } = "";
        public string LastStopReason { get; private set; } = "";
        public int StartedCount { get; private set; }
        public int RejectedTextCount { get; private set; }
        public int ObservedPlaybackCount { get; private set; }
        public string LastObservedKey { get; private set; } = "";
        public int ManifestLineCount => lines.Count;
        public AudioSource Source => source;
        public float ClipSeconds => source != null && source.clip != null ? source.clip.length : 0;
        public Line[] ManifestLines => new List<Line>(lines.Values).ToArray();

        public static GospelNativityVoice Attach(GameObject owner, GospelStorybookAudio soundtrack)
        {
            var result = owner.GetComponent<GospelNativityVoice>();
            if (result == null) result = owner.AddComponent<GospelNativityVoice>();
            result.Initialize(soundtrack);
            return result;
        }

        void Initialize(GospelStorybookAudio soundtrack)
        {
            music = soundtrack;
            if (initialized) return;
            initialized = true;
            VoiceEnabled = PlayerPrefs.GetInt(PreferenceKey, 1) != 0;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
            source.pitch = 1; source.dopplerLevel = 0; source.priority = 32;
            if (music != null) music.PlaybackSettingsChanged += ApplyPreferences;
            var asset = Resources.Load<TextAsset>(ManifestPath);
            if (asset == null) { Debug.LogWarning("Missing Nativity voice manifest; story remains playable without narration."); return; }
            Manifest manifest;
            try { manifest = JsonUtility.FromJson<Manifest>(asset.text); }
            catch (Exception error) { Debug.LogWarning("Invalid Nativity voice manifest: " + error.Message); return; }
            if (manifest?.lines == null) return;
            foreach (var line in manifest.lines)
            {
                if (line == null || string.IsNullOrEmpty(line.key) || string.IsNullOrEmpty(line.text) ||
                    string.IsNullOrEmpty(line.resourcePath) || !line.resourcePath.StartsWith("Voice/GospelNativity/", StringComparison.Ordinal) ||
                    line.resourcePath.Contains("..") || lines.ContainsKey(line.key))
                { Debug.LogWarning("Invalid or duplicate Nativity voice entry."); continue; }
                lines.Add(line.key, line);
            }
        }

        public void SetVoiceEnabled(bool value)
        {
            VoiceEnabled = value;
            PlayerPrefs.SetInt(PreferenceKey, value ? 1 : 0); PlayerPrefs.Save();
            if (!value) Stop("voice-disabled");
        }

        void ApplyPreferences()
        {
            if (music == null || !music.CanPlayNarration) { Stop("audio-settings"); return; }
            if (source != null) source.volume = Mathf.Clamp01(music.Volume * .98f);
        }

        public bool PlayForTurn(string questId, int beatIndex, GospelDialogueTurn turn)
            => PlayForTurn(questId + ":" + beatIndex, turn);

        public bool PlayForTurn(string stableKey, GospelDialogueTurn turn)
        {
            // Rebuilding the same visible story screen must not restart its sentence.
            if (currentKey == stableKey && (IsPlaying || IsPending) && requested != null &&
                turn != null && requested.text == turn.Text && requested.speaker == turn.Speaker) return true;
            Stop("new-visible-line");
            if (!initialized || !VoiceEnabled || applicationPaused || music == null || !music.CanPlayNarration ||
                turn == null || string.IsNullOrEmpty(stableKey) || !lines.TryGetValue(stableKey, out var line)) return false;
            if (!string.Equals(line.text, turn.Text, StringComparison.Ordinal) ||
                !string.Equals(line.speaker, turn.Speaker, StringComparison.Ordinal))
            { RejectedTextCount++; return false; }
            requested = line; currentKey = stableKey;
            if (loaded.TryGetValue(line.resourcePath, out var clip) && clip != null)
            { StartClip(clip); return true; }
            pendingGeneration = generation;
            pending = Resources.LoadAsync<AudioClip>(line.resourcePath);
            return true;
        }

        void Update()
        {
            if (!initialized) return;
            if (!VoiceEnabled || applicationPaused || music == null || !music.CanPlayNarration)
            { if (IsPlaying || IsPending) Stop("audio-inactive"); return; }
            source.volume = Mathf.Clamp01(music.Volume * .98f);
            if (pending != null && pending.isDone)
            {
                var request = pending; pending = null;
                if (pendingGeneration != generation || requested == null) return;
                var clip = request.asset as AudioClip;
                if (clip == null) { Stop("missing-clip"); return; }
                loaded[requested.resourcePath] = clip;
                StartClip(clip);
            }
            if (source.isPlaying && source.time > 0 && source.volume > 0 && requested != null &&
                !string.IsNullOrEmpty(currentKey) && observedGeneration != generation)
            {
                observedGeneration = generation; ObservedPlaybackCount++; LastObservedKey = currentKey;
                Debug.Log("GOSPEL_NATIVITY_NARRATION actual-start key=" + currentKey + " clip=" + source.clip.name +
                    " textSha256=" + requested.textSha256 + " seconds=" + source.clip.length.ToString("F3", CultureInfo.InvariantCulture) +
                    " sourceTime=" + source.time.ToString("F3", CultureInfo.InvariantCulture) +
                    " sourceGain=" + source.volume.ToString("F3", CultureInfo.InvariantCulture));
            }
            music.SetNarrationActive(IsPlaying);
        }

        void StartClip(AudioClip clip)
        {
            if (requested == null || !VoiceEnabled || applicationPaused || music == null || !music.CanPlayNarration) return;
            source.Stop(); source.clip = clip; source.pitch = 1;
            source.volume = Mathf.Clamp01(music.Volume * .98f);
            source.Play(); LastStartedKey = currentKey; StartedCount++;
            music.SetNarrationActive(source.isPlaying);
        }

        public void Stop(string reason = "navigation")
        {
            string oldKey = currentKey;
            bool wasPlaying = IsPlaying, wasPending = IsPending;
            float oldTime = source != null ? source.time : 0;
            generation++; pending = null; requested = null; currentKey = "";
            if (source != null) { source.Stop(); source.clip = null; }
            music?.SetNarrationActive(false); LastStopReason = reason;
            if ((wasPlaying || wasPending) && !string.IsNullOrEmpty(oldKey))
                Debug.Log("GOSPEL_NATIVITY_NARRATION stop key=" + oldKey + " reason=" + reason +
                    " hadPlaying=" + wasPlaying + " hadPending=" + wasPending +
                    " sourceTime=" + oldTime.ToString("F3", CultureInfo.InvariantCulture));
        }

        void OnApplicationPause(bool paused)
        { applicationPaused = paused; if (paused) Stop("application-paused"); }
        void OnDisable() => Stop("component-disabled");
        void OnDestroy()
        {
            if (music != null) music.PlaybackSettingsChanged -= ApplyPreferences;
            Stop("component-destroyed");
        }
    }
}
