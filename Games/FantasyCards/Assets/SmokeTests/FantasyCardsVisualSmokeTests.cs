using System;
using System.Collections;
using System.Linq;
using FantasyCards.Core;
using FantasyCards.Presentation;
using FirstGame.Shared;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using TMPro;

namespace FantasyCards.SmokeTests
{
    // Native UI/asset/layout assertions. Browser pointer tests are a separate acceptance layer.
    public sealed class FantasyCardsVisualSmokeTests
    {
        IDisposable storageScope;
        [UnitySetUp] public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            storageScope = PersistentSaves.UseBackendForTesting(new MemorySaveBackend());
            yield return SmokeUi.LoadBoot();
        }
        [UnityTearDown] public IEnumerator LeaveGame()
        {
            yield return SmokeUi.UnloadBoot();
            storageScope?.Dispose(); storageScope = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }
        [UnityTest] public IEnumerator IllustratedGalleryDetailAndCompactBattleKeepUsableControls()
        {
            var music = UnityEngine.Object.FindObjectsByType<BgmPlayer>(FindObjectsSortMode.None)
                .Where(player => player.gameObject.activeInHierarchy).ToArray();
            Assert.That(music.Length, Is.EqualTo(1), "The actual game must own exactly one background soundtrack player.");
            var soundtrack = Resources.Load<AudioClip>("Audio/starlight-overture");
            Assert.That(soundtrack, Is.Not.Null, "The shipped soundtrack must import as an AudioClip.");
            Assert.That(music[0].Clip, Is.SameAs(soundtrack));
            Assert.That(soundtrack.length, Is.InRange(90f, 120f));
            Assert.That(soundtrack.channels, Is.EqualTo(2));
            var musicSources = music[0].GetComponents<AudioSource>().Where(source => source.clip == soundtrack).ToArray();
            Assert.That(musicSources.Length, Is.EqualTo(1), "Screen changes must not layer duplicate BGM sources.");
            Assert.That(musicSources[0].loop, Is.True);
            // This checks actual player wiring/import, not audible browser or physical speaker output.
            yield return SmokeUi.Capture("FantasyCards", "06-title-mobile", 844, 390,
                () => { AssertTouchButtons("새로운 결투", "이어하기", "카드 도감", "덱 구성 · 24장", "저장 내보내기 / 가져오기", "음악"); AssertGradientMeshes(); });
            SmokeUi.Click("음악"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "16-music-settings-693x390", 693, 390, () =>
            {
                AssertTouchButtons(music[0].Muted ? "음악 켜기" : "음악 끄기", "음량 -", "음량 +", "돌아가기");
                AssertReadableLabel("별빛의 선율");
                AssertReadableLabel("음량 " + Mathf.RoundToInt(music[0].Volume * 100) + "%");
            });
            SmokeUi.Click("돌아가기"); yield return null;
            SmokeUi.Click("카드 도감"); yield return null;
            SmokeUi.Click("다음"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "07-hero-gallery", 1920, 1080, AssertArtCrops);
            SmokeUi.Click("서약의 수호자"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "08-hero-detail", 1920, 1080, AssertArtCrops);
            yield return SmokeUi.Capture("FantasyCards", "09-hero-detail-mobile", 844, 390, () => AssertTouchButtons("돌아가기"));
            SmokeUi.Click("돌아가기"); yield return null;
            SmokeUi.Click("타이틀"); yield return null;
            SmokeUi.Click("덱 구성 · 24장"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "10-deck-mobile", 844, 390,
                () => AssertTouchButtons("+", "-", "기본 덱 복원", "덱 저장", "이전", "다음", "타이틀"));
            SmokeUi.Click("타이틀"); yield return null;
            SmokeUi.Click("새로운 결투");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                5, "Battle Timeline must release the input lock.");
            Assert.That(PersistentSaves.TryLoad("fantasy-battle", "starlight-oath", 1, CardCatalog.Version, out var before, out var error), Is.True, error);
            var savedBattle = JsonUtility.FromJson<BattleState>(before);
            SmokeUi.Click("새벽의 정원"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "11-battle-mobile", 844, 390, () =>
            {
                AssertTouchButtons("저장", "타이틀", "확대 보기", "지형 전개", "공격 단계", "전투 기록", "음악");
                Assert.That(SmokeUi.HasText("내 지형"), Is.True);
                Assert.That(SmokeUi.HasText("여명 0"), Is.True);
                AssertArtCrops();
                AssertHandNames(savedBattle);
                AssertReadableLabel("지형 전개");
            });
            yield return SmokeUi.Capture("FantasyCards", "13-battle-phone-letterbox", 693, 390, () =>
            {
                AssertTouchButtons("저장", "타이틀", "확대 보기", "지형 전개", "공격 단계", "전투 기록", "음악");
                AssertHandNames(savedBattle); AssertReadableLabel("지형 전개");
            });
            yield return SmokeUi.Capture("FantasyCards", "12-battle-720", 1280, 720, () =>
            { AssertArtCrops(); AssertHandNames(savedBattle); });
            yield return SmokeUi.Capture("FantasyCards", "14-battle-1080-hand-names", 1920, 1080, () =>
            { AssertArtCrops(); AssertHandNames(savedBattle); });
            Assert.That(PersistentSaves.TryLoad("fantasy-battle", "starlight-oath", 1, CardCatalog.Version, out var after, out error), Is.True, error);
            Assert.That(after, Is.EqualTo(before), "Gallery, selection, resizing, and captures must not alter battle state or RNG.");

