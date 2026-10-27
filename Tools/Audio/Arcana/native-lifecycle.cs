// Exercise the real native host against a deterministic AudioSource clock.
// These doubles verify lifecycle decisions, not Unity device output or PCM.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using FantasyCards.Presentation;

namespace UnityEngine
{
    [AttributeUsage(AttributeTargets.Class)] public sealed class DisallowMultipleComponent : Attribute { }
    public class Object { public static T FindFirstObjectByType<T>() where T : class => null; }
    public class MonoBehaviour : Object { public GameObject gameObject = new GameObject(); }
    public class AudioListener : Object { }
    public enum AudioDataLoadState { Unloaded, Loading, Loaded, Failed }
    public class AudioClip { public string name; public AudioDataLoadState loadState = AudioDataLoadState.Loaded; public void LoadAudioData() { } }
    public class AudioSource : Object
    {
        public bool playOnAwake, loop, mute, isPlaying;
        public float spatialBlend, dopplerLevel, volume, time;
        public int priority, starts, stops;
        public float lastStartPosition;
        public AudioClip clip;
        bool paused;
        public void Play() { lastStartPosition = time; isPlaying = true; paused = false; starts++; }
        public void Pause() { paused = isPlaying || paused; isPlaying = false; }
        public void UnPause() { if (paused) { isPlaying = true; paused = false; } }
        public void Stop() { isPlaying = false; paused = false; time = 0; stops++; }
    }
    public class GameObject
    {
        public List<object> components = new List<object>();
        public T AddComponent<T>() where T : new() { var item = new T(); components.Add(item); return item; }
    }
    public static class Time { public static float unscaledDeltaTime; }
    public static class AudioSettings { public static double dspTime; }
    public static class Mathf
    {
        public static float Clamp01(float n) => Math.Max(0, Math.Min(1, n));
        public static float MoveTowards(float n, float goal, float step) => Math.Abs(goal - n) <= step ? goal : n + Math.Sign(goal - n) * step;
    }
    public static class Resources { public static T Load<T>(string path) where T : class => new AudioClip { name = path.Split('/').Last() } as T; }
    public static class Debug { public static void LogWarning(string warning) => Console.WriteLine(warning); }
}

