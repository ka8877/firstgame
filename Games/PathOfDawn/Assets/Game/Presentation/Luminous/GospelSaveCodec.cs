using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using PathOfDawn.Core;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Unity inline serialization materializes absent/null custom classes. Use
    /// the original top-level JSON to distinguish a legacy save from a present,
    /// invalid Chronicle payload; keep all semantic validation in GospelJourney.
    /// Newtonsoft is the existing locked Unity package, not a new dependency.
    /// </summary>
    public static class GospelSaveCodec
    {
        public static GospelSave Decode(string payload)
        {
            var document = Parse(payload);
            bool hasChronicle = document.TryGetValue("chronicle", StringComparison.Ordinal, out var token)
                && token.Type != JTokenType.Null;
            if (hasChronicle && token.Type != JTokenType.Object)
                throw new ArgumentException("생애 여정의 저장 형식이 잘못되었습니다. 기존 파일을 보존했습니다.");
            // Do not ask Unity to decode an explicit null inline object either.
            // This does not normalize an explicitly supplied empty/version0 object.
            if (!hasChronicle) document.Remove("chronicle");
            var decoded = JsonUtility.FromJson<GospelSave>(document.ToString(Formatting.None));
            if (decoded == null) throw new ArgumentException("저장 내용이 없습니다.");
            if (!hasChronicle) decoded.chronicle = null;
            return decoded;
        }

        public static string Encode(GospelSave snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            string payload = JsonUtility.ToJson(snapshot);
            if (snapshot.chronicle != null) return payload;
            // Unity writes a synthesized version0 inline object for a null field.
            // Omit it only when the actual in-memory snapshot is a legacy journey.
            var document = Parse(payload);
            document.Remove("chronicle");
            return document.ToString(Formatting.None);
        }

        static JObject Parse(string payload)
        {
            if (string.IsNullOrWhiteSpace(payload)) throw new ArgumentException("저장 내용이 없습니다.");
            using (var text = new StringReader(payload))
            using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 32 })
            {
                var document = JObject.Load(reader, new JsonLoadSettings
                {
                    DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error,
                    LineInfoHandling = LineInfoHandling.Ignore
                });
                if (reader.Read()) throw new JsonReaderException("저장 문서 뒤에 추가 내용이 있습니다.");
                return document;
            }
        }
    }
}
