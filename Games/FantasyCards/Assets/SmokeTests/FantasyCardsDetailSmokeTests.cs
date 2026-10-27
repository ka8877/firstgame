using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    public sealed partial class FantasyCardsRevivalSmokeTests
    {
        [UnityTest]
        public IEnumerator DetailProfilePersistsAndCompletedBattleRecordsOnlyOnce()
        {
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            int wins = app.LocalProfileWins, losses = app.LocalProfileLosses, draws = app.LocalProfileDraws;
            SmokeUi.Click("프로필"); yield return null;
            var input = UnityEngine.Object.FindObjectsByType<TMP_InputField>(FindObjectsSortMode.None)
                .Single(field => field.gameObject.activeInHierarchy && field.gameObject.name == "Profile nickname input");
            input.text = "리마스터 검증";
            SmokeUi.Click("이름 저장"); yield return null;
            Assert.That(app.LocalProfileName, Is.EqualTo("리마스터 검증"));
            var choice = ActiveButtons("Profile avatar ").First(button => button.gameObject.name != "Profile avatar " + app.LocalProfileAvatarId);
            string avatar = choice.gameObject.name.Substring("Profile avatar ".Length);
            choice.onClick.Invoke(); yield return null;
            Assert.That(app.LocalProfileAvatarId, Is.EqualTo(avatar));
            Assert.That(app.LocalProfileWins, Is.EqualTo(wins), "Changing a portrait cannot award a battle result.");
            yield return SmokeUi.Capture("FantasyCards", "arcana-profile-local", 1280, 720);
            yield return SmokeUi.LoadBoot();
            app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            Assert.That(app.LocalProfileName, Is.EqualTo("리마스터 검증"));
            Assert.That(app.LocalProfileAvatarId, Is.EqualTo(avatar));
            Assert.That(app.LocalProfileLosses, Is.EqualTo(losses));
            Assert.That(app.LocalProfileDraws, Is.EqualTo(draws));
            SmokeUi.Click("프로필"); yield return null;
            Assert.That(SmokeUi.HasText("리마스터 검증"), Is.True);

            // Native-only conserved completed fixture exercises the actual result
            // screen callback twice. Browser checks use ordinary play, not this state.
            var engine = new BattleEngine(8137, new RuleSet(), DemoCatalog.GetDeck("forest").CopyCards());
            var enemy = engine.State.players[1]; enemy.discard.AddRange(enemy.deck); enemy.deck.Clear();
            engine.State.openingHandOpen = false; engine.State.phase = BattlePhase.Complete; engine.State.winner = 0;
            BattleEngine.Validate(engine.State);
            string key = "FC.profile.recorded." + Hash128.Compute(JsonUtility.ToJson(engine.State));
            bool had = PlayerPrefs.HasKey(key); int old = PlayerPrefs.GetInt(key);
            try
            {
                PlayerPrefs.DeleteKey(key);
                uint rng = engine.State.randomState;
                InstallFidelityFixture(app, engine); yield return null;
                Assert.That(app.LocalProfileWins, Is.EqualTo(wins + 1));
                InstallFidelityFixture(app, engine); yield return null;
                Assert.That(app.LocalProfileWins, Is.EqualTo(wins + 1), "Repeated result draw/continue must not award a second win.");
                Assert.That(engine.State.randomState, Is.EqualTo(rng));
                Assert.That(app.LocalProfileLosses, Is.EqualTo(losses));
                Assert.That(app.LocalProfileDraws, Is.EqualTo(draws));
            }
            finally
            {
                if (had) PlayerPrefs.SetInt(key, old); else PlayerPrefs.DeleteKey(key);
                PlayerPrefs.Save();
            }
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }
    }
}
