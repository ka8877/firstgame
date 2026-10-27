using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Only owns sound presentation. No battle state, save data or gameplay RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class FmSfxHost : MonoBehaviour
    {
        private const int VoiceCount = 12, ObservationLimit = 96;
        private static readonly string[] Cues = {
            "ui-hover", "click", "confirm", "cancel", "error", "card-select", "card-pick", "card-draw", "card-shuffle",
            "summon", "terrain", "equip", "spell", "heal", "coin", "attack", "counter", "hit", "shield",
            "destroy", "burn", "turn", "victory", "loss", "save"
        };
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, double> lastStarts = new Dictionary<string, double>();
        private readonly List<CueObservation> observations = new List<CueObservation>();
        private readonly AudioSource[] voices = new AudioSource[VoiceCount];
        private readonly double[] voiceStartedAt = new double[VoiceCount];
        private bool initialized;
        public string LastCue { get; private set; } = "";
        public int PlayedCount { get; private set; }
        public int LoadedCueCount => clips.Count;
        public int ActiveVoiceCount
        {
            get { int count = 0; foreach (var voice in voices) if (voice != null && voice.isPlaying) count++; return count; }
        }
        [Serializable]
        public sealed class CueObservation
        {
            public string cue, clip;
            public int sequence, voice;
            public float volume, length;
            public double dspTime;
        }
        public CueObservation[] History => observations.ConvertAll(item => new CueObservation {
            cue = item.cue, clip = item.clip, sequence = item.sequence, voice = item.voice,
            volume = item.volume, length = item.length, dspTime = item.dspTime
        }).ToArray();

        internal void Initialize()
        {
            if (initialized) return; initialized = true;
            foreach (string cue in Cues)
            {
                var clip = Resources.Load<AudioClip>("Audio/FmRestoration/" + cue);
                if (clip == null) { Debug.LogWarning("Missing FM remaster sound effect: " + cue); continue; }
                clip.LoadAudioData(); clips[cue] = clip;
            }
            for (int i = 0; i < VoiceCount; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false; source.loop = false; source.spatialBlend = 0;
                source.dopplerLevel = 0; source.priority = 80; voices[i] = source;
            }
            ApplySettings();
        }
        internal void ApplySettings()
        {
            foreach (var voice in voices)
                if (voice != null)
                {
                    voice.volume = FmBattleAudio.Volume;
                    if (FmBattleAudio.Muted || FmBattleAudio.Volume <= 0) voice.Stop();
                }
        }
        internal void Play(string requestedCue)
        {
            Initialize();
            string cue = ResolveCue(requestedCue);
            if (FmBattleAudio.Muted || FmBattleAudio.Volume <= 0 || !clips.TryGetValue(cue, out var clip)) return;
            if (clip.loadState != AudioDataLoadState.Loaded) { clip.LoadAudioData(); return; }
            double now = AudioSettings.dspTime;
            // Only identical accidental dispatches are coalesced. Different semantic
            // cues (swing then impact; card then terrain) can overlap normally.
            double gap = cue == "ui-hover" ? .095 : cue == "click" ? .035 : .012;
            if (lastStarts.TryGetValue(cue, out double last) && now - last < gap) return;
            lastStarts[cue] = now;
            int index = -1;
            for (int i = 0; i < VoiceCount; i++) if (!voices[i].isPlaying) { index = i; break; }
            if (index < 0)
            {
                // Hover is expendable. It must not replace a combat or result voice.
                if (cue == "ui-hover") return;
                index = 0;
                for (int i = 1; i < VoiceCount; i++) if (voiceStartedAt[i] < voiceStartedAt[index]) index = i;
            }
            var source = voices[index]; source.Stop(); source.clip = clip;
            source.volume = FmBattleAudio.Volume; source.pitch = 1; source.Play();
            voiceStartedAt[index] = now; LastCue = cue; PlayedCount++;
            observations.Add(new CueObservation { cue = cue, clip = clip.name, sequence = PlayedCount,
                voice = index, volume = source.volume, length = clip.length, dspTime = now });
            if (observations.Count > ObservationLimit) observations.RemoveAt(0);
        }
        private static string ResolveCue(string cue)
        {
            switch (cue)
            {
                case "play": return "summon";
                case "draw": return "card-draw";
                case "shuffle": return "card-shuffle";
                case "phase": return "turn";
                case "defeat": return "destroy";
                default: return cue;
            }
        }
        private void OnDestroy() { foreach (var voice in voices) if (voice != null) voice.Stop(); }
    }
}