static class VerifyNativeLifecycle
{
    static readonly MethodInfo update = typeof(FmLocalMusicHost).GetMethod("Update", BindingFlags.Instance | BindingFlags.NonPublic);
    static int assertions;
    static void Check(bool valid, string message) { assertions++; if (!valid) throw new Exception(message); }
    static void Near(float actual, float expected, string label, float tolerance = .025f) => Check(Math.Abs(actual - expected) <= tolerance, label + ": " + actual + " vs " + expected);
    static (FmLocalMusicHost host, UnityEngine.AudioSource[] audio) Create()
    {
        var host = new FmLocalMusicHost(); host.Initialize();
        return (host, host.gameObject.components.OfType<UnityEngine.AudioSource>().ToArray());
    }
    static void Advance(FmLocalMusicHost host, UnityEngine.AudioSource[] audio, float seconds)
    {
        int count = (int)Math.Round(seconds / .01f);
        for (int i = 0; i < count; i++)
        {
            UnityEngine.Time.unscaledDeltaTime = .01f;
            UnityEngine.AudioSettings.dspTime += .01;
            update.Invoke(host, null);
            foreach (var source in audio) if (source.isPlaying) source.time += .01f;
        }
    }
    static void Main()
    {
        var (host, audio) = Create();
        Check(audio.Length == 4, "Native host must keep exactly four sources");
        host.Apply("login", false, .6f); Advance(host, audio, 5);
        Check(host.ActiveScene == "login", "Login plays");
        Check(audio[0].time > 4.9f, "Actual source clock advanced");
        Check(audio[0].volume > .59f, "Initial fade completes");
        float before = audio[0].time;
        for (int i = 0; i < 8; i++) host.Apply("login", false, .6f);
        Advance(host, audio, .2f); Near(audio[0].time, before + .2f, "Same-role redraw retains position");
        Check(audio[0].starts == 1, "Same-role redraw does not issue Play again");
        Check(host.SameRoleApplies >= 8 && host.SceneTransitionCount == 0, "Same-role calls do not count as transitions");

        host.Apply("lobby", false, .6f); Advance(host, audio, .09f);
        Check(audio[0].isPlaying && audio[1].isPlaying, "A short real crossfade has both tails playing");
        Check(audio[0].volume > 0 && audio[0].volume < .6f && audio[1].volume > 0 && audio[1].volume < .6f, "Both crossfade gains lie between endpoints");
        Advance(host, audio, 5);
        Check(audio.Count(source => source.isPlaying) == 1, "Only one loop after fade");
        Check(!audio[0].isPlaying, "Outgoing source pauses after its tail");
        host.Apply("deck", false, .6f); Advance(host, audio, 2);
        host.Apply("lobby", false, .6f); Advance(host, audio, .1f);
        Near(audio[1].lastStartPosition, 0, "Returning lobby starts from zero");
        var lobbyStarts = host.PlaybackStarts.Where(start => start.scene == "lobby").ToArray();
        Check(lobbyStarts.Length == 2 && lobbyStarts.All(start => start.sourceTime < .1f), "Bounded real source start ledger proves both lobby starts at zero");
        Check(lobbyStarts[1].dspTime > lobbyStarts[0].dspTime && lobbyStarts[1].transition > lobbyStarts[0].transition, "Start ledger has ordered DSP timestamps and role serials");
        var copied = host.PlaybackStarts; copied[0].sourceTime = 987;
        Check(host.PlaybackStarts[0].sourceTime != 987, "Diagnostics cannot mutate internal playback history");
        Check(audio[1].time < .15f, "Returning lobby did not resume its old five-second timestamp");
        Advance(host, audio, .6f);
        float pausedPosition = audio[1].time;
        int starts = audio[1].starts;
        host.Apply("lobby", true, .6f); Advance(host, audio, 1);
        Near(audio[1].time, pausedPosition, "Mute pauses the recording");
        Check(audio.All(source => !source.isPlaying), "All sources pause while muted");
        host.Apply("lobby", false, .2f); Advance(host, audio, .5f);
        Near(audio[1].time, pausedPosition + .5f, "Unmute retains same-role position");
        Check(audio[1].starts == starts, "Unmute uses UnPause rather than restarting");
        Near(audio[1].volume, .2f, "Current master volume after fade");

        host.Apply("lobby", true, .2f); host.Apply("login", true, .2f);
        Advance(host, audio, 1); Check(audio.All(source => !source.isPlaying), "Muted navigation does not start hidden playback");
        host.Apply("login", false, .2f); Advance(host, audio, .1f);
        Check(audio[0].time < .15f && audio[0].lastStartPosition == 0, "Muted role change restarts when unmuted");
        Advance(host, audio, 2);
        host.Apply("deck", false, .2f); Advance(host, audio, .01f);
        host.Apply("login", false, .2f); Advance(host, audio, .5f);
        Check(host.ActiveScene == "login", "Rapid A-B-A returns to requested role");
        Check(audio[0].lastStartPosition == 0 && audio[0].time < .4f, "Rapid return fades old tail then rewinds");
        Check(audio.Count(source => source.isPlaying) == 1, "Rapid changes settle on one loop");

        var (loadingHost, loadingAudio) = Create();
        loadingHost.Apply("login", false, .6f); Advance(loadingHost, loadingAudio, 1);
        loadingAudio[3].clip.loadState = UnityEngine.AudioDataLoadState.Loading;
        loadingHost.Apply("battle", false, .6f); Advance(loadingHost, loadingAudio, 1);
        Check(loadingAudio[0].isPlaying && !loadingAudio[3].isPlaying, "Keep known outgoing music while target data loads");
        loadingAudio[3].clip.loadState = UnityEngine.AudioDataLoadState.Loaded;
        Advance(loadingHost, loadingAudio, .6f);
        Check(loadingHost.ActiveScene == "battle" && loadingAudio[3].lastStartPosition == 0, "Loaded battle starts at zero");
        Check(loadingAudio.Count(source => source.isPlaying) == 1, "No loading-time duplicate loop");
        Console.WriteLine("{\"status\":\"passed\",\"assertions\":" + assertions + ",\"scope\":\"Actual native host source with deterministic AudioSource doubles; no Unity PCM/device claim.\"}");
    }
}
