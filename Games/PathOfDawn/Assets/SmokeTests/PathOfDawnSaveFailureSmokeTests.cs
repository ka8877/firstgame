using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FirstGame.Shared;
using Ink.UnityIntegration;
using NUnit.Framework;
using PathOfDawn.Core;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;

namespace PathOfDawn.SmokeTests
{
    public sealed class PathOfDawnSaveFailureSmokeTests
    {
        const string Slot = "pod-save-failure-test";
        const string Fault = "Simulated storage write failure";
        sealed class FailingBackend : ISaveBackend
        {
            readonly Dictionary<string, string> slots = new Dictionary<string, string>();
            public bool FailWrites;
            public bool Exists(string slot) => slots.ContainsKey(slot);
            public string Read(string slot) => slots[slot];
            public void WriteAtomic(string slot, string contents)
            {
                if (FailWrites) throw new IOException(Fault);
                slots[slot] = contents;
            }
        }

        IDisposable storageScope;
        FailingBackend backend;
        bool hadSlot, hadScale, hadVolume, captured;
        string previousSlot, previousClipboard;
        float previousScale, previousVolume, previousListenerVolume;

        [UnitySetUp]
        public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            hadSlot = PlayerPrefs.HasKey("POD.activeSlot"); previousSlot = PlayerPrefs.GetString("POD.activeSlot", "");
            hadScale = PlayerPrefs.HasKey("POD.textScale"); previousScale = PlayerPrefs.GetFloat("POD.textScale", 1);
            hadVolume = PlayerPrefs.HasKey("POD.volume"); previousVolume = PlayerPrefs.GetFloat("POD.volume", .65f);
            previousListenerVolume = AudioListener.volume; previousClipboard = GUIUtility.systemCopyBuffer; captured = true;
            PlayerPrefs.SetString("POD.activeSlot", Slot); PlayerPrefs.SetFloat("POD.textScale", 1); PlayerPrefs.SetFloat("POD.volume", .65f);
            backend = new FailingBackend(); storageScope = PersistentSaves.UseBackendForTesting(backend);
            var ink = Resources.Load<InkFile>("Story/galilee");
            Assert.That(ink != null && ink.isCompiled, Is.True);
            var savedA = new NarrativeSession(ink.storyJson);
            savedA.BeginShoreChoice(); savedA.Choose(0); savedA.BeginRoomChoice();
            Assert.That(savedA.World.pointId, Is.EqualTo("room"));
            Assert.That(PersistentSaves.TrySave(Slot, "path-of-dawn", 1, "portable-1",
                JsonUtility.ToJson(savedA.CaptureSave()), out var error), Is.True, error);
            yield return SmokeUi.LoadBoot();
        }

        [UnityTearDown]
        public IEnumerator LeaveGame()
        {
            yield return SmokeUi.UnloadBoot();
            storageScope?.Dispose(); storageScope = null;
            if (captured)
            {
                if (hadSlot) PlayerPrefs.SetString("POD.activeSlot", previousSlot); else PlayerPrefs.DeleteKey("POD.activeSlot");
                if (hadScale) PlayerPrefs.SetFloat("POD.textScale", previousScale); else PlayerPrefs.DeleteKey("POD.textScale");
                if (hadVolume) PlayerPrefs.SetFloat("POD.volume", previousVolume); else PlayerPrefs.DeleteKey("POD.volume");
                PlayerPrefs.Save(); AudioListener.volume = previousListenerVolume;
                GUIUtility.systemCopyBuffer = previousClipboard; captured = false;
            }
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest]
        public IEnumerator WriteFailureKeepsCurrentOutcomeAndDoesNotExportOlderSave()
        {
            var originalBytes = backend.Read(Slot);
            SmokeUi.Click("이어하기"); yield return null;
            yield return DrainDialogue();
            backend.FailWrites = true;
            SmokeUi.Click("석조 방을 쉼터로 준비하고 돌아갈 이들을 기다린다."); yield return null;
            yield return DrainDialogue();
            yield return SmokeUi.WaitForVisibleKorean("문턱의 등불");
            Assert.That(backend.Read(Slot), Is.EqualTo(originalBytes), "Failed autosave must preserve saved room checkpoint A.");

            SmokeUi.Click("저장하고 타이틀로"); yield return null;
            Assert.That(SmokeUi.HasText("문턱의 등불"), Is.True, "Failed result save must keep current outcome B open.");
            Assert.That(SmokeUi.HasText("저장하지 못했습니다: "), Is.True);
            Assert.That(SmokeUi.HasText(Fault), Is.True);
            Assert.That(SmokeUi.FindButton("새 게임"), Is.Null);
            Assert.That(backend.Read(Slot), Is.EqualTo(originalBytes));

            SmokeUi.Click("설정 [Esc]"); yield return null;
            var field = UnityEngine.Object.FindFirstObjectByType<TMP_InputField>();
            Assert.That(field, Is.Not.Null);
            GUIUtility.systemCopyBuffer = "clipboard-must-not-be-replaced";
            SmokeUi.Click("저장 내보내기"); yield return null;
            Assert.That(field.text, Does.StartWith("저장하지 못했습니다: "));
            Assert.That(field.text, Does.Contain(Fault));
            Assert.That(GUIUtility.systemCopyBuffer, Is.EqualTo("clipboard-must-not-be-replaced"));
            Assert.That(SmokeUi.HasText("저장 JSON을 표시했습니다."), Is.False);
            Assert.That(backend.Read(Slot), Is.EqualTo(originalBytes), "Export failure must not change saved checkpoint A.");

            SmokeUi.Click("저장하고 타이틀로"); yield return null;
            Assert.That(SmokeUi.HasText("설정과 여정 보관"), Is.True);
            Assert.That(SmokeUi.FindButton("새 게임"), Is.Null, "Do not leave current B after a failed save.");
            Assert.That(field.text, Does.Contain(Fault));
            Assert.That(backend.Read(Slot), Is.EqualTo(originalBytes));

            // Recover storage and retry through the same UI. The exported payload must now be B.
            backend.FailWrites = false;
            SmokeUi.Click("저장 내보내기"); yield return null;
            Assert.That(field.text, Is.EqualTo(backend.Read(Slot)));
            Assert.That(backend.Read(Slot), Is.Not.EqualTo(originalBytes));
            var envelope = JsonUtility.FromJson<PortableSave>(field.text);
            var currentB = JsonUtility.FromJson<NarrativeSave>(envelope.payload);
            Assert.That(currentB.world.pointId, Is.EqualTo("ending"));
            Assert.That(currentB.world.shelterPrepared, Is.True);
            Assert.That(currentB.meta.routes, Does.Contain("shelter"));
            SmokeUi.Click("저장하고 타이틀로"); yield return null;
            yield return SmokeUi.WaitForVisibleKorean("새벽의 길");
            LogAssert.NoUnexpectedReceived();
        }

        static IEnumerator DrainDialogue()
        {
            for (var i = 0; i < 60; i++)
            {
                var label = new[] { "다음 대사 [E]", "선택하기 [E]", "계속 [E]" }
                    .FirstOrDefault(candidate => SmokeUi.FindButton(candidate) != null);
                if (label == null) yield break;
                SmokeUi.Click(label); yield return null;
            }
            Assert.Fail("Dialogue did not settle within 60 visible lines.");
        }
    }
}
