using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Chapter arrangements, quiet environmental beds and physical movement cues.
    /// Presentation only: never owns Scripture progress, save data or gameplay RNG.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class GospelStorybookAudio : MonoBehaviour
    {
        const string Prefix = "GOSPEL.music";
        const float CrossfadeSeconds = 1.15f;
        static readonly string[] Chapters = { "birth", "temple", "jordan" };
        static readonly string[] Cues = { "footstep", "jump", "land", "page", "dialogue", "scripture", "ui", "dash" };
        readonly AudioSource[] music = new AudioSource[3], environment = new AudioSource[3];
        readonly AudioSource[] effects = new AudioSource[8];
        readonly float[] weights = new float[3], voiceLevels = new float[8];
        readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        readonly Dictionary<string, double> lastStarts = new Dictionary<string, double>();
        readonly List<CueObservation> observations = new List<CueObservation>();
        readonly bool[] started = new bool[3];
        GospelPilgrimageMotor player;
        int selected, active = -1, nextVoice, stepIndex, observedDashSequence;
        bool initialized, hasUserGesture, applicationPaused, motionInitialized, wasGrounded, hasGroundContact;
        bool narrationActive;
        Vector3 previousPosition;
        float stepDistance, nextStepAt, narrationGain = 1;

        public float Volume { get; private set; }
        public bool Muted { get; private set; }
        public bool IsPlaying => active >= 0 && music[active] != null && music[active].isPlaying && !Muted;
        public string ActiveChapter => active < 0 ? "" : Chapters[active];
        public string RequestedChapter => Chapters[selected];
        public AudioClip Clip => active < 0 ? music[selected]?.clip : music[active]?.clip;
        public int PlayedCount { get; private set; }
        public string LastCue { get; private set; } = "";
        public int FootstepCount { get; private set; }
        public int ChapterTransitionCount { get; private set; }
        public bool OpeningCinematic { get; private set; }
        public int SuppressedCinematicCueCount { get; private set; }
        public bool CanPlayNarration => initialized && hasUserGesture && !applicationPaused && !Muted && Volume > 0;
        public float NarrationMusicGain => narrationGain;
        public event Action PlaybackSettingsChanged;

        [Serializable]
        public sealed class CueObservation
        {
            public string cue, clip;
            public int sequence;
            public float volume, pitch, seconds;
            public double dspTime;
        }

        public CueObservation[] History => observations.ConvertAll(item => new CueObservation {
            cue = item.cue, clip = item.clip, sequence = item.sequence, volume = item.volume,
            pitch = item.pitch, seconds = item.seconds, dspTime = item.dspTime
        }).ToArray();

        public static GospelStorybookAudio Attach(GameObject owner, GospelPilgrimageMotor traveler)
        {
            var result = owner.GetComponent<GospelStorybookAudio>();
            if (result == null) result = owner.AddComponent<GospelStorybookAudio>();
            result.Initialize(traveler);
            return result;
        }

        void Initialize(GospelPilgrimageMotor traveler)
        {
            player = traveler;
            if (initialized) return;
            initialized = true;
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(Prefix + ".volume", .5f));
            Muted = PlayerPrefs.GetInt(Prefix + ".muted", 0) != 0;
            for (int i = 0; i < Chapters.Length; i++)
            {
                music[i] = CreateSource(true, 64);
                // Keep the previous original recording intact as a fallback.
                music[i].clip = i == 0 ? Resources.Load<AudioClip>("Audio/GospelNativity/music-birth") : null;
                if (music[i].clip == null)
                    music[i].clip = Resources.Load<AudioClip>("Audio/GospelStorybook/music-" + Chapters[i]);
                environment[i] = CreateSource(true, 110);
                environment[i].clip = Resources.Load<AudioClip>("Audio/GospelStorybook/ambience-" + Chapters[i]);
                music[i].clip?.LoadAudioData(); environment[i].clip?.LoadAudioData();
            }
            foreach (string cue in Cues)
            {
                var clip = Resources.Load<AudioClip>("Audio/GospelStorybook/" + cue);
                if (clip != null) { clip.LoadAudioData(); clips[cue] = clip; }
                else Debug.LogWarning("Missing storybook cue: " + cue);
            }
            for (int i = 0; i < effects.Length; i++) effects[i] = CreateSource(false, 90);
#if !UNITY_WEBGL || UNITY_EDITOR
            hasUserGesture = true;
#endif
            ApplyLevels();
        }

        AudioSource CreateSource(bool loop, int priority)
        {
            var source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false; source.loop = loop; source.spatialBlend = 0;
            source.dopplerLevel = 0; source.priority = priority; source.volume = 0;
            return source;
        }

        /// <summary>Called from the existing real pointer/key UI path.</summary>
        public void NotifyUserGesture() => hasUserGesture = true;

        public void SetNarrationActive(bool value) => narrationActive = value;

        /// <summary>The opening is a story, with no UI/reward/page chimes between its lines.</summary>
        public void SetOpeningCinematic(bool value)
        {
            if (OpeningCinematic == value) return;
            OpeningCinematic = value;
            if (!value) return;
            foreach (var effect in effects)
                if (effect != null && effect.isPlaying && IsCinematicCue(effect.clip)) effect.Stop();
        }

        bool IsCinematicCue(AudioClip clip)
        {
            if (clip == null) return false;
            foreach (string cue in new[] { "ui", "dialogue", "scripture", "page" })
                if (clips.TryGetValue(cue, out var candidate) && candidate == clip) return true;
            return false;
        }

        public void SetChapter(string chapter)
        {
            if (chapter == "baptism") chapter = "jordan";
            int next = Array.IndexOf(Chapters, chapter);
            if (next < 0) next = 0;
            if (selected == next) return;
            selected = next; ChapterTransitionCount++;
            motionInitialized = false; stepDistance = 0;
        }

        public void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
            PlayerPrefs.SetFloat(Prefix + ".volume", Volume); PlayerPrefs.Save(); ApplyLevels();
            PlaybackSettingsChanged?.Invoke();
        }

        public void SetMuted(bool value)
        {
            Muted = value;
            PlayerPrefs.SetInt(Prefix + ".muted", value ? 1 : 0); PlayerPrefs.Save();
            if (value) SuspendVoices();
            ApplyLevels();
            PlaybackSettingsChanged?.Invoke();
        }

        void Update()
        {
            narrationGain = Mathf.MoveTowards(narrationGain, narrationActive ? .32f : 1,
                Time.unscaledDeltaTime / (narrationActive ? .12f : .60f));
            if (!initialized || applicationPaused || !hasUserGesture || Muted || Volume <= 0) return;
            // A loading chapter does not silence the currently audible one.
            var target = music[selected];
            if (target != null && target.clip != null && target.clip.loadState == AudioDataLoadState.Loaded)
            {
                active = selected;
                StartOrResume(selected);
            }
            for (int i = 0; i < Chapters.Length; i++)
            {
                float goal = i == active ? 1 : 0;
                weights[i] = Mathf.MoveTowards(weights[i], goal, Time.unscaledDeltaTime / CrossfadeSeconds);
                if (i != active && weights[i] <= 0)
                {
                    if (music[i].isPlaying) music[i].Pause();
                    if (environment[i].isPlaying) environment[i].Pause();
                }
            }
            ApplyLevels(); ObserveMotion();
        }

        void StartOrResume(int index)
        {
            if (!music[index].isPlaying)
            {
                if (started[index]) { music[index].UnPause(); if (!music[index].isPlaying) music[index].Play(); }
                else { music[index].time = 0; music[index].Play(); started[index] = true; }
            }
            var bed = environment[index];
            if (bed.clip != null && bed.clip.loadState == AudioDataLoadState.Loaded && !bed.isPlaying)
            {
                bed.UnPause();
                if (!bed.isPlaying) bed.Play();
            }
        }

        void ApplyLevels()
        {
            float level = Muted || applicationPaused ? 0 : Volume;
            for (int i = 0; i < Chapters.Length; i++)
            {
                float weight = Mathf.Sqrt(weights[i]);
                if (music[i] != null) music[i].volume = level * weight * .83f * narrationGain;
                if (environment[i] != null) environment[i].volume = level * weight * .22f * narrationGain;
            }
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] != null) effects[i].volume = level * voiceLevels[i];
        }

        void ObserveMotion()
        {
            if (player == null || !player.Enabled)
            { motionInitialized = false; stepDistance = 0; return; }
            Vector3 position = player.Position;
            bool grounded = player.IsGrounded;
            if (!motionInitialized)
            { previousPosition = position; wasGrounded = grounded; hasGroundContact = grounded; observedDashSequence = player.DashSequence; motionInitialized = true; return; }
            // A slow frame can consume the entire physical burst before audio
            // observes IsDashing. The motor's actual-start serial records that
            // event once; a pending UI request never makes a premature sound.
            if (player.DashSequence != observedDashSequence)
            { observedDashSequence = player.DashSequence; Play("dash"); }
            Vector3 delta = position - previousPosition;
            previousPosition = position;
            float distance = new Vector2(delta.x, delta.z).magnitude;
            if (distance > 2) { stepDistance = 0; wasGrounded = grounded; return; }
            if (wasGrounded && !grounded && player.Velocity.y > 1.4f) Play("jump");
            if (!wasGrounded && grounded && hasGroundContact) { Play("land"); stepDistance = 0; nextStepAt = Time.unscaledTime + .16f; }
            if (grounded && !player.IsDashing && player.MeasuredSpeed > .4f)
            {
                stepDistance += distance;
                float stride = player.MeasuredSpeed > 4 ? 1.05f : .79f;
                if (stepDistance >= stride && Time.unscaledTime >= nextStepAt)
                { Play("footstep"); stepDistance %= stride; nextStepAt = Time.unscaledTime + .16f; }
            }
            else stepDistance = 0;
            wasGrounded = grounded; if (grounded) hasGroundContact = true;
        }

        public void Play(string cue)
        {
            if (OpeningCinematic && (cue == "ui" || cue == "dialogue" || cue == "scripture" || cue == "page"))
            { SuppressedCinematicCueCount++; return; }
            if (!initialized || !hasUserGesture || applicationPaused || Muted || Volume <= 0 ||
                string.IsNullOrEmpty(cue) || !clips.TryGetValue(cue, out var clip)) return;
            if (clip.loadState != AudioDataLoadState.Loaded) { clip.LoadAudioData(); return; }
            double now = AudioSettings.dspTime;
            double gap = cue == "scripture" ? .5 : cue == "footstep" ? .13 : .07;
            if (lastStarts.TryGetValue(cue, out double last) && now - last < gap) return;
            lastStarts[cue] = now;
            int index = -1;
            for (int i = 0; i < effects.Length; i++)
                if (!effects[i].isPlaying) { index = i; break; }
            if (index < 0)
            {
                // Quiet walking never interrupts a page, dialogue or acquired verse.
                if (cue == "footstep") return;
                index = nextVoice++ % effects.Length;
            }
            var source = effects[index]; source.Stop(); source.clip = clip;
            float level = cue == "footstep" ? .39f : cue == "land" ? .50f :
                cue == "jump" || cue == "dash" ? .40f : cue == "dialogue" ? .42f : cue == "scripture" ? .79f : .65f;
            source.pitch = cue == "footstep" ? 1 + ((stepIndex++ % 5) - 2) * .026f : 1;
            source.panStereo = cue == "footstep" ? (stepIndex % 2 == 0 ? -.035f : .035f) : 0;
            voiceLevels[index] = level; source.volume = Volume * level; source.Play();
            LastCue = cue; PlayedCount++;
            if (cue == "footstep") FootstepCount++;
            observations.Add(new CueObservation { cue = cue, clip = clip.name, sequence = PlayedCount,
                volume = source.volume, pitch = source.pitch, seconds = clip.length, dspTime = now });
            if (observations.Count > 96) observations.RemoveAt(0);
        }

        void SuspendVoices()
        {
            for (int i = 0; i < Chapters.Length; i++)
            {
                music[i]?.Pause(); environment[i]?.Pause(); weights[i] = 0;
            }
            foreach (var effect in effects) effect?.Stop();
            active = -1; motionInitialized = false; stepDistance = 0;
        }

        void OnApplicationPause(bool paused)
        { applicationPaused = paused; if (paused) SuspendVoices(); ApplyLevels(); }

        void OnDestroy()
        {
            foreach (var source in music) if (source != null) source.Stop();
            foreach (var source in environment) if (source != null) source.Stop();
            foreach (var source in effects) if (source != null) source.Stop();
        }
    }
}
