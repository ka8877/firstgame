using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    /// <summary>Vector battle effects. No textures, random rolls, or game state.</summary>
    [RequireComponent(typeof(CanvasRenderer))]
    public sealed class FmBattleFxGraphic : MaskableGraphic
    {
        public enum EffectStyle { Slash, Impact, Shield, Spell, Heal, Burn, Defeat, Summon }
        public EffectStyle Style;
        public float Progress;
        public Color Tint = new Color(1, .82f, .4f);

        protected FmBattleFxGraphic() { useLegacyMeshGeneration = false; }
        protected override void Awake() { base.Awake(); raycastTarget = false; }

        protected override void OnPopulateMesh(VertexHelper vh)
        {
            vh.Clear();
            var rect = GetPixelAdjustedRect();
            var center = rect.center;
            float scale = Mathf.Min(rect.width, rect.height), t = Mathf.Clamp01(Progress);
            float fade = Mathf.Sin(t * Mathf.PI);
            var tint = Tint; tint.a *= fade;
            // A soft core and layered sparks remain readable over both bright
            // original foils and the dark field. All trajectories are analytic;
            // no Unity/global/Core random state is consumed by presentation.
            if (Style != EffectStyle.Burn && Style != EffectStyle.Defeat)
                Glow(vh, center, scale * (.12f + .19f * t), new Color(tint.r, tint.g, tint.b, tint.a * .27f));
            if (Style == EffectStyle.Slash)
            {
                float reach = Mathf.SmoothStep(0, 1, Mathf.Clamp01(t * 2.1f));
                for (int blade = 0; blade < 3; blade++)
                {
                    float offset = (blade - 1) * .055f;
                    var a = center + new Vector2(-.48f, -.34f + offset) * scale;
                    var b = Vector2.Lerp(a, center + new Vector2(.48f, .34f + offset) * scale, reach);
                    Line(vh, a, b, Mathf.Max(1, scale * .065f * (1 - t)), new Color(tint.r, tint.g, tint.b, tint.a * .55f));
                    Line(vh, a, b, Mathf.Max(1, scale * .018f * (1 - t)), new Color(1, .97f, .82f, tint.a));
                }
                Sparks(vh, center, scale, t, tint, 14);
            }
            else if (Style == EffectStyle.Shield)
            {
                float radius = scale * (.25f + .07f * t);
                var points = new[] { new Vector2(0, .9f), new Vector2(.7f, .55f), new Vector2(.58f, -.35f), new Vector2(0, -.92f), new Vector2(-.58f, -.35f), new Vector2(-.7f, .55f), new Vector2(0, .9f) };
                for (int i = 1; i < points.Length; i++) Line(vh, center + points[i - 1] * radius, center + points[i] * radius, scale * .025f, tint);
                Line(vh, center + Vector2.up * radius * .58f, center - Vector2.up * radius * .5f, scale * .02f, tint);
                Line(vh, center - Vector2.right * radius * .36f, center + Vector2.right * radius * .36f, scale * .02f, tint);
                Ring(vh, center, radius * 1.35f, scale * .015f, tint, .15f, Mathf.PI * 1.83f);
                Sparks(vh, center, scale * .8f, t, tint, 8);
            }
            else if (Style == EffectStyle.Spell || Style == EffectStyle.Heal || Style == EffectStyle.Summon)
            {
                float radius = scale * (.17f + .20f * t);
                Ring(vh, center, radius, scale * .024f, new Color(tint.r,tint.g,tint.b,tint.a*.40f), t * 2, Mathf.PI * 2 + t * 2);
                Ring(vh, center, radius, scale * .009f, new Color(1,.97f,.85f,tint.a), t * 2, Mathf.PI * 2 + t * 2);
                Ring(vh, center, radius * .78f, scale * .009f, tint, -t * 3, Mathf.PI * 2 - t * 3);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4 + t * .55f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    var point = center + dir * radius;
                    Diamond(vh, point, scale * .027f, tint);
                    Line(vh, point, center + dir * (radius + scale * .075f), scale * .012f, tint);
                }
                if (Style == EffectStyle.Summon)
                {
                    for (int i = 0; i < 13; i++)
                    {
                        float phase = Mathf.Repeat(t * 1.08f + i * .071f, 1);
                        float x = Mathf.Sin(i * 2.39f) * .32f;
                        var point = center + new Vector2(x, -.36f + phase * .78f) * scale;
                        var beam = new Color(tint.r, tint.g, tint.b, tint.a * Mathf.Sin(phase * Mathf.PI));
                        Line(vh, point, point + Vector2.up * scale * (.06f + .10f * phase), scale * .010f, beam);
                        Diamond(vh, point + Vector2.up * scale * .04f, scale * .017f, new Color(1,.96f,.78f,beam.a));
                    }
                }
                if (Style == EffectStyle.Heal)
                {
                    Line(vh, center - Vector2.right * scale * .11f, center + Vector2.right * scale * .11f, scale * .05f, tint);
                    Line(vh, center - Vector2.up * scale * .11f, center + Vector2.up * scale * .11f, scale * .05f, tint);
                }
                else
                    for (int i = 0; i < 6; i++)
                    {
                        float a = (i * 60 + 30) * Mathf.Deg2Rad;
                        float b = ((i + 2) * 60 + 30) * Mathf.Deg2Rad;
                        Line(vh, center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius * .6f,
                            center + new Vector2(Mathf.Cos(b), Mathf.Sin(b)) * radius * .6f, scale * .01f, tint);
                    }
            }
            else if (Style == EffectStyle.Burn || Style == EffectStyle.Defeat)
            {
                Glow(vh, center - Vector2.up * scale * .12f, scale * (.25f + t * .15f), new Color(1,.20f,.025f,tint.a*.30f));
                for (int i = 0; i < 17; i++)
                {
                    float x = Mathf.Sin(i * 3.13f) * .34f;
                    float phase = Mathf.Repeat(t * 1.15f + i * .071f, 1);
                    var p = center + new Vector2(x + Mathf.Sin(phase * 8 + i) * .04f, -.25f + phase * .70f) * scale;
                    var ember = Color.Lerp(tint, new Color(1, .22f, .07f, tint.a), i / 17f);
                    Diamond(vh, p, scale * (.008f + .016f * (1 - phase)), ember);
                    Line(vh, p, p - Vector2.up * scale * .065f, scale * .006f, ember);
                }
                if (Style == EffectStyle.Defeat) Sparks(vh, center, scale, t, new Color(1,.56f,.14f,tint.a), 18);
            }
            else
            {
                int rays = 14;
                float inner = scale * (.05f + t * .15f), outer = scale * (.16f + t * .34f);
                for (int i = 0; i < rays; i++)
                {
                    float a = i * Mathf.PI * 2 / rays + .15f;
                    var dir = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Line(vh, center + dir * inner, center + dir * outer, scale * .023f * (1 - t), tint);
                    Line(vh, center + dir * inner, center + dir * outer, scale * .007f * (1 - t), new Color(1,1,.90f,tint.a));
                    Diamond(vh, center + dir * outer, scale * .019f * (1 - t * .65f), tint);
                }
                Ring(vh, center, scale * (.08f + t * .3f), scale * .02f * (1 - t), tint, 0, Mathf.PI * 2);
                Glow(vh, center, scale * .13f * (1 - t), new Color(1,1,.90f,tint.a));
            }
        }

        static void Glow(VertexHelper vh, Vector2 center, float radius, Color tint)
        {
            const int segments = 32; int first = vh.currentVertCount;
            vh.AddVert(center, tint, Vector2.zero); var edge = tint; edge.a = 0;
            for (int i = 0; i <= segments; i++)
            {
                float a = i * Mathf.PI * 2 / segments;
                vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, edge, Vector2.zero);
                if (i > 0) vh.AddTriangle(first, first + i, first + i + 1);
            }
        }
        static void Sparks(VertexHelper vh, Vector2 center, float scale, float t, Color tint, int count)
        {
            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f;
                var direction = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                float distance = scale * (.08f + (.19f + (i % 4) * .035f) * Mathf.Sqrt(t));
                var point = center + direction * distance + Vector2.down * (t * t * scale * .08f);
                Diamond(vh, point, scale * (.009f + (i % 3) * .004f) * (1 - t * .70f), tint);
                Line(vh, point, point - direction * scale * .055f * (1 - t), scale * .004f, tint);
            }
        }

        static void Diamond(VertexHelper vh, Vector2 center, float radius, Color32 tint)
        {
            int first = vh.currentVertCount;
            vh.AddVert(center + Vector2.up * radius, tint, Vector2.zero);
            vh.AddVert(center + Vector2.right * radius, tint, Vector2.zero);
            vh.AddVert(center - Vector2.up * radius, tint, Vector2.zero);
            vh.AddVert(center - Vector2.right * radius, tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
        static void Ring(VertexHelper vh, Vector2 center, float radius, float width, Color32 tint, float from, float to)
        {
            var previous = center + new Vector2(Mathf.Cos(from), Mathf.Sin(from)) * radius;
            for (int i = 1; i <= 48; i++)
            {
                float a = Mathf.Lerp(from, to, i / 48f);
                var next = center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius;
                Line(vh, previous, next, width, tint); previous = next;
            }
        }
        static void Line(VertexHelper vh, Vector2 from, Vector2 to, float width, Color32 tint)
        {
            var direction = to - from;
            var normal = new Vector2(-direction.y, direction.x).normalized * Mathf.Max(.35f, width) * .5f;
            int first = vh.currentVertCount;
            vh.AddVert(from - normal, tint, Vector2.zero); vh.AddVert(from + normal, tint, Vector2.zero);
            vh.AddVert(to + normal, tint, Vector2.zero); vh.AddVert(to - normal, tint, Vector2.zero);
            vh.AddTriangle(first, first + 1, first + 2); vh.AddTriangle(first, first + 2, first + 3);
        }
    }
}
