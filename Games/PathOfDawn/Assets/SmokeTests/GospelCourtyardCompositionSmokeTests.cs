using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Physical coordinates and actual rendered geometry; visual quality still needs original-frame review.</summary>
    public sealed class GospelCourtyardCompositionSmokeTests
    {
        GameObject fixture, cameraObject, player;
        GospelPilgrimageWorld world;
        GospelPilgrimageCamera rig;
        Camera camera;

        sealed class RenderedGeometry
        {
            public string Name;
            public Vector3[] Vertices;
            public int[] Triangles;
            public Bounds Bounds;
        }

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            fixture = new GameObject("Courtyard composition regression");
            world = fixture.AddComponent<GospelPilgrimageWorld>();
            world.Build();
            player = new GameObject("Preserved encounter feet");
            player.transform.SetParent(fixture.transform, false);
            cameraObject = new GameObject("Courtyard sightline camera", typeof(Camera));
            cameraObject.transform.SetParent(fixture.transform, false);
            camera = cameraObject.GetComponent<Camera>();
            rig = cameraObject.AddComponent<GospelPilgrimageCamera>();
            rig.Initialize(camera, player.transform);
            rig.InputEnabled = false;
            yield return null;
        }

        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if (fixture != null) Object.Destroy(fixture);
            yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator SavedMeetingsKeepTheirCoordinatesAndAnOpenArchitecturalBodySightline()
        {
            // These are the established public-world coordinates, including the
            // listening meeting that previously sat beneath a broad cornice.
            string[] ids = { "nazareth-departure", "pilgrim-spring", "listening-court", "teachers-circle" };
            Vector2[] saved = { new Vector2(-27, -5), new Vector2(-12, -8), new Vector2(11, 10), new Vector2(18, 4) };
            var geometry = new List<RenderedGeometry>();
            foreach (var filter in world.TempleRoot.GetComponentsInChildren<MeshFilter>())
            {
                var renderer = filter.GetComponent<MeshRenderer>();
                if (renderer == null || !renderer.enabled || !filter.gameObject.activeInHierarchy
                    || !filter.name.StartsWith("Static scene batch · ")) continue;
                var vertices = filter.sharedMesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = filter.transform.TransformPoint(vertices[i]);
                geometry.Add(new RenderedGeometry { Name = filter.name, Vertices = vertices,
                    Triangles = filter.sharedMesh.triangles, Bounds = renderer.bounds });
            }
            Assert.That(geometry, Is.Not.Empty, "Inspect the actual combined render meshes, not only collider bounds.");
            for (int meeting = 0; meeting < ids.Length; meeting++)
            {
                var quest = GospelChronicleCatalog.Quests.Single(q => q.Id == ids[meeting]);
                Vector3 position = world.PositionForQuest(quest);
                Assert.That(new Vector2(position.x, position.z), Is.EqualTo(saved[meeting]), ids[meeting]);
                Assert.That(position.y, Is.EqualTo(world.HeightAt(position.x, position.z)).Within(.0001f));
                player.transform.position = position;
                rig.Snap();
                yield return null;
                foreach (float height in new[] { .20f, .85f, 1.55f })
                foreach (float side in new[] { -.16f, .16f })
                {
                    Vector3 target = position + camera.transform.up * height + camera.transform.right * side;
                    Vector3 screen = camera.WorldToViewportPoint(target);
                    Assert.That(screen.z, Is.GreaterThan(0));
                    Assert.That(screen.x, Is.InRange(.08f, .92f), ids[meeting]);
                    Assert.That(screen.y, Is.InRange(.08f, .92f), ids[meeting]);
                    var ray = camera.ViewportPointToRay(screen);
                    float targetDistance = Vector3.Dot(target - ray.origin, ray.direction);
                    foreach (var mesh in geometry)
                    {
                        if (!mesh.Bounds.IntersectRay(ray, out float boundDistance) || boundDistance >= targetDistance) continue;
                        for (int triangle = 0; triangle < mesh.Triangles.Length; triangle += 3)
                        {
                            float distance = TriangleDistance(ray, mesh.Vertices[mesh.Triangles[triangle]],
                                mesh.Vertices[mesh.Triangles[triangle + 1]], mesh.Vertices[mesh.Triangles[triangle + 2]]);
                            if (distance < targetDistance - .04f && distance > 0)
                                Assert.Fail(ids[meeting] + " body height " + height + " is hidden by " + mesh.Name);
                        }
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator DefaultViewMakesTheWholeFigureReadableWithoutChangingScreenRelativeDirections()
        {
            player.transform.position = world.SpawnForChapter("temple");
            rig.Snap();
            yield return null;
            Assert.That(camera.orthographic, Is.True);
            Assert.That(Vector3.Dot(camera.transform.right, Vector3.right), Is.GreaterThan(.9999f));
            Assert.That(camera.transform.eulerAngles.x, Is.EqualTo(43).Within(.001f));
            Vector3 feet = camera.WorldToViewportPoint(player.transform.position);
            Vector3 head = camera.WorldToViewportPoint(player.transform.position + camera.transform.up * 1.64f);
            Assert.That(head.y - feet.y, Is.InRange(.16f, .22f),
                "The small browser canvas needs a legible full figure while retaining surrounding terrain.");
            Assert.That(feet.y, Is.GreaterThan(.12f));
            Assert.That(head.y, Is.LessThan(.88f));
            rig.SetZoom(GospelPilgrimageCamera.MaximumZoom);
            rig.Snap();
            Assert.That(camera.orthographicSize, Is.EqualTo(GospelPilgrimageCamera.MaximumZoom));
            Assert.That(Vector3.Dot(camera.transform.right, Vector3.right), Is.GreaterThan(.9999f));
        }

        static float TriangleDistance(Ray ray, Vector3 a, Vector3 b, Vector3 c)
        {
            Vector3 ab = b - a, ac = c - a;
            Vector3 p = Vector3.Cross(ray.direction, ac);
            float determinant = Vector3.Dot(ab, p);
            if (Mathf.Abs(determinant) < .000001f) return float.PositiveInfinity;
            float inverse = 1f / determinant;
            Vector3 offset = ray.origin - a;
            float u = Vector3.Dot(offset, p) * inverse;
            if (u < 0 || u > 1) return float.PositiveInfinity;
            Vector3 q = Vector3.Cross(offset, ab);
            float v = Vector3.Dot(ray.direction, q) * inverse;
            if (v < 0 || u + v > 1) return float.PositiveInfinity;
            float distance = Vector3.Dot(ac, q) * inverse;
            return distance > 0 ? distance : float.PositiveInfinity;
        }
    }
}
