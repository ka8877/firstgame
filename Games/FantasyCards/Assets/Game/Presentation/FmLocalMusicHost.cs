using System.Collections.Generic;
using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Four bundled tracks, restarted only when their actual scene role changes.</summary>
    [DisallowMultipleComponent]
    public sealed class FmLocalMusicHost : MonoBehaviour
    {
        private readonly string[] scenes = { "login", "lobby", "deck", "battle" };
        private readonly string[] files = { "title", "lobby", "deck", "battle" };
        private readonly AudioSource[] sources = new AudioSource[4];
        private readonly float[] weights = new float[4];
        private const float FadeOutSeconds = .18f, FadeInSeconds = .42f;
        private int selected, active = -1;
        private bool muted, restartRequested = true;
        private float volume;
        private AudioListener outputListener;

        // Read-only playback diagnostics; they do not own any game or save state.
        public string RequestedScene => scenes[selected];
        public string ActiveScene => active < 0 ? "" : scenes[active];
        public float PlaybackPosition => active < 0 || sources[active] == null ? 0 : sources[active].time;
        public int SceneTransitionCount { get; private set; }
        public int SameRoleApplies { get; private set; }
        [System.Serializable]
        public sealed class PlaybackStart
        {
            public string scene, clip;
            public int transition;
            public float sourceTime;
            public double dspTime;
        }
        private readonly List<PlaybackStart> playbackStarts = new List<PlaybackStart>();
        public PlaybackStart[] PlaybackStarts => playbackStarts.ConvertAll(entry => new PlaybackStart {
            scene = entry.scene, clip = entry.clip, transition = entry.transition,
            sourceTime = entry.sourceTime, dspTime = entry.dspTime
        }).ToArray();

        internal void Initialize()
        {
            EnsureOutputListener();
            for (int i = 0; i < sources.Length; i++)
            {
                var source = gameObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.loop = true;
                source.spatialBlend = 0;
                source.dopplerLevel = 0;
                source.priority = 64;
                source.volume = 0;
                source.clip = Resources.Load<AudioClip>("Audio/ModernCard/" + files[i]);
                if (source.clip != null) source.clip.LoadAudioData();
                else Debug.LogWarning("Bundled card theme is missing: " + files[i]);
                sources[i] = source;
            }
        }
        internal void Apply(string scene, bool isMuted, float level)
        {
            int next = System.Array.IndexOf(scenes, scene);
            if (next < 0) next = 0;
            if (selected != next)
            {
                selected = next;
                restartRequested = true;
                SceneTransitionCount++;
            }
            else SameRoleApplies++;
            volume = Mathf.Clamp01(level);
            if (isMuted && !muted)
            {
                // Muting is a user pause, not a scene change. Preserve the playhead.
                for (int i = 0; i < sources.Length; i++)
                {
                    if (sources[i] != null) { sources[i].Pause(); sources[i].volume = 0; }
                    weights[i] = 0;
                }
                active = -1;
            }
            muted = isMuted;
            foreach (var source in sources) if (source != null) source.mute = muted;
        }
        private void Update()
        {
            EnsureOutputListener();
            if (muted) return;
            var target = sources[selected];
            if (target != null && target.clip != null && target.clip.loadState == AudioDataLoadState.Loaded)
            {
                if (restartRequested)
                {
                    // Rapid A -> B -> A can return while A is still fading. Let
                    // that audible tail finish before rewinding the same source.
                    if (weights[selected] > .001f && target.isPlaying) active = -1;
                    else
                    {
                        target.Stop();
                        target.time = 0;
                        target.volume = 0;
                        weights[selected] = 0;
                        target.Play();
                        playbackStarts.Add(new PlaybackStart { scene = scenes[selected], clip = target.clip.name,
                            transition = SceneTransitionCount, sourceTime = target.time, dspTime = AudioSettings.dspTime });
                        if (playbackStarts.Count > 24) playbackStarts.RemoveAt(0);
                        active = selected;
                        restartRequested = false;
                    }
                }
                else
                {
                    if (!target.isPlaying) target.UnPause();
                    if (!target.isPlaying) target.Play();
                    active = selected;
                }
            }
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null) continue;
                float goal = i == active ? 1 : 0;
                weights[i] = Mathf.MoveTowards(weights[i], goal,
                    Time.unscaledDeltaTime / (goal > weights[i] ? FadeInSeconds : FadeOutSeconds));
                source.volume = weights[i] * volume;
                if (i != active && weights[i] <= 0 && source.isPlaying) source.Pause();
            }
        }
        private void EnsureOutputListener()
        {
            if (outputListener != null) return;
            outputListener = Object.FindFirstObjectByType<AudioListener>();
            if (outputListener == null) outputListener = gameObject.AddComponent<AudioListener>();
        }
        private void OnDestroy()
        {
            foreach (var source in sources) if (source != null) source.Stop();
        }
    }
}
