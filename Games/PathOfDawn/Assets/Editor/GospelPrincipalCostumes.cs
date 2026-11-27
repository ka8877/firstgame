using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace FirstGame.Editor
{
// Draft the outer cloth against the actor's real neutral skinned surface. Every
// contact sample carries the source triangle's weights into the finished garment.
public static class GospelPrincipalCostumes
{
    const string Art = "Assets/Art/GalileeAtelier/";
    static readonly string[] Cast = { "JesusChild", "Mary", "Joseph", "JesusAdult", "Teacher" };

    readonly struct Point
    {
        public readonly Vector3 position;
        public readonly BoneWeight weight;
        public Point(Vector3 p, BoneWeight w)
        {
            position = p;
            weight = w;
        }
    }
    sealed class Tri
    {
        public Vector3 a, b, c;
        public BoneWeight wa, wb, wc;
        public float x0, x1, y0, y1, denominator;
    }

    sealed class Tailor
    {
        readonly string id;
        readonly Transform root;
        readonly Animator rig;
        readonly List<Transform> bones = new List<Transform>();
        readonly List<Tri> shell = new List<Tri>();
        readonly List<Tri> capSource = new List<Tri>();
        readonly List<Vector3> positions = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>();
        readonly List<BoneWeight> weights = new List<BoneWeight>();
        readonly List<int>[] sections = Enumerable.Range(0, 5).Select(i => new List<int>()).ToArray();
        readonly float shoulder, waist, stature, span, zAxis, side;
        int slot;
        public Tailor(string identity, GameObject model)
        {
            id = identity;
            root = model.transform;
            rig = model.GetComponentInChildren<Animator>();
            var left = Bone(HumanBodyBones.LeftUpperArm).position;
            var right = Bone(HumanBodyBones.RightUpperArm).position;
            var hips = Bone(HumanBodyBones.Hips).position;
            stature = Bone(HumanBodyBones.Neck).position.y / .84f;
            shoulder = left.y + stature * .033f;
            waist = hips.y + stature * .051f;
            span = Mathf.Abs(right.x - left.x) * .55f;
            zAxis = hips.z;
            side = id == "Mary" || id == "Teacher" ? -1 : 1;
            var skins = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            foreach (var r in skins)
            {
                string n = r.name;
                if (!r.enabled ||
                    !(n == "Body" || n.EndsWith("__Body") || n.Contains("Tailored") || n.Contains("ArtistLinen") ||
                      n.Contains("SewnLinen") || n.Contains("FittedShirt")))
                    continue;
                AddShell(r);
            }
            if (shell.Count == 0)
                throw new InvalidOperationException(id + " has no woven contact surface");
        }
        Transform Bone(HumanBodyBones bone) => rig.GetBoneTransform(bone);
        int BoneId(Transform t)
        {
            int i = bones.IndexOf(t);
            if (i >= 0)
                return i;
            bones.Add(t);
            return bones.Count - 1;
        }
        void AddShell(SkinnedMeshRenderer r)
        {
            var mesh = r.sharedMesh;
            var pts = GospelSculptedCastBaker.WorldVertices(r);
            var bw = mesh.boneWeights;
            var lookup = r.bones.Select(BoneId).ToArray();
            var mapped = bw.Select(w => new BoneWeight { boneIndex0 = lookup[w.boneIndex0], weight0 = w.weight0,
                                                         boneIndex1 = lookup[w.boneIndex1], weight1 = w.weight1,
                                                         boneIndex2 = lookup[w.boneIndex2], weight2 = w.weight2,
                                                         boneIndex3 = lookup[w.boneIndex3], weight3 = w.weight3 })
                             .ToArray();
            var triangles = mesh.triangles;
            for (int j = 0; j < triangles.Length; j += 3)
            {
                int a = triangles[j], b = triangles[j + 1], c = triangles[j + 2];
                var t =
                    new Tri { a = pts[a], b = pts[b], c = pts[c], wa = mapped[a], wb = mapped[b], wc = mapped[c] };
                t.x0 = Mathf.Min(t.a.x, t.b.x, t.c.x);
                t.x1 = Mathf.Max(t.a.x, t.b.x, t.c.x);
                t.y0 = Mathf.Min(t.a.y, t.b.y, t.c.y);
                t.y1 = Mathf.Max(t.a.y, t.b.y, t.c.y);
                t.denominator = (t.b.y - t.c.y) * (t.a.x - t.c.x) + (t.c.x - t.b.x) * (t.a.y - t.c.y);
                if (t.y0 > shoulder + stature * .04f || t.y1 < stature * .12f)
                    continue;
                shell.Add(t);
                if (r.name.EndsWith("__ArtistLinenUpper") || r.name.EndsWith("__IllustrationFittedShirt") ||
                    (id == "Mary" && r.name.EndsWith("__Body")))
                    capSource.Add(t);
            }
        }
        static BoneWeight Mix(BoneWeight a, BoneWeight b, BoneWeight c, Vector3 ratio)
        {
            var sum = new Dictionary<int, float>();
            void Add(int n, float v)
            {
                if (v > 1e-6f)
                    sum[n] = (sum.TryGetValue(n, out float old) ? old : 0) + v;
            }
            void Source(BoneWeight w, float k)
            {
                Add(w.boneIndex0, w.weight0 * k);
                Add(w.boneIndex1, w.weight1 * k);
                Add(w.boneIndex2, w.weight2 * k);
                Add(w.boneIndex3, w.weight3 * k);
            }
            Source(a, ratio.x);
            Source(b, ratio.y);
            Source(c, ratio.z);
            var top = sum.OrderByDescending(t => t.Value).Take(4).ToArray();
            float total = top.Sum(t => t.Value);
            if (total <= 0)
                throw new InvalidOperationException("Empty skin weights on fitted cloth");
            int I(int i) => i < top.Length ? top[i].Key : 0;
            float W(int i) => i < top.Length ? top[i].Value / total : 0;
            return new BoneWeight { boneIndex0 = I(0), weight0 = W(0), boneIndex1 = I(1), weight1 = W(1),
                                    boneIndex2 = I(2), weight2 = W(2), boneIndex3 = I(3), weight3 = W(3) };
        }
        BoneWeight Torso(float y)
        {
            var lower = Bone(HumanBodyBones.Hips);
            var mid = Bone(HumanBodyBones.Spine);
            var upper = Bone(HumanBodyBones.Chest);
            if (!upper)
                upper = mid;
            Transform a = y < mid.position.y ? lower : mid, b = y < mid.position.y ? mid : upper;
            float t = Mathf.InverseLerp(a.position.y, b.position.y, y);
            return new BoneWeight { boneIndex0 = BoneId(a), weight0 = 1 - t, boneIndex1 = BoneId(b), weight1 = t };
        }
        Point Surface(float x, float y, int face, float ease)
        {
            float best = float.NegativeInfinity;
            Tri selected = null;
            Vector3 bary = Vector3.zero;
            foreach (var t in shell)
            {
                if (x < t.x0 || x > t.x1 || y < t.y0 || y > t.y1 || Mathf.Abs(t.denominator) < 1e-9f)
                    continue;
                float a = ((t.b.y - t.c.y) * (x - t.c.x) + (t.c.x - t.b.x) * (y - t.c.y)) / t.denominator;
                float b = ((t.c.y - t.a.y) * (x - t.c.x) + (t.a.x - t.c.x) * (y - t.c.y)) / t.denominator;
                float c = 1 - a - b;
                if (a < -.0001f || b < -.0001f || c < -.0001f)
                    continue;
                float depth = face * (t.a.z * a + t.b.z * b + t.c.z * c);
                if (depth <= best)
                    continue;
                best = depth;
                selected = t;
                bary = new Vector3(a, b, c);
            }
            float fallback = zAxis + face * stature * .065f *
                                         Mathf.Sqrt(Mathf.Clamp(1 - Mathf.Pow(x / (span * 1.76f), 2), .1f, 1));
            var bone = selected == null ? Torso(y) : Mix(selected.wa, selected.wb, selected.wc, bary);
            return new Point(new Vector3(x, y, (selected == null ? fallback : best * face) + ease * face), bone);
        }
        void Grid(int cols, int rows, Func<float, float, Point> sample, float length = 1, float uvStart = 0,
                  bool reverse = false)
        {
            int start = positions.Count;
            for (int j = 0; j <= rows; j++)
                for (int i = 0; i <= cols; i++)
                {
                    float u = i / (float)cols, v = j / (float)rows;
                    var p = sample(u, v);
                    positions.Add(root.InverseTransformPoint(p.position));
                    weights.Add(p.weight);
                    uv.Add(slot == 2 ? new Vector2(v, u * 4f) : new Vector2(u, uvStart + length * v));
                    if (i == 0 || j == 0)
                        continue;
                    int d = positions.Count - 1, c = d - 1, b = d - cols - 1, a = b - 1;
                    if (reverse)
                    {
                        sections[slot].AddRange(new[] { a, c, b, b, c, d });
                    }
                    else
                    {
                        sections[slot].AddRange(new[] { a, b, c, b, d, c });
                    }
                }
        }
        float Width(float t, bool back)
        {
            float bottom = id == "Joseph"       ? .73f
                           : id == "Mary"       ? 1.43f
                           : id == "Teacher"    ? .78f
                           : id == "JesusAdult" ? 1.67f
                                                : 1.12f;
            return span * (Mathf.Lerp(bottom, 1.22f, t) + (id == "JesusAdult" ? .98f
                                                           : back             ? .48f
                                                                              : .29f) *
                                                              Mathf.Sin(t * Mathf.PI));
        }
        Point Crest(float u, float v)
        {
            float cross = u - .5f;
            float x = side * span * .86f + cross * span * 1.05f;
            float z = Bone(HumanBodyBones.LeftUpperArm).position.z + Mathf.Cos(v * Mathf.PI) * stature * .033f;
            float top = 0;
            Tri selected = null;
            Vector3 bary = Vector3.zero;
            foreach (var t in shell)
            {
                if (x < t.x0 || x > t.x1 || t.y1 < shoulder - stature * .11f)
                    continue;
                float denominator = (t.b.z - t.c.z) * (t.a.x - t.c.x) + (t.c.x - t.b.x) * (t.a.z - t.c.z);
                if (Mathf.Abs(denominator) < 1e-9f)
                    continue;
                float a = ((t.b.z - t.c.z) * (x - t.c.x) + (t.c.x - t.b.x) * (z - t.c.z)) / denominator;
                float b = ((t.c.z - t.a.z) * (x - t.c.x) + (t.a.x - t.c.x) * (z - t.c.z)) / denominator;
                float c = 1 - a - b;
                if (Mathf.Min(a, b, c) < -.0001f)
                    continue;
                float y = t.a.y * a + t.b.y * b + t.c.y * c;
                if (y <= top || y > shoulder + stature * .045f)
                    continue;
                top = y;
                selected = t;
                bary = new Vector3(a, b, c);
            }
            float ridge = .0145f + .004f * Mathf.Sin(u * 21f + v * .42f);
            float easedZ = z + Mathf.Cos(v * Mathf.PI) * stature * .013f;
            if (selected != null)
                return new Point(new Vector3(x, top + stature * ridge, easedZ),
                                 Mix(selected.wa, selected.wb, selected.wc, bary));
            float slope = Mathf.Abs(x) / span;
            var upperArm = Bone(x < 0 ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            var arm = new BoneWeight { boneIndex0 = BoneId(upperArm), weight0 = 1 };
            return new Point(new Vector3(x, shoulder - stature * (.015f * slope + .005f) + stature * ridge, easedZ),
                             Mix(Torso(shoulder), arm, arm,
                                 new Vector3(1 - Mathf.Clamp01(slope - .35f), Mathf.Clamp01(slope - .35f), 0)));
        }
        Point Drape(float u, float v, int face)
        {
            bool back = face < 0;
            float fall = id == "Joseph" ? .16f : id == "JesusChild" ? .17f : id == "Mary" ? .30f : .26f;
            float start = waist - stature * (back ? fall * .84f : fall);
            var crest = Crest(u, back ? 1 : 0);
            float y = Mathf.Lerp(start, crest.position.y, v);
            float t = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(waist - stature * .035f, shoulder, y));
            float center = side * span * Mathf.Lerp(id == "Teacher" ? .78f : back ? -.50f : -.74f, .98f, t);
            float cross = (u - .5f);
            float x = Mathf.Lerp(center + cross * Width(t, back), crest.position.x,
                                 Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.67f, 1, v)));
            float dip = (1 - v) * (1 - v) * stature *
                        (.038f * Mathf.Cos(Mathf.PI * (u - .22f)) + .014f * Mathf.Sin(2 * u * Mathf.PI));
            y -= dip;
            float fold = Mathf.Sin(u * Mathf.PI * 7 + Mathf.Sin(v * 4.8f) * 1.16f);
            float ease = stature * (.010f + .008f * (fold * .5f + .5f) + .003f * Mathf.Sin(Mathf.PI * v));
            var contact = Surface(x, y, face, ease);
            float bend = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.87f, 1f, v));
            return new Point(Vector3.Lerp(contact.position, crest.position, bend),
                             Mix(contact.weight, crest.weight, crest.weight, new Vector3(1 - bend, bend, 0)));
        }
        static List<Point> Cut(List<Point> poly, Func<Vector3, float> distance)
        {
            var result = new List<Point>();
            if (poly.Count == 0)
                return result;
            var previous = poly[poly.Count - 1];
            float d0 = distance(previous.position);
            foreach (var current in poly)
            {
                float d1 = distance(current.position);
                if ((d0 >= 0) != (d1 >= 0))
                {
                    float t = d0 / (d0 - d1);
                    result.Add(
                        new Point(Vector3.Lerp(previous.position, current.position, t),
                                  Mix(previous.weight, current.weight, current.weight, new Vector3(1 - t, t, 0))));
                }
                if (d1 >= 0)
                    result.Add(current);
                previous = current;
                d0 = d1;
            }
            return result;
        }
        void ShoulderCap()
        {
            slot = 0;
            float z = Bone(HumanBodyBones.LeftUpperArm).position.z;
            foreach (var tri in capSource)
            {
                if (tri.y1 < shoulder - stature * .080f)
                    continue;
                var polygon = new List<Point> { new Point(tri.a, tri.wa), new Point(tri.b, tri.wb),
                                                new Point(tri.c, tri.wc) };
                polygon = Cut(polygon, p => p.x * side - span * .39f);
                polygon = Cut(polygon, p => span * 1.66f - p.x * side);
                polygon = Cut(polygon, p => p.y - shoulder + stature * (.071f + .010f * p.x * side / span));
                polygon = Cut(polygon, p => stature * .09f - Mathf.Abs(p.z - z));
                if (polygon.Count < 3)
                    continue;
                int first = positions.Count;
                foreach (var vertex in polygon)
                {
                    var p = vertex.position;
                    var radial = new Vector3(side * .23f, .56f, Mathf.Clamp((p.z - z) / (stature * .033f), -1, 1))
                                     .normalized;
                    float u = Mathf.InverseLerp(span * .39f, span * 1.66f, p.x * side);
                    positions.Add(
                        root.InverseTransformPoint(p + radial * stature * (.0135f + .0017f * Mathf.Cos(u * 22))));
                    weights.Add(vertex.weight);
                    uv.Add(new Vector2(u, .68f + (p.z - z) / stature * 3));
                }
                for (int i = 1; i + 1 < polygon.Count; i++)
                    sections[slot].AddRange(new[] { first, first + i, first + i + 1 });
            }
        }
        void Mantle()
        {
            slot = 0;
            Grid(24, 48, (u, v) => Drape(u, v, 1), 1.22f);
            Grid(24, 44, (u, v) => Drape(u, v, -1), 1.19f, 0, true);
            Grid(24, 12, (u, v) => Crest(u, v), .12f, 1.22f);
        }
        void Clavus()
        {
            slot = 1;
            // A narrow woven band sewn into each side of the tunic travels under the mantle.
            for (int face = -1; face <= 1; face += 2)
                for (int dir = -1; dir <= 1; dir += 2)
                {
                    int sign = dir, depth = face;
                    Grid(5, 38,
                         (u, v) =>
                         {
                             float low = id == "JesusChild" ? .385f : .167f;
                             float y = Mathf.Lerp(stature * low, shoulder - stature * .021f, v);
                             float x =
                                 sign * span * (.55f + .12f * Mathf.Pow(1 - v, 2)) + (u - .5f) * stature * .014f;
                             return Surface(x, y, depth, stature * .0036f);
                         },
                         2.0f, 0, depth < 0);
                }
        }
        void WorkApron()
        {
            if (id != "Joseph")
                return;
            slot = 3;
            Grid(24, 21,
                 (u, v) =>
                 {
                     float x = (u - .5f) * span * Mathf.Lerp(2.0f, 2.17f, v);
                     float y =
                         waist - stature * (.245f * (1 - v) + .010f * Mathf.Sin(Mathf.PI * u * 2.1f) * (1 - v));
                     var contact = Surface(x, y, 1, stature * (.010f + .0025f * Mathf.Cos(u * 24)));
                     return contact;
                 });
        }
        void SandalLacing()
        {
            slot = 3;
            for (int sideIndex = 0; sideIndex < 2; sideIndex++)
            {
                var foot = Bone(sideIndex == 0 ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);
                var shin = Bone(sideIndex == 0 ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
                float direction = sideIndex == 0 ? -1 : 1;
                var w = new BoneWeight { boneIndex0 = BoneId(foot), weight0 = .65f, boneIndex1 = BoneId(shin),
                                         weight1 = .35f };
                for (int cross = 0; cross < 2; cross++)
                {
                    int turn = cross == 0 ? 1 : -1;
                    Grid(4, 25,
                         (u, v) =>
                         {
                             float a = (v * 1.08f + .055f) * Mathf.PI * 2 * turn * direction;
                             float radius = stature * (.0205f + .002f * Mathf.Cos(a));
                             var pos =
                                 foot.position + new Vector3(Mathf.Sin(a) * radius,
                                                             stature * (.006f + .047f * v + (u - .5f) * .0028f),
                                                             Mathf.Cos(a) * radius * 1.09f);
                             return new Point(pos, w);
                         });
                }
            }
        }
        void Belt()
        {
            slot = 2;
            // Elliptical belt follows both surfaces. Small front fold breaks the silhouette.
            Grid(64, 6,
                 (u, v) =>
                 {
                     float angle = u * Mathf.PI * 2;
                     float sin = Mathf.Sin(angle), cos = Mathf.Cos(angle);
                     float y = waist + (v - .5f) * stature * .030f + Mathf.Sin(angle + .4f) * stature * .006f;
                     float rx = span * (id == "JesusChild" ? 1.23f : 1.39f);
                     float x = sin * rx;
                     var hit = Surface(x, y, cos >= 0 ? 1 : -1, stature * .013f);
                     float elliptic = zAxis + cos * stature * (cos >= 0 ? .099f : .090f);
                     float z = cos >= 0 ? Mathf.Max(elliptic, hit.position.z) : Mathf.Min(elliptic, hit.position.z);
                     z = Mathf.Lerp(zAxis, z, Mathf.SmoothStep(0, 1, Mathf.Abs(cos) / .28f));
                     return new Point(new Vector3(x, y, z), Torso(y));
                 },
                 1f);
            slot = 3;
            float knotX = side * span * .67f, knotY = waist - stature * .008f;
            var knot = Surface(knotX, knotY, 1, stature * .026f);
            if (id == "Joseph")
            {
                // Gusseted leather pouch and an overlapping flap with a bronze stud.
                float k = stature * .048f;
                Grid(16, 12,
                     (u, v) =>
                     {
                         float angle = u * Mathf.PI * 2;
                         float rad = (.78f + .22f * Mathf.Sin(Mathf.PI * v)) * (.92f + .08f * v);
                         var pos = knot.position + new Vector3(k * Mathf.Sin(angle) * rad, k * (v - 1.0f) * 1.28f,
                                                               k * .38f * Mathf.Cos(angle) * rad);
                         return new Point(pos, knot.weight);
                     });
                Grid(14, 6,
                     (u, v) =>
                     {
                         float x = (u - .5f) * 1.58f;
                         return new Point(knot.position + new Vector3(k * x,
                                                                      -k * (v * (.51f + .22f * (1 - x * x)) - .02f),
                                                                      k * (.43f + .04f * Mathf.Sin(Mathf.PI * v))),
                                          knot.weight);
                     });
                Stud(knot.position + new Vector3(0, -k * .47f, k * .48f), k * .095f, knot.weight);
            }
            else
            {
                float tail = id == "Teacher" ? .125f : .09f;
                slot = 0;
                Grid(8, 15,
                     (u, v) =>
                     {
                         var p = Surface(knotX + (u - .5f) * stature * .019f +
                                             stature * .014f * Mathf.Sin(v * Mathf.PI),
                                         waist - stature * tail * v, 1, stature * .023f);
                         return new Point(p.position + Vector3.forward * (.012f * v), p.weight);
                     },
                     .23f);
                Stud(knot.position, stature * .010f, knot.weight);
            }
        }
        void Stud(Vector3 center, float radius, BoneWeight w)
        {
            slot = 4;
            Grid(12, 6,
                 (u, v) =>
                 {
                     float a = u * Mathf.PI * 2, b = v * Mathf.PI;
                     return new Point(center + radius * new Vector3(Mathf.Cos(a) * Mathf.Sin(b), Mathf.Cos(b),
                                                                    .55f * Mathf.Sin(a) * Mathf.Sin(b)),
                                      w);
                 });
        }
        public void Sew(Material[] materials)
        {
            Mantle();
            ShoulderCap();
            Clavus();
            WorkApron();
            Belt();
            SandalLacing();
            string asset = Art + id + " travel costume.asset";
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(asset);
            bool fresh = !mesh;
            if (fresh)
                mesh = new Mesh();
            else
                mesh.Clear(false);
            mesh.name = id + " woven mantle, tunic borders and belt";
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uv);
            mesh.subMeshCount = sections.Length;
            for (int i = 0; i < sections.Length; i++)
                mesh.SetTriangles(sections[i], i);
            mesh.boneWeights = weights.ToArray();
            mesh.bindposes = bones.Select(t => t.worldToLocalMatrix * root.localToWorldMatrix).ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            if (fresh)
                AssetDatabase.CreateAsset(mesh, asset);
            else
                EditorUtility.SetDirty(mesh);
            string name = id + "__TravelCostume";
            var node = root.Find(name);
            if (!node)
            {
                node = new GameObject(name).transform;
                node.SetParent(root, false);
            }
            var renderer = node.GetComponent<SkinnedMeshRenderer>();
            if (!renderer)
                renderer = node.gameObject.AddComponent<SkinnedMeshRenderer>();
            renderer.bones = bones.ToArray();
            renderer.rootBone = root;
            renderer.sharedMesh = mesh;
            renderer.sharedMaterials = materials;
            renderer.updateWhenOffscreen = true;
            renderer.localBounds = mesh.bounds;
        }
    }
    static Material Surface(string name, string texture, string tint, float sheen)
    {
        string file = Art + name + ".mat";
        var mat = AssetDatabase.LoadAssetAtPath<Material>(file);
        var shader = Shader.Find("FirstGame/Galilee/Loom");
        if (!shader)
            throw new InvalidOperationException("Missing loom shader");
        if (!mat)
        {
            mat = new Material(shader) { name = name };
            AssetDatabase.CreateAsset(mat, file);
        }
        mat.shader = shader;
        ColorUtility.TryParseHtmlString(tint, out var color);
        mat.SetColor("_Color", color);
        mat.SetFloat("_Sheen", sheen);
        if (texture != null)
        {
            string path = Art + texture + "-tabletweave.jpg";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer.wrapMode != TextureWrapMode.Repeat || importer.maxTextureSize != 512)
            {
                importer.wrapMode = TextureWrapMode.Repeat;
                importer.maxTextureSize = 512;
                importer.mipmapEnabled = true;
                importer.filterMode = FilterMode.Trilinear;
                importer.anisoLevel = 4;
                importer.SaveAndReimport();
            }
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }
        EditorUtility.SetDirty(mat);
        return mat;
    }
    static void JoinNeckline(string id, SkinnedMeshRenderer collar, Animator rig)
    {
        string file = Art + id + " matched neckline.asset";
        var current = collar.sharedMesh;
        if (AssetDatabase.GetAssetPath(current) != file)
        {
            var mesh = Object.Instantiate(current);
            mesh.name = id + " lined collar";
            var points = GospelSculptedCastBaker.WorldVertices(collar);
            var facing = new Vector2[points.Length];
            var tone = new Color32[points.Length];
            int side = id == "Teacher" || id == "Mary" ? -1 : 1;
            var neck = rig.GetBoneTransform(HumanBodyBones.Neck).position;
            float span = Mathf.Abs(rig.GetBoneTransform(HumanBodyBones.LeftUpperArm).position.x) * 1.1f;
            for (int i = 0; i < points.Length; i++)
            {
                float x = (points[i].x - neck.x) * side / span;
                facing[i] =
                    new Vector2(Mathf.Clamp01((x - .32f) / 1.39f), .66f + 3.4f * (points[i].z - neck.z) / neck.y);
                byte dye = (byte)Mathf.RoundToInt(255 * Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.26f, .40f, x)));
                tone[i] = new Color32(dye, dye, dye, 255);
            }
            mesh.uv2 = facing;
            mesh.colors32 = tone;
            AssetDatabase.CreateAsset(mesh, file);
            collar.sharedMesh = mesh;
        }
        var mat = Surface(id + " joined collar", id, "#ffffff", .07f);
        ColorUtility.TryParseHtmlString(id == "Mary"      ? "#ac6c57"
                                        : id == "Joseph"  ? "#b38d65"
                                        : id == "Teacher" ? "#a8a780"
                                                          : "#d2bf98",
                                        out var linen);
        mat.SetColor("_Undyed", linen);
        mat.SetFloat("_Lining", 1);
        EditorUtility.SetDirty(mat);
        collar.sharedMaterials = Enumerable.Repeat(mat, collar.sharedMesh.subMeshCount).ToArray();
    }
    static void PleatSkirt(string id, SkinnedMeshRenderer renderer, Animator rig)
    {
        string asset = Art + renderer.name + " skirt pleats.asset";
        if (AssetDatabase.GetAssetPath(renderer.sharedMesh) == asset)
            return;
        var mesh = Object.Instantiate(renderer.sharedMesh);
        mesh.name = renderer.name + " pleated linen";
        var root = rig.GetBoneTransform(HumanBodyBones.Hips);
        float waist = root.position.y, phase = id == "Mary" ? .49f : id == "Teacher" ? 1.4f : .03f;
        var points = mesh.vertices;
        var world = GospelSculptedCastBaker.WorldVertices(renderer);
        var matrices = renderer.bones.Select((b, i) => b.localToWorldMatrix * mesh.bindposes[i]).ToArray();
        var weights = mesh.boneWeights;
        for (int i = 0; i < points.Length; i++)
        {
            var vector = world[i] - root.position;
            float fall = Mathf.InverseLerp(waist * .99f, waist * .23f, world[i].y);
            var radial = new Vector3(vector.x, 0, vector.z).normalized;
            float angle = Mathf.Atan2(vector.x, vector.z);
            float fold = Mathf.Sin(angle * 14 + phase + .63f * fall + .16f * Mathf.Sin(angle * 3 + fall * 2));
            float secondary = Mathf.Sin(angle * 7 + phase - fall * .62f);
            var shift =
                radial * (.003f + waist * fall * (.013f * (.5f + .5f * fold) + .006f * (.5f + .5f * secondary)));
            shift.y = fall * waist * .006f * (fold + .4f * secondary);
            var w = weights[i];
            Matrix4x4 matrix = Matrix4x4.zero;
            for (int j = 0; j < 16; j++)
                matrix[j] = matrices[w.boneIndex0][j] * w.weight0 + matrices[w.boneIndex1][j] * w.weight1 +
                            matrices[w.boneIndex2][j] * w.weight2 + matrices[w.boneIndex3][j] * w.weight3;
            points[i] += matrix.inverse.MultiplyVector(shift);
        }
        mesh.vertices = points;
        mesh.RecalculateNormals();
        mesh.RecalculateTangents();
        mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh, asset);
        renderer.sharedMesh = mesh;
    }
    public static void Bake()
    {
        foreach (var id in Cast)
        {
            var materials = new[] { Surface(id + " tablet woven wool", id, "#ffffff", .16f),
                                    Surface(id + " linen clavi", id, "#e5d9c3", .07f),
                                    Surface(id + " belt braid", id, "#baa376", .12f),
                                    Surface("Chestnut harness leather", "saddle", "#d7b99f", .22f),
                                    Surface("Hammered warm bronze", null, "#b99b64", .68f) };
            string folder = id == "JesusChild" ? "GospelHero20261119" : "GospelNpr";
            string path = "Assets/Resources/Art/" + folder + "/Characters/" + id + ".prefab";
            var model = PrefabUtility.LoadPrefabContents(path);
            try
            {
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.name.EndsWith("__TailoredTunic") || r.name.EndsWith("__WovenHem") ||
                        r.name.EndsWith("__SewnLinenTunic"))
                        PleatSkirt(id, r, model.GetComponentInChildren<Animator>());
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.name.EndsWith("__HumanNecklineV1"))
                        JoinNeckline(id, r, model.GetComponentInChildren<Animator>());
                new Tailor(id, model).Sew(materials);
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.name.EndsWith("__OpenVeil"))
                        r.sharedMaterial = materials[0];
                foreach (var r in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                    if (r.name.EndsWith("__WovenSash") || r.name.EndsWith("__FoldedWoolMantle") ||
                        r.name.EndsWith("__ShoulderMantle") || r.name.EndsWith("__SashEnd"))
                        r.enabled = false;
                PrefabUtility.SaveAsPrefabAsset(model, path);
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(model);
            }
        }
        AssetDatabase.SaveAssets();
    }
}
}
