using System;
using System.Collections.Generic;
using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Only owns sound presentation. No battle state, save data or gameplay RNG.</summary>
    [DisallowMultipleComponent]
    public sealed class FmSfxHost : MonoBehaviour
    {
        private const int VoiceCount = 16, ObservationLimit = 96;
        private static readonly string[] Cues = {
            "ui-hover", "click", "confirm", "cancel", "error", "card-select", "card-pick", "card-draw", "card-shuffle",
            "summon", "terrain", "equip", "spell", "heal", "coin", "attack", "counter", "hit", "shield",
            "destroy", "burn", "turn", "victory", "loss", "save",
            "coin-head", "coin-tail", "stat-up", "stat-down", "card-land", "rare-reveal"
        };
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, double> lastStarts = new Dictionary<string, double>();
        private readonly List<CueObservation> observations = new List<CueObservation>();
        private readonly AudioSource[] voices = new AudioSource[VoiceCount];
        private readonly double[] voiceStartedAt = new double[VoiceCount];
        private readonly int[] voiceImportance = new int[VoiceCount];
        private readonly float[] voiceLevels = new float[VoiceCount];
        private readonly Dictionary<string, int> variations = new Dictionary<string, int>();
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
            public float volume, length, pitch, pan;
            public double dspTime;
        }
        public CueObservation[] History => observations.ConvertAll(item => new CueObservation {
            cue = item.cue, clip = item.clip, sequence = item.sequence, voice = item.voice,
            volume = item.volume, length = item.length, pitch = item.pitch, pan = item.pan, dspTime = item.dspTime
        }).ToArray();

        internal void Initialize()
        {
            if (initialized) return; initialized = true;
            foreach (string cue in Cues)
            {
                var clip = Resources.Load<AudioClip>("Audio/FmRenewal/" + cue)
                    ?? Resources.Load<AudioClip>("Audio/FmRestoration/" + cue);
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
            for (int i = 0; i < voices.Length; i++)
            {
                var voice = voices[i];
                if (voice != null)
                {
                    voice.volume = FmBattleAudio.Volume * voiceLevels[i];
                    if (FmBattleAudio.Muted || FmBattleAudio.Volume <= 0) voice.Stop();
                }
            }
        }
        internal void Play(string requestedCue, float pan = 0, int variation = -1)
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
            int importance = Importance(cue);
            int index = -1;
            for (int i = 0; i < VoiceCount; i++) if (!voices[i].isPlaying) { index = i; break; }
            if (index < 0)
            {
                // Interface chatter cannot steal a hit, coin or result tail.
                for (int i = 0; i < VoiceCount; i++)
                    if (voiceImportance[i] <= importance && (index < 0 || voiceImportance[i] < voiceImportance[index]
                        || (voiceImportance[i] == voiceImportance[index] && voiceStartedAt[i] < voiceStartedAt[index]))) index = i;
                if (index < 0) return;
            }
            var source = voices[index]; source.Stop(); source.clip = clip;
            float level = cue == "ui-hover" ? .62f : cue == "click" ? .86f : 1;
            if (variation < 0) { variations.TryGetValue(cue, out variation); variations[cue] = variation + 1; }
            // Small deterministic timbre variations do not touch gameplay RNG.
            bool vary = cue.StartsWith("card-") || cue.StartsWith("coin") || cue == "hit" || cue == "attack" || cue == "counter";
            source.volume = FmBattleAudio.Volume * level;
            source.pitch = vary ? 1 + ((variation % 3) - 1) * .025f : 1;
            source.panStereo = Mathf.Clamp(pan, -.28f, .28f);
            source.priority = importance >= 2 ? 48 : 100;
            source.Play();
            voiceLevels[index] = level; voiceImportance[index] = importance;
            voiceStartedAt[index] = now; LastCue = cue; PlayedCount++;
            observations.Add(new CueObservation { cue = cue, clip = clip.name, sequence = PlayedCount,
                voice = index, volume = source.volume, length = clip.length, pitch = source.pitch, pan = source.panStereo, dspTime = now });
            if (observations.Count > ObservationLimit) observations.RemoveAt(0);
        }
        private static int Importance(string cue)
        {
            if (cue == "victory" || cue == "loss") return 3;
            if (cue == "ui-hover") return 0;
            if (cue == "click" || cue == "card-select" || cue == "card-pick" || cue == "cancel") return 1;
            return 2;
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
