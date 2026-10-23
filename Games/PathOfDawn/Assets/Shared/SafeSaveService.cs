using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace FirstGame.Shared
{
    [Serializable]
    public sealed class PortableSave
    {
        public string format = "firstgame.portable";
        public int envelopeVersion = 1;
        public string gameId;
        public int schema;
        public string contentVersion;
        public string savedAtUtc;
        public string payload;
        public string sha256;
    }

    public interface ISaveBackend
    {
        bool Exists(string slot);
        string Read(string slot);
        void WriteAtomic(string slot, string contents);
    }

    // Independent of Unity: a codec is supplied by the platform adapter or test runner.
    public sealed class SafeSaveService
    {
        public const int MaximumBytes = 8 * 1024 * 1024;
        readonly ISaveBackend backend;
        readonly Func<PortableSave, string> encode;
        readonly Func<string, PortableSave> decode;

        public SafeSaveService(ISaveBackend backend, Func<PortableSave, string> encode,
            Func<string, PortableSave> decode)
        {
            this.backend = backend ?? throw new ArgumentNullException(nameof(backend));
            this.encode = encode ?? throw new ArgumentNullException(nameof(encode));
            this.decode = decode ?? throw new ArgumentNullException(nameof(decode));
        }

        public bool Exists(string slot) { ValidateSlot(slot); return backend.Exists(slot); }

        public bool TrySave(string slot, string gameId, int schema, string contentVersion,
            string payload, out string error)
        {
            return Guard(() =>
            {
                ValidateSlot(slot);
                ValidatePayload(payload);
                if (string.IsNullOrWhiteSpace(gameId) || schema < 1 || string.IsNullOrEmpty(contentVersion))
                    throw new InvalidDataException("저장 식별자 또는 버전이 올바르지 않습니다.");
                // A damaged or incompatible previous save must be kept for recovery.
                if (backend.Exists(slot)) CheckExpected(Parse(backend.Read(slot)), gameId, schema, contentVersion);
                var save = new PortableSave
                {
                    gameId = gameId, schema = schema, contentVersion = contentVersion,
                    savedAtUtc = DateTime.UtcNow.ToString("O"), payload = payload
                };
                save.sha256 = Digest(save);
                var json = encode(save);
                Parse(json);
                backend.WriteAtomic(slot, json);
            }, out error);
        }

        public bool TryLoad(string slot, string gameId, int schema, string contentVersion,
            out string payload, out string error)
        {
            string result = null;
            bool success = Guard(() =>
            {
                ValidateSlot(slot);
                if (!backend.Exists(slot)) throw new FileNotFoundException("이어할 저장이 없습니다.");
                var save = Parse(backend.Read(slot));
                CheckExpected(save, gameId, schema, contentVersion);
                result = save.payload;
            }, out error);
            payload = result;
            return success;
        }

        public bool TryExport(string slot, out string portableJson, out string error)
        {
            string result = null;
            bool success = Guard(() =>
            {
                ValidateSlot(slot);
                if (!backend.Exists(slot)) throw new FileNotFoundException("내보낼 저장이 없습니다.");
                result = backend.Read(slot);
                Parse(result);
            }, out error);
            portableJson = result;
            return success;
        }

        public bool TryImport(string slot, string gameId, int schema, string contentVersion,
            string portableJson, Func<string, bool> validatePayload, out string error)
        {
            return Guard(() =>
            {
                ValidateSlot(slot);
                var save = Parse(portableJson);
                CheckExpected(save, gameId, schema, contentVersion);
                if (validatePayload == null || !validatePayload(save.payload))
                    throw new InvalidDataException("게임 상태 검증에 실패했습니다. 기존 저장을 유지합니다.");
                // Explicit import may replace a damaged save; the backend keeps its backup.
                backend.WriteAtomic(slot, portableJson);
            }, out error);
        }

        PortableSave Parse(string json)
        {
            if (string.IsNullOrEmpty(json) || Encoding.UTF8.GetByteCount(json) > MaximumBytes)
                throw new InvalidDataException("저장 파일이 비어 있거나 허용 크기를 초과했습니다.");
            var save = decode(json);
            if (save == null || save.format != "firstgame.portable" || save.envelopeVersion != 1 ||
                string.IsNullOrEmpty(save.gameId) || save.schema < 1 || string.IsNullOrEmpty(save.contentVersion))
                throw new InvalidDataException("지원하지 않는 저장 형식입니다. 원본을 유지합니다.");
            ValidatePayload(save.payload);
            if (!string.Equals(save.sha256, Digest(save), StringComparison.Ordinal))
                throw new InvalidDataException("저장 데이터 무결성 검사에 실패했습니다. 원본을 유지합니다.");
            return save;
        }

        static void CheckExpected(PortableSave save, string gameId, int schema, string contentVersion)
        {
            if (save.gameId != gameId || save.schema != schema || save.contentVersion != contentVersion)
                throw new InvalidDataException("다른 게임 또는 호환되지 않는 버전의 저장입니다. 원본을 유지합니다.");
        }

        static string Digest(PortableSave save)
        {
            var canonical = save.format + "\n" + save.envelopeVersion + "\n" + save.gameId + "\n" +
                save.schema + "\n" + save.contentVersion + "\n" + save.savedAtUtc + "\n" + save.payload;
            using (var hash = SHA256.Create())
            {
                var bytes = hash.ComputeHash(Encoding.UTF8.GetBytes(canonical));
                return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant();
            }
        }

        static void ValidatePayload(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload) || Encoding.UTF8.GetByteCount(payload) > MaximumBytes / 2)
                throw new InvalidDataException("게임 상태가 비어 있거나 너무 큽니다.");
        }

        public static void ValidateSlot(string slot)
        {
            if (string.IsNullOrEmpty(slot) || slot.Length > 80) throw new ArgumentException("Invalid save slot.");
            foreach (char c in slot)
                if (!(c >= 'a' && c <= 'z') && !(c >= 'A' && c <= 'Z') && !(c >= '0' && c <= '9') && c != '-' && c != '_')
                    throw new ArgumentException("Invalid save slot.");
        }

        static bool Guard(Action action, out string error)
        {
            try { action(); error = null; return true; }
            catch (Exception e) when (!(e is OutOfMemoryException)) { error = e.Message; return false; }
        }
    }

    public sealed class FileSaveBackend : ISaveBackend
    {
        readonly string directory;
        public FileSaveBackend(string directory) { this.directory = directory; }
        string PathFor(string slot) { SafeSaveService.ValidateSlot(slot); return Path.Combine(directory, slot + ".json"); }
        public bool Exists(string slot) { return File.Exists(PathFor(slot)); }
        public string Read(string slot)
        {
            var path = PathFor(slot);
            if (new FileInfo(path).Length > SafeSaveService.MaximumBytes) throw new InvalidDataException("Save too large.");
            return File.ReadAllText(path, Encoding.UTF8);
        }
        public void WriteAtomic(string slot, string contents)
        {
            Directory.CreateDirectory(directory);
            var path = PathFor(slot);
            var temporary = path + ".tmp-" + Guid.NewGuid().ToString("N");
            try
            {
                using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var writer = new StreamWriter(stream, new UTF8Encoding(false)))
                { writer.Write(contents); writer.Flush(); stream.Flush(true); }
                if (File.Exists(path)) File.Replace(temporary, path, path + ".backup");
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
