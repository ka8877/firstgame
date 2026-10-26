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
    // Actual Unity callbacks, images and layout. Browser hit-testing is checked separately.
    public sealed class FantasyCardsVisualSmokeTests
    {
        IDisposable storageScope, preferenceScope;
        [UnitySetUp] public IEnumerator EnterGame()
        {
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            yield return new EnterPlayMode();
            storageScope = PersistentSaves.UseBackendForTesting(new MemorySaveBackend());
            preferenceScope = SmokeUi.PreserveDemoPreferences();
            yield return SmokeUi.LoadBoot();
        }
        [UnityTearDown] public IEnumerator LeaveGame()
        {
            yield return SmokeUi.UnloadBoot();
            preferenceScope?.Dispose(); preferenceScope = null;
            storageScope?.Dispose(); storageScope = null;
            if (Application.isPlaying) yield return new ExitPlayMode();
        }

        [UnityTest] public IEnumerator CatalogFiltersOriginalCardsAndDeckAnalysisUseLiveData()
        {
            Assert.That(CardCatalog.All.Length, Is.EqualTo(96));
            Assert.That(CardCatalog.All.Select(card => card.element).Distinct().Count(), Is.EqualTo(7));
            var music = UnityEngine.Object.FindObjectsByType<BgmPlayer>(FindObjectsSortMode.None)
                .Where(player => player.gameObject.activeInHierarchy).ToArray();
            Assert.That(music.Length, Is.EqualTo(1));
            var soundtrack = Resources.Load<AudioClip>("Audio/starlight-overture");
            Assert.That(soundtrack, Is.Not.Null);
            Assert.That(music[0].Clip, Is.SameAs(soundtrack));
            Assert.That(soundtrack.length, Is.InRange(90f, 120f));
            Assert.That(soundtrack.channels, Is.EqualTo(2));
            var source = music[0].GetComponents<AudioSource>().Single(audio => audio.clip == soundtrack);
            Assert.That(source.loop, Is.True);
            yield return SmokeUi.Capture("FantasyCards", "06-classic-title-mobile", 844, 390,
                () => { AssertTouchButtons("새로운 결투", "이어하기", "카드 도감", "덱 구성 · 50장", "저장 내보내기 / 가져오기", "음악"); AssertGradientMeshes(); });
            SmokeUi.Click("음악"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "16-classic-music-mobile", 693, 390, () =>
            {
                AssertTouchButtons(music[0].Muted ? "음악 켜기" : "음악 끄기", "음량 -", "음량 +", "돌아가기");
                AssertReadableLabel("음량 " + Mathf.RoundToInt(music[0].Volume * 100) + "%");
            });
            SmokeUi.Click("돌아가기"); yield return null;
            SmokeUi.Click("카드 도감"); yield return null;
            var sample = CardCatalog.All.First(card => card.kind == CardKind.Unit && card.cost > 0 && card.cost < 8);
            SmokeUi.Click("속성 · 전체"); yield return null;
            SmokeUi.Click(CardCatalog.ElementName(sample.element)); yield return null;
            AssertVisibleCatalog(card => card.element == sample.element);
            SmokeUi.Click("종류 · 전체"); yield return null;
            SmokeUi.Click("유닛"); yield return null;
            AssertVisibleCatalog(card => card.element == sample.element && card.kind == CardKind.Unit);
            SmokeUi.Click("소울 · 전체"); yield return null;
            SmokeUi.Click(sample.cost + " 소울"); yield return null;
            SmokeUi.Click("등급 · 전체"); yield return null;
            SmokeUi.Click(CardCatalog.RarityName(sample.rarity)); yield return null;
            AssertVisibleCatalog(card => card.element == sample.element && card.kind == CardKind.Unit && card.cost == sample.cost && card.rarity == sample.rarity);
            yield return SmokeUi.Capture("FantasyCards", "07-classic-filtered-gallery", 1920, 1080, AssertArtCrops);
            yield return SmokeUi.Capture("FantasyCards", "07-classic-filtered-gallery-mobile", 844, 390, () =>
                AssertTouchButtons("속성 · " + CardCatalog.ElementName(sample.element), "종류 · 유닛", "소울 · " + sample.cost, "등급 · " + CardCatalog.RarityName(sample.rarity), "타이틀", "필터 초기화"));
            ClickObject("Card " + sample.id); yield return null;
            SmokeUi.AssertVisibleKorean(sample.name);
            Assert.That(SmokeUi.HasText("AP " + sample.attack), Is.True);
            Assert.That(SmokeUi.HasText("DP " + sample.defense), Is.True);
            Assert.That(SmokeUi.HasText("HP " + sample.health), Is.True);
            yield return SmokeUi.Capture("FantasyCards", "08-classic-card-detail", 1920, 1080, AssertArtCrops);
            SmokeUi.Click("원본 카드"); yield return null;
            var original = GameObject.Find("Original card image")?.GetComponent<RawImage>();
            Assert.That(original, Is.Not.Null);
            Assert.That(original.texture, Is.SameAs(Resources.Load<Texture2D>(sample.artResource)));
            Assert.That(original.uvRect, Is.EqualTo(new Rect(0, 0, 1, 1)), "Original view must not crop or rewrite the printed card.");
            yield return SmokeUi.Capture("FantasyCards", "09-classic-original-card-mobile", 844, 390, () => AssertTouchButtons("능력 설명"));
            SmokeUi.Click("능력 설명"); yield return null;
            SmokeUi.Click("돌아가기"); yield return null;
            SmokeUi.Click("필터 초기화"); yield return null;
            AssertVisibleCatalog(card => true);
            // Deliberately choose an empty combination using public filter controls.
            int absent = Enumerable.Range(0, 8).First(cost => !CardCatalog.All.Any(card => card.kind == CardKind.Terrain && card.cost == cost));
            SmokeUi.Click("종류 · 전체"); yield return null; SmokeUi.Click("지형"); yield return null;
            SmokeUi.Click("소울 · 전체"); yield return null; SmokeUi.Click(absent + " 소울"); yield return null;
            Assert.That(SmokeUi.HasText("이 조건에 맞는 카드가 없습니다."), Is.True);
            Assert.That(VisibleCards().Length, Is.Zero);
            SmokeUi.Click("필터 초기화"); yield return null;
            AssertVisibleCatalog(card => true);
            SmokeUi.Click("타이틀"); yield return null;
            SmokeUi.Click("덱 구성 · 50장"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "10-classic-deck-editor-mobile", 844, 390,
                () => AssertTouchButtons("+", "-", "기본 덱 복원", "덱 저장", "덱 분석", "이전", "다음", "타이틀"));
            var app = UnityEngine.Object.FindFirstObjectByType<FantasyCardsApp>();
            var deck = (System.Collections.Generic.List<string>)typeof(FantasyCardsApp).GetField("customDeck", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic).GetValue(app);
            SmokeUi.Click("덱 분석"); yield return null;
            int[] counts = new int[9]; foreach (string id in deck) counts[Mathf.Min(8, CardCatalog.Get(id).cost)]++;
            for (int cost = 0; cost < counts.Length; cost++)
            {
                var bucket = GameObject.Find("Cost bucket " + cost);
                Assert.That(bucket, Is.Not.Null);
                Assert.That(bucket.transform.Find("Count").GetComponent<TMP_Text>().text, Is.EqualTo(counts[cost].ToString()));
            }
            Assert.That(counts.Sum(), Is.EqualTo(50));
            yield return SmokeUi.Capture("FantasyCards", "10-classic-deck-curve-mobile", 844, 390, () => AssertTouchButtons("돌아가기"));
            SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator SelectableAvatarsDecksAndWholeHandShuffleUseActualUiCallbacks()
        {
            Assert.That(DemoCatalog.Leaders.Length, Is.EqualTo(4));
            Assert.That(DemoCatalog.StarterDecks.Length, Is.GreaterThanOrEqualTo(4));
            SmokeUi.Click("새로운 결투"); yield return null;
            SmokeUi.TraceUiState("avatar after new");
            foreach (var leader in DemoCatalog.Leaders) Assert.That(GameObject.Find("Leader " + leader.id), Is.Not.Null);
            ClickObject("Leader kael"); yield return null;
            SmokeUi.TraceUiState("avatar before capture");
            yield return SmokeUi.Capture("FantasyCards", "17-classic-avatar-selection-mobile", 693, 390,
                () => { AssertTouchButtons("타이틀", "기본 덱 선택"); foreach (var leader in DemoCatalog.Leaders) AssertReadableLabel(leader.name); },
                () => SmokeUi.TraceUiState("avatar capture ready"));
            SmokeUi.TraceUiState("avatar after capture");
            SmokeUi.Click("기본 덱 선택"); yield return null;
            ClickObject("Starter deck knights"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "18-classic-starter-decks-mobile", 693, 390,
                () => { AssertTouchButtons("아바타", "내 편집 덱", "상대 선택"); foreach (var deck in DemoCatalog.StarterDecks.Take(4)) AssertReadableLabel(deck.name); });
            SmokeUi.Click("상대 선택"); yield return null; ClickObject("Encounter 1"); yield return null;
            yield return SmokeUi.Capture("FantasyCards", "19-classic-three-duels-mobile", 693, 390,
                () => { AssertTouchButtons("덱 선택", "플레이 안내", "덱을 섞고 시작"); foreach (var encounter in DemoCatalog.Encounters) AssertReadableLabel(encounter.name); });
            SmokeUi.Click("덱을 섞고 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("이대로 시작") != null, 8, "Real shuffle must deal the opening hand.");
            var initial = SmokeUi.ReadSavedBattle();
            Assert.That(initial.players[0].leaderId, Is.EqualTo("kael"));
            Assert.That(initial.players[0].deckId, Is.EqualTo("knights"));
            Assert.That(initial.encounterIndex, Is.EqualTo(1));
            Assert.That(initial.openingHandOpen, Is.True);
            Assert.That(initial.players[0].hand.Count, Is.EqualTo(6));
            yield return SmokeUi.Capture("FantasyCards", "20-classic-opening-hand-mobile", 693, 390,
                () => { AssertTouchButtons("전체 손패 교체", "이대로 시작"); AssertHandNames(initial); });
            SmokeUi.Click("전체 손패 교체"); yield return null;
            var reshuffled = SmokeUi.ReadSavedBattle();
            Assert.That(reshuffled.randomState, Is.Not.EqualTo(initial.randomState));
            Assert.That(reshuffled.players[0].hand.Count, Is.EqualTo(6));
            Assert.That(reshuffled.openingHandOpen, Is.True, "Whole-hand replacement does not skip the confirmation screen.");
            Assert.That(SmokeUi.FindButton("전체 손패 교체").interactable, Is.False);
            Assert.That(reshuffled.players[0].deck.Concat(reshuffled.players[0].hand).Select(card => card.instanceId).OrderBy(id => id),
                Is.EqualTo(initial.players[0].deck.Concat(initial.players[0].hand).Select(card => card.instanceId).OrderBy(id => id)), "Whole-hand shuffle conserves all fifty physical cards.");
            SmokeUi.Click("이대로 시작");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장") != null && SmokeUi.FindButton("저장").interactable, 8, "Confirmation must enter preparation.");
            var started = SmokeUi.ReadSavedBattle();
            Assert.That(started.openingHandOpen, Is.False);
            Assert.That(started.players[0].leaderSkillUses, Is.Zero);
            Assert.That(SmokeUi.FindButton("리더 기술 사용"), Is.Null);
            ClickObject("Player leader kael"); yield return null;
            SmokeUi.AssertVisibleKorean("전투 기록");
            SmokeUi.Click("돌아가기"); yield return null;
            var card = started.players[0].hand[0];
            var pointer = new ExtendedPointerEventData(EventSystem.current) { pointerId = 42, pointerType = UIPointerType.MouseOrPen, position = new Vector2(300, 60) };
            ExecuteEvents.Execute(GameObject.Find("Card " + card.definitionId), pointer, ExecuteEvents.pointerEnterHandler);
            yield return null;
            Assert.That(GameObject.Find("Hover explanation"), Is.Not.Null, "Positive Input System mouse IDs still require hover help.");
            yield return SmokeUi.Capture("FantasyCards", "22-classic-card-hover-mobile", 693, 390, () =>
            {
                var popup = GameObject.Find("Hover explanation"); Assert.That(popup, Is.Not.Null);
                Assert.That(popup.GetComponentsInChildren<TMP_Text>().Any(label => label.text.Contains(CardCatalog.Get(card.definitionId).name)), Is.True);
            }, () => ExecuteEvents.Execute(GameObject.Find("Card " + card.definitionId), pointer, ExecuteEvents.pointerEnterHandler));
            ExecuteEvents.Execute(GameObject.Find("Card " + card.definitionId), pointer, ExecuteEvents.pointerExitHandler); yield return null;
            Assert.That(GameObject.Find("Hover explanation"), Is.Null);
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadSavedBattle()), Is.EqualTo(JsonUtility.ToJson(started)), "Inspection and hover never consume card/coin randomness.");
            LogAssert.NoUnexpectedReceived();
        }

        [UnityTest] public IEnumerator CompactDeploymentAndPreparationDriveAutomaticBattleWithoutStateInjection()
        {
            yield return SmokeUi.StartPlayableBattle(true);
            var initial = SmokeUi.ReadSavedBattle();
            var terrain = SmokeUi.TerrainForUnit(); var land = CardCatalog.Get(terrain.definitionId);
            ClickObject("Card " + land.id); yield return null;
            foreach (var size in new[] { new Vector2Int(693, 390), new Vector2Int(844, 390), new Vector2Int(1280, 720), new Vector2Int(1920, 1080) })
                yield return SmokeUi.Capture("FantasyCards", "11-classic-battle-" + size.x + "x" + size.y, size.x, size.y, () =>
                {
                    AssertTouchButtons("안내", "음악", "저장", "타이틀", "지형 전개", "준비 완료", "손패 셔플");
                    AssertHandNames(initial); AssertHandCardWidths(initial, size.x < 1400 ? .16f : .133f);
                    AssertPlayerAvatarLabels(initial); AssertArtCrops();
                });
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadSavedBattle()), Is.EqualTo(JsonUtility.ToJson(initial)), "Layout must not change saved state or RNG.");
            SmokeUi.Click("지형 전개");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장")?.interactable == true, 8, "Terrain animation must finish.");
            var engine = SmokeUi.ReadEngine();
            var unit = engine.State.players[0].hand.First(card => CardCatalog.Get(card.definitionId).kind == CardKind.Unit && engine.CanPlay(0, card.instanceId, 0, out _));
            ClickObject("Card " + unit.definitionId); yield return null; ClickObject("Lane 0 0");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장")?.interactable == true, 8, "Unit animation must finish.");
            var placed = SmokeUi.ReadSavedBattle();
            Assert.That(placed.players[0].AtLane(0)?.definitionId, Is.EqualTo(unit.definitionId));
            Assert.That(placed.players[0].hand.Count, Is.EqualTo(initial.players[0].hand.Count - 2));
            Assert.That(placed.players[0].levelPoints, Is.EqualTo(initial.players[0].levelPoints - land.landPointCost));
            Assert.That(placed.randomState, Is.EqualTo(initial.randomState), "Deployment does not roll combat coins.");
            foreach (var size in new[] { new Vector2Int(693, 390), new Vector2Int(1280, 720) })
                yield return SmokeUi.Capture("FantasyCards", "15-classic-unit-lane-" + size.x + "x" + size.y, size.x, size.y,
                    () => { AssertLaneUnitLabels(placed); AssertHandNames(placed); AssertArtCrops(); });
            int firstTurn = placed.turn;
            SmokeUi.Click("준비 완료");
            yield return SmokeUi.WaitUntil(() => SmokeUi.ReadEngine().State.turn > firstTurn && SmokeUi.ReadEngine().IsPreparing(0) && SmokeUi.FindButton("준비 완료")?.interactable == true,
                30, "Opponent preparation and the first automatic combat must hand control back for defense.");
            var defended = SmokeUi.ReadSavedBattle();
            Assert.That(defended.phase, Is.EqualTo(BattlePhase.DefensePreparation));
            Assert.That(defended.activePlayer, Is.EqualTo(1));
            Assert.That(defended.events.Count(item => item.kind == "ready"), Is.GreaterThanOrEqualTo(3));
            Assert.That(defended.events.Any(item => item.kind == "upkeep"), Is.True);
            Assert.That(defended.events.Any(item => item.kind == "phase" && item.text.StartsWith("전투 ·", StringComparison.Ordinal)), Is.True,
                "The prepared fronts must actually enter automatic combat; a legal zero-damage coin result is allowed.");
            yield return SmokeUi.Capture("FantasyCards", "23-classic-defense-preparation", 1280, 720, () => AssertTouchButtons("준비 완료", "저장", "타이틀"));
            SmokeUi.Click("타이틀"); yield return null;
            yield return SmokeUi.LoadBoot();
            SmokeUi.Click("이어하기"); yield return null;
            Assert.That(JsonUtility.ToJson(SmokeUi.ReadEngine().State), Is.EqualTo(JsonUtility.ToJson(defended)),
                "Continue must retain both preparation flags, committed spells, card zones and RNG while the human is defending.");
            Assert.That(SmokeUi.FindButton("손패 셔플").interactable, Is.True, "A later preparation has its own whole-hand shuffle opportunity.");
            SmokeUi.Click("손패 셔플");
            yield return SmokeUi.WaitUntil(() => SmokeUi.FindButton("저장")?.interactable == true, 8, "Preparation shuffle must finish before committing defense.");
            var shuffledDefense = SmokeUi.ReadSavedBattle();
            Assert.That(shuffledDefense.players[0].lastShuffleTurn, Is.EqualTo(defended.turn));
            Assert.That(shuffledDefense.randomState, Is.Not.EqualTo(defended.randomState));
            Assert.That(shuffledDefense.players[0].hand.Count, Is.EqualTo(defended.players[0].hand.Count));
            Assert.That(shuffledDefense.players[0].deck.Concat(shuffledDefense.players[0].hand).Select(card => card.instanceId).OrderBy(id => id),
                Is.EqualTo(defended.players[0].deck.Concat(defended.players[0].hand).Select(card => card.instanceId).OrderBy(id => id)));
            Assert.That(SmokeUi.FindButton("손패 셔플").interactable, Is.False, "One preparation must not allow a second shuffle.");
            SmokeUi.Click("준비 완료");
            yield return SmokeUi.WaitUntil(() => SmokeUi.ReadEngine().State.turn > defended.turn && SmokeUi.ReadEngine().IsPreparing(0) && SmokeUi.FindButton("준비 완료")?.interactable == true,
                30, "Defense completion must resolve battle and return to the player's attack preparation.");
            var next = SmokeUi.ReadSavedBattle();
            Assert.That(next.phase, Is.EqualTo(BattlePhase.Main));
            Assert.That(next.activePlayer, Is.EqualTo(0));
            Assert.That(next.players[0].hand.Count, Is.EqualTo(6));
            BattleEngine.Validate(next); SmokeUi.AssertNoMissingScripts(); LogAssert.NoUnexpectedReceived();
        }

        static Button[] VisibleCards() => UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
            .Where(button => button.gameObject.activeInHierarchy && button.name.StartsWith("Card ", StringComparison.Ordinal)).ToArray();
        static void AssertVisibleCatalog(Func<CardDefinition, bool> predicate)
        {
            var expected = CardCatalog.All.Where(predicate).Take(4).Select(card => "Card " + card.id).OrderBy(id => id);
            Assert.That(VisibleCards().Select(button => button.name).OrderBy(id => id), Is.EqualTo(expected), "Visible card tiles must follow the actual combined filters.");
        }
        static void ClickObject(string name)
        {
            var obj = GameObject.Find(name); Assert.That(obj, Is.Not.Null, name);
            var button = obj.GetComponent<Button>(); Assert.That(button, Is.Not.Null, name);
            Assert.That(button.IsActive() && button.IsInteractable(), Is.True, name);
            button.onClick.Invoke();
        }
        static void AssertLaneUnitLabels(BattleState state)
        {
            for (int owner = 0; owner < 2; owner++)
            {
                for (int index = 0; index < state.rules.unitLanes; index++)
                    Assert.That(GameObject.Find("Lane " + owner + " " + index), Is.Not.Null, "All five ordered front-line slots must be visible.");
                foreach (var unit in state.players[owner].units)
                {
                    var lane = GameObject.Find("Lane " + owner + " " + unit.lane);
                    var card = CardCatalog.Get(unit.definitionId);
                    var labels = lane.GetComponentsInChildren<TMP_Text>();
                    foreach (string value in new[] { card.name, "AP " + CardCatalog.UnitAttack(unit) + "  DP " + CardCatalog.UnitDefense(unit) + "  HP " + CardCatalog.UnitHealth(unit) })
                    {
                        var label = labels.SingleOrDefault(text => text.text == value);
                        Assert.That(label, Is.Not.Null, "The deployed original card must show " + value);
                        AssertReadableLabel(label);
                    }
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
                    // The player must be able to read play requirements and rarity
                    // on the hand card itself, without opening its inspector.
                    string stats = definition.kind == CardKind.Unit
                        ? "AP " + definition.attack + "  DP " + definition.defense + "  HP " + definition.health
                        : CardCatalog.KindName(definition.kind) + " · " + CardCatalog.ElementName(definition.element);
                    foreach (string value in new[] { definition.name, "Lv " + definition.level,
                        definition.coloredCost + "+" + definition.genericCost, CardCatalog.RarityCode(definition.rarity), stats })
                    {
                        var labels = button.GetComponentsInChildren<TMP_Text>().Where(text => text.text == value).ToArray();
                        Assert.That(labels.Length, Is.EqualTo(1), definition.name + " hand label: " + value);
                        AssertReadableLabel(labels[0]);
                    }
                }
            }
        }
        static void AssertPlayerAvatarLabels(BattleState state)
        {
            var leader = DemoCatalog.GetLeader(state.players[0].leaderId);
            var panel = GameObject.Find("Player leader " + leader.id);
            Assert.That(panel, Is.Not.Null);
            foreach (string value in new[] { leader.name, "전투 기록" })
            {
                var label = panel.GetComponentsInChildren<TMP_Text>().SingleOrDefault(text => text.text == value);
                Assert.That(label, Is.Not.Null, value + " must identify the avatar and its available action.");
                AssertReadableLabel(label);
            }
        }
        static void AssertHandCardWidths(BattleState state, float cardWidthFraction)
        {
            var cards = VisibleCards();
            Assert.That(cards.Length, Is.EqualTo(state.players[0].hand.Count));
            foreach (var card in cards)
            {
                var rect = (RectTransform)card.transform;
                var canvas = card.GetComponentInParent<Canvas>().rootCanvas;
                var corners = new Vector3[4]; rect.GetWorldCorners(corners);
                var camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
                // Measure the complete card rectangle, including cards partly or
                // wholly outside the scroll viewport. Hover scaling is a separate
                // effect; it must not hide a stale layout width after resizing.
                float renderedWidth = Mathf.Abs(RectTransformUtility.WorldToScreenPoint(camera, corners[2]).x -
                    RectTransformUtility.WorldToScreenPoint(camera, corners[0]).x);
                float layoutWidth = renderedWidth / Mathf.Abs(rect.localScale.x);
                float expected = canvas.pixelRect.width * cardWidthFraction * .95f;
                Assert.That(layoutWidth, Is.EqualTo(expected).Within(1f), card.name + " must keep its hand-card width after resize to " + canvas.pixelRect.size);
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
            var canvas = text.GetComponentInParent<Canvas>().rootCanvas;
            // TMP's SDF character quad includes material padding beyond ink metrics
            // (TextMeshProUGUI.GenerateTextMesh: top_left/bottom_left/top_right).
            // Allow one display pixel for that fringe and fractional rasterization,
            // consistently across CanvasScaler resolutions; glyph completeness and
            // the 12 px reading size remain separate strict requirements.
            float fringe = 1f / canvas.scaleFactor;
            foreach (var glyph in glyphs)
            {
                Assert.That(glyph.bottomLeft.x, Is.GreaterThanOrEqualTo(bounds.xMin - fringe), text.text + " left glyph edge");
                Assert.That(glyph.topRight.x, Is.LessThanOrEqualTo(bounds.xMax + fringe), text.text + " right glyph edge");
                Assert.That(glyph.bottomLeft.y, Is.GreaterThanOrEqualTo(bounds.yMin - fringe), text.text + " bottom glyph edge");
                Assert.That(glyph.topRight.y, Is.LessThanOrEqualTo(bounds.yMax + fringe), text.text + " top glyph edge");
            }
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
                .Where(button => button.gameObject.activeInHierarchy && button.gameObject.name.StartsWith("Card ", StringComparison.Ordinal)).ToArray();
            int visibleCount = 0;
            foreach (var card in cards)
            {
                var shape = card.GetComponent<UiRoundedImage>();
                Assert.That(shape, Is.Not.Null, "Cards use a curved silhouette, not a rectangular frame over square art.");
                Assert.That(card.GetComponent<Mask>()?.enabled, Is.True, "Artwork and labels must clip to the card silhouette.");
                var frame = card.transform.Find("Fine rounded frame")?.GetComponent<UiRoundedImage>();
                Assert.That(frame, Is.Not.Null);
                if (shape.canvasRenderer.cull)
                {
                    Assert.That(card.GetComponentInParent<ScrollRect>(), Is.Not.Null,
                        "Only cards outside the hand's scroll viewport may defer their rendered mesh.");
                    continue;
                }
                visibleCount++;
                var mesh = shape.canvasRenderer.GetMesh();
                Assert.That(mesh, Is.Not.Null); Assert.That(mesh.vertexCount, Is.GreaterThan(16));
                var rect = shape.rectTransform.rect;
                var corner = new Vector2(rect.xMin, rect.yMin);
                Assert.That(mesh.vertices.All(vertex => Vector2.Distance(new Vector2(vertex.x, vertex.y), corner) > 1), Is.True,
                    "The rendered card geometry must leave the square corner outside its curved silhouette.");
                Assert.That(frame.canvasRenderer.GetMesh()?.vertexCount, Is.GreaterThan(16));
            }
            if (cards.Length > 0) Assert.That(visibleCount, Is.GreaterThan(0), "The current card screen must render at least one actual card.");
        }
        static void AssertArtCrops()
        {
            AssertRoundedCards();
            var backdrop = Resources.Load<Texture2D>("Art/battle-background");
            Assert.That(backdrop, Is.Not.Null);
            var assets = Resources.LoadAll<FantasyCards.Data.CardAsset>("Data/Cards");
            Assert.That(assets.Length, Is.EqualTo(96));
            foreach (var card in CardCatalog.All)
            {
                var asset = assets.Single(item => item.stableId == card.id);
                var texture = Resources.Load<Texture2D>(card.artResource);
                Assert.That(texture, Is.Not.Null, card.name + " original image");
                Assert.That(texture.width, Is.GreaterThanOrEqualTo(200));
                Assert.That(texture.height, Is.GreaterThanOrEqualTo(300));
                Assert.That(asset.illustration, Is.Not.Null);
                Assert.That(asset.illustration.texture, Is.SameAs(texture));
                Assert.That(asset.illustration.rect, Is.EqualTo(new Rect(0, 0, texture.width, texture.height)), "Store the original full-card bounds; artwork crops belong to UI only.");
            }
            var cardImages = assets.Select(asset => asset.illustration.texture).ToArray();
            var portraits = DemoCatalog.Leaders.Select(leader => Resources.Load<Texture2D>("Art/portrait-" + leader.id)).ToArray();
            Assert.That(portraits.All(portrait => portrait != null), Is.True);
            var crops = UnityEngine.Object.FindObjectsByType<IllustrationCrop>(FindObjectsSortMode.None).Where(crop => crop.gameObject.activeInHierarchy).ToArray();
            Assert.That(crops.Length, Is.GreaterThan(0));
            Assert.That(crops.Any(crop => crop.GetComponent<RawImage>().texture == backdrop), Is.True);
            foreach (var crop in crops)
            {
                var image = crop.GetComponent<RawImage>(); var source = crop.SourceUv; var uv = image.uvRect;
                Assert.That(image.texture, Is.Not.Null);
                Assert.That(source.xMin, Is.GreaterThanOrEqualTo(0)); Assert.That(source.yMin, Is.GreaterThanOrEqualTo(0));
                Assert.That(source.xMax, Is.LessThanOrEqualTo(1)); Assert.That(source.yMax, Is.LessThanOrEqualTo(1));
                if (cardImages.Contains(image.texture)) Assert.That(source.width * source.height, Is.GreaterThan(.3f), "Original artwork must retain a useful contiguous region.");
                else if (image.texture == backdrop || portraits.Contains(image.texture)) Assert.That(source, Is.EqualTo(new Rect(0, 0, 1, 1)));
                Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(source.xMin - .00001f)); Assert.That(uv.yMin, Is.GreaterThanOrEqualTo(source.yMin - .00001f));
                Assert.That(uv.xMax, Is.LessThanOrEqualTo(source.xMax + .00001f)); Assert.That(uv.yMax, Is.LessThanOrEqualTo(source.yMax + .00001f));
                var frame = ((RectTransform)crop.transform).rect;
                Assert.That(uv.width * image.texture.width / (uv.height * image.texture.height), Is.EqualTo(frame.width / frame.height).Within(.002f), "Artwork keeps its source proportions.");
            }
        }
    }
}
