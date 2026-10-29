using System.Collections;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace PathOfDawn.SmokeTests
{
    public sealed partial class RenewalSmokeTests
    {
        Vector3[] ChronicleLimbVertices()
        {
            var meshes = App.Player.Actor.GetComponentsInChildren<MeshFilter>()
                .Where(m => m.sharedMesh != null && m.sharedMesh.name.EndsWith("continuous weighted surface")).ToArray();
            Assert.That(meshes.Length, Is.EqualTo(4), "All four rendered limb surfaces must use the actual continuous rig.");
            return meshes.SelectMany(m => m.sharedMesh.vertices).ToArray();
        }

        void AssertChronicleLampUpright()
        {
            var lamp = GameObject.Find("Held lamp · world-upright wrist socket");
            Assert.That(lamp, Is.Not.Null);
            Assert.That(lamp.GetComponent<SpriteRenderer>().sprite, Is.Not.Null);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(lamp.transform.eulerAngles.z, 0)), Is.LessThan(.5f),
                "The actual held lamp must remain upright when the figure turns or leans.");
        }

        IEnumerator ChronicleActualCharacterPoses(string age, bool adult)
        {
            Assert.That(App.Ui.CurrentView, Is.EqualTo("journey"));
            Assert.That(App.Player.Actor.IsAdult, Is.EqualTo(adult));
            Assert.That(App.Player.Actor.UsesContinuousLimbRig, Is.True);
            Assert.That(App.Player.Actor.ContinuousLimbCount, Is.EqualTo(4));
            Assert.That(App.Player.Actor.UsesChroniclePainting, Is.EqualTo(adult));
            Assert.That(App.Player.Actor.UsesOriginalChildAtlas, Is.EqualTo(!adult));
            float startX = App.Player.Position.x;
            if (App.Journey.LampEquipped)
            {
                yield return Key(UnityEngine.InputSystem.Key.B);
                yield return ChronicleClick("등잔 보관");
                yield return ChronicleClick("여정으로");
            }
            Assert.That(App.Player.Actor.LampEquipped, Is.False);
            for (int i = 0; i < 6; i++)
            { InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0, 120)); yield return null; }
            yield return new WaitForSecondsRealtime(.65f);
            yield return Key(UnityEngine.InputSystem.Key.H);
            foreach (var direction in new[] { UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.A })
            {
                string side = direction == UnityEngine.InputSystem.Key.D ? "right" : "left";
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction));
                yield return new WaitForSecondsRealtime(.24f);
                yield return SmokeUi.Capture("PathOfDawn", "chronicle-" + attempt + "-full-route-" + age + "-handsfree-walk-" + side,
                    1280, 720, settleSeconds: 0f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction, UnityEngine.InputSystem.Key.LeftShift));
                yield return new WaitForSecondsRealtime(.28f);
                yield return SmokeUi.Capture("PathOfDawn", "chronicle-" + attempt + "-full-route-" + age + "-handsfree-run-" + side,
                    1280, 720, settleSeconds: 0f);
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            SmokeUi.Click("게임으로 · H"); yield return null;
            if (!App.Journey.LampEquipped)
            {
                yield return Key(UnityEngine.InputSystem.Key.B);
                yield return ChronicleClick("등잔 장착");
                Assert.That(App.Journey.LampEquipped, Is.True);
                yield return ChronicleClick("여정으로");
            }
            Assert.That(App.Player.Actor.LampEquipped, Is.True);
            for (int i = 0; i < 6; i++)
            { InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0, 120)); yield return null; }
            yield return new WaitForSecondsRealtime(.65f);
            Assert.That(App.Zoom, Is.EqualTo(3f).Within(.05f));
            yield return ChronicleShot(age + "-close-idle-hud");
            yield return Key(UnityEngine.InputSystem.Key.H);
            Assert.That(App.Ui.ScenicView, Is.True);
            yield return ChronicleShot(age + "-close-idle-scenic");
            foreach (var direction in new[] { UnityEngine.InputSystem.Key.D, UnityEngine.InputSystem.Key.A })
            {
                string side = direction == UnityEngine.InputSystem.Key.D ? "right" : "left";
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction));
                yield return new WaitForSecondsRealtime(.23f);
                var before = ChronicleLimbVertices();
                for (int frame = 0; frame < 3; frame++)
                {
                    AssertChronicleLampUpright();
                    yield return SmokeUi.Capture("PathOfDawn", "chronicle-" + attempt + "-full-route-" + age + "-walk-" + side + "-" + frame,
                        1280, 720, settleSeconds: 0f);
                    yield return new WaitForSecondsRealtime(.13f);
                }
                var after = ChronicleLimbVertices();
                Assert.That(before.Zip(after, (a, b) => (a - b).sqrMagnitude).Sum(), Is.GreaterThan(.00001f),
                    "The real rendered limb vertices must deform across distinct walking phases.");
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(direction, UnityEngine.InputSystem.Key.LeftShift));
                yield return new WaitForSecondsRealtime(.32f);
                AssertChronicleLampUpright();
                yield return SmokeUi.Capture("PathOfDawn", "chronicle-" + attempt + "-full-route-" + age + "-run-" + side,
                    1280, 720, settleSeconds: 0f);
                InputSystem.QueueStateEvent(keyboard, new KeyboardState()); yield return null;
            }
            yield return new WaitForSecondsRealtime(.2f);
            float ground = App.Player.Position.y;
            yield return Key(UnityEngine.InputSystem.Key.Space);
            yield return new WaitForSecondsRealtime(.10f);
            Assert.That(App.Player.Position.y, Is.GreaterThan(ground + .12f));
            AssertChronicleLampUpright();
            yield return SmokeUi.Capture("PathOfDawn", "chronicle-" + attempt + "-full-route-" + age + "-jump", 1280, 720, settleSeconds: 0f);
            yield return new WaitForSecondsRealtime(1.25f);
            Assert.That(App.Player.Position.y, Is.EqualTo(ground).Within(.18f));
            yield return ChronicleShot(age + "-landed-lamp");
            SmokeUi.Click("게임으로 · H"); yield return null;
            Assert.That(App.Ui.ScenicView, Is.False);
            for (int i = 0; i < 4; i++)
            { InputSystem.QueueDeltaStateEvent(mouse.scroll, new Vector2(0, -120)); yield return null; }
            yield return new WaitForSecondsRealtime(.6f);
            float deadline = Time.realtimeSinceStartup + 8;
            while (Mathf.Abs(App.Player.Position.x - startX) > .15f && Time.realtimeSinceStartup < deadline)
            {
                InputSystem.QueueStateEvent(keyboard, new KeyboardState(App.Player.Position.x < startX
                    ? UnityEngine.InputSystem.Key.D : UnityEngine.InputSystem.Key.A));
                yield return new WaitForSecondsRealtime(.025f);
            }
            InputSystem.QueueStateEvent(keyboard, new KeyboardState());
            yield return new WaitForSecondsRealtime(.15f);
            Assert.That(App.Player.Position.x, Is.EqualTo(startX).Within(.35f),
                "The ordinary pose walk must return outside the first quest's interaction radius.");
        }
    }
}
