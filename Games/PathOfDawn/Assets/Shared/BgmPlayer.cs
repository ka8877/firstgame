using UnityEngine;

namespace FirstGame.Shared
{
    /// <summary>A single persistent soundtrack on an app object, independent of game/save state.</summary>
    [DisallowMultipleComponent]
    public sealed class BgmPlayer : MonoBehaviour
    {
        private AudioSource source;
        private string preferencePrefix;
        private bool hasUserGesture, initialized;
        private float fade;
        public float Volume { get; private set; }
        public bool Muted { get; private set; }
        public bool IsPlaying => source != null && source.isPlaying;
        public AudioClip Clip => source != null ? source.clip : null;

        public static BgmPlayer Attach(GameObject owner, string resourcePath, string preferencePrefix, float defaultVolume = .6f)
        {
            var player = owner.GetComponent<BgmPlayer>();
            if (player == null) player = owner.AddComponent<BgmPlayer>();
            player.Initialize(resourcePath, preferencePrefix, defaultVolume);
            return player;
        }

        private void Initialize(string resourcePath, string prefix, float defaultVolume)
        {
            if (initialized) return;
            initialized = true;
            preferencePrefix = prefix;
            Volume = Mathf.Clamp01(PlayerPrefs.GetFloat(prefix + ".volume", defaultVolume));
            Muted = PlayerPrefs.GetInt(prefix + ".muted", 0) != 0;
            source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = true;
            source.spatialBlend = 0;
            source.dopplerLevel = 0;
            source.priority = 64;
            source.volume = 0;
            source.mute = Muted;
            source.clip = Resources.Load<AudioClip>(resourcePath);
            if (source.clip == null)
            {
                Debug.LogWarning("Background music resource is missing: " + resourcePath);
                return;
            }
            source.clip.LoadAudioData();
#if !UNITY_WEBGL || UNITY_EDITOR
            // Desktop players can start on the title screen. Web browsers require a real gesture.
            hasUserGesture = true;
#endif
        }

        /// <summary>Call from an actual pointer/key event; never manufactures browser permission.</summary>
        public void NotifyUserGesture()
        {
            hasUserGesture = true;
            TryPlay();
        }

        public void SetVolume(float value)
        {
            Volume = Mathf.Clamp01(value);
            if (source != null) source.volume = Volume * fade;
            if (!initialized) return;
            PlayerPrefs.SetFloat(preferencePrefix + ".volume", Volume);
            PlayerPrefs.Save();
        }

        public void SetMuted(bool value)
        {
            Muted = value;
            if (source != null) source.mute = value;
            if (!initialized) return;
            PlayerPrefs.SetInt(preferencePrefix + ".muted", value ? 1 : 0);
            PlayerPrefs.Save();
        }

        private void Update()
        {
            TryPlay();
            if (source == null || !source.isPlaying) return;
            fade = Mathf.MoveTowards(fade, 1, Time.unscaledDeltaTime / .45f);
            source.volume = Volume * fade;
        }

        private void TryPlay()
        {
            if (!hasUserGesture || source == null || source.clip == null || source.isPlaying) return;
            if (source.clip.loadState != AudioDataLoadState.Loaded) return;
            source.Play();
        }

        private void OnDestroy()
        {
            if (source != null) source.Stop();
        }
    }
}
