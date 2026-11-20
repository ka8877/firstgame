using System;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>New visual source identity/expression map; owns no gameplay or animation.</summary>
    [DisallowMultipleComponent]
    public sealed class GospelHeroModelProfile : MonoBehaviour
    {
        public const string CharacterResourceRoot = "Art/GospelHero20261119/Characters/";
        [SerializeField] string logicalId;
        [SerializeField] string sourceSha256;
        [SerializeField] string exportedModelSha256;
        [SerializeField] float authoredHeight;
        [SerializeField] TextAsset expressionBindings;
        public string LogicalId => logicalId;
        public string SourceSha256 => sourceSha256;
        public string ExportedModelSha256 => exportedModelSha256;
        public float AuthoredHeight => authoredHeight;
        public TextAsset ExpressionBindings => expressionBindings;

        public void Configure(string actorId, string originalSourceSha256,
            string modelSha256, float height, TextAsset exactExpressionBindings)
        {
            if (string.IsNullOrEmpty(actorId) || !Hash(originalSourceSha256) || !Hash(modelSha256)
                || float.IsNaN(height) || float.IsInfinity(height) || height <= 0 || !exactExpressionBindings)
                throw new ArgumentException("Explicit visual source hashes, height and expression bindings are required.");
            logicalId = actorId; sourceSha256 = originalSourceSha256;
            exportedModelSha256 = modelSha256; authoredHeight = height;
            expressionBindings = exactExpressionBindings;
        }

        // Missing new profile: preserve legacy lookup. Broken admitted profile:
        // return an error rather than quietly showing an old source character.
        public static bool TryResolve(string actorId, out GameObject prefab, out string error)
        {
            prefab = Resources.Load<GameObject>(CharacterResourceRoot + actorId);
            error = null;
            if (!prefab) return false;
            var profile = prefab.GetComponent<GospelHeroModelProfile>();
            if (!profile || profile.logicalId != actorId || !Hash(profile.sourceSha256)
                || !Hash(profile.exportedModelSha256) || !profile.expressionBindings
                || float.IsNaN(profile.authoredHeight) || float.IsInfinity(profile.authoredHeight)
                || Mathf.Abs(profile.authoredHeight - GospelNprCharacterView.HeightFor(actorId)) > .001f)
            {
                error = "Malformed admitted Gospel hero profile: " + actorId;
                prefab = null; return false;
            }
            return true;
        }
        static bool Hash(string value)
        {
            if (value == null || value.Length != 64) return false;
            foreach (char c in value)
                if (!(c >= '0' && c <= '9') && !(c >= 'a' && c <= 'f')) return false;
            return true;
        }
    }
}
