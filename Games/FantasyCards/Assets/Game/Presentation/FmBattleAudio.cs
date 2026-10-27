using System;
using UnityEngine;

namespace FantasyCards.Presentation
{
    /// <summary>Authored remaster effects. These are not recovered original FM recordings.</summary>
    public static class FmBattleAudio
    {
        private const string MutedKey = "FC.effects.muted", VolumeKey = "FC.effects.volume";
        private static FmSfxHost host;
        public static bool Muted => PlayerPrefs.GetInt(MutedKey, 0) != 0;
        public static float Volume => Mathf.Clamp01(PlayerPrefs.GetFloat(VolumeKey, .82f));
        public static string LastCue => host == null ? "" : host.LastCue;
        public static int PlayedCount => host == null ? 0 : host.PlayedCount;
        public static FmSfxHost.CueObservation[] History => host == null ? Array.Empty<FmSfxHost.CueObservation>() : host.History;
        public static void Initialize()
        {
            if (host != null) return;
            host = UnityEngine.Object.FindFirstObjectByType<FmSfxHost>();
            if (host == null)
            {
                var owner = new GameObject("Fantasy Masters sound effects");
                UnityEngine.Object.DontDestroyOnLoad(owner);
                host = owner.AddComponent<FmSfxHost>();
            }
            host.Initialize();
        }
        // Persistent voices survive UI redraws and do not inherit the old .24 source gain.
        public static void Play(AudioSource unusedSource, string cue) => Play(cue);
        public static void Play(string cue)
        {
            if (Muted || Volume <= 0 || string.IsNullOrEmpty(cue)) return;
            Initialize(); host.Play(cue);
        }
        public static void SetMuted(bool value)
        {
            PlayerPrefs.SetInt(MutedKey, value ? 1 : 0); PlayerPrefs.Save();
            if (host != null) host.ApplySettings();
        }
        public static void SetVolume(float value)
        {
            PlayerPrefs.SetFloat(VolumeKey, Mathf.Clamp01(value)); PlayerPrefs.Save();
            if (host != null) host.ApplySettings();
        }
        public static void AttachHover(GameObject target)
        {
            if (target != null && target.GetComponent<FmSfxHover>() == null) target.AddComponent<FmSfxHover>();
        }
    }
}