            SmokeUi.Click("지형 전개");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                5, "Terrain presentation must release the input lock.");
            SmokeUi.Click("별길의 정찰자"); yield return null;
            var targetLane = GameObject.Find("Lane 0 0");
            Assert.That(targetLane, Is.Not.Null, "The actual first friendly battlefield lane must exist.");
            var place = targetLane.GetComponent<Button>();
            Assert.That(place.IsActive() && place.IsInteractable(), Is.True);
            place.onClick.Invoke(); // The same lane callback used by real pointer placement.
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                5, "Unit presentation must release the input lock.");
            Assert.That(PersistentSaves.TryLoad("fantasy-battle", "starlight-oath", 1, CardCatalog.Version, out var placed, out error), Is.True, error);
            var placedBattle = JsonUtility.FromJson<BattleState>(placed);
            Assert.That(placedBattle.players[0].AtLane(0)?.definitionId, Is.EqualTo("dawn_scout"));
            Assert.That(placedBattle.players[0].hand.Count, Is.EqualTo(savedBattle.players[0].hand.Count - 2));
            Assert.That(placedBattle.players[0].dawn, Is.EqualTo(0));
            Assert.That(placedBattle.randomState, Is.EqualTo(savedBattle.randomState), "Terrain and unit deployment must not consume coin RNG.");
            foreach (var viewport in new[] { new Vector2Int(693, 390), new Vector2Int(844, 390), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
            {
                yield return SmokeUi.Capture("FantasyCards", "15-unit-lane-" + viewport.x + "x" + viewport.y, viewport.x, viewport.y, () =>
                { AssertLaneUnitLabels(placedBattle); AssertHandNames(placedBattle); AssertArtCrops(); });
            }
            Assert.That(PersistentSaves.TryLoad("fantasy-battle", "starlight-oath", 1, CardCatalog.Version, out var placedAfter, out error), Is.True, error);
            Assert.That(placedAfter, Is.EqualTo(placed), "Unit layout and capture must not change the saved battle state.");
            LogAssert.NoUnexpectedReceived();
        }
        static void AssertLaneUnitLabels(BattleState savedBattle)
        {
            for (int owner = 0; owner < savedBattle.players.Count; owner++)
            foreach (var unit in savedBattle.players[owner].units)
            {
                var lane = GameObject.Find("Lane " + owner + " " + unit.lane);
                Assert.That(lane, Is.Not.Null);
                var definition = CardCatalog.Get(unit.definitionId);
                var labels = lane.GetComponentsInChildren<TMP_Text>();
                var expected = new[] { definition.name, "공격 " + (definition.attack + unit.bonusPower) + "  생명 " + (definition.health - unit.damage),
                    unit.summoningSick ? "소환 대기" : unit.exhausted ? "행동 완료" : null };
                foreach (var value in expected.Where(value => value != null))
                {
                    var matches = labels.Where(text => text.text == value).ToArray();
                    Assert.That(matches.Length, Is.EqualTo(1), "Actual battlefield lane must contain " + value);
                    AssertReadableLabel(matches[0]);
                }
                // Check the extra coin badge whenever the small two-line layout is active.
                var canvas = lane.GetComponentInParent<Canvas>().rootCanvas;
                if (canvas.pixelRect.height < 600)
                {
                    var coin = labels.SingleOrDefault(text => text.text == "코인 " + (savedBattle.rules.baseCoins + unit.extraCoins));
                    Assert.That(coin, Is.Not.Null); AssertReadableLabel(coin);
                }
            }
        }
        static void AssertHandNames(BattleState savedBattle)
        {
            var cards = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.gameObject.activeInHierarchy && button.gameObject.name.StartsWith("Card ", StringComparison.Ordinal)).ToArray();
            Assert.That(cards.Length, Is.EqualTo(savedBattle.players[0].hand.Count), "Every saved hand card must have its own visible card UI.");
            foreach (var group in savedBattle.players[0].hand.GroupBy(card => card.definitionId))
            {
                var definition = CardCatalog.Get(group.Key);
                var matches = cards.Where(button => button.gameObject.name == "Card " + definition.id).ToArray();
                Assert.That(matches.Length, Is.EqualTo(group.Count()), definition.name + " hand instances");
                foreach (var button in matches)
                {
                    // Scope to the actual hand card; an inspector with the same label must not satisfy this check.
                    var labels = button.GetComponentsInChildren<TMP_Text>().Where(text => text.text == definition.name).ToArray();
                    Assert.That(labels.Length, Is.EqualTo(1), definition.name + " hand name label");
                    AssertReadableLabel(labels[0]);
                }
            }
        }
        static void AssertReadableLabel(string label)
        {
            var text = UnityEngine.Object.FindObjectsByType<TMP_Text>(FindObjectsSortMode.None)
                .First(t => t.gameObject.activeInHierarchy && t.text == label);
            AssertReadableLabel(text);
        }
        static void AssertReadableLabel(TMP_Text text)
        {
            text.ForceMeshUpdate();
            var glyphs = text.textInfo.characterInfo.Take(text.textInfo.characterCount).Where(character => character.isVisible).ToArray();
            var rendered = new string(glyphs.Select(character => character.character).ToArray());
            Assert.That(rendered, Is.EqualTo(new string(text.text.Where(character => !char.IsWhiteSpace(character)).ToArray())),
                text.text + " must render every glyph without ellipsis or hidden characters.");
            Assert.That(text.isTextTruncated, Is.False, text.text + " must not be truncated.");
            var bounds = text.rectTransform.rect;
            foreach (var glyph in glyphs)
            {
                Assert.That(glyph.bottomLeft.x, Is.GreaterThanOrEqualTo(bounds.xMin - .5f), text.text + " left glyph edge");
                Assert.That(glyph.topRight.x, Is.LessThanOrEqualTo(bounds.xMax + .5f), text.text + " right glyph edge");
                Assert.That(glyph.bottomLeft.y, Is.GreaterThanOrEqualTo(bounds.yMin - .5f), text.text + " bottom glyph edge");
                Assert.That(glyph.topRight.y, Is.LessThanOrEqualTo(bounds.yMax + .5f), text.text + " top glyph edge");
            }
            var canvas = text.GetComponentInParent<Canvas>().rootCanvas;
            Assert.That(text.fontSize * canvas.scaleFactor, Is.GreaterThanOrEqualTo(11.9f), text.text + " must remain readable on the canvas.");
        }
        static void AssertGradientMeshes()
        {
            var gradients = UnityEngine.Object.FindObjectsByType<UiGradient>(FindObjectsSortMode.None).Where(g => g.gameObject.activeInHierarchy).ToArray();
            Assert.That(gradients.Length, Is.GreaterThanOrEqualTo(2));
            foreach (var gradient in gradients)
            {
                var mesh = gradient.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null, "Artwork contrast shade must have a renderer mesh.");
                Assert.That(mesh.vertexCount, Is.EqualTo(4), "Artwork contrast shade must produce an actual rendered quad.");
                Assert.That(mesh.colors32.Any(c => c.a > 200), Is.True, "Contrast shading requires opaque vertices.");
            }
        }
        static void AssertTouchButtons(params string[] labels)
        {
            foreach (var label in labels)
            {
                var button = SmokeUi.FindButton(label); Assert.That(button, Is.Not.Null, label);
                var rect = (RectTransform)button.transform; var canvas = button.GetComponentInParent<Canvas>().rootCanvas;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                var minimum = RectTransformUtility.WorldToScreenPoint(camera, corners[0]);
                var maximum = RectTransformUtility.WorldToScreenPoint(camera, corners[2]);
                Assert.That(maximum.x - minimum.x, Is.GreaterThanOrEqualTo(44), label + " touch width");
                Assert.That(maximum.y - minimum.y, Is.GreaterThanOrEqualTo(44), label + " touch height");
                Assert.That(minimum.x, Is.GreaterThanOrEqualTo(0)); Assert.That(minimum.y, Is.GreaterThanOrEqualTo(0));
                Assert.That(maximum.x, Is.LessThanOrEqualTo(canvas.pixelRect.width + 1));
                Assert.That(maximum.y, Is.LessThanOrEqualTo(canvas.pixelRect.height + 1));
            }
        }
        static void AssertRoundedCards()
        {
            var cards = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(button => button.gameObject.activeInHierarchy && button.gameObject.name.StartsWith("Card ", StringComparison.Ordinal));
            foreach (var card in cards)
            {
                var shape = card.GetComponent<UiRoundedImage>();
                Assert.That(shape, Is.Not.Null, "Cards use a curved silhouette, not a rectangular frame over square art.");
                var mesh = shape.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null); Assert.That(mesh.vertexCount, Is.GreaterThan(16));
                var rect = shape.rectTransform.rect;
                var corner = new Vector2(rect.xMin, rect.yMin);
                Assert.That(mesh.vertices.All(vertex => Vector2.Distance(new Vector2(vertex.x, vertex.y), corner) > 1), Is.True,
                    "The rendered card geometry must leave the square corner outside its curved silhouette.");
                Assert.That(card.GetComponent<Mask>()?.enabled, Is.True, "Artwork and labels must clip to the card silhouette.");
                var frame = card.transform.Find("Fine rounded frame")?.GetComponent<UiRoundedImage>();
                Assert.That(frame, Is.Not.Null); Assert.That(frame.canvasRenderer.GetMesh()?.vertexCount, Is.GreaterThan(16));
            }
        }
        static void AssertArtCrops()
        {
            AssertRoundedCards();
            var backdrop = Resources.Load<Texture2D>("Art/battle-background");
            Assert.That(backdrop, Is.Not.Null, "The illustrated fantasy battlefield must be included in the player.");
            var crops = UnityEngine.Object.FindObjectsByType<IllustrationCrop>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.activeInHierarchy).ToArray();
            Assert.That(crops.Length, Is.GreaterThan(0));
            Assert.That(crops.Any(crop => crop.GetComponent<RawImage>().texture == backdrop), Is.True,
                "The illustrated battlefield must be visible in the current screen, not merely loaded as an unused asset.");
            foreach (var crop in crops)
            {
                var image = crop.GetComponent<RawImage>(); var source = crop.SourceUv; var uv = image.uvRect;
                Assert.That(image.texture, Is.Not.Null);
                Assert.That(image.texture.width, Is.GreaterThanOrEqualTo(1254), "New illustration atlas must be connected.");
                Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(source.xMin - .00001f));
                Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(source.yMin - .00001f));
                Assert.That(uv.xMax, Is.LessThanOrEqualTo(source.xMax + .00001f));
                Assert.That(uv.yMax, Is.LessThanOrEqualTo(source.yMax + .00001f));
                var frame = ((RectTransform)crop.transform).rect;
                Assert.That(uv.width * image.texture.width / (uv.height * image.texture.height),
                    Is.EqualTo(frame.width / frame.height).Within(.002f), "Artwork must retain its proportions.");
            }
        }
    }
}
