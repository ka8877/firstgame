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
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
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
            // Keep this fixed-seed visual regression independent from the real selectable demo flow below.
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            typeof(FantasyCardsApp).GetMethod("StartBattle", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .Invoke(app, new object[] { 731029u });
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable,
                5, "Battle Timeline must release the input lock.");
            Assert.That(PersistentSaves.TryLoad("fantasy-battle-demo-v2", "starlight-oath", 1, CardCatalog.Version, out var before, out var error), Is.True, error);
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
            Assert.That(PersistentSaves.TryLoad("fantasy-battle-demo-v2", "starlight-oath", 1, CardCatalog.Version, out var after, out error), Is.True, error);
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
            Assert.That(PersistentSaves.TryLoad("fantasy-battle-demo-v2", "starlight-oath", 1, CardCatalog.Version, out var placed, out error), Is.True, error);
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
            Assert.That(PersistentSaves.TryLoad("fantasy-battle-demo-v2", "starlight-oath", 1, CardCatalog.Version, out var placedAfter, out error), Is.True, error);
            Assert.That(placedAfter, Is.EqualTo(placed), "Unit layout and capture must not change the saved battle state.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator SelectableLeadersDecksAndOpeningHandUseActualUiCallbacks()
        {
            Assert.That(CardCatalog.All.Length, Is.EqualTo(40));
            Assert.That(DemoCatalog.Leaders.Length, Is.EqualTo(4));
            Assert.That(DemoCatalog.StarterDecks.Length, Is.EqualTo(4));
            SmokeUi.Click("새로운 결투"); yield return null;
            foreach (var leader in DemoCatalog.Leaders) Assert.That(GameObject.Find("Leader " + leader.id), Is.Not.Null);
            ClickObject("Leader kael"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "17-leader-selection-693x390", 693, 390, () =>
            {
                AssertTouchButtons("타이틀", "기본 덱 선택");
                foreach (var leader in DemoCatalog.Leaders) AssertReadableLabel(leader.name);
            });
            SmokeUi.Click("기본 덱 선택"); yield return null;
            ClickObject("Starter deck knights"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "18-four-starter-decks-693x390", 693, 390, () =>
            {
                AssertTouchButtons("리더 선택", "내 편집 덱 사용", "상대 선택");
                foreach (var deck in DemoCatalog.StarterDecks) AssertReadableLabel(deck.name);
            });
            SmokeUi.Click("상대 선택"); yield return null;
            ClickObject("Encounter 1"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "19-three-duels-693x390", 693, 390, () =>
            {
                AssertTouchButtons("덱 선택", "플레이 안내", "덱을 섞고 시작");
                foreach (var encounter in DemoCatalog.Encounters) AssertReadableLabel(encounter.name);
            });
            SmokeUi.Click("덱을 섞고 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null, 5, "Real deck shuffle must lead to the opening hand.");
            var initial = ReadDemoBattle();
            Assert.That(initial.players[0].leaderId, Is.EqualTo("kael"));
            Assert.That(initial.players[0].deckId, Is.EqualTo("knights"));
            Assert.That(initial.encounterIndex, Is.EqualTo(1));
            Assert.That(initial.openingHandOpen, Is.True);
            ClickObject("Card " + initial.players[0].hand[0].definitionId); yield return null;
            Assert.That(ReadDemoBattle().randomState, Is.EqualTo(initial.randomState), "Selecting replacement cards is UI-only.");
            yield return SmokeUi.Capture("FantasyCards", "20-opening-hand-693x390", 693, 390, () =>
            { AssertTouchButtons("선택 해제", "선택한 1장 교체"); AssertHandNames(initial); });
            SmokeUi.Click("선택한 1장 교체");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable, 5, "The new hand must enter the actual battle.");
            var started = ReadDemoBattle();
            Assert.That(started.openingHandOpen, Is.False);
            Assert.That(started.players[0].mulliganUsed, Is.True);
            Assert.That(started.players[0].hand.Count, Is.EqualTo(initial.rules.openingHand));
            Assert.That(started.randomState, Is.Not.EqualTo(initial.randomState));
            ClickObject("Player leader kael"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "21-leader-skill-693x390", 693, 390, () =>
            {
                AssertTouchButtons("돌아가기", "리더 기술 사용");
                AssertReadableLabel(DemoCatalog.GetLeader("kael").skillName);
                Assert.That(SmokeUi.FindButton("리더 기술 사용").interactable, Is.False, "The real zero-resource opening cannot use a skill.");
            });
            SmokeUi.Click("돌아가기"); yield return null;
            var card = started.players[0].hand[0];
            var handObject = GameObject.Find("Card " + card.definitionId);
            var pointer = new ExtendedPointerEventData(EventSystem.current) { pointerId = 42, pointerType = UIPointerType.MouseOrPen, position = new Vector2(300, 60) };
            ExecuteEvents.Execute(handObject, pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(GameObject.Find("Hover explanation"), Is.Not.Null, "Input System mouse pointer IDs can be positive and must show explanations.");
            yield return SmokeUi.Capture("FantasyCards", "22-card-hover-693x390", 693, 390, () =>
            {
                ExecuteEvents.Execute(GameObject.Find("Card " + card.definitionId), pointer, ExecuteEvents.pointerEnterHandler);
                Canvas.ForceUpdateCanvases();
                var popup = GameObject.Find("Hover explanation"); Assert.That(popup, Is.Not.Null);
                foreach (var label in popup.GetComponentsInChildren<TMP_Text>()) AssertReadableLabel(label);
            });
            ExecuteEvents.Execute(GameObject.Find("Card " + card.definitionId), pointer, ExecuteEvents.pointerExitHandler); yield return null;
            Assert.That(GameObject.Find("Hover explanation"), Is.Null);
            Assert.That(JsonUtility.ToJson(ReadDemoBattle()), Is.EqualTo(JsonUtility.ToJson(started)), "Tooltip and leader inspection must not alter state or RNG.");
            LogAssert.NoUnexpectedReceived();
        }

        static BattleState ReadDemoBattle()
        {
            Assert.That(PersistentSaves.TryLoad("fantasy-battle-demo-v2", "starlight-oath", 1, CardCatalog.Version, out var payload, out var error), Is.True, error);
            return JsonUtility.FromJson<BattleState>(payload);
        }

        static void ClickObject(string name)
        {
            var obj = GameObject.Find(name); Assert.That(obj, Is.Not.Null, name);
            var button = obj.GetComponent<Button>(); Assert.That(button, Is.Not.Null, name);
            Assert.That(button.IsActive() && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
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
            var illustrations = Resources.LoadAll<FantasyCards.Data.CardAsset>("Data/Cards").Select(card => card.illustration).ToArray();
            Assert.That(illustrations.Length, Is.EqualTo(40), "All forty authored card illustrations must be connected.");
            Assert.That(illustrations.All(sprite => sprite != null), Is.True);
            var atlases = illustrations.Select(sprite => sprite.texture).Distinct().ToArray();
            Assert.That(atlases.Length, Is.EqualTo(10), "Forty cards use ten distinct two-by-two atlases.");
            foreach (var sprite in illustrations)
            {
                Assert.That(sprite.texture.width, Is.GreaterThanOrEqualTo(1254), "Card atlas resolution must retain 627px cells.");
                Assert.That(sprite.texture.height, Is.GreaterThanOrEqualTo(1254));
                Assert.That(sprite.rect.width, Is.GreaterThanOrEqualTo(627));
                Assert.That(sprite.rect.height, Is.GreaterThanOrEqualTo(627));
                Assert.That(sprite.rect.width * 2, Is.EqualTo(sprite.texture.width));
                Assert.That(sprite.rect.height * 2, Is.EqualTo(sprite.texture.height));
            }
            var portraits = DemoCatalog.Leaders.Select(leader => Resources.Load<Texture2D>("Art/portrait-" + leader.id)).ToArray();
            foreach (var portrait in portraits)
            {
                Assert.That(portrait, Is.Not.Null, "Each leader must use its authored full portrait.");
                Assert.That(portrait.width, Is.GreaterThanOrEqualTo(1000));
                Assert.That(portrait.height, Is.GreaterThanOrEqualTo(1250));
                Assert.That((float)portrait.width / portrait.height, Is.EqualTo(.8f).Within(.002f), "Leader portraits have a four-by-five source, not a card atlas cell.");
            }
            var crops = UnityEngine.Object.FindObjectsByType<IllustrationCrop>(FindObjectsSortMode.None)
                .Where(c => c.gameObject.activeInHierarchy).ToArray();
            Assert.That(crops.Length, Is.GreaterThan(0));
            Assert.That(crops.Any(crop => crop.GetComponent<RawImage>().texture == backdrop), Is.True,
                "The illustrated battlefield must be visible in the current screen, not merely loaded as an unused asset.");
            foreach (var crop in crops)
            {
                var image = crop.GetComponent<RawImage>(); var source = crop.SourceUv; var uv = image.uvRect;
                Assert.That(image.texture, Is.Not.Null);
                if (atlases.Contains(image.texture))
                {
                    // Artwork uses the authored 627px cell, inset by half a pixel on each edge.
                    Assert.That(illustrations.Any(sprite => sprite.texture == image.texture &&
                        Mathf.Abs(source.x * image.texture.width - (sprite.rect.x + .5f)) < .01f &&
                        Mathf.Abs(source.y * image.texture.height - (sprite.rect.y + .5f)) < .01f &&
                        Mathf.Abs(source.width * image.texture.width - (sprite.rect.width - 1)) < .01f &&
                        Mathf.Abs(source.height * image.texture.height - (sprite.rect.height - 1)) < .01f), Is.True,
                        "Each card crop must stay in one complete authored atlas cell, with the sampling inset.");
                }
                else
                {
                    Assert.That(image.texture == backdrop || portraits.Contains(image.texture), Is.True,
                        "Non-atlas artwork must be the battlefield or a connected leader portrait.");
                    Assert.That(source, Is.EqualTo(new Rect(0, 0, 1, 1)), "Portraits and backdrops use their whole source image.");
                    if (image.texture == backdrop) Assert.That(image.texture.width, Is.GreaterThanOrEqualTo(1254));
                }
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
