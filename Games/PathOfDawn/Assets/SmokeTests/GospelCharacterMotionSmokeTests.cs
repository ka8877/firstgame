using System;
using System.Collections;
using System.Collections.Generic;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Licensed complete-body renders, separate real gait banks and controller-owned roots.</summary>
    public sealed class GospelCharacterMotionSmokeTests
    {
        GameObject root;
        IDisposable storage;
        MemorySaveBackend backend;
        bool hadSlot;
        string oldSlot;
        readonly List<GospelPilgrimageActor> actors = new List<GospelPilgrimageActor>();
        readonly List<Transform> feet = new List<Transform>();
        GospelPilgrimageApp App => UnityEngine.Object.FindFirstObjectByType<GospelPilgrimageApp>();
        Camera View => App.SceneCamera;

        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            backend = new MemorySaveBackend(); backend.WriteAtomic("motion-preserved-player", "original player bytes");
            storage = PersistentSaves.UseBackendForTesting(backend);
            hadSlot = PlayerPrefs.HasKey("GOSPEL.activeSlot"); oldSlot = PlayerPrefs.GetString("GOSPEL.activeSlot", "");
            PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            yield return SmokeUi.LoadBoot();
            Assert.That(App, Is.Not.Null); Assert.That(View, Is.SameAs(Camera.main));
            root = new GameObject("Neutral complete-body motion diagnostic");
            root.transform.position = App.Player.Position;
            var focus = new GameObject("Actual scene camera family focus").transform;
            focus.SetParent(root.transform, false); focus.localPosition = Vector3.forward * 2.3f;
            App.CameraRig.SetTarget(focus); App.CameraRig.SetZoom(4.5f); App.CameraRig.Snap();
            App.CameraRig.InputEnabled = false;
            // This is an art fixture in the real Boot world. Hide the title
            // overlay and its duplicate playable visual, retaining the genuine
            // scene camera, terrain, materials and normal Capture safeguards.
            App.Ui.CanvasView.gameObject.SetActive(false);
            App.Actor.gameObject.SetActive(false);
            yield return null;
        }

        [UnityTearDown] public IEnumerator TearDown()
        {
            if (root != null) UnityEngine.Object.Destroy(root);
            actors.Clear(); feet.Clear();
            yield return SmokeUi.UnloadBoot();
            Assert.That(backend.Read("motion-preserved-player"), Is.EqualTo("original player bytes"));
            storage?.Dispose();
            if (hadSlot) PlayerPrefs.SetString("GOSPEL.activeSlot", oldSlot); else PlayerPrefs.DeleteKey("GOSPEL.activeSlot");
            PlayerPrefs.Save();
            yield return null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        GospelPilgrimageActor Add(string identity, int index, bool woman = false, bool ambient = false)
        {
            var foot = new GameObject(identity + " persistent physical root").transform;
            foot.SetParent(root.transform, false);
            foot.localPosition = new Vector3((index % 5 - 2) * 1.38f, 0, (index / 5) * 2.3f);
            var position = foot.position;
            position.y = App.World.HeightAt(position.x, position.z) + .035f; foot.position = position;
            GospelPilgrimageActor actor;
            if (identity == "jesus-child" || identity == "jesus-adult")
                actor = GospelPilgrimageActor.Build(foot, View, identity == "jesus-adult");
            else if (ambient) actor = GospelPilgrimageActor.BuildAmbient(foot, View, "unused-original-family-fallback", woman, 1.72f, Color.white);
            else actor = GospelPilgrimageActor.BuildNpc(foot, View, identity, woman ? 1.63f : 1.78f);
            actor.SetGroundSampler(App.World.HeightAt);
            actor.SetMotion(Vector3.zero, true, 180);
            actors.Add(actor); feet.Add(foot); return actor;
        }

        [UnityTest] public IEnumerator EveryStoryAndAmbientIdentityUsesEightActualSynchronizedLocomotionDrawings()
        {
            Add("jesus-child",0); Add("jesus-adult",1);
            var identities = new HashSet<string>(StringComparer.Ordinal);
            int index = 2;
            foreach (var quest in GospelChronicleCatalog.Quests)
            {
                if (quest.IsPrologue || !identities.Add(quest.NpcId)) continue;
                Add(quest.NpcId, index++, quest.NpcId == "mary");
            }
            Assert.That(identities.Count, Is.EqualTo(9), "Cover every unchanged live-world story identity from both chapters.");
            Add("pilgrim-man",index++,false,true); Add("pilgrim-woman",index,true,true);
            yield return null;
            var origins = new Vector3[actors.Count];
            var instanceIds = new int[actors.Count];
            var poses = new HashSet<int>[actors.Count];
            var poseRegions = new HashSet<string>[actors.Count];
            var actualSources = new HashSet<string>();
            for (int i = 0; i < actors.Count; i++)
            {
                var actor = actors[i];
                Assert.That(actor.UsesLayeredMotion, Is.True, feet[i].name);
                Assert.That(actor.MotionContractVersion, Is.EqualTo(2));
                Assert.That(actor.MotionLayerCount, Is.EqualTo(1)); Assert.That(actor.MotionDirectionCount, Is.EqualTo(8));
                Assert.That(actor.AnimationFrameCount, Is.EqualTo(160));
                Assert.That(actor.FramesPerDirectionCount, Is.EqualTo(20));
                Assert.That(actor.WalkPoseCount, Is.EqualTo(8)); Assert.That(actor.RunPoseCount, Is.EqualTo(8));
                Assert.That(actor.WalkStrideMeters, Is.InRange(.65f, 2.1f));
                Assert.That(actor.RunStrideMeters, Is.GreaterThan(actor.WalkStrideMeters));
                Assert.That(actor.RunStrideMeters, Is.LessThan(3.2f));
                Assert.That(actor.OriginalAtlasResource, Does.StartWith("Art/GospelMotion/Neutral/"));
                actualSources.Add(actor.OriginalAtlasResource);
                Assert.That(actor.PortraitSprite, Is.Not.Null);
                if (actor.IsNpc)
                {
                    Assert.That(actor.PortraitSprite.texture, Is.SameAs(actor.CurrentSprite.texture));
                    Assert.That(actor.PortraitSprite.rect.height, Is.LessThan(actor.CurrentSprite.rect.height),
                        "A dialogue portrait uses the measured head and shoulders, not a miniature whole actor.");
                }
                origins[i] = feet[i].position; instanceIds[i] = feet[i].GetInstanceID();
                poses[i] = new HashSet<int>(); poseRegions[i] = new HashSet<string>();
                actor.SetMotion(Vector3.right * 1.65f, true, 90);
            }
            float until = Time.realtimeSinceStartup + 2.1f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                for (int i = 0; i < actors.Count; i++)
                {
                    var actor = actors[i];
                    Assert.That(actor.CurrentState, Is.EqualTo("Walk")); Assert.That(actor.CurrentFacing, Is.EqualTo("Right"));
                    Assert.That(feet[i].GetInstanceID(), Is.EqualTo(instanceIds[i]));
                    Assert.That(feet[i].position, Is.EqualTo(origins[i]));
                    Assert.That(actor.VisualFootOrigin.position, Is.EqualTo(origins[i]));
                    poses[i].Add(actor.CurrentFrame);
                    var regions = new List<string>();
                    int synchronizedFrame = 5 * 20 + actor.CurrentFrame; // actual E index5
                    foreach (var renderer in actor.MotionRenderers)
                    {
                        Assert.That(renderer.sprite, Is.Not.Null);
                        Assert.That(renderer.sprite.name, Does.EndWith("authored pose " + synchronizedFrame));
                        Assert.That(renderer.flipX, Is.False, "Eight actual source facings replace reflection of one static picture.");
                        Assert.That(renderer.sprite.texture.width, Is.GreaterThan(128));
                        Assert.That(float.IsNaN(renderer.bounds.center.y) || float.IsInfinity(renderer.bounds.center.y), Is.False);
                        regions.Add(renderer.sprite.texture.name + ":" + renderer.sprite.rect);
                    }
                    poseRegions[i].Add(string.Join("|", regions));
                }
            }
            Assert.That(actualSources.SetEquals(new[] { "Art/GospelMotion/Neutral/child", "Art/GospelMotion/Neutral/adult",
                "Art/GospelMotion/Neutral/mary", "Art/GospelMotion/Neutral/joseph", "Art/GospelMotion/Neutral/teacher" }), Is.True,
                "Both ages and the named/ambient family use the five actual licensed source adaptations.");
            for (int i = 0; i < actors.Count; i++)
            {
                Assert.That(poses[i].SetEquals(new[] {4,5,6,7,8,9,10,11}), Is.True, feet[i].name);
                Assert.That(poseRegions[i].Count, Is.EqualTo(8), "The actual body regions must change through all eight poses: " + feet[i].name);
                poses[i].Clear(); poseRegions[i].Clear();
                actors[i].SetMotion(Vector3.right * 5.4f, true, 90);
            }
            until = Time.realtimeSinceStartup + 1.8f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                for (int i = 0; i < actors.Count; i++)
                {
                    var actor = actors[i];
                    Assert.That(actor.CurrentState, Is.EqualTo("Run"));
                    Assert.That(actor.CurrentFrame, Is.InRange(12, 19), "Running selects its separate longer-stride drawings.");
                    poses[i].Add(actor.CurrentFrame);
                    poseRegions[i].Add(actor.CurrentSprite.texture.name + ":" + actor.CurrentSprite.rect);
                    Assert.That(feet[i].GetInstanceID(), Is.EqualTo(instanceIds[i]));
                    Assert.That(feet[i].position, Is.EqualTo(origins[i]));
                    Assert.That(actor.VisualFootOrigin.position, Is.EqualTo(origins[i]));
                }
            }
            for (int i = 0; i < actors.Count; i++)
            {
                Assert.That(poses[i].SetEquals(new[] {12,13,14,15,16,17,18,19}), Is.True, feet[i].name);
                Assert.That(poseRegions[i].Count, Is.EqualTo(8), "All eight actual run source regions must play.");
                actors[i].SetMotion(Vector3.zero, true, 180);
            }
            yield return null;
            yield return SmokeUi.Capture("PathOfDawn", "client-motion-original-actor-family", 1280, 720, settleSeconds:.12f);
        }

        [UnityTest] public IEnumerator NamedNpcFacesAllEightPartnersStopsItsGaitAndKeepsTheSamePhysicalFeet()
        {
            var actor = Add("joseph", 2);
            var origin = feet[0].position;
            int identity = feet[0].GetInstanceID();
            Vector3[] offsets = {Vector3.back,new Vector3(-1,0,-1),Vector3.left,new Vector3(-1,0,1),
                Vector3.forward,new Vector3(1,0,1),Vector3.right,new Vector3(1,0,-1)};
            string[] names = {"Front","FrontLeft","Left","BackLeft","Back","BackRight","Right","FrontRight"};
            actor.SetMotion(Vector3.left * 2.05f, true, 270);
            yield return new WaitForSecondsRealtime(.35f);
            Assert.That(actor.CurrentState, Is.EqualTo("Walk"));
            var spriteBeforeConversation = actor.CurrentSprite;
            for (int i = 0; i < offsets.Length; i++)
            {
                actor.SetMotion(Vector3.zero, true, 270);
                actor.FaceConversationPartner(origin + offsets[i] * 2);
                yield return null;
                Assert.That(actor.CurrentState, Is.EqualTo("Idle")); Assert.That(actor.CurrentFacing, Is.EqualTo(names[i]));
                Assert.That(actor.CurrentFrame, Is.Zero, "An accepted conversation plants the authored stance.");
                Assert.That(feet[0].position, Is.EqualTo(origin)); Assert.That(feet[0].GetInstanceID(), Is.EqualTo(identity));
                Assert.That(actor.VisualFootOrigin.position, Is.EqualTo(origin));
            }
            Assert.That(actor.CurrentSprite, Is.Not.EqualTo(spriteBeforeConversation));
            actor.SetMotion(new Vector3(1,3,0), false, 90); yield return null;
            Assert.That(actor, Is.Not.Null, "The source actor must remain alive through the real scene's flight drawing.");
            Assert.That(actor.CurrentState, Is.EqualTo("Jump")); Assert.That(actor.CurrentFrame, Is.EqualTo(6));
            actor.SetMotion(new Vector3(1,-3,0), false, 90); yield return null;
            Assert.That(actor, Is.Not.Null, "The same actor must survive the falling pose transition.");
            Assert.That(actor.CurrentState, Is.EqualTo("Fall")); Assert.That(actor.CurrentFrame, Is.EqualTo(10));
            actor.SetMotion(Vector3.zero,true,180); yield return null;
            Assert.That(actor.CurrentState, Is.EqualTo("Idle"));
            Assert.That(feet[0].position, Is.EqualTo(origin));
        }
    }
}
