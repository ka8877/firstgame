using System.Runtime.InteropServices;
using UnityEngine;

namespace FantasyCards.Presentation
{
    // The original composer's hosted player supplies the music. No copied audio
    // or unrelated fallback track is bundled as the original soundtrack.
    public static class FmOriginalMusic
    {
        private static string scene;
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void FMOriginalMusic_Message(string message);
        [DllImport("__Internal")] private static extern int FMOriginalMusic_GetMuted();
        [DllImport("__Internal")] private static extern float FMOriginalMusic_GetVolume();
#endif
        [System.Serializable] private sealed class Message
        {
            public string change;
            public string scene;
            public bool muted;
            public float volume;
        }
        public static bool Muted
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                int value = FMOriginalMusic_GetMuted();
                if (value >= 0) PlayerPrefs.SetInt("FC.originalMusic.muted", value);
#endif
                return PlayerPrefs.GetInt("FC.originalMusic.muted", 0) != 0;
            }
        }
        public static float Volume
        {
            get
            {
#if UNITY_WEBGL && !UNITY_EDITOR
                float value = FMOriginalMusic_GetVolume();
                if (value >= 0) PlayerPrefs.SetFloat("FC.originalMusic.volume", Mathf.Clamp01(value));
#endif
                return Mathf.Clamp01(PlayerPrefs.GetFloat("FC.originalMusic.volume", .6f));
            }
        }
        public static void SetScene(string value)
        {
            if (scene == value) return;
            scene = value;
            Send("scene");
        }
        public static void SetMuted(bool value)
        {
            PlayerPrefs.SetInt("FC.originalMusic.muted", value ? 1 : 0);
            Send("muted");
        }
        public static void SetVolume(float value)
        {
            PlayerPrefs.SetFloat("FC.originalMusic.volume", Mathf.Clamp01(value));
            Send("volume");
        }
        private static void Send(string change)
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FMOriginalMusic_Message(JsonUtility.ToJson(new Message {
                change = change,
                scene = scene ?? "login",
                muted = PlayerPrefs.GetInt("FC.originalMusic.muted", 0) != 0,
                volume = PlayerPrefs.GetFloat("FC.originalMusic.volume", .6f)
            }));
#endif
        }
    }
}
