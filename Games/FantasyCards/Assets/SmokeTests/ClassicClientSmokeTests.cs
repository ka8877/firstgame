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
    public sealed partial class NativitySmokeTests
    {
        [UnityTest]
        public IEnumerator LateClientRoomsAndProfileSurviveReloadWithoutChangingEconomyOrRng()
        {
            yield return ReceiveStarter();
            Assert.That(App.QuestCurrentScreen, Is.EqualTo("lobby"));
            AssertClassicClientViewport();
            var roomWindow = GameObject.Find("Late FM1 live waiting room").GetComponent<RectTransform>();
            Assert.That(Vector2.Distance(roomWindow.anchorMin, new Vector2(205f / 640, 139f / 480)), Is.LessThan(.000001f));
            Assert.That(Vector2.Distance(roomWindow.anchorMax, new Vector2(630f / 640, 421f / 480)), Is.LessThan(.000001f));
            var rooms = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Classic live room ", StringComparison.Ordinal)).ToArray();
            Assert.That(rooms.Length, Is.EqualTo(FmScenarioCatalog.Stages.Length));
            Assert.That(rooms.Count(button => button.interactable), Is.EqualTo(1), "Only the actual unlocked first scenario may be entered by a fresh owned profile.");
            foreach (int index in new[] { 4, 5 })
            {
                var empty = GameObject.Find("Classic empty room socket " + index);
                Assert.That(empty.GetComponentInChildren<FmClassicClientSkin>(), Is.Not.Null, "Empty sockets retain the same engraved client frame.");
                Assert.That(empty.GetComponent<Button>(), Is.Null, "An empty decorative socket must not pretend to be a playable room.");
            }
            var source = Resources.Load<Texture2D>("Art/FmClientMotion/late-client-interface-reference");
            Assert.That(source, Is.Not.Null, "The actual retained late-client reference must be imported.");
            Assert.That(new Vector2Int(source.width, source.height), Is.EqualTo(new Vector2Int(640, 480)));
            var hero = GameObject.Find("Bounded original hero art").GetComponent<RawImage>();
            Assert.That(hero.texture, Is.EqualTo(source));
            Assert.That(hero.uvRect, Is.EqualTo(new Rect(7f / 640, 115f / 480, 196f / 640, 307f / 480)), "Use the actual 2017 waiting-room barmaid, rather than another screen's character.");
            var status = GameObject.Find("Live disabled multiplayer status covering original capture").GetComponent<RectTransform>();
            Assert.That(status.GetComponent<Image>().color.a, Is.EqualTo(1));
            Assert.That(status.anchorMin, Is.EqualTo(new Vector2(97f / 196, 273f / 307)));
            Assert.That(status.anchorMax, Is.EqualTo(new Vector2(189f / 196, 305f / 307)));
            Assert.That(status.GetComponentInChildren<TMP_Text>().text, Does.Contain("추후 업데이트"));
            var skins = UnityEngine.Object.FindObjectsByType<FmClassicClientSkin>(FindObjectsSortMode.None).Where(skin => skin.gameObject.activeInHierarchy).ToArray();
            Assert.That(skins.Any(skin => skin.ButtonSkin), Is.True);
            Assert.That(skins.Any(skin => !skin.ButtonSkin), Is.True);
            Canvas.ForceUpdateCanvases();
            foreach (var skin in skins)
            {
                Assert.That(skin.Source, Is.EqualTo(source));
                Assert.That(skin.texture, Is.EqualTo(source));
                AssertSkinRendererHasGeometry(skin);
            }
            AssertClassicPixelCoverage();
            Assert.That(GameObject.Find("Restage fantasy title artwork"), Is.Null);
            yield return Shot("classic-live-client-lobby");
            yield return Shot("classic-live-client-lobby-touch", 616, 346);

            string economyBefore = JsonUtility.ToJson(App.QuestProfileSnapshot);
            SmokeUi.Click("내정보"); yield return null;
            Assert.That(GameObject.Find("Late FM1 My Information window"), Is.Not.Null);
            Assert.That(GameObject.Find("Late FM1 live waiting room"), Is.Not.Null, "The real waiting room remains visible below My Information.");
            Assert.That(GameObject.Find("Classic system and local record window"), Is.Not.Null);
            Assert.That(GameObject.Find("Profile modal input shield").GetComponent<Image>().raycastTarget, Is.True);
            var modal = GameObject.Find("Late FM1 My Information window").GetComponent<RectTransform>();
            Assert.That(Vector2.Distance(modal.anchorMin, new Vector2(331f / 640, 221f / 480)), Is.LessThan(.000001f));
            Assert.That(Vector2.Distance(modal.anchorMax, new Vector2(618f / 640, 438f / 480)), Is.LessThan(.000001f));
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

        static void AssertClassicClientViewport()
        {
            var viewport = GameObject.Find("Classic 4 by 3 client viewport").GetComponent<RectTransform>();
            Assert.That(viewport.rect.width / viewport.rect.height, Is.EqualTo(4f / 3).Within(.0001f));
            var corners = new Vector3[4]; viewport.GetWorldCorners(corners);
            var a = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            var b = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            Assert.That((b.x - a.x) / (b.y - a.y), Is.EqualTo(4f / 3).Within(.001f));
            Assert.That((a.x + b.x) * .5f, Is.EqualTo(Screen.width * .5f).Within(1));
            Assert.That((a.y + b.y) * .5f, Is.EqualTo(Screen.height * .5f).Within(1));
            Assert.That(a.x, Is.GreaterThanOrEqualTo(-1));Assert.That(a.y, Is.GreaterThanOrEqualTo(-1));
            Assert.That(b.x, Is.LessThanOrEqualTo(Screen.width + 1));Assert.That(b.y, Is.LessThanOrEqualTo(Screen.height + 1));
        }

        static void AssertClassicPixelCoverage()
        {
            var pixel = Resources.Load<TMP_FontAsset>("Fonts/ClassicPixel");
            Assert.That(pixel, Is.Not.Null, "The licensed Galmuri substitute must be generated before the actual UI test.");
            Assert.That(pixel.atlasPopulationMode, Is.EqualTo(AtlasPopulationMode.Static));
            Assert.That(pixel.sourceFontFile, Is.Null, "The baked static font must not retain a runtime TTF dependency.");
            Assert.That(Resources.Load<Font>("Fonts/Galmuri11"), Is.Null, "The build must use the baked atlas, with the Galmuri source kept in Editor/Fonts.");
            Assert.That(pixel.atlasRenderMode, Is.EqualTo(UnityEngine.TextCore.LowLevel.GlyphRenderMode.RASTER_HINTED));
            Assert.That(pixel.faceInfo.pointSize, Is.EqualTo(12));
            Assert.That(pixel.material.shader.name, Is.EqualTo("TextMeshPro/Mobile/Bitmap"));
            foreach (var atlas in pixel.atlasTextures) Assert.That(atlas.filterMode, Is.EqualTo(FilterMode.Point));
            string hangul = new string(Enumerable.Range(0xac00, 0xd7a3 - 0xac00 + 1).Select(c => (char)c).ToArray());
            string jamo = new string(Enumerable.Range(0x3131, 0x318e - 0x3131 + 1).Select(c => (char)c).ToArray());
            Assert.That(pixel.HasCharacters(hangul + jamo, out uint[] missingKorean), Is.True, "The static font must contain every NFC Hangul syllable and compatibility Jamo.");
            string names = string.Concat(CardCatalog.All.Select(card => card.name)) + string.Concat(OriginalCardArchive.All.Select(card => card.name));
            Assert.That(pixel.HasCharacters(names, out uint[] missingNames, true), Is.True, "Every actual playable/archive card name must resolve in the pixel font or preserved fallback.");
            Assert.That(pixel.fallbackFontAssetTable, Does.Contain(Resources.Load<TMP_FontAsset>("Fonts/Korean")));
            Assert.That(GameObject.Find("Live disabled multiplayer status covering original capture").GetComponentInChildren<TMP_Text>().font, Is.EqualTo(pixel));
        }

        static void AssertSkinRendererHasGeometry(FmClassicClientSkin skin)
        {
            // Check the actual renderer, not just the presence of a component:
            // the rejected Client02 had valid resources but invisible skins.
            var rendered = ReadRenderedUiMesh(skin);
            Assert.That(rendered.vertexCount, Is.GreaterThanOrEqualTo(20), "The actual source skin must have nonempty visible UI geometry: " + skin.name);
            Assert.That(rendered.triangles.Length, Is.GreaterThanOrEqualTo(30), skin.name);
            Assert.That(rendered.colors32.Any(color => color.a > 0), Is.True, skin.name);
        }

        static Mesh ReadRenderedUiMesh(Graphic graphic)
        {
            var getter = typeof(CanvasRenderer).GetMethod("GetMesh", Type.EmptyTypes);
            Mesh rendered;
            if (getter != null) rendered = getter.Invoke(graphic.canvasRenderer, null) as Mesh;
            else
            {
                getter = typeof(CanvasRenderer).GetMethod("GetMesh", new[] { typeof(Mesh) });
                Assert.That(getter, Is.Not.Null, "Pinned Unity must expose its actual UI renderer mesh.");
                rendered = new Mesh(); getter.Invoke(graphic.canvasRenderer, new object[] { rendered });
            }
            Assert.That(rendered, Is.Not.Null, graphic.name);
            return rendered;
        }
    }
}
