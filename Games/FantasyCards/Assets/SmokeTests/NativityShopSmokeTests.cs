using System;
using System.Collections;
using System.Collections.Generic;
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
    public sealed partial class NativitySmokeTests
    {
        IEnumerator ReceiveStarter()
        {
            SmokeUi.Click("게임 시작");yield return null;
            NamedButton("Quest starter choice mono-light").onClick.Invoke();yield return null;
            SmokeUi.Click("이 덱을 무료로 받기");yield return null;
            Assert.That(App.QuestSp,Is.EqualTo(600));Assert.That(App.QuestDeckCards.Count,Is.EqualTo(50));
        }
        static void AssertShopLabels()
        {
            var labels=UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .Where(text=>text.gameObject.activeInHierarchy&&(text.name.StartsWith("Shop actual product name ")||text.name.StartsWith("Shop actual product price "))).ToArray();
            Assert.That(labels.Length,Is.EqualTo(8));
            foreach(var label in labels){label.ForceMeshUpdate();Assert.That(label.textInfo.characterInfo.Take(label.textInfo.characterCount).Any(c=>c.isVisible),Is.True,"Actual product name or price glyphs missing: "+label.name);}
        }
        [UnityTest] public IEnumerator FantasyTitleAndAllNineteenActualProductsHaveDistinctPackaging()
        {
            Assert.That(GameObject.Find("Classic FM client wordmark"),Is.Not.Null);
            var logo=GameObject.Find("Original native FM login wordmark").GetComponent<RawImage>();
            var entrySource=Resources.Load<Texture2D>("Art/FmClientMotion/native-entry-reference");
            Assert.That(entrySource,Is.Not.Null);Assert.That(new Vector2Int(entrySource.width,entrySource.height),Is.EqualTo(new Vector2Int(530,398)));
            Assert.That(logo.texture,Is.EqualTo(entrySource));
            Assert.That(logo.uvRect,Is.EqualTo(new Rect(178f/530,355f/398,180f/530,43f/398)),"Use the verified 2011 native client's red-wing wordmark, not the unrelated website portrait.");
            Assert.That(GameObject.Find("Restage fantasy title artwork"),Is.Null,"The user-rejected invented sanctuary must not be used by the classic client.");
            var painting=UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Where(image=>image.gameObject.activeInHierarchy&&image.name.StartsWith("Native entry source ")).ToArray();
            Assert.That(painting.Length,Is.EqualTo(10));
            var omitted=new[]{new Rect(172f/530,204f/398,188f/530,112f/398),new Rect(463f/530,315f/398,66f/530,82f/398),new Rect(481f/530,1f/398,48f/530,8f/398),new Rect(368f/530,126f/398,24f/530,21f/398)};
            foreach(var image in painting)
            {
                Assert.That(image.texture,Is.EqualTo(entrySource));
                foreach(var region in omitted)Assert.That(image.uvRect.Overlaps(region),Is.False,"Painting sampling must exclude the old credential controls, rating, version and captured cursor: "+image.name);
            }
            Assert.That(UnityEngine.Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Any(image=>image.gameObject.activeInHierarchy&&image.name.StartsWith("Original FM character tile ")),Is.False);
            Assert.That(GameObject.Find("Inactive original password row").GetComponent<TMP_InputField>(),Is.Null,"The local client must not collect a web password or authentication code.");
            Assert.That(GameObject.Find("Native 2011 live window left header ornament").GetComponent<RawImage>().texture,Is.EqualTo(entrySource));
            Assert.That(GameObject.Find("Native 2011 live window bottom edge").GetComponent<RawImage>().texture,Is.EqualTo(entrySource));
            Assert.That(GameObject.Find("Native entry source rating omission").GetComponent<FantasyCards.Presentation.FmArtworkFeather>(),Is.Not.Null);
            Assert.That(GameObject.Find("Native entry source cursor omission").GetComponent<FantasyCards.Presentation.FmArtworkFeather>(),Is.Not.Null);
            Canvas.ForceUpdateCanvases();
            foreach(string patchName in new[]{"rating omission","cursor omission"})
            {
                var mesh=ReadRenderedUiMesh(GameObject.Find("Native entry source "+patchName).GetComponent<RawImage>());
                Assert.That(mesh.colors32.Any(c=>c.a==0)&&mesh.colors32.Any(c=>c.a==255)&&mesh.colors32.Any(c=>c.a>0&&c.a<255),Is.True,"The rendered painting composition must have an opaque covering centre and feathered edges: "+patchName);
            }
            Assert.That(SmokeUi.FindButton("게임 시작"),Is.Not.Null);
            yield return Shot("fantasy-title");yield return Shot("fantasy-title-touch",616,346);
            yield return ReceiveStarter();yield return Shot("owned-lobby");yield return Shot("owned-lobby-touch",616,346);
            SmokeUi.Click("카드 상점");yield return null;
            yield return Shot("distinct-product-shop",inspect:AssertShopLabels);
            yield return Shot("distinct-product-shop-touch",616,346,AssertShopLabels);
            string unchanged=JsonUtility.ToJson(App.QuestProfileSnapshot);
            var styles=new HashSet<string>();
            foreach(var offer in FmQuestCatalog.Offers)
            {
                Call("ShowQuestOffer",offer.id);yield return null;
                var cover=UnityEngine.Object.FindObjectsByType<FmRestagePackGraphic>(FindObjectsSortMode.None).Single(p=>p.gameObject.activeInHierarchy);
                Assert.That(cover.OfferId,Is.EqualTo(offer.id));Assert.That(cover.StyleKey,Is.EqualTo(offer.id));
                Assert.That(cover.IsDeck,Is.EqualTo(offer.deckId.Length>0));Assert.That(cover.IsOpeningBack,Is.False);
                string signature=cover.ArtworkTile+"/"+ColorUtility.ToHtmlStringRGBA(cover.Accent)+"/"+cover.IsDeck;
                Assert.That(styles.Add(signature),Is.True,"Two different actual products must not have the same artwork, colour and package form: "+offer.id);
                Assert.That(cover.GetComponentsInChildren<TMP_Text>().Any(text=>text.name=="Pack edition "+offer.id&&!string.IsNullOrEmpty(text.text)),Is.True);
            }
            Assert.That(styles.Count,Is.EqualTo(19));Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot),Is.EqualTo(unchanged),"Looking at different packaging must not draw cards, deduct SP or advance pack RNG.");
            Call("ShowQuestOffer","premium");yield return null;yield return Shot("premium-product-odds");
            Assert.That(backend.Read("preserved-player"),Is.EqualTo("original player bytes"));
            SmokeUi.AssertNoMissingScripts();LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator ProductBackSurvivesSealedReloadAndRevealsAllFiveActualDrawSlotsOnce()
        {
            yield return ReceiveStarter();SmokeUi.Click("카드 상점");yield return null;
            NamedButton("Quest shop offer standard").onClick.Invoke();yield return null;
            SmokeUi.Click("400 SP로 구입");yield return null;
            Assert.That(App.QuestSp,Is.EqualTo(200));var purchase=App.QuestProfileSnapshot.purchases.Single();
            string id=purchase.id;var actualCards=purchase.cardIds.ToArray();uint sealedRandom=App.QuestProfileSnapshot.packRandomState;
            Assert.That(purchase.opened,Is.False);Assert.That(actualCards.Length,Is.EqualTo(5));
            var sealedCover=UnityEngine.Object.FindObjectsByType<FmRestagePackGraphic>(FindObjectsSortMode.None).Single(p=>p.gameObject.activeInHierarchy);
            Assert.That(sealedCover.OfferId,Is.EqualTo("standard"));yield return Shot("specific-sealed-product");
            yield return SmokeUi.LoadBoot();SmokeUi.Click("게임 시작");yield return null;
            Call("ShowQuestPurchases");yield return null;NamedButton("Quest purchased pack "+id).onClick.Invoke();yield return null;
            Assert.That(App.QuestProfileSnapshot.purchases.Single().cardIds,Is.EqualTo(actualCards));Assert.That(App.QuestProfileSnapshot.packRandomState,Is.EqualTo(sealedRandom));
            Assert.That(UnityEngine.Object.FindObjectsByType<FmRestagePackGraphic>(FindObjectsSortMode.None).Single(p=>p.gameObject.activeInHierarchy).OfferId,Is.EqualTo("standard"));
            yield return Shot("continued-specific-sealed-product-touch",616,346);
            SmokeUi.Click("카드팩 개봉");
            var backs=UnityEngine.Object.FindObjectsByType<FmRestagePackGraphic>(FindObjectsSortMode.None).Where(p=>p.gameObject.activeInHierarchy&&p.IsOpeningBack).ToArray();
            Assert.That(backs.Length,Is.EqualTo(5));Assert.That(backs.All(p=>p.OfferId=="standard"),Is.True);
            yield return SmokeUi.Capture("FantasyCards","nativity-"+attempt+"-five-product-backs-opening",1280,720,settleSeconds:0);
            yield return SmokeUi.WaitUntil(()=>App.QuestCurrentScreen=="pack-reveal",6,"The actual sealed pack did not finish its presentation.");
            void AssertFiveResults()
            {
                var slots=UnityEngine.Object.FindObjectsByType<FmRestageAcquiredCard>(FindObjectsSortMode.None).Where(s=>s.gameObject.activeInHierarchy).OrderBy(s=>s.SlotIndex).ToArray();
                Assert.That(slots.Length,Is.EqualTo(5));Assert.That(slots.Select(s=>s.CardId),Is.EqualTo(actualCards));
                foreach(var slot in slots)Assert.That(slot.OwnedCount,Is.EqualTo(App.QuestOwnedCount(slot.CardId)));
            }
            AssertFiveResults();Assert.That(App.QuestProfileSnapshot.ownedCards.Sum(card=>card.count),Is.EqualTo(55));
            Assert.That(App.QuestSp,Is.EqualTo(200));Assert.That(App.QuestProfileSnapshot.packRandomState,Is.EqualTo(sealedRandom));
            yield return Shot("five-actual-card-draw-slots",inspect:AssertFiveResults);
            yield return Shot("five-actual-card-draw-slots-touch",616,346,AssertFiveResults);
            string opened=JsonUtility.ToJson(App.QuestProfileSnapshot);
            yield return SmokeUi.LoadBoot();SmokeUi.Click("게임 시작");yield return null;Call("ShowQuestPurchases");yield return null;
            NamedButton("Quest purchased pack "+id).onClick.Invoke();yield return null;AssertFiveResults();
            Assert.That(JsonUtility.ToJson(App.QuestProfileSnapshot),Is.EqualTo(opened),"Viewing or reloading five results must never award a second copy or reroll a result.");
            Assert.That(backend.Read("preserved-player"),Is.EqualTo("original player bytes"));SmokeUi.AssertNoMissingScripts();LogAssert.NoUnexpectedReceived();
        }
    }
}
