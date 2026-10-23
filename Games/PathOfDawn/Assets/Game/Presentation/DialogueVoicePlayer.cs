using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>Offline narration keyed by the exact displayed Ink line, independent of story state.</summary>
    [DisallowMultipleComponent]
    public sealed class DialogueVoicePlayer : MonoBehaviour
    {
        [Serializable]
        private sealed class VoiceManifest { public string version; public VoiceLine[] lines; }

        [Serializable]
        private sealed class VoiceLine
        {
            public string id;
            public string text;
            public string resourcePath;
        }

        private const string PreferencePrefix = "POD.voice";
        private readonly Dictionary<string, VoiceLine> lines = new Dictionary<string, VoiceLine>(StringComparer.Ordinal);
        private AudioSource source;
        private VoiceLine currentLine;
        private bool initialized, playWhenLoaded;

        public bool Muted { get; private set; }
        public float Volume { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying && !Muted;
        public bool HasCurrentLine => currentLine != null && source != null && source.clip != null;
        public string CurrentLineId => currentLine != null ? currentLine.id : string.Empty;
        public string CurrentLineText => currentLine != null ? currentLine.text : string.Empty;
        public AudioClip CurrentClip => source != null ? source.clip : null;
        public int SupportedLineCount => lines.Count;

        public static DialogueVoicePlayer Attach(GameObject owner)
        {
            var player = owner.GetComponent<DialogueVoicePlayer>();
            if (player == null) player = owner.AddComponent<DialogueVoicePlayer>();
            player.Initialize();
            return player;
        }

        private void Initialize()
        {
            if (initialized) return;
            initialized = true;
            Muted = PlayerPrefs.GetInt(PreferencePrefix + ".muted", 0) != 0;
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(PreferencePrefix + ".volume", 1f));
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.priority = 32;
            source.volume = Volume;
            source.mute = Muted;
            var asset = Resources.Load<TextAsset>("Voice/opening-voice");
            if (asset == null)
            {
                Debug.LogWarning("Opening voice manifest is unavailable.");
                return;
            }
            var manifest = JsonUtility.FromJson<VoiceManifest>(asset.text);
            if (manifest == null || manifest.lines == null) return;
            foreach (var entry in manifest.lines)
            {
                if (entry == null || string.IsNullOrWhiteSpace(entry.id) || string.IsNullOrWhiteSpace(entry.text)
                    || string.IsNullOrWhiteSpace(entry.resourcePath)) continue;
                var key = entry.text.Trim();
                if (lines.ContainsKey(key))
                {
                    Debug.LogWarning("Duplicate opening voice line: " + entry.id);
                    continue;
                }
                lines.Add(key, entry);
            }
        }

        /// <summary>Replaces the previous voice. Exact text matching prevents outdated lines from speaking.</summary>
        public bool PlayLine(string displayedFullLine)
        {
            Initialize();
            Clear();
            if (string.IsNullOrWhiteSpace(displayedFullLine) || !lines.TryGetValue(displayedFullLine.Trim(), out var line)) return false;
            var clip = Resources.Load<AudioClip>(line.resourcePath);
            if (clip == null)
            {
                Debug.LogWarning("Opening voice clip is unavailable: " + line.id);
                return false;
            }
            currentLine = line;
            source.clip = clip;
            clip.LoadAudioData();
            if (!Muted) Replay();
            return true;
        }

        public bool Replay()
        {
            Stop();
            if (!HasCurrentLine || Muted || !isActiveAndEnabled) return false;
            playWhenLoaded = true;
            TryPlayLoadedClip();
            return true;
        }

        /// <summary>Stops output but retains the current line for the replay button.</summary>
        public void Stop()
        {
            playWhenLoaded = false;
            if (source != null) source.Stop();
        }

        /// <summary>Stops output and removes the current line when leaving dialogue.</summary>
        public void Clear()
        {
            Stop();
            currentLine = null;
            if (source != null) source.clip = null;
        }

        public void SetMuted(bool value)
        {
            Initialize();
            Muted = value;
            source.mute = value;
            if (value) Stop();
            PlayerPrefs.SetInt(PreferencePrefix + ".muted", value ? 1 : 0);
            PlayerPrefs.Save();
        }

        public void SetVolume(float value)
        {
            Initialize();
            Volume = Mathf.Clamp01(value);
            source.volume = Volume;
            PlayerPrefs.SetFloat(PreferencePrefix + ".volume", Volume);
            PlayerPrefs.Save();
        }

        private void Update() => TryPlayLoadedClip();

        private void TryPlayLoadedClip()
        {
            if (!playWhenLoaded || !HasCurrentLine || Muted) return;
            if (source.clip.loadState == AudioDataLoadState.Failed)
            {
                playWhenLoaded = false;
                Debug.LogWarning("Opening voice clip failed to decode: " + CurrentLineId);
                return;
            }
            if (source.clip.loadState != AudioDataLoadState.Loaded) return;
            playWhenLoaded = false;
            source.Play();
        }

        private void OnDisable() => Stop();
        private void OnDestroy() => Clear();
    }
}
