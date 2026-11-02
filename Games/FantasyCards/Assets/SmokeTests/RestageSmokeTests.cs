using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using TMPro;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    public sealed partial class RestageSmokeTests
    {
        const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
        IDisposable storage, preferences;
        MemorySaveBackend backend;
        string attempt;
        FantasyCardsApp App => UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
        void Call(string method, params object[] args) => typeof(FantasyCardsApp).GetMethod(method, Private).Invoke(App,args);
        void Set(string field, object value) => typeof(FantasyCardsApp).GetField(field,Private).SetValue(App,value);
        IEnumerator Shot(string name,int width=1280,int height=720,Action inspect=null) =>
            SmokeUi.Capture("FantasyCards","restage-"+attempt+"-"+name,width,height,inspect:inspect);
        IEnumerator WaitIdle(string reason="The current actual battle presentation did not finish.") =>
            SmokeUi.WaitUntil(()=>!App.IsBattlePlaybackActive,12,reason);
        static Button NamedButton(string name) => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Single(button=>button.gameObject.activeInHierarchy&&button.name==name);
        void Install(BattleEngine engine)
        {
            App.StopAllCoroutines();Set("engine",engine);Set("locked",false);Set("battleVisualState",null);
            Set("battlePlaybackActive",false);Set("questBattleMode",false);Set("selectedCard",-1);Set("selectedAttacker",-1);
            PlayerPrefs.SetInt("FC.quest.lastBattleMode",0);Call("ShowBattle");Canvas.ForceUpdateCanvases();
        }
        [UnitySetUp] public IEnumerator SetUp()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);yield return new EnterPlayMode();
            attempt=Environment.GetEnvironmentVariable("FIRSTGAME_NATIVE_ATTEMPT")??DateTime.UtcNow.ToString("yyyyMMddHHmmss");
            backend=new MemorySaveBackend();backend.WriteAtomic("preserved-player","original player bytes");
            storage=PersistentSaves.UseBackendForTesting(backend);preferences=SmokeUi.PreserveDemoPreferences();
            yield return SmokeUi.LoadBoot();
        }
        [UnityTearDown] public IEnumerator TearDown()
        {
            yield return SmokeUi.UnloadBoot();preferences?.Dispose();storage?.Dispose();
            if(Application.isPlaying)yield return new ExitPlayMode();
        }
        static void AssertFactionHeaders()
        {
            foreach(int owner in new[]{0,1})
            {
                var labels=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Where(text=>text.gameObject.activeInHierarchy&&text.name=="Faction identity "+owner).ToArray();
                Assert.That(labels.Length,Is.GreaterThanOrEqualTo(1));
                foreach(var label in labels)
                {
                    Assert.That(label.text,Is.EqualTo(owner==0?"내 전열":"상대 AI 전열"));label.ForceMeshUpdate();
                    Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(character=>character.isVisible),Is.True,
                        "The actual own/opponent field identity must remain visible: "+owner);
                }
            }
        }
    }
}
