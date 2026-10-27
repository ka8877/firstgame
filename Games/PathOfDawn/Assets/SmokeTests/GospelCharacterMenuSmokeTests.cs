using System.Collections;
using System.Linq;
using NUnit.Framework;
using PathOfDawn.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    public sealed partial class GospelJourneySmokeTests
    {
        [UnityTest]
        public IEnumerator ToonCharacterInventoryLampAndLiveMapPersistWithoutAdvancingScripture()
        {
            yield return BeginTemple();
            int objective=App.Journey.ObjectiveIndex;
            var collected=App.Journey.CollectedReadings.ToArray();
            yield return Press(Key.I);
            Assert.That(App.CharacterMenuTab,Is.EqualTo("character"));
            Assert.That(App.Player.InputEnabled,Is.False);
            SmokeUi.Click("소지품");yield return null;
            Assert.That(App.CharacterMenuTab,Is.EqualTo("inventory"));
            var items=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(b=>b.gameObject.activeInHierarchy&&b.name.StartsWith("Inventory item · ")).ToArray();
            Assert.That(items.Length,Is.EqualTo(GospelBelongings.All.Count));
            Canvas.ForceUpdateCanvases();yield return null;
            foreach(var icon in UnityEngine.Object.FindObjectsByType<PathOfDawn.Presentation.GospelBelongingIcon>(FindObjectsSortMode.None).Where(i=>i.gameObject.activeInHierarchy))
            {
                Assert.That(icon.canvasRenderer,Is.Not.Null);
                Assert.That(icon.canvasRenderer.cull,Is.False,"Each actual inventory icon must remain visible.");
                Assert.That(icon.canvasRenderer.GetMesh(),Is.Not.Null);
                Assert.That(icon.canvasRenderer.GetMesh().vertexCount,Is.GreaterThan(0),"An icon component without rendered geometry must not count as a visible item icon.");
            }
            items.Single(b=>b.name=="Inventory item · travel-lamp").onClick.Invoke();yield return null;
            Assert.That(App.SelectedBelongingId,Is.EqualTo(GospelBelongings.LampId));
            SmokeUi.Click("등잔 켜고 장착");yield return null;
            Assert.That(App.Journey.LampEquipped,Is.True);
            Assert.That(App.UtilityLightVisible,Is.True);
            Assert.That(GameObject.Find("Oil lamp warm light").GetComponent<Light>().enabled,Is.True);
            Assert.That(GameObject.Find("Equipped travel lamp").GetComponentsInChildren<Renderer>().Length,Is.GreaterThan(0),
                "Equipping must show a real modeled lamp, not only a changed UI flag.");
            Assert.That(ReadSave().equippedUtility,Is.EqualTo(GospelBelongings.LampId));
            yield return SmokeUi.Capture("PathOfDawn","toon-inventory-lamp-equipped",1280,720);
            SmokeUi.Click("안내도");yield return null;
            Assert.That(App.CharacterMenuTab,Is.EqualTo("map"));
            var marker=GameObject.Find("Player position").GetComponent<RectTransform>().anchoredPosition;
            Assert.That(GameObject.Find("Current objective"),Is.Not.Null);
            foreach(var npc in App.World.GetComponentsInChildren<PathOfDawn.Presentation.GospelNpcAnchor>())
                Assert.That(GameObject.Find("NPC · "+npc.NpcId),Is.Not.Null);
            yield return SmokeUi.Capture("PathOfDawn","toon-courtyard-live-map",1280,720);
            SmokeUi.Click("여정으로");yield return null;
            Assert.That(App.Player.InputEnabled,Is.True);
            var before=App.Player.transform.position;
            yield return WalkTo(before+Vector3.right*3.3f);
            yield return Press(Key.M);
            var moved=GameObject.Find("Player position").GetComponent<RectTransform>().anchoredPosition;
            Assert.That(Vector2.Distance(marker,moved),Is.GreaterThan(3),"Map must derive current marker from actual moved position.");
            SmokeUi.Click("여정");yield return null;
            Assert.That(App.CharacterMenuTab,Is.EqualTo("journey"));
            Assert.That(App.Journey.ObjectiveIndex,Is.EqualTo(objective));
            Assert.That(App.Journey.CollectedReadings,Is.EqualTo(collected));
            SmokeUi.Click("저장");yield return null;
            yield return SmokeUi.LoadBoot();
            SmokeUi.Click("이어하기");yield return null;
            Assert.That(App.Journey.LampEquipped,Is.True);
            yield return SmokeUi.WaitUntil(()=>App.UtilityLightVisible,5,"A restored equipped lamp must reappear in the real world.");
            Assert.That(App.Journey.ObjectiveIndex,Is.EqualTo(objective));
            Assert.That(App.Journey.CollectedReadings,Is.EqualTo(collected));
            yield return SmokeUi.Capture("PathOfDawn","toon-restored-equipped-lamp",1280,720);
            yield return Press(Key.B);
            SmokeUi.Click("등잔 끄고 보관");yield return null;
            Assert.That(App.Journey.LampEquipped,Is.False);
            Assert.That(App.UtilityLightVisible,Is.False);
            Assert.That(ReadSave().equippedUtility,Is.Empty);
            SmokeUi.Click("여정으로");yield return null;
            Assert.That(App.Player.InputEnabled,Is.True);
            TestContext.WriteLine("GOSPEL_TOON_INVENTORY_OK realLamp=true lampSavedRestored=true liveMap=true scriptureUnchanged=true");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
