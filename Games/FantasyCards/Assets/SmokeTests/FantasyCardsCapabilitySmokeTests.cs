using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using NUnit.Framework;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace FantasyCards.SmokeTests
{
    // Uses the existing boot, memory saves, preference restoration and collection helpers.
    // This is a Native UI callback regression, not a browser pointer or public release check.
    public sealed partial class FantasyCardsRevivalSmokeTests
    {
        const string CapabilitySwarm = "fm_2042920028", CapabilityRabi = "fm_2062120091";
        const string CapabilityDeathHand = "fm_2042920002", CapabilityLongSword = "fm_2042940039";
        const string CapabilityAll = "fm_5112322069"; // 킹핀사이클롭, source UnitType=ALL.

        [UnityTest]
        public IEnumerator OriginalCapabilityLabelsAndBrowseFiltersPreserveKnownCardsAndSaves()
        {
            // Independent original rows: UnitType 11=BEAST,21=GEAR,31=MAGIC,1=ALL.
            // Do not derive these expectations from the presentation mapping under test.
            Assert.That(CardCatalog.Get(CapabilitySwarm).capabilityMask, Is.EqualTo(1));
            Assert.That(CardCatalog.Get(CapabilityRabi).capabilityMask, Is.EqualTo(2));
            Assert.That(CardCatalog.Get(CapabilityDeathHand).capabilityMask, Is.EqualTo(4));
            Assert.That(CardCatalog.Get(CapabilityAll).capabilityMask, Is.EqualTo(7));
            Assert.That(CardCatalog.Get(CapabilityLongSword).requiredCapabilityMask, Is.EqualTo(6));
            string[] definitionIdentity = CapabilityDefinitionIdentity();
            var editorDeck = ReadEditorDeck().ToArray();
            string[] slots = { "preserved-previous-player-slot", SmokeUi.BattleSlot, SmokeUi.DeckSlot,
                FmQuestEconomy.SaveSlot, "fantasy-quest-battle-v1", "fantasy-quest-deck-v1" };
            var saved = slots.ToDictionary(slot => slot, slot => backend.Exists(slot) ? backend.Read(slot) : null);

            SmokeUi.Click("자유 연습"); yield return null;
            SmokeUi.Click("출전 카드"); yield return null;
            yield return CapabilityCheckExplanation(CapabilitySwarm, "유닛 능력 · 야수 / 랭크 N");
            yield return CapabilityCheckExplanation(CapabilityRabi, "유닛 능력 · 장비 / 랭크 N");
            yield return CapabilityCheckExplanation(CapabilityDeathHand, "유닛 능력 · 마법 / 랭크 N");
            yield return CapabilityCheckExplanation(CapabilityAll, "유닛 능력 · 만능 / 랭크 N");
            yield return CapabilityCheckExplanation(CapabilityLongSword, "장착 조건 · 장비 · 마법");

            // Exercise real filter buttons and source-ID search callbacks. A rotated label
            // must fail membership checks even if a count and predicate agree with each other.
            string[] filters = { "야수", "장비", "마법", "만능" };
            string[] exclusiveOriginals = { CapabilitySwarm, CapabilityRabi, CapabilityDeathHand, null };
            string[] originalUnits = { CapabilitySwarm, CapabilityRabi, CapabilityDeathHand, CapabilityAll };
            for (int filter = 0; filter < filters.Length; filter++)
            {
                yield return CapabilityChooseFilter(filters[filter]);
                foreach (string id in originalUnits)
                {
                    SearchFor(id.Substring(3)); yield return null;
                    bool included = id == CapabilityAll || id == exclusiveOriginals[filter];
                    Assert.That(VisibleCards().Select(card => card.id).ToArray(),
                        Is.EqualTo(included ? new[] { id } : new string[0]),
                        filters[filter] + " must follow the original ability of " + id);
                }
                SearchFor(CapabilityLongSword.Substring(3)); yield return null;
                Assert.That(VisibleCards(), Is.Empty, "Unit capability filters must not treat an equipment condition as a unit ability.");
            }

            yield return CapabilityChooseFilter("전체");
            SearchFor(CapabilityLongSword.Substring(3)); yield return null;
            Assert.That(VisibleCards().Select(card => card.id), Is.EqualTo(new[] { CapabilityLongSword }));

            Assert.That(CapabilityDefinitionIdentity(), Is.EqualTo(definitionIdentity), "Browsing must never rewrite source IDs or ability masks.");
            Assert.That(ReadEditorDeck(), Is.EqualTo(editorDeck), "Read-only browsing must preserve the actual editor deck.");
            foreach (string slot in slots)
            {
                Assert.That(backend.Exists(slot), Is.EqualTo(saved[slot] != null), "Browsing changed save existence: " + slot);
                if (saved[slot] != null) Assert.That(backend.Read(slot), Is.EqualTo(saved[slot]), "Browsing changed save bytes: " + slot);
            }
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        static string[] CapabilityDefinitionIdentity() => CardCatalog.All
            .Select(card => card.id + ":" + card.sourceId + ":" + card.capabilityMask + ":" + card.requiredCapabilityMask).ToArray();

        static IEnumerator CapabilityCheckExplanation(string id, string expected)
        {
            SearchFor(id.Substring(3)); yield return null;
            Assert.That(VisibleCards().Select(card => card.id), Is.EqualTo(new[] { id }));
            var button = ActiveButtons("Card ").Single();
            var pointer = new ExtendedPointerEventData(EventSystem.current) { pointerId = 42,
                pointerType = UIPointerType.MouseOrPen, position = new Vector2(Screen.width * .5f, Screen.height * .4f) };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return SmokeUi.WaitUntil(() => GameObject.Find("Hover explanation") != null, 2, "The actual card hover did not open.");
            Assert.That(GameObject.Find("Hover explanation").GetComponentsInChildren<TMP_Text>()
                .Any(label => label.text.Contains(expected)), Is.True, "Hover must use the original ability: " + id);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerExitHandler); yield return null;
            button.onClick.Invoke(); yield return null;
            Assert.That(SmokeUi.HasText(expected), Is.True, "Detail must use the original ability: " + id);
            SmokeUi.Click("돌아가기"); yield return null;
        }

        static IEnumerator CapabilityChooseFilter(string name)
        {
            UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.gameObject.activeInHierarchy && button.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.GetComponentInParent<Button>() == button && label.text.StartsWith("능력 · ", StringComparison.Ordinal)))
                .onClick.Invoke();
            yield return null;
            UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Single(button => button.gameObject.activeInHierarchy && button.GetComponentsInChildren<TMP_Text>()
                    .Any(label => label.GetComponentInParent<Button>() == button && label.text.StartsWith(name + " · ", StringComparison.Ordinal)))
                .onClick.Invoke();
            yield return null;
            Assert.That(SmokeUi.FindButton("능력 · " + name), Is.Not.Null, "The actual collection filter did not change.");
        }
    }
}
