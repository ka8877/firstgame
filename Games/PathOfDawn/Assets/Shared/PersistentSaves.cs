using System;
using System.IO;
using UnityEngine;

namespace FirstGame.Shared
{
    public static class PersistentSaves
    {
        static SafeSaveService service;
        static SafeSaveService Service
        {
            get
            {
                if (service != null) return service;
#if UNITY_WEBGL && !UNITY_EDITOR
                ISaveBackend backend = new BrowserSaveBackend();
#else
                ISaveBackend backend = new FileSaveBackend(Path.Combine(Application.persistentDataPath, "Saves"));
#endif
                service = new SafeSaveService(backend, s => JsonUtility.ToJson(s, true), JsonUtility.FromJson<PortableSave>);
                return service;
            }
        }
        public static bool Exists(string slot) => Service.Exists(slot);
        public static bool TrySave(string slot, string gameId, int schema, string contentVersion, string payload, out string error)
            => Service.TrySave(slot, gameId, schema, contentVersion, payload, out error);
        public static bool TryLoad(string slot, string gameId, int schema, string contentVersion, out string payload, out string error)
            => Service.TryLoad(slot, gameId, schema, contentVersion, out payload, out error);
        public static bool TryExport(string slot, out string portableJson, out string error)
            => Service.TryExport(slot, out portableJson, out error);
        public static bool TryImport(string slot, string gameId, int schema, string contentVersion, string portableJson,
            Func<string, bool> validatePayload, out string error)
            => Service.TryImport(slot, gameId, schema, contentVersion, portableJson, validatePayload, out error);

#if UNITY_EDITOR
        // PlayMode tests isolate storage and never touch the developer's actual saves.
        public static IDisposable UseBackendForTesting(ISaveBackend backend)
        {
            var previous = service;
            service = new SafeSaveService(backend, s => JsonUtility.ToJson(s, true), JsonUtility.FromJson<PortableSave>);
            return new RestoreBackend(() => service = previous);
        }
        sealed class RestoreBackend : IDisposable
        {
            Action restore;
            public RestoreBackend(Action restore) { this.restore = restore; }
            public void Dispose() { restore?.Invoke(); restore = null; }
        }
#endif

        // WebGL PlayerPrefs is persisted by Unity to browser IndexedDB. Browser storage is origin-specific.
        sealed class BrowserSaveBackend : ISaveBackend
        {
            static string Key(string slot) { SafeSaveService.ValidateSlot(slot); return "firstgame.save." + slot; }
            public bool Exists(string slot) => PlayerPrefs.HasKey(Key(slot));
            public string Read(string slot) => PlayerPrefs.GetString(Key(slot));
            public void WriteAtomic(string slot, string contents)
            {
                var key = Key(slot);
                var previous = PlayerPrefs.GetString(key, "");
                if (PlayerPrefs.HasKey(key)) PlayerPrefs.SetString(key + ".backup", previous);
                try { PlayerPrefs.SetString(key, contents); PlayerPrefs.Save(); }
                catch { if (!string.IsNullOrEmpty(previous)) PlayerPrefs.SetString(key, previous); else PlayerPrefs.DeleteKey(key); throw; }
            }
        }
    }
}
