using System;
using System.Collections.Generic;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>A small cached chapter grid for background people; no NavMesh bake or gameplay randomness.</summary>
    sealed class GospelPeopleNavigation
    {
        const float Cell = 1f, Radius = .24f;
        readonly GospelPilgrimageWorld world;
        readonly Transform people, player;
        readonly Collider[] overlaps = new Collider[32];
        readonly RaycastHit[] hits = new RaycastHit[32];
        readonly int width, height;
        readonly Vector3[] points;
        readonly bool[] clear;
        readonly float[] costs, distance;
        readonly int[] previous;
        readonly bool[] closed;
        readonly List<int> open = new List<int>();
        readonly List<int> reverse = new List<int>();
        readonly Rect bounds;

        public GospelPeopleNavigation(GospelPilgrimageWorld scene, Transform peopleRoot, Transform traveler)
        {
            world = scene; people = peopleRoot; player = traveler; bounds = world.Bounds;
            width = Mathf.FloorToInt(bounds.width / Cell) + 1;
            height = Mathf.FloorToInt(bounds.height / Cell) + 1;
            int count = width * height;
            points = new Vector3[count]; clear = new bool[count]; costs = new float[count];
            distance = new float[count]; previous = new int[count]; closed = new bool[count];
            var trail = world.MapTrail;
            for (int z = 0; z < height; z++) for (int x = 0; x < width; x++)
            {
                int i = z * width + x;
                float px = bounds.xMin + x * Cell, pz = bounds.yMin + z * Cell;
                points[i] = new Vector3(px, world.HeightAt(px, pz), pz);
                clear[i] = bounds.Contains(new Vector2(px, pz)) && IsClear(points[i]);
                float pathDistance = float.MaxValue;
                foreach (var p in trail) pathDistance = Mathf.Min(pathDistance, (p - new Vector2(px, pz)).sqrMagnitude);
                bool paved = false;
                foreach (var court in world.MapCourts) if (court.Contains(new Vector2(px, pz))) paved = true;
                // People prefer the actual footpath and paved courts, while an
                // open garden remains traversable when avoiding a vessel/tree.
                costs[i] = paved || pathDistance < 1.1f ? 1 : 1.24f;
            }
        }

        bool Ignored(Collider collider)
        {
            if (collider == null) return true;
            Transform t = collider.transform;
            return t.IsChildOf(people) || t.IsChildOf(player);
        }

        public bool IsClear(Vector3 position, float radius = Radius)
            => ClearanceProblem(position, radius) == null;

        public bool CanTraverse(Vector3 from, Vector3 to) => SegmentClear(from, to);

        public string ClearanceProblem(Vector3 position, float radius = Radius)
        {
            float ground = world.HeightAt(position.x, position.z);
            Vector3 bottom = new Vector3(position.x, ground + .31f, position.z);
            int count = Physics.OverlapCapsuleNonAlloc(bottom, bottom + Vector3.up * 1.10f, radius,
                overlaps, ~0, QueryTriggerInteraction.Ignore);
            if (count >= overlaps.Length) return "overlap buffer full";
            for (int i = 0; i < count; i++) if (!Ignored(overlaps[i])) return overlaps[i].name;
            return null;
        }

        bool SegmentClear(Vector3 from, Vector3 to)
        {
            from.y = world.HeightAt(from.x, from.z) + .31f;
            to.y = world.HeightAt(to.x, to.z) + .31f;
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < .001f) return true;
            int count = Physics.CapsuleCastNonAlloc(from, from + Vector3.up * 1.10f, Radius,
                delta / length, hits, length, ~0, QueryTriggerInteraction.Ignore);
            if (count >= hits.Length) return false;
            for (int i = 0; i < count; i++)
                if (!Ignored(hits[i].collider) && hits[i].normal.y < .7f) return false;
            return true;
        }

        int Nearest(Vector3 point)
        {
            int best = -1; float score = float.PositiveInfinity;
            for (int i = 0; i < points.Length; i++)
            {
                if (!clear[i]) continue;
                float d = (new Vector2(point.x, point.z) - new Vector2(points[i].x, points[i].z)).sqrMagnitude;
                if (d < score && SegmentClear(point, points[i])) { score = d; best = i; }
            }
            return best;
        }

        public bool FindPath(Vector3 from, Vector3 target, List<Vector3> result)
        {
            result.Clear();
            if (IsClear(target) && SegmentClear(from, target)) { result.Add(target); return true; }
            int start = Nearest(from), goal = Nearest(target);
            if (start < 0 || goal < 0) return false;
            Array.Clear(closed, 0, closed.Length);
            for (int i = 0; i < distance.Length; i++) { distance[i] = float.PositiveInfinity; previous[i] = -1; }
            open.Clear(); reverse.Clear(); open.Add(start); distance[start] = 0;
            while (open.Count > 0)
            {
                int chosen = 0; float best = float.PositiveInfinity;
                for (int i = 0; i < open.Count; i++)
                {
                    int n = open[i]; float score = distance[n] + Vector3.Distance(points[n], points[goal]);
                    if (score < best) { best = score; chosen = i; }
                }
                int node = open[chosen]; open.RemoveAt(chosen);
                if (node == goal)
                {
                    for (int n = goal; n != start; n = previous[n])
                    {
                        if (n < 0) return false;
                        reverse.Add(n);
                    }
                    // Nearest validated this connector. Omitting it cuts an
                    // unchecked corner from the actual actor to its neighbour.
                    if (Vector2.Distance(new Vector2(from.x, from.z),
                        new Vector2(points[start].x, points[start].z)) > .02f)
                        result.Add(points[start]);
                    for (int i = reverse.Count - 1; i >= 0; i--) result.Add(points[reverse[i]]);
                    if (IsClear(target) && SegmentClear(points[goal], target)) result.Add(target);
                    return result.Count > 0;
                }
                closed[node] = true;
                int x = node % width, z = node / width;
                for (int dz = -1; dz <= 1; dz++) for (int dx = -1; dx <= 1; dx++)
                {
                    if (dx == 0 && dz == 0) continue;
                    int nx = x + dx, nz = z + dz;
                    if (nx < 0 || nx >= width || nz < 0 || nz >= height) continue;
                    int next = nz * width + nx;
                    if (!clear[next] || closed[next] || Mathf.Abs(points[next].y - points[node].y) > .42f) continue;
                    if (dx != 0 && dz != 0 && (!clear[z * width + nx] || !clear[nz * width + x])) continue;
                    if (!SegmentClear(points[node], points[next])) continue;
                    float proposal = distance[node] + (dx != 0 && dz != 0 ? 1.414214f : 1) * costs[next];
                    if (proposal >= distance[next]) continue;
                    distance[next] = proposal; previous[next] = node;
                    if (!open.Contains(next)) open.Add(next);
                }
            }
            return false;
        }
    }
}
