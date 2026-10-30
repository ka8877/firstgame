using System;
using System.Linq;
using FantasyCards.Core;
using FirstGame.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // The archived 2005 official manual supplies the room list, right rail,
    // engraved frame and game-room hierarchy. All buttons below drive live state.
    public sealed partial class FantasyCardsApp
    {
        private bool originalMusicMuted;
        private float originalMusicVolume;

        private void InitializeOriginalMusic()
        {
            originalMusicMuted = FmOriginalMusic.Muted;
            originalMusicVolume = FmOriginalMusic.Volume;
            FmOriginalMusic.SetMuted(originalMusicMuted);
            FmOriginalMusic.SetVolume(originalMusicVolume);
        }

        private void RevivalBackdrop(bool calm)
        {
            DetailPanorama(true);
            Gradient(root, 0, 0, 1, 1, new Color(.025f, .035f, .09f, calm ? .64f : .82f),
                new Color(.045f, .035f, .11f, calm ? .75f : .88f), true);
        }

        private void SkinFragment(Transform parent, Texture2D source, Rect pixelsFromTop, float x0, float y0, float x1, float y1)
        {
            var image = new GameObject("Original client decorative fragment", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            var rect = (RectTransform)image.transform; rect.SetParent(parent, false); Anchor(rect, x0, y0, x1, y1);
            image.texture = source; image.raycastTarget = false;
            image.uvRect = new Rect(pixelsFromTop.x / source.width, 1 - pixelsFromTop.yMax / source.height, pixelsFromTop.width / source.width, pixelsFromTop.height / source.height);
        }

        private void ClientBrand(Transform parent, float x0, float y0, float x1, float y1)
        {
            var title = Text(parent, canvas.pixelRect.width < 900 ? "FM" : "Fantasy Masters", x0, Mathf.Lerp(y0, y1, .18f), x1, y1, 43, ivory, TextAlignmentOptions.Center);
            title.gameObject.name = "Client brand";
            title.font = Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font;
            title.fontStyle = FontStyles.Normal;
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.characterSpacing = .8f;
            title.fontSizeMin = Mathf.Max(14, 12 / Mathf.Max(.01f, canvas.scaleFactor)); title.fontSizeMax = Mathf.Max(43, title.fontSizeMin);
        }

        private void ClientRail(Action back, string heading = "SINGLE PLAY")
        {
            DrawDetailClientRail(back, heading);
        }

        private void ShowRevivalTitle()
        {
            ShowDetailTitle();
        }

        private void ShowLobby()
        {
            if (questFreePracticeView) ShowDetailLobby(); else ShowQuestEntry();
        }

        private void ShowRevivalRoom()
        {
            battleScreen = false; redraw = ShowEncounterSelection; Clear(); FmOriginalMusic.SetScene("lobby"); RestorationBackdrop(); ClientRail(ShowLobby, "대전 준비실");
            var opponent = DemoCatalog.GetEncounter(selectedEncounter);
            var scenario = FmScenarioCatalog.Stages.FirstOrDefault(stage => stage.encounterIndex == selectedEncounter);
            var room = RestorationPanel(root, .048f, .305f, .744f, .936f, RestorationLeather, "Classic game waiting room");
            Text(room, scenario != null ? "SCENARIO  /  " + scenario.name : "GAME ROOM  /  연습 대전실 " + (selectedEncounter + 1).ToString("00"), .045f, .83f, .955f, .96f, 32, gold);
            Text(room, "PLAYER", .045f, .70f, .42f, .83f, 22, RestorationMetal);
            Text(room, "DECK", .45f, .70f, .95f, .83f, 22, RestorationMetal);
            string deck = useCustomDeck ? "내 편집 덱" : DemoCatalog.GetDeck(selectedDeckId).name;
            RoomPlayerRow(room, LocalProfileName, deck, .48f, false);
            RoomPlayerRow(room, "컴퓨터", DemoCatalog.GetDeck(opponent.deckId).name, .27f, true);
            Text(room, "덱 승리 · 50장 · 손패 6장 · 전열 5칸\n공격 준비 → 방어 준비 → 배틀 → 소울코스트", .045f, .045f, .95f, .25f, 21, new Color(.79f, .75f, .63f), TextAlignmentOptions.Center);
            RestorationButton(root, "START · 대전 시작", .51f, .19f, .744f, .285f, BeginSelectedBattle, true);
            RestorationButton(root, "덱 선택", .048f, .19f, .27f, .285f, ShowDeckSelection);
            RestorationButton(root, "게임 방법", .28f, .19f, .50f, .285f, () => ShowRulesHelp(ShowEncounterSelection));
            var notice = RestorationPanel(root, .048f, .05f, .744f, .17f, new Color(.045f, .035f, .028f), "Waiting room rules notice");
            Text(notice, scenario != null ? scenario.objective : opponent.description, .025f, .40f, .975f, .94f, 23, ivory);
            Text(notice, "지형과 소울을 준비하고 다섯 칸의 전열로 상대 덱을 소진시키세요.", .025f, .04f, .975f, .46f, 21, RestorationMetal);
        }

        private void RoomPlayerRow(Transform parent, string name, string deck, float y, bool opponent)
        {
            var row = RestorationPanel(parent, .045f, y, .955f, y + .19f, new Color(.13f, .115f, .085f), "Waiting player row");
            Text(row, name, .035f, .06f, .40f, .94f, 29, opponent ? teal : ivory);
            Text(row, deck, .45f, .06f, .97f, .94f, 24, ivory);
        }

        private void ShowRevivalDecks() { ShowArcanaDecks(); }

        private static string DeckShortName(string id)
        {
            return string.Join(" · ", DemoCatalog.GetDeck(id).cards.Select(CardCatalog.Get)
                .Where(card => card.kind == CardKind.Terrain).Select(card => card.element).Distinct().Select(CardCatalog.ElementName));
        }
        private void SetOriginalVolume(float value)
        {
            originalMusicVolume = Mathf.Clamp01(value);
            FmOriginalMusic.SetVolume(originalMusicVolume);
        }

        private void OriginalLoginCharacter()
        {
            string[] names = { "02", "04", "06", "08" };
            var images = names.Select(name => Resources.Load<Texture2D>("Art/Revival/login-" + name)).ToArray();
            if (images.Any(texture => texture == null)) return;
            var bounds = new GameObject("Original login art bounds", typeof(RectTransform)).GetComponent<RectTransform>();
            bounds.SetParent(root, false); Anchor(bounds, .69f, .15f, .955f, .94f);
            var holder = Panel(bounds, 0, 0, 1, 1, new Color(.72f, .72f, .72f));
            holder.gameObject.name = "Original 2005 login character";
            var fit = holder.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            int height = images.Sum(texture => texture.height); fit.aspectRatio = (float)images[0].width / height;
            float top = 1;
            foreach (var texture in images)
            {
                float bottom = top - (float)texture.height / height;
                var image = new GameObject("Original login tile " + texture.name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(holder, false); Anchor((RectTransform)image.transform, 0, bottom, 1, top); image.texture = texture; image.raycastTarget = false; top = bottom;
            }
            Frame(holder, gold, 2);
        }

        private void PrintedImage(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1)
        {
            RenewalSourcePrint(parent, card.artResource, x0, y0, x1, y1, "Original printed card");
        }

        private void DrawPrintedCard(Transform parent, CardDefinition card, float x0, float y0, float x1, float y1, bool selected, Action onClick, bool interactable)
        {
            DrawArcanaCard(parent, card, x0, y0, x1, y1, selected, onClick, interactable);
        }

        private TMP_Text MiniCardText(Transform parent, string value, float x0, float y0, float x1, float y1, float size, Color tint, string name)
        {
            var label = Text(parent, value, x0, y0, x1, y1, size, tint, TextAlignmentOptions.Center);
            label.gameObject.name = name;
            label.font = font; label.fontStyle = FontStyles.Normal;
            label.textWrappingMode = TextWrappingModes.NoWrap;
            label.characterWidthAdjustment = 0;
            label.GetComponent<UiCanvasTextSize>().Configure(size, true, true);
            // Compact numeric cells use glyph bounds, not a whole font ascent
            // line, to avoid dropping every glyph in an otherwise legible row.
            label.overflowMode = TextOverflowModes.Overflow;
            return label;
        }

        private void DrawRevivalBattleRail()
        {
            var player = BattleDisplayState.players[0]; var enemy = BattleDisplayState.players[1];
            var rail = Panel(root, .817f, .025f, .979f, .977f, new Color(.05f, .08f, .12f, .98f)); Frame(rail, gold, 2); Ornament(rail, gold);
            rail.gameObject.name = "Battle resource rail";
            if (canvas.pixelRect.height < 600 && canvas.pixelRect.width < 900)
            { DrawCompactBattleRail(rail); return; }
            DrawChronicleBattleBrand(rail, .04f, .91f, .96f, .984f);
            var phase = Panel(rail, .04f, .837f, .96f, .905f, new Color(.11f, .17f, .22f));
            ReadableText(phase, BattleDisplayState.turn + "턴 · " + (locked ? "공방 판정 중" : engine.PreparingPlayer == 1 ? "상대 준비 중" : PhaseName(BattleDisplayState.phase)), .02f, .06f, .98f, .94f, 23, gold, TextAlignmentOptions.Center);
            var lp = Panel(rail, .04f, .758f, .485f, .828f, new Color(.085f, .16f, .18f)); Frame(lp, new Color(.36f, .43f, .29f), 1);
            ReadableText(lp, "LP  " + player.levelPoints, .04f, .02f, .96f, .98f, 25, teal, TextAlignmentOptions.Center);
            var total = Panel(rail, .515f, .758f, .96f, .828f, new Color(.18f, .17f, .13f)); Frame(total, new Color(.49f, .37f, .18f), 1);
            ReadableText(total, "총소울 " + player.genericSoul, .04f, .02f, .96f, .98f, 23, gold, TextAlignmentOptions.Center);
            BindTooltip(lp.gameObject, "레벨 포인트", "지형을 전개할 때 사용합니다. 현재 LP " + player.levelPoints);
            BindTooltip(total.gameObject, "총소울", "카드 사용과 유닛 유지에 필요한 총소울입니다. 색 소울 조건도 함께 충족해야 합니다.");
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                int col = i % 2, row = i / 2;
                float x = .04f + col * .47f, y = .692f - row * .059f;
                var orb = Panel(rail, x, y, x + .445f, y + .052f, new Color(.08f, .125f, .17f));
                ElementIcon(orb, elements[i], .025f, .14f, .23f, .86f);
                ReadableText(orb, CardCatalog.ElementName(elements[i]), .26f, .06f, .68f, .94f, 20, ElementColor(elements[i]));
                ReadableText(orb, player.Resource(elements[i]).ToString(), .68f, .05f, .96f, .95f, 25, ivory, TextAlignmentOptions.MidlineRight);
                BindTooltip(orb.gameObject, CardCatalog.ElementName(elements[i]) + " 소울", "현재 사용할 수 있는 색 소울 " + player.Resource(elements[i]));
            }
            ReadableText(rail, "지형 " + player.terrain.Count + " / " + BattleDisplayState.rules.terrainLimit, .525f, .515f, .955f, .568f, 19, teal, TextAlignmentOptions.Center);
            var decks = Panel(rail, .04f, .365f, .96f, .507f, new Color(.075f, .12f, .16f)); Frame(decks, new Color(.43f, .35f, .20f), 1);
            var opponent = Button(decks, "상대 정보 · 덱 " + enemy.deck.Count, .025f, .53f, .975f, .98f,
                () => ShowPolishBattleDesk(), slate, 21, !locked);
            opponent.gameObject.name = "Opponent information and battle desk";
            BindTooltip(opponent.gameObject, "상대 정보 · 전투 기록 · 연습 메모", "AI 상대의 공개 정보와 실제 전투 기록을 확인하고 개인 메모를 남깁니다.");
            ReadableText(decks, "내 덱 " + player.deck.Count, .025f, .035f, .515f, .485f, 24, ivory, TextAlignmentOptions.Center);
            var discard = Button(decks, "소각 " + player.discard.Count, .53f, .035f, .975f, .485f, () => { battleArchivePage = 0; ShowBattleArchive(0); }, slate, 23, !locked);
            BindDiscardDrop((RectTransform)discard.transform);
            BindTooltip(discard.gameObject, "소각장 · 손패 소각", "눌러 소각장을 봅니다. 준비 중 손패 카드를 이곳으로 끌어놓아 소각할 수 있습니다. 업그레이드 카드는 소각 시 총소울 +1을 얻습니다.");
            Button(rail, "기록 / 메뉴", .04f, .286f, .96f, .354f, ShowRevivalBattleMenu, slate, 21, !locked);
            var selected = player.hand.Find(card => card.instanceId == selectedCard);
            var definition = selected == null ? null : CardCatalog.Get(selected.definitionId);
            bool myPreparation = engine.IsPreparing(0);
            string action = definition != null && definition.kind == CardKind.Terrain ? "지형 전개" : definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition) ? "마법 예약" : "카드 상세";
            Button(rail, action, .04f, .207f, .96f, .275f, () => { if (action == "카드 상세") ShowDetail(CardCatalog.Get(inspectedId), ShowBattle); else Play(-1); }, slate, 22, !locked && myPreparation);
            bool canShuffle = engine.CanShuffleHand(0, out string shuffleReason);
            var shuffle = Button(rail, "손패 셔플", .04f, .128f, .96f, .196f, () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, slate, 22, !locked && canShuffle);
            BindTooltip(shuffle.gameObject, "손패 셔플", canShuffle ? "손패를 덱에 돌려놓고 6장을 다시 뽑습니다." : shuffleReason);
            if (!battlePlaybackActive) Button(rail, "준비 완료", .04f, .035f, .96f, .116f, EndTurn, gold, 27, !locked && myPreparation);
        }

        private void DrawCompactBattleRail(RectTransform rail)
        {
            var player = BattleDisplayState.players[0]; var enemy = BattleDisplayState.players[1];
            float height = Mathf.Max(1, canvas.pixelRect.height * .952f);
            float cursor = 1 - 4 / height;
            Vector2 Row(float gap = 1)
            { var row = new Vector2(cursor - 22 / height, cursor); cursor = row.x - gap / height; return row; }
            RectTransform Plate(Vector2 row, float left = .03f, float right = .97f)
            { return Panel(rail, left, row.x, right, row.y, new Color(.08f, .125f, .17f)); }
            TMP_Text Label(Transform parent, string value, string role, float left = .02f, float right = .98f, Color? tint = null)
            { return MiniCardText(parent, value, left, .02f, right, .98f, 23, tint ?? ivory, "Rail " + role); }
            void ActionButton(string label, Vector2 row, Action action, Color color, bool enabled)
            {
                var button = Button(rail, label, .03f, row.x, .97f, row.y, action, color, 23, enabled);
                var text = button.GetComponentInChildren<TMP_Text>();
                text.gameObject.name = "Rail button " + label; text.textWrappingMode = TextWrappingModes.NoWrap; text.characterWidthAdjustment = 0;
            }

            // The 616x346 game canvas leaves a 100px-wide rail. Reserve real
            // line height and give text the icon space, keeping all full names.
            var brandRow = Row(); DrawChronicleBattleBrand(rail, .03f, brandRow.x, .97f, brandRow.y);
            Label(Plate(Row()), BattleDisplayState.turn + "턴 · " + (locked ? "공방 판정 중" : engine.PreparingPlayer == 1 ? "상대 준비 중" : PhaseName(BattleDisplayState.phase)), "phase", tint: gold);
            var points = Row();
            var lp = Plate(points, .03f, .49f); Label(lp, "LP" + player.levelPoints, "LP", tint: teal);
            var total = Plate(points, .51f, .97f); Label(total, "총소울" + player.genericSoul, "total soul", tint: gold);
            BindTooltip(lp.gameObject, "레벨 포인트", "지형을 전개할 때 사용합니다. 현재 LP " + player.levelPoints);
            BindTooltip(total.gameObject, "총소울", "카드 사용과 유닛 유지에 필요한 총소울입니다. 색 소울 조건도 함께 충족해야 합니다.");
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int rowIndex = 0; rowIndex < 4; rowIndex++)
            {
                var row = Row(1);
                for (int col = 0; col < 2; col++)
                {
                    int index = rowIndex * 2 + col;
                    float cellLeft = rowIndex == 3 ? (col == 0 ? .03f : .445f) : .03f + col * .48f;
                    float cellRight = rowIndex == 3 ? (col == 0 ? .435f : .97f) : .49f + col * .48f;
                    var cell = Plate(row, cellLeft, cellRight);
                    if (index < elements.Length)
                    {
                        var element = elements[index];
                        Label(cell, CardCatalog.ElementName(element), "soul name " + element, .03f, .64f, ElementColor(element));
                        Label(cell, player.Resource(element).ToString(), "soul value " + element, .66f, .99f);
                        BindTooltip(cell.gameObject, CardCatalog.ElementName(element) + " 소울", "현재 사용할 수 있는 색 소울 " + player.Resource(element));
                    }
                    else Label(cell, "지형" + player.terrain.Count + "/" + BattleDisplayState.rules.terrainLimit, "terrain", .01f, .99f, teal);
                }
            }
            ActionButton("상대 정보 · " + enemy.deck.Count, Row(1), () => ShowPolishBattleDesk(), slate, !locked);
            var zoneRow = Row(1);
            Label(Plate(zoneRow, .03f, .49f), "덱 " + player.deck.Count, "player deck");
            var discard = Button(rail, "소각 " + player.discard.Count, .51f, zoneRow.x, .97f, zoneRow.y,
                () => { battleArchivePage = 0; ShowBattleArchive(0); }, slate, 23, !locked);
            BindDiscardDrop((RectTransform)discard.transform);
            BindTooltip(discard.gameObject, "소각장 · 손패 소각", "눌러 소각장을 보거나, 준비 중 손패를 끌어놓아 소각합니다. 업그레이드 소각은 총소울 +1입니다.");
            ActionButton("기록 / 메뉴", Row(), ShowRevivalBattleMenu, slate, !locked);
            var selected = player.hand.Find(card => card.instanceId == selectedCard);
            var definition = selected == null ? null : CardCatalog.Get(selected.definitionId);
            bool myPreparation = engine.IsPreparing(0);
            string action = definition != null && definition.kind == CardKind.Terrain ? "지형 전개" : definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition) ? "마법 예약" : "카드 상세";
            ActionButton(action, Row(), () => { if (action == "카드 상세") ShowDetail(CardCatalog.Get(inspectedId), ShowBattle); else Play(-1); }, slate, !locked && myPreparation);
            bool canShuffle = engine.CanShuffleHand(0, out string shuffleReason);
            ActionButton("손패 셔플", Row(), () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, slate, !locked && canShuffle);
            var readyRow = Row();
            if (!battlePlaybackActive) ActionButton("준비 완료", readyRow, EndTurn, gold, !locked && myPreparation);
            if (!battlePlaybackActive) Label(Plate(Row()), "AP / DP / HP", "hand stat legend", tint: new Color(.70f, .81f, .94f));
        }

        private void ShowRevivalBattleMenu()
        {
            battleScreen = false; redraw = ShowRevivalBattleMenu; Clear(); polishBack = ShowBattle; ResetPolishTap(); Backdrop(true);
            Text(root, "전투 메뉴", .07f, .81f, .93f, .95f, 43, gold, TextAlignmentOptions.Center);
            Button(root, "전투 기록", .18f, .61f, .48f, .75f, ShowBattleLog, slate, 27);
            Button(root, "음악", .52f, .61f, .82f, .75f, () => ShowSoundSettings(ShowRevivalBattleMenu), slate, 27);
            Button(root, "저장", .18f, .42f, .48f, .56f, () => { if (Save()) message = "전장과 다음 코인 상태를 저장했습니다."; ShowRevivalBattleMenu(); }, slate, 27);
            Button(root, "대기실", .52f, .42f, .82f, .56f, () => LeaveBattle(ShowLobby), slate, 27);
            Button(root, "소각장 / 상태", .18f, .225f, .48f, .365f, () => { battleArchivePage = 0; ShowBattleArchive(); }, slate, 25);
            Button(root, "전투로", .52f, .225f, .82f, .365f, ShowBattle, gold, 29);
            Text(root, StatusMessage(), .08f, .05f, .92f, .15f, 24, ivory, TextAlignmentOptions.Center);
        }

    }
}
