using System;
using System.Collections;
using System.Linq;
using FirstGame.Shared;
using NUnit.Framework;
using PathOfDawn.Core;
using PathOfDawn.Presentation;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace PathOfDawn.SmokeTests
{
    public sealed partial class RenewalSmokeTests
    {
        [UnityTest, Timeout(600000)] public IEnumerator RadiancePaintedWorldScenicMotionAndFirstReadingSavePersist()
        {
            yield return BeginJourney();
            Assert.That(App.World.UsesRadianceVista,Is.True,"The actual resource-backed painted vista must be active.");
            Assert.That(App.Player.Actor.UsesRadiancePainting,Is.True,"The new articulated boy painting must be active.");
            yield return Shot("radiance-normal-spawn");
            yield return Key(UnityEngine.InputSystem.Key.H);Assert.That(App.Ui.ScenicView,Is.True);
            yield return Shot("radiance-scenic-spawn");
            SmokeUi.Click("게임으로 · H");yield return null;Assert.That(App.Ui.ScenicView,Is.False);
            yield return Shot("radiance-restored-hud");
            yield return MoveTo(-20);float x=App.Player.Position.x;
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D));yield return new WaitForSecondsRealtime(.25f);
            for(int i=0;i<3;i++){yield return SmokeUi.Capture("PathOfDawn","polish-"+attempt+"-walk-right-"+i,1280,720,settleSeconds:0f);yield return new WaitForSecondsRealtime(.13f);}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return null;Assert.That(App.Player.Position.x-x,Is.GreaterThan(.5f));
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.A));yield return new WaitForSecondsRealtime(.25f);
            for(int i=0;i<3;i++){yield return SmokeUi.Capture("PathOfDawn","polish-"+attempt+"-walk-left-"+i,1280,720,settleSeconds:0f);yield return new WaitForSecondsRealtime(.13f);}
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.D,UnityEngine.InputSystem.Key.LeftShift));yield return new WaitForSecondsRealtime(.35f);yield return Shot("run-right");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState(UnityEngine.InputSystem.Key.A,UnityEngine.InputSystem.Key.LeftShift));yield return new WaitForSecondsRealtime(.35f);yield return Shot("run-left");
            InputSystem.QueueStateEvent(keyboard,new KeyboardState());yield return new WaitForSecondsRealtime(.2f);
            float ground=App.Player.Position.y;yield return Key(UnityEngine.InputSystem.Key.Space);yield return new WaitForSecondsRealtime(.10f);
            Assert.That(App.Player.Position.y,Is.GreaterThan(ground+.12f));yield return SmokeUi.Capture("PathOfDawn","polish-"+attempt+"-jump",1280,720,settleSeconds:0f);
            yield return new WaitForSecondsRealtime(1.2f);Assert.That(App.Player.Position.y,Is.EqualTo(ground).Within(.18f));yield return Shot("landed");
            for(int i=0;i<4;i++){InputSystem.QueueDeltaStateEvent(mouse.scroll,new Vector2(0,120));yield return null;}
            yield return new WaitForSecondsRealtime(.6f);Assert.That(App.Zoom,Is.EqualTo(3f).Within(.05f));yield return Shot("close-character");
            for(int i=0;i<8;i++){InputSystem.QueueDeltaStateEvent(mouse.scroll,new Vector2(0,-120));yield return null;}
            yield return new WaitForSecondsRealtime(.7f);yield return Shot("radiance-wide-forest");
            yield return Key(UnityEngine.InputSystem.Key.B);yield return null;
            // The fourth real inventory tile selects the lamp; its image and description precede equip.
            var tiles=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).Where(b=>b.gameObject.activeInHierarchy&&b.name=="Button ").ToArray();
            var lamp=SmokeUi.FindButton("등잔 장착");
            if(lamp==null||!lamp.gameObject.activeInHierarchy)
            {
                var tile=UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None).FirstOrDefault(b=>b.gameObject.activeInHierarchy&&b.GetComponent<RectTransform>().anchoredPosition.x==36&&Mathf.Abs(b.GetComponent<RectTransform>().anchoredPosition.y+323)<1);
                Assert.That(tile,Is.Not.Null,"The fourth visible item tile must select the clay lamp.");tile.onClick.Invoke();yield return null;
            }
            SmokeUi.Click("등잔 장착");yield return null;Assert.That(App.Journey.LampEquipped,Is.True);yield return Shot("inventory-lamp");SmokeUi.Click("여정으로");yield return null;
            yield return Key(UnityEngine.InputSystem.Key.M);yield return Shot("codex-locked");Assert.That(App.Journey.CodexCount,Is.Zero);SmokeUi.Click("여정으로");yield return null;
            yield return MoveTo(App.ObjectiveX);Assert.That(App.CanInteract,Is.True);
            yield return Key(UnityEngine.InputSystem.Key.E);Assert.That(App.Ui.CurrentView,Is.EqualTo("reading"));
            yield return Shot("radiance-parent-dialogue");
            int turns=0;while(App.Ui.CurrentView=="reading"&&turns++<12){SmokeUi.Click("다음 말씀");yield return null;}
            Assert.That(App.Journey.CodexCount,Is.EqualTo(1));
            yield return Key(UnityEngine.InputSystem.Key.Escape);SmokeUi.Click("현재 여정 저장");yield return null;
            float savedX=App.Player.Position.x;string slot=App.ActiveSaveSlot;Assert.That(backend.Exists(slot),Is.True);
            yield return SmokeUi.LoadBoot();SmokeUi.Click("이어하기");yield return null;
            Assert.That(App.Journey.CodexCount,Is.EqualTo(1));Assert.That(App.Journey.LampEquipped,Is.True);
            Assert.That(App.Player.Position.x,Is.EqualTo(savedX).Within(.08f));
            Assert.That(App.World.UsesRadianceVista&&App.Player.Actor.UsesRadiancePainting,Is.True);
            yield return Shot("radiance-continued-first-reading");
            yield return Key(UnityEngine.InputSystem.Key.M);yield return Shot("radiance-codex-one");
            SmokeUi.Click("여정으로");yield return null;
            yield return MoveTo(22.5f);yield return Shot("radiance-temple-approach");
            LogAssert.NoUnexpectedReceived();
        }
    }
}
