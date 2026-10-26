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
#endif
        [System.Serializable] private sealed class Message
        {
            public string scene;
            public bool muted;
            public float volume;
        }
        public static void SetScene(string value)
        {
            if (scene == value) return;
            scene = value;
            Send();
        }
        public static void SetMuted(bool value)
        {
            PlayerPrefs.SetInt("FC.originalMusic.muted", value ? 1 : 0);
            Send();
        }
        public static void SetVolume(float value)
        {
            PlayerPrefs.SetFloat("FC.originalMusic.volume", Mathf.Clamp01(value));
            Send();
        }
        private static void Send()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            FMOriginalMusic_Message(JsonUtility.ToJson(new Message {
                scene = scene ?? "login",
                muted = PlayerPrefs.GetInt("FC.originalMusic.muted", 0) != 0,
                volume = PlayerPrefs.GetFloat("FC.originalMusic.volume", .6f)
            }));
#endif
        }
    }
}
