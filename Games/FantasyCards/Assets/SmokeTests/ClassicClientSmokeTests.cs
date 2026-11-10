using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    public sealed partial class NativitySmokeTests
    {
        [UnityTest]
        public IEnumerator LateClientRoomsAndProfileSurviveReloadWithoutChangingEconomyOrRng()
        {
            yield return ReceiveStarter();
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("lobby"));
            var rooms = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Classic live room ", StringComparison.Ordinal)).ToArray();
            Assert.That(rooms.Length, Is.EqualTo(FmScenarioCatalog.Stages.Length));
            Assert.That(rooms.Count(button => button.interactable), Is.EqualTo(1), "Only the actual unlocked first scenario may be entered by a fresh owned profile.");
            var source = Resources.Load<Texture2D>("Art/FmClassic/late-client-reference");
            Assert.That(source, Is.Not.Null, "The actual retained late-client reference must be imported.");
            Assert.That(new Vector2Int(source.width, source.height), Is.EqualTo(new Vector2Int(740, 555)));
            var hero = GameObject.Find("Bounded original hero art").GetComponent<RawImage>();
            Assert.That(hero.texture, Is.EqualTo(source));
            Assert.That(hero.uvRect, Is.EqualTo(new Rect(8f / 740, 133f / 555, 222f / 740, 360f / 555)), "Only the unobstructed portrait region may be sampled; original names, labels, chat and chrome must stay outside.");
            Assert.That(GameObject.Find("Restage fantasy title artwork"), Is.Null);
            yield return Shot("classic-live-client-lobby");
            yield return Shot("classic-live-client-lobby-touch", 616, 346);

            string economyBefore = JsonUtility.ToJson(App.QuestProfileSnapshot);
            SmokeUi.Click("내정보"); yield return null;
            Assert.That(GameObject.Find("Late FM1 My Information window"), Is.Not.Null);
            var nickname = GameObject.Find("Profile nickname input").GetComponent<TMP_InputField>();
            var greeting = GameObject.Find("Profile battle greeting input").GetComponent<TMP_InputField>();
            nickname.text = "기록 지킴이"; greeting.text = "평안이 함께";
            Assert.That(greeting.characterLimit, Is.EqualTo(15));
            SmokeUi.Click("저장"); yield return null;
            Assert.That(App.LocalProfileName, Is.EqualTo("기록 지킴이"));
            Assert.That(App.LocalProfileGreeting, Is.EqualTo("평안이 함께"));
            Assert.That(GameObject.Find("Late FM1 My Information window"), Is.Not.Null);
            Assert.That(GameObject.Find("Profile avatar " + App.LocalProfileAvatarId), Is.Null, "The initial My Information window must show its compact portrait; the chooser is a separate action.");
            yield return Shot("classic-my-information");
            yield return Shot("classic-my-information-touch", 616, 346);
            SmokeUi.Click("돌아가기"); yield return null;
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("lobby"));
            SmokeUi.Click("카드 상점"); yield return null;
            Assert.That(GameObject.Find("Classic live card shop counter"), Is.Not.Null);
            Assert.That(NamedButton("Quest shop offer standard").interactable, Is.True);
            yield return Shot("classic-shop-live-products");
            yield return Shot("classic-shop-live-products-touch", 616, 346);
            Call("ShowQuestOffer", "standard"); yield return null;
            Assert.That(App.QuestSp, Is.EqualTo(600));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(economyBefore), "Profile edits and browsing live shop windows must not alter ownership, SP, tickets or pack RNG.");

            yield return SmokeUi.LoadBoot();
            Assert.That(App.LocalProfileName, Is.EqualTo("기록 지킴이"));
            Assert.That(App.LocalProfileGreeting, Is.EqualTo("평안이 함께"));
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot), Is.EqualTo(economyBefore));
            SmokeUi.Click("게임 시작"); yield return null;
            NamedButton("Classic live room 0").onClick.Invoke(); yield return null;
            SmokeUi.Click("대전 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null && SmokeUi.FindButton("이대로 시작").interactable, 12, "Actual owned battle must reach its opening-hand choice.");
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("준비 완료") != null && SmokeUi.FindButton("준비 완료").interactable, 12, "The real opening-hand animation must release battle input.");
            var engine = SmokeUi.ReadEngine();
            foreach (Element element in Enum.GetValues(typeof(Element)))
            {
                var value = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                    .Single(text => text.gameObject.activeInHierarchy && text.name == "Soul value " + element);
                Assert.That(value.text, Is.EqualTo(engine.State.players[0].Resource(element).ToString()));
            }
            Assert.That(GameObject.Find("Classic actual player information 0"), Is.Not.Null);
            Assert.That(GameObject.Find("Classic actual player information 1"), Is.Not.Null);
            string battleBefore = JsonUtility.ToJson(engine.State);
            Call("ShowBattle"); yield return null;
            Assert.That(JsonUtility.ToJson(engine.State), Is.EqualTo(battleBefore), "The radial counters, portraits and client windows must display existing state without rolling new coins or changing the engine.");
            yield return Shot("classic-blue-battle-client");
            yield return Shot("classic-blue-battle-client-touch", 616, 346);
            Assert.That(backend.Read("preserved-player"), Is.EqualTo("original player bytes"));
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }
    }
}
