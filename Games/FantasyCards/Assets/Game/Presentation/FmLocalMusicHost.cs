using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Native playback uses the same four offline files as the Web template.</summary>
    [DisallowMultipleComponent]
    public sealed class FmLocalMusicHost : MonoBehaviour
    {
        private readonly string[] scenes = { "login", "lobby", "deck", "battle" };
        private readonly string[] files = { "title", "lobby", "deck", "battle" };
        private readonly AudioSource[] sources = new AudioSource[4];
        private readonly float[] weights = new float[4];
        private int selected, active = -1;
        private bool muted;
        private float volume;
        private AudioListener outputListener;

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
            selected = System.Array.IndexOf(scenes, scene);
            if (selected < 0) selected = 0;
            muted = isMuted;
            volume = Mathf.Clamp01(level);
            foreach (var source in sources) if (source != null) source.mute = muted;
        }
        private void Update()
        {
            // A scene reload may destroy a previously borrowed camera listener.
            EnsureOutputListener();
            var target = sources[selected];
            if (target != null && target.clip != null && target.clip.loadState == AudioDataLoadState.Loaded)
            {
                if (!target.isPlaying) target.UnPause();
                if (!target.isPlaying) target.Play();
                active = selected;
            }
            for (int i = 0; i < sources.Length; i++)
            {
                var source = sources[i];
                if (source == null) continue;
                weights[i] = Mathf.MoveTowards(weights[i], i == active ? 1 : 0, Time.unscaledDeltaTime / .35f);
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
