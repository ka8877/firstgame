using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // Original cards are fetched on demand, not decoded thousands at startup.
    // Existing Resources artwork keeps its original bytes and import settings.
    public sealed class FmStreamingCardArt : MonoBehaviour
    {
        private sealed class Binding { public RawImage image; public Action<Texture2D> ready; }
        private sealed class Entry
        {
            public Texture2D texture; public bool pending; public float used, retryAt; public int failures;
            public readonly List<Binding> bindings = new List<Binding>();
        }
        private static FmStreamingCardArt host;
        private readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>();
        private readonly Queue<string> queue = new Queue<string>();
        private int active;
        private const string Prefix = "StreamingCards/";
        public static bool IsStreamed(string resource) => !string.IsNullOrEmpty(resource) && resource.StartsWith(Prefix, StringComparison.Ordinal);
        private static FmStreamingCardArt Host
        {
            get
            {
                if (host == null) { var go = new GameObject("Original card artwork cache"); DontDestroyOnLoad(go); host = go.AddComponent<FmStreamingCardArt>(); }
                return host;
            }
        }
        public static Texture2D Peek(string resource)
        {
            if (!IsStreamed(resource)) return Resources.Load<Texture2D>(resource ?? "");
            if (host != null && host.entries.TryGetValue(resource, out var entry)) { entry.used = Time.unscaledTime; return entry.texture; }
            return null;
        }
        public static void Bind(RawImage image, string resource, Action<Texture2D> ready = null)
        {
            if (!IsStreamed(resource)) { var texture = Peek(resource); image.texture = texture; if (texture != null) ready?.Invoke(texture); return; }
            var owner = Host;
            // Rebinding an existing image cancels its old result consumer.
            foreach (var previous in owner.entries.Values)
                previous.bindings.RemoveAll(binding => binding.image == null || binding.image == image);
            if (!owner.entries.TryGetValue(resource, out var entry)) { entry = new Entry(); owner.entries.Add(resource, entry); }
            entry.used = Time.unscaledTime; entry.bindings.RemoveAll(binding => binding.image == null);
            entry.bindings.Add(new Binding { image = image, ready = ready });
            if (entry.texture != null) { image.texture = entry.texture; ready?.Invoke(entry.texture); return; }
            image.texture = Resources.Load<Texture2D>("Art/ClassicCards/Decor/card-back");
            if (!entry.pending && Time.unscaledTime >= entry.retryAt) { entry.pending = true; owner.queue.Enqueue(resource); }
        }
        private void Update()
        {
            while (active < 3 && queue.Count > 0)
            {
                string resource = queue.Dequeue();
                if (!entries.TryGetValue(resource, out var entry)) continue;
                entry.bindings.RemoveAll(binding => binding.image == null || !binding.image.gameObject.activeInHierarchy);
                if (entry.bindings.Count == 0)
                { entry.pending = false; if (entry.texture == null) entries.Remove(resource); continue; }
                active++; StartCoroutine(Load(resource));
            }
        }
        private IEnumerator Load(string resource)
        {
            var entry = entries[resource]; string name = resource.Substring(Prefix.Length);
            // Catalog paths are stable filenames; reject traversal even for malformed data.
            if (name.IndexOfAny(new[] { '/', '\\', ':' }) >= 0 || name.Contains("..")) { entry.pending = false; active--; yield break; }
            string path = Application.streamingAssetsPath.TrimEnd('/') + "/Cards/" + name + ".jpg";
            if (!path.Contains("://")) path = new Uri(path).AbsoluteUri;
            using (var request = UnityWebRequestTexture.GetTexture(path, true))
            {
                request.timeout = 30; yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    entry.texture = DownloadHandlerTexture.GetContent(request); entry.texture.name = name;
                    entry.texture.filterMode = FilterMode.Bilinear; entry.texture.wrapMode = TextureWrapMode.Clamp; entry.failures = 0; entry.retryAt = 0;
                    entry.bindings.RemoveAll(binding => binding.image == null);
                    foreach (var binding in entry.bindings)
                    { binding.image.texture = entry.texture; binding.ready?.Invoke(entry.texture); }
                }
                else
                {
                    entry.failures++; entry.retryAt = Time.unscaledTime + Mathf.Min(300, 15 * Mathf.Pow(2, Mathf.Min(4, entry.failures)));
                    Debug.LogWarning("Original card image unavailable: " + name);
                }
            }
            entry.pending = false; active--; Trim();
        }
        private void Trim()
        {
            foreach (var entry in entries.Values) entry.bindings.RemoveAll(binding => binding.image == null);
            var unused = entries.Where(pair => pair.Value.texture != null && pair.Value.bindings.Count == 0 && !pair.Value.pending)
                .OrderBy(pair => pair.Value.used).ToArray();
            int count = entries.Values.Count(entry => entry.texture != null);
            foreach (var pair in unused)
            { if (count-- <= 96) break; Destroy(pair.Value.texture); entries.Remove(pair.Key); }
        }
        private void OnDestroy()
        { foreach (var entry in entries.Values) if (entry.texture != null) Destroy(entry.texture); entries.Clear(); if (host == this) host = null; }
    }
}
