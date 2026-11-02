using System.Collections;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    /// <summary>Actual world/physics/input regressions; aesthetic review uses the separate full-player captures.</summary>
    public sealed class GospelCanopyVisualSmokeTests
    {
        GameObject fixture;
        GospelPilgrimageWorld world;
        Keyboard keyboard;
        InputSettings previousInputs, testInputs;

        [UnitySetUp]
        public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
            yield return new EnterPlayMode();
            previousInputs=InputSystem.settings; testInputs=Object.Instantiate(previousInputs);
            testInputs.hideFlags=HideFlags.HideAndDontSave;
            testInputs.editorInputBehaviorInPlayMode=InputSettings.EditorInputBehaviorInPlayMode.AllDeviceInputAlwaysGoesToGameView;
            testInputs.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=testInputs; keyboard=InputSystem.AddDevice<Keyboard>();
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());
            fixture=new GameObject("Canopy world regression fixture");
            world=fixture.AddComponent<GospelPilgrimageWorld>(); world.Build();
            yield return null;
        }
        [UnityTearDown]
        public IEnumerator TearDown()
        {
            if(keyboard!=null&&keyboard.added) InputSystem.RemoveDevice(keyboard);
            if(previousInputs!=null) InputSystem.settings=previousInputs;
            if(fixture!=null) Object.Destroy(fixture);
            yield return null;
            if(Application.isPlaying) yield return new ExitPlayMode();
            if(testInputs!=null&&InputSystem.settings!=testInputs) Object.DestroyImmediate(testInputs);
        }
        [UnityTest]
        public IEnumerator PaintedChaptersKeepEveryEncounterOnTheContinuousPhysicalFloor()
        {
            Assert.That(world.PaintedSceneryCount,Is.GreaterThan(100));
            Assert.That(world.SceneryBatchCount,Is.InRange(6,16),"Painted trees, rocks and planting must stay combined instead of one draw per prop.");
            foreach(string chapter in new[]{"temple","baptism"})
            {
                world.SetChapter(chapter); Physics.SyncTransforms(); yield return null;
                var root=chapter=="temple"?world.TempleRoot:world.JordanRoot;
                var terrain=root.GetComponentsInChildren<MeshRenderer>().Single(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="FirstGame/Pilgrimage/PaintedGround");
                Assert.That(terrain.sharedMaterial.shader.isSupported,Is.True);
                Assert.That(terrain.GetComponent<MeshCollider>(),Is.Not.Null,"The textured plane must remain the actual walkable 3D floor.");
                foreach(var quest in GospelChronicleCatalog.Quests.Where(q=>!q.IsPrologue&&(q.ChapterId=="baptism")== (chapter=="baptism")))
                {
                    Vector3 p=world.PositionForQuest(quest);
                    Assert.That(world.IsInside(p),Is.True,quest.Id);
                    var hits=Physics.RaycastAll(p+Vector3.up*2,Vector3.down,4);
                    Assert.That(hits.Any(h=>h.collider==terrain.GetComponent<MeshCollider>()&&Mathf.Abs(h.point.y-p.y)<.20f),Is.True,
                        "The preserved encounter must have the continuous terrain beneath it: "+quest.Id);
                }
                foreach(var scenery in root.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial!=null&&r.sharedMaterial.shader.name=="FirstGame/Pilgrimage/PaintedScenery"))
                {
                    Assert.That(scenery.sharedMaterial.shader.isSupported,Is.True);
                    Assert.That(scenery.sharedMaterial.GetTexture("_BaseMap"),Is.Not.Null);
                }
            }
            Debug.Log("GOSPEL_CANOPY_WORLD physical-encounters=true free-XZ-bounds=true painted-props="+world.PaintedSceneryCount+" scene-batches="+world.SceneryBatchCount);
        }
        [UnityTest]
        public IEnumerator RealTouchAxesPlantAtRestAndTheCameraKeepsTheLandscapeSteadyThroughAJump()
        {
            var player=new GameObject("Canopy real movement capsule"); player.transform.SetParent(fixture.transform,false);
            var cameraObject=new GameObject("Canopy real follow camera"); cameraObject.transform.SetParent(fixture.transform,false);
            var camera=cameraObject.AddComponent<Camera>();
            var motor=player.AddComponent<GospelPilgrimageMotor>(); motor.Initialize(camera); motor.SetBounds(world.Bounds);
            var collisionProbe=player.AddComponent<GospelCanopyCollisionProbe>();
            motor.SetPose(world.SpawnForChapter("temple"));
            var rig=cameraObject.AddComponent<GospelPilgrimageCamera>(); rig.Initialize(camera,player.transform); rig.InputEnabled=false;
            float deadline=Time.realtimeSinceStartup+3;
            while(!motor.IsGrounded&&Time.realtimeSinceStartup<deadline) yield return null;
            Assert.That(motor.IsGrounded,Is.True);
            Vector3 start=motor.Position;
            string initialContacts=string.Join(" | ",Physics.OverlapCapsule(start+Vector3.up*.29f,start+Vector3.up*1.30f,.28f)
                .Where(c=>c!=motor.Controller).Select(c=>c.name+"@"+c.bounds.ToString()));
            float inputAt=Time.realtimeSinceStartup, simulationAt=Time.time;
            motor.SetTouchInput(new Vector2(1,1));
            // This fixture enters play mode from the EditMode runner. Use the
            // same real-time yield as the shipping-input/story smoke fixtures
            // so the runner keeps pumping ordinary MonoBehaviour frames.
            yield return new WaitForSecondsRealtime(.34f);
            Vector3 moving=motor.Position;
            string movement=" start="+start+" end="+moving+" velocity="+motor.Velocity+" speed="+motor.MeasuredSpeed
                +" facing="+motor.FacingYaw+" grounded="+motor.IsGrounded+" inputEnabled="+motor.Enabled+" componentEnabled="+motor.enabled
                +" realElapsed="+(Time.realtimeSinceStartup-inputAt)+" simulationElapsed="+(Time.time-simulationAt)
                +" dt="+Time.deltaTime+" scale="+Time.timeScale+" initialContacts="+initialContacts+" collisionContacts="+collisionProbe.Contacts;
            Debug.Log("GOSPEL_CANOPY_TOUCH_DIAGNOSTIC"+movement);
            Assert.That(moving.x-start.x,Is.GreaterThan(.35f),"The +X touch axis must move the real capsule."+movement);
            Assert.That(moving.z-start.z,Is.GreaterThan(.35f),"The +Z touch axis must move the real capsule."+movement);
            Assert.That(motor.MeasuredSpeed,Is.InRange(2.6f,3.55f),"Diagonal input must retain the walk speed rather than gain sqrt(2) speed.");
            motor.SetTouchInput(Vector2.zero); yield return new WaitForSecondsRealtime(.22f);
            Assert.That(motor.MeasuredSpeed,Is.LessThan(.08f),"Releasing the real input must plant the capsule without a skating tail.");
            Assert.That(Vector2.Distance(new Vector2(moving.x,moving.z),new Vector2(motor.Position.x,motor.Position.z)),Is.LessThan(.4f));
            rig.Snap(); float groundY=motor.Position.y, cameraY=camera.transform.position.y, characterRise=0,cameraRise=0;
            motor.RequestJump(); deadline=Time.realtimeSinceStartup+1.25f;
            while(Time.realtimeSinceStartup<deadline)
            {
                yield return null;
                characterRise=Mathf.Max(characterRise,motor.Position.y-groundY);
                cameraRise=Mathf.Max(cameraRise,camera.transform.position.y-cameraY);
            }
            Assert.That(characterRise,Is.GreaterThan(.65f),"This must exercise a real controller jump.");
            Assert.That(cameraRise,Is.LessThan(characterRise*.55f+.12f),"Following the full jump vertically makes the whole landscape bob.");
            Assert.That(world.IsInside(motor.Position),Is.True);
            Debug.Log("GOSPEL_CANOPY_MOTION real-touch-XZ=true stop-speed="+motor.MeasuredSpeed+" jump-rise="+characterRise+" camera-rise="+cameraRise);
        }
    }

    public sealed class GospelCanopyCollisionProbe : MonoBehaviour
    {
        public string Contacts { get; private set; } = "";
        void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if(hit.collider.name=="Continuous painted walkable terrain") return;
            string contact=hit.collider.name+" normal="+hit.normal+" bounds="+hit.collider.bounds;
            if(!Contacts.Contains(contact)) Contacts+=contact+" | ";
        }
    }
}
