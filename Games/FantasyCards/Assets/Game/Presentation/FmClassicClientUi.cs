using System;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // Live client windows follow the retained December 2017 FM1 room screen and
    // the My Information window in the 2018 retrospective. The original web
    // illustration belongs to the entry screen, never behind client controls.
    public sealed partial class FantasyCardsApp
    {
        static readonly Color ClassicBlack = new Color(.012f, .013f, .014f);
        static readonly Color ClassicRed = new Color(.25f, .055f, .055f);
        static readonly Color ClassicSteel = new Color(.46f, .47f, .45f);
        static readonly Color ClassicBlue = new Color(.065f, .16f, .20f);
        const string ProfileGreetingKey = "FC.profile.greeting";
        public string LocalProfileGreeting => SafeProfileGreeting(PlayerPrefs.GetString(ProfileGreetingKey, ""));

        static string SafeProfileGreeting(string value) => new string((value ?? "").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(15).ToArray()).Trim();

        void ClassicDesktop()
        {
            Panel(root, 0, 0, 1, 1, ClassicBlack);
            var rim = Panel(root, .007f, .008f, .993f, .992f, Color.clear);
            rim.name = "Late FM1 black and red client frame";
            Frame(rim, ClassicRed, 2);
            RestorationTrim(rim);
            if (QuestCurrentScreen != "title")
                Gradient(root, .012f, .905f, .988f, .987f, new Color(.10f, .105f, .105f), new Color(.015f, .018f, .020f));
        }

        RectTransform ClassicWindow(Transform parent, string name, float x, float y, float xx, float yy, bool paper = false)
        {
            var panel = Panel(parent, x, y, xx, yy, paper ? new Color(.28f, .27f, .23f) : new Color(.021f, .023f, .024f));
            panel.name = name;
            panel.GetComponent<UiRoundedImage>().Radius = 0;
            Frame(panel, paper ? new Color(.58f, .56f, .46f) : ClassicSteel, 1.5f);
            RestorationTrim(panel);
            return panel;
        }

        Button ClassicCapsule(Transform parent, string label, float x, float y, float xx, float yy, Action action, bool enabled = true, bool selected = false)
        {
            var button = Button(parent, label, x, y, xx, yy, action, selected ? new Color(.27f, .11f, .10f) : ClassicBlue, 23, enabled);
            var shape = button.GetComponent<UiRoundedImage>(); shape.Radius = 11;
            shape.TopTint = new Color(1.9f, 1.8f, 1.65f); shape.BottomTint = new Color(.35f, .40f, .43f);
            Frame((RectTransform)button.transform, new Color(.52f, .57f, .55f), 1.5f);
            return button;
        }

        void ClassicWordmark(Transform parent, float x, float y, float xx, float yy)
        {
            var reference = Resources.Load<Texture2D>("Art/FmClassic/late-client-reference");
            if (reference != null && reference.width == 740 && reference.height == 555)
            {
                // Preserve the recognisable original red-wing logo. This UV
                // region excludes the neighbouring controls and all old names.
                var bounds = Panel(parent, x, y, xx, yy, Color.clear);
                bounds.name = "Classic FM client wordmark";
                var logo = new GameObject("Original red-wing FM logo", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                logo.transform.SetParent(bounds, false); Anchor((RectTransform)logo.transform, 0, 0, 1, 1);
                logo.texture = reference; logo.uvRect = new Rect(0, 497f / 555, 236f / 740, 58f / 555); logo.raycastTarget = false;
                var fit = logo.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent; fit.aspectRatio = 236f / 58;
                return;
            }
            var shadow = Text(parent, "FANTASY\nMASTERS", x + .003f, y - .003f, xx + .003f, yy - .003f, 42, new Color(.31f, .025f, .018f), TextAlignmentOptions.Center);
            shadow.name = "Classic FM wordmark shadow";
            var name = Text(parent, "FANTASY\nMASTERS", x, y, xx, yy, 42, new Color(.96f, .62f, .23f), TextAlignmentOptions.Center);
            name.font = Resources.Load<TMP_FontAsset>("Fonts/Display") ?? font; name.fontStyle = FontStyles.Bold | FontStyles.Italic;
            name.lineSpacing = -24; name.characterSpacing = 2;
            name.name = "Classic FM client wordmark";
        }

        void ClassicTopbar(bool owned)
        {
            ClassicWordmark(root, .02f, .895f, .285f, .987f);
            string[] labels = { "대기실", owned ? "덱 편집" : "출전 카드", "카드 앨범", "카드 상점", "내정보" };
            Action[] actions = { owned ? (Action)ShowQuestLobby : ShowFreePracticeLobby,
                owned ? (Action)(() => ShowQuestOwnedCards(true)) : () => ShowCollection(false),
                ShowRestorationOriginalArchive, ShowQuestShop, () => ShowLocalProfile(redraw ?? ShowLobby) };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = .318f + i * .129f;
                ClassicCapsule(root, labels[i], x, .923f, x + .123f, .976f, actions[i], !(i == 4 && QuestCurrentScreen == "profile"));
            }
        }

        void ClassicScreenHeader(string title, string subtitle)
        {
            ClassicWordmark(root, .038f, .902f, .31f, .979f);
            var strip = ClassicWindow(root, "Classic client page title", .33f, .916f, .744f, .968f);
            Text(strip, title, .04f, .02f, .96f, .98f, 26, ivory);
            RestageText(root, title, .045f, .837f, .744f, .907f, 35, ivory, "Restage screen title");
            RestageText(root, subtitle, .045f, .779f, .744f, .836f, 22, RestorationMetal, "Restage screen subtitle");
            RestageHairline(root, .045f, .767f, .744f, new Color(.36f, .12f, .11f));
        }

        void ClassicOriginalCharacter(Transform parent, float x, float y, float xx, float yy, bool clientPortrait = true)
        {
            var bounds = ClassicWindow(parent, "Original FM character portrait", x, y, xx, yy);
            var lateClient = clientPortrait ? Resources.Load<Texture2D>("Art/FmClassic/late-client-reference") : null;
            if (lateClient != null && lateClient.width == 740 && lateClient.height == 555)
            {
                // This unchanged retained JPEG is a reference capture. Sample
                // only the unobstructed character artwork: the source logo,
                // old UI, profile, names, chat and bottom labels stay outside.
                var portrait = Panel(bounds, .015f, .015f, .985f, .985f, Color.clear);
                portrait.name = "Original late client character artwork";
                var image = new GameObject("Bounded original hero art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(portrait, false); Anchor((RectTransform)image.transform, 0, 0, 1, 1);
                image.texture = lateClient; image.uvRect = new Rect(8f / 740, (555f - 422) / 555, 222f / 740, 360f / 555);
                image.raycastTarget = false;
                var ratio = image.gameObject.AddComponent<AspectRatioFitter>(); ratio.aspectMode = AspectRatioFitter.AspectMode.FitInParent; ratio.aspectRatio = 222f / 360;
                return;
            }
            string[] tiles = { "02", "04", "06", "08" };
            var images = tiles.Select(n => Resources.Load<Texture2D>("Art/Revival/login-" + n)).ToArray();
            if (images.Any(t => t == null)) return;
            var holder = Panel(bounds, .015f, .015f, .985f, .985f, Color.clear);
            var fit = holder.gameObject.AddComponent<AspectRatioFitter>(); fit.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
            int height = images.Sum(t => t.height); fit.aspectRatio = images[0].width / (float)height;
            float top = 1;
            foreach (var texture in images)
            {
                float bottom = top - texture.height / (float)height;
                var image = new GameObject("Original FM character tile " + texture.name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(holder, false); Anchor((RectTransform)image.transform, 0, bottom, 1, top);
                image.texture = texture; image.raycastTarget = false; top = bottom;
            }
        }

        void DrawClassicTitle()
        {
            QuestCurrentScreen = "title"; battleScreen = false; redraw = ShowTitle;
            Clear(); UpdateWorld(); FmOriginalMusic.SetScene("login"); ClassicDesktop();
            ClassicWordmark(root, .34f, .715f, .93f, .955f);
            ClassicOriginalCharacter(root, .055f, .12f, .325f, .905f, false);
            var login = ClassicWindow(root, "Classic local client entry", .385f, .233f, .905f, .705f);
            Text(login, "판타지마스터즈", .055f, .80f, .945f, .96f, 33, gold, TextAlignmentOptions.Center);
            Text(login, "로컬 플레이어", .07f, .62f, .36f, .76f, 23, RestorationMetal);
            var player = ClassicWindow(login, "Saved local login identity", .38f, .62f, .93f, .76f, true);
            Text(player, LocalProfileName, .04f, .05f, .96f, .95f, 25, ivory, TextAlignmentOptions.Center);
            ClassicCapsule(login, "게임 시작", .07f, .385f, .93f, .565f, EnterQuestGame);
            ClassicCapsule(login, "전투 이어하기", .07f, .17f, .93f, .34f, Continue, HasAnyBattleSave);
            Text(login, "싱글 플레이 · 멀티플레이 추후 업데이트", .065f, .025f, .935f, .145f, 20, RestorationMetal, TextAlignmentOptions.Center);
            ClassicCapsule(root, "카드 앨범", .385f, .12f, .55f, .204f, ShowRestorationOriginalArchive);
            ClassicCapsule(root, "자유 연습", .565f, .12f, .73f, .204f, ShowFreePracticeLobby);
            ClassicCapsule(root, "소리 설정", .745f, .12f, .905f, .204f, () => ShowSoundSettings(ShowTitle));
            Text(root, "출전 " + CardCatalog.All.Length.ToString("N0") + "종  /  원본 카드 " + OriginalCardArchive.All.Length.ToString("N0") + "종", .37f, .055f, .92f, .105f, 21, RestorationMetal, TextAlignmentOptions.Center);
        }

        void DrawClassicLobby(bool owned)
        {
            if (owned && (questEconomy == null || !questEconomy.HasStarter)) { ShowQuestEntry(); return; }
            questFreePracticeView = !owned;
            QuestCurrentScreen = owned ? "lobby" : "free-practice";
            battleScreen = false; redraw = owned ? (Action)ShowQuestLobby : ShowLobby;
            Clear(); UpdateWorld(); FmOriginalMusic.SetScene("lobby"); ClassicDesktop(); ClassicTopbar(owned);
            ClassicOriginalCharacter(root, .02f, .265f, .288f, .885f);
            var name = ClassicWindow(root, "Local player identity at waiting room", .025f, .198f, .283f, .258f);
            Text(name, LocalProfileName, .025f, .03f, .975f, .97f, 25, ivory, TextAlignmentOptions.Center);
            ClassicCapsule(root, "게임 방법", .025f, .11f, .15f, .185f, () => ShowRulesHelp(redraw));
            ClassicCapsule(root, owned ? "자유 연습" : "싱글 게임", .158f, .11f, .283f, .185f, owned ? (Action)ShowFreePracticeLobby : EnterQuestGame);
            ClassicCapsule(root, "덱 편집", .025f, .025f, .15f, .098f, owned ? (Action)(() => ShowQuestOwnedCards(true)) : () => ShowCollection(true));
            ClassicCapsule(root, "타이틀", .158f, .025f, .283f, .098f, ShowTitle);

            var rooms = ClassicWindow(root, "Late FM1 live waiting room", .312f, .302f, .975f, .89f);
            string[] tabs = { owned ? "싱글 게임" : "연습 대전", owned ? "AI 대전" : "시나리오", "멀티 · 추후 업데이트" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                ClassicCapsule(rooms, tabs[i], .025f + i * .32f, .865f, .325f + i * .32f, .972f,
                    () => { if (tab == 2) ShowQuestMultiplayerNotice(); else if (owned) { questLobbyTab = tab; ShowQuestLobby(); } else { restorationLobbyMode = tab; ShowLobby(); } }, i != 2,
                    (owned ? questLobbyTab : restorationLobbyMode) == i);
            }
            if (owned && !string.IsNullOrEmpty(questEconomy.ActiveBattleTicketId))
            {
                Text(rooms, "진행 중인 싱글 대전", .06f, .59f, .94f, .77f, 29, ivory, TextAlignmentOptions.Center);
                Text(rooms, "저장한 대전을 이어서 플레이할 수 있습니다.", .06f, .40f, .94f, .58f, 23, RestorationMetal, TextAlignmentOptions.Center);
                ClassicCapsule(rooms, "싱글 대전 이어하기", .075f, .16f, .66f, .34f, ContinueQuestBattle,
                    HasQuestBattleSave && questEconomy.ActiveBattleTicketId == PlayerPrefs.GetString(QuestTicketKey, ""));
                ClassicCapsule(rooms, "대전 포기", .69f, .16f, .94f, .34f, ShowQuestAbandonConfirmation);
            }
            else if (owned ? questLobbyTab == 0 : restorationLobbyMode == 1)
            {
                var completed = owned ? questEconomy.CompletedScenarios : FmScenarioCatalog.Stages.Where(s => PlayerPrefs.GetInt("FC.scenario.won." + s.id, 0) != 0).Select(s => s.id).ToArray();
                for (int i = 0; i < FmScenarioCatalog.Stages.Length; i++)
                {
                    var stage = FmScenarioCatalog.Stages[i]; bool available = FmScenarioCatalog.IsUnlocked(stage.id, completed);
                    ClassicRoom(rooms, i, stage.name, "시나리오 · " + (completed.Contains(stage.id) ? "완료" : "AI 대전"), available,
                        () => { if (owned) ShowQuestStage(stage.id); else ShowRestorationScenario(stage.id); });
                }
            }
            else
            {
                var practices = DemoCatalog.Encounters.Select((encounter, index) => new { encounter, index })
                    .Where(e => !FmScenarioCatalog.Stages.Any(s => s.encounterIndex == e.index)).Take(4).ToArray();
                for (int i = 0; i < practices.Length; i++)
                {
                    var entry = practices[i];
                    ClassicRoom(rooms, i, entry.encounter.name, "AI 연습 · 50장", true,
                        () => { if (owned) ShowQuestEncounter(entry.index); else { selectedEncounter = entry.index; ShowEncounterSelection(); } });
                }
            }
            var log = ClassicWindow(root, "Classic system and local record window", .312f, .023f, .975f, .28f);
            var heading = Panel(log, .012f, .81f, .988f, .97f, new Color(.18f, .055f, .052f));
            Text(heading, "시스템 / 기록", .025f, .02f, .975f, .98f, 22, ivory);
            string status = !string.IsNullOrEmpty(questNotice) ? questNotice : owned
                ? "카드팩과 보유 덱은 상점과 덱 편집에서 확인하세요."
                : "자유 연습 · SP 보상 없음 · 기본 덱 " + DemoCatalog.StarterDecks.Length + "개";
            Text(log, status, .03f, .55f, .97f, .79f, 23, new Color(.77f, .80f, .69f));
            Text(log, LocalProfileWins + "승 " + LocalProfileLosses + "패 " + LocalProfileDraws + "무" + (owned ? "  /  SP " + QuestSp.ToString("N0") + "  /  보유 " + QuestOwnedTotal + "장" : "  /  출전 " + CardCatalog.All.Length + "종"), .03f, .29f, .97f, .55f, 23, gold);
            Text(log, string.IsNullOrEmpty(LocalProfileGreeting) ? "온라인 대전·채팅은 추후 업데이트 예정입니다." : "대전 한마디  " + LocalProfileGreeting, .03f, .045f, .97f, .29f, 21, RestorationMetal);
        }

        void ClassicRoom(RectTransform rooms, int index, string title, string description, bool available, Action enter)
        {
            float x = index % 2 == 0 ? .035f : .515f;
            float top = .805f - (index / 2) * .37f;
            var room = Button(rooms, "", x, top - .325f, x + .45f, top, enter, new Color(.027f, .030f, .031f), 22, available);
            room.name = "Classic live room " + index;
            RestorationTrim((RectTransform)room.transform);
            Text(room.transform, (index + 1).ToString("00") + "  " + title, .045f, .54f, .955f, .94f, 26, ivory);
            Text(room.transform, description, .045f, .12f, .79f, .53f, 22, RestorationMetal);
            Text(room.transform, available ? "입장" : "잠김", .75f, .08f, .96f, .53f, 22, available ? gold : RestorationMetal, TextAlignmentOptions.Right);
        }

        void ClassicWalletRail()
        {
            var rail = ClassicWindow(root, "Classic My Information rail", .77f, .045f, .96f, .962f);
            Text(rail, "내정보", .07f, .91f, .93f, .98f, 26, ivory, TextAlignmentOptions.Center);
            var profile = Button(rail, "", .09f, .62f, .91f, .90f, () => ShowLocalProfile(redraw ?? ShowLobby), ClassicBlack);
            Artwork(profile.transform, LocalProfileAvatar(), .02f, .04f, .98f, .97f);
            Text(rail, LocalProfileName, .07f, .55f, .93f, .62f, 23, ivory, TextAlignmentOptions.Center);
            string[] labels = { "SP", "GP", "보유", "승 / 패" };
            string[] values = { QuestSp.ToString("N0"), QuestGp + " · 준비 중", QuestOwnedTotal + "장", LocalProfileWins + " / " + LocalProfileLosses };
            for (int i = 0; i < labels.Length; i++)
            {
                float y = .48f - i * .075f;
                var row = ClassicWindow(rail, "Actual profile value " + labels[i], .07f, y, .93f, y + .067f, true);
                Text(row, labels[i], .045f, .03f, .34f, .97f, 21, ivory);
                Text(row, values[i], .35f, .03f, .96f, .97f, 22, i == 0 ? gold : ivory, TextAlignmentOptions.Right);
            }
            ClassicCapsule(rail, "카드 앨범", .08f, .151f, .92f, .213f, ShowRestorationOriginalArchive);
            ClassicCapsule(rail, "음악 / 효과음", .08f, .081f, .92f, .143f, () => ShowSoundSettings(redraw ?? ShowLobby));
            ClassicCapsule(rail, "대기실", .08f, .011f, .92f, .073f, ShowQuestLobby);
        }

        void DrawClassicShop()
        {
            if (questEconomy == null || !questEconomy.HasStarter) { ShowQuestEntry(); return; }
            QuestScreen("shop", ShowQuestShop, "카드 상점", "SP 상품을 선택해 구성·가격을 확인하세요.");
            string[] filters = { "SP 부스터", "매일 무료 A/B/C", "속성 기본 덱" };
            for (int i = 0; i < filters.Length; i++)
            {
                int choice = i; float x = .045f + i * .237f;
                ClassicCapsule(root, filters[i], x, .681f, x + .225f, .752f, () => { questShopFilter = choice; questShopPage = 0; ShowQuestShop(); }, true, questShopFilter == i);
            }
            var offers = FmQuestCatalog.Offers.Where(o => questShopFilter == 1 ? o.dailyOneChoice : questShopFilter == 2 ? o.deckId.Length > 0 : !o.dailyOneChoice && o.deckId.Length == 0).ToArray();
            int pages = Mathf.Max(1, (offers.Length + 3) / 4); questShopPage = Mathf.Clamp(questShopPage, 0, pages - 1);
            var shelf = ClassicWindow(root, "Classic live card shop counter", .045f, .226f, .744f, .655f);
            for (int i = 0; i < 4 && questShopPage * 4 + i < offers.Length; i++)
            {
                var offer = offers[questShopPage * 4 + i]; float top = .975f - i * .242f;
                var tile = Button(shelf, "", .02f, top - .225f, .98f, top, () => ShowQuestOffer(offer.id), new Color(.038f, .036f, .032f), 23);
                tile.name = "Quest shop offer " + offer.id;
                QuestPackPicture(tile.transform, offer, .012f, .05f, .13f, .95f);
                Text(tile.transform, offer.name, .16f, .46f, .61f, .94f, 26, ivory).name = "Shop actual product name " + offer.id;
                bool available = questEconomy.CanBuyPack(offer.id, QuestCalendarDay, out var reason);
                Text(tile.transform, available ? "구성 · 확률 확인" : reason, .16f, .04f, .64f, .46f, 20, RestorationMetal);
                Text(tile.transform, offer.priceSp == 0 ? "오늘 무료" : offer.priceSp.ToString("N0") + " SP", .65f, .32f, .965f, .86f, 27, gold, TextAlignmentOptions.Right).name = "Shop actual product price " + offer.id;
                Text(tile.transform, offer.cardCount + "장", .72f, .025f, .965f, .34f, 20, ivory, TextAlignmentOptions.Right);
            }
            ClassicCapsule(root, "이전", .045f, .135f, .16f, .209f, () => { questShopPage--; ShowQuestShop(); }, questShopPage > 0);
            Text(root, (questShopPage + 1) + "/" + pages, .17f, .135f, .28f, .209f, 23, gold, TextAlignmentOptions.Center);
            ClassicCapsule(root, "다음", .29f, .135f, .405f, .209f, () => { questShopPage++; ShowQuestShop(); }, questShopPage + 1 < pages);
            ClassicCapsule(root, "구매한 카드팩 · " + QuestUnopenedPackCount + "개", .42f, .135f, .744f, .209f, ShowQuestPurchases);
            QuestNavigation();
        }

        TMP_InputField ClassicProfileInput(Transform parent, string name, string value, float x, float y, float xx, float yy, int limit)
        {
            var area = ClassicWindow(parent, name, x, y, xx, yy); area.GetComponent<Image>().raycastTarget = true;
            var field = area.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(area, .035f, .08f, .965f, .92f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var text = Text(viewport, value, 0, 0, 1, 1, 25, ivory); text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
            field.textViewport = viewport; field.textComponent = text; field.characterLimit = limit; field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.Standard; field.text = value; return field;
        }

        void DrawClassicProfile(Action back)
        {
            QuestCurrentScreen = "profile"; battleScreen = false; redraw = () => ShowLocalProfile(back); Clear(); ClassicDesktop(); ClassicTopbar(questEconomy != null && questEconomy.HasStarter);
            ClassicOriginalCharacter(root, .018f, .12f, .285f, .885f);
            var window = ClassicWindow(root, "Late FM1 My Information window", .315f, .18f, .955f, .885f);
            var bar = Panel(window, .015f, .895f, .985f, .98f, new Color(.035f, .09f, .12f));
            Text(bar, "내정보", .04f, .02f, .90f, .98f, 27, ivory, TextAlignmentOptions.Center);
            ClassicCapsule(bar, "X", .91f, .08f, .985f, .92f, back);
            var portrait = ClassicWindow(window, "Selected profile avatar", .035f, .23f, .345f, .865f);
            var avatar = LocalProfileAvatar(); Artwork(portrait, avatar, .025f, .025f, .975f, .975f);
            ClassicCapsule(window, "초상 선택", .035f, .115f, .345f, .205f, () => ShowClassicAvatarPicker(() => ShowLocalProfile(back)));
            Text(window, "이름", .375f, .77f, .54f, .865f, 23, RestorationMetal);
            var nickname = ClassicProfileInput(window, "Profile nickname input", LocalProfileName, .55f, .765f, .96f, .865f, 12);
            string[] labels = { "승 / 패 / 무", "SP", "보유 카드", "히어로 속성" };
            string[] values = { LocalProfileWins + " / " + LocalProfileLosses + " / " + LocalProfileDraws, QuestSp.ToString("N0"), QuestOwnedTotal + "장", CardCatalog.ElementName(avatar.element) };
            for (int i = 0; i < labels.Length; i++)
            {
                float top = .745f - i * .105f;
                var row = ClassicWindow(window, "My Information " + labels[i], .375f, top - .091f, .96f, top);
                Text(row, labels[i], .025f, .03f, .47f, .97f, 22, RestorationMetal);
                Text(row, values[i], .48f, .03f, .975f, .97f, 23, i == 1 ? gold : ivory, TextAlignmentOptions.Right);
            }
            Text(window, "대전 한마디 (15자)", .375f, .23f, .96f, .325f, 22, RestorationMetal);
            var greeting = ClassicProfileInput(window, "Profile battle greeting input", LocalProfileGreeting, .375f, .13f, .96f, .23f, 15);
            ClassicCapsule(window, "저장", .40f, .025f, .665f, .11f, () =>
            {
                PlayerPrefs.SetString(ProfileNameKey, SafeProfileName(nickname.text)); PlayerPrefs.SetString(ProfileGreetingKey, SafeProfileGreeting(greeting.text));
                PlayerPrefs.Save(); profileDraftName = LocalProfileName; ShowLocalProfile(back);
            });
            ClassicCapsule(window, "돌아가기", .69f, .025f, .955f, .11f, back);
            Text(root, "내정보는 이 기기에 저장됩니다.", .315f, .102f, .955f, .16f, 21, RestorationMetal, TextAlignmentOptions.Center);
            ClassicCapsule(root, "저장 옮기기", .65f, .025f, .955f, .09f, () => { profileTransferReturn = () => ShowLocalProfile(back); ShowTransfer(); });
        }

        void ShowClassicAvatarPicker(Action back)
        {
            battleScreen = false; redraw = () => ShowClassicAvatarPicker(back); Clear(); ClassicDesktop();
            ClassicScreenHeader("초상 선택", "원작 카드 원화를 내정보의 초상으로 사용합니다.");
            var choices = ProfileAvatars();
            for (int i = 0; i < choices.Count; i++)
            {
                var card = choices[i]; float x = .045f + i * .13f;
                var tile = Button(root, "", x, .27f, x + .118f, .74f,
                    () => { PlayerPrefs.SetString(ProfileAvatarKey, card.id); PlayerPrefs.Save(); back(); }, slate);
                Artwork(tile.transform, card, .025f, .16f, .975f, .98f);
                Text(tile.transform, card.name, .03f, .02f, .97f, .16f, 21, ivory, TextAlignmentOptions.Center);
            }
            ClassicCapsule(root, "돌아가기", .35f, .08f, .65f, .20f, back);
        }

        void DrawClassicBattleRail()
        {
            var state = BattleDisplayState; var player = state.players[0];
            var rail = ClassicWindow(root, "Battle resource rail", .817f, .018f, .983f, .984f);
            // The late client puts the seven colour counters around its total
            // soul orb; no battle RNG or private opponent hand is inspected.
            var soul = ClassicWindow(rail, "Classic radial soul display", .025f, .804f, .975f, .989f);
            var orb = Panel(soul, .29f, .19f, .71f, .90f, Color.clear); RestorationTrim(orb, true);
            RenewalValueText(orb, player.genericSoul.ToString(), .10f, .25f, .90f, .78f, 28, ivory, "Actual total soul orb");
            RenewalValueText(orb, "총소울", .04f, .015f, .96f, .27f, 17, gold, "Actual total soul label");
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            for (int i = 0; i < elements.Length; i++)
            {
                float angle = (135 - i * 40) * Mathf.Deg2Rad;
                float x = .5f + Mathf.Cos(angle) * .36f, y = .50f + Mathf.Sin(angle) * .34f;
                var cell = Panel(soul, x - .11f, y - .12f, x + .11f, y + .12f, new Color(.023f, .025f, .027f, .8f));
                ElementIcon(cell, elements[i], .02f, .10f, .48f, .90f);
                RenewalValueText(cell, player.Resource(elements[i]).ToString(), .48f, .02f, .98f, .98f, 21, ivory, "Soul value " + elements[i]);
                BindTooltip(cell.gameObject, CardCatalog.ElementName(elements[i]) + " 소울", "사용 가능 " + player.Resource(elements[i]));
            }
            var phase = ClassicWindow(rail, "Classic turn and phase", .025f, .719f, .975f, .795f);
            RenewalValueText(phase, "Turn " + state.turn, .035f, .50f, .97f, .98f, 21, ivory, "Classic turn number");
            RenewalValueText(phase, locked ? "Wait" : engine.PreparingPlayer == 1 ? "상대 준비" : PhaseName(state.phase), .035f, .02f, .97f, .51f, 20, gold, "Actual battle phase");
            ClassicBattlePlayer(rail, 1, .515f, .707f);
            RenewalValueText(rail, "VS", .1f, .486f, .9f, .523f, 20, new Color(.66f, .15f, .12f), "Classic versus divider");
            ClassicBattlePlayer(rail, 0, .297f, .489f);

            var selected = player.hand.Find(card => card.instanceId == selectedCard);
            var definition = selected == null ? null : CardCatalog.Get(selected.definitionId);
            bool mine = engine.IsPreparing(0), fieldDiscard = selected == null && !locked && engine.CanDiscardField(0, selectedAttacker, out _);
            string label = fieldDiscard ? "선택 카드 소각" : definition != null && definition.kind == CardKind.Terrain ? "지형 전개"
                : definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition) ? "마법 예약" : "카드 상세";
            ClassicCapsule(rail, "기록 / 메뉴", .045f, .229f, .955f, .285f, ShowRevivalBattleMenu, !locked);
            ClassicCapsule(rail, label, .045f, .158f, .955f, .219f,
                () => { if (fieldDiscard) DiscardSelectedField(selectedAttacker); else if (label == "카드 상세") ShowDetail(CardCatalog.Get(inspectedId), ShowBattle); else Play(-1); }, !locked && mine);
            bool canShuffle = engine.CanShuffleHand(0, out string reason);
            var shuffle = ClassicCapsule(rail, "손패 셔플", .045f, .087f, .955f, .148f, () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, !locked && canShuffle);
            BindTooltip(shuffle.gameObject, "손패 셔플", canShuffle ? "손패를 덱으로 되돌리고 6장을 다시 뽑습니다." : reason);
            if (!battlePlaybackActive) ClassicCapsule(rail, "준비 완료", .045f, .014f, .955f, .077f, EndTurn, !locked && mine, true);
        }

        void ClassicBattlePlayer(RectTransform rail, int owner, float bottom, float top)
        {
            var state = BattleDisplayState; var player = state.players[owner];
            var encounter = DemoCatalog.GetEncounter(Mathf.Clamp(state.encounterIndex, 0, DemoCatalog.Encounters.Length - 1));
            var avatar = owner == 0 ? LocalProfileAvatar() : DemoCatalog.GetDeck(encounter.deckId).cards.Select(CardCatalog.Get).First(card => card.kind == CardKind.Unit);
            var panel = ClassicWindow(rail, "Classic actual player information " + owner, .025f, bottom, .975f, top);
            string nickname = owner == 0 ? LocalProfileName : encounter.name + " · AI";
            var name = RenewalValueText(panel, nickname, .025f, .76f, .975f, .985f, 22, ivory, "Battle nickname " + owner);
            name.overflowMode = TextOverflowModes.Ellipsis;
            var portrait = Button(panel, "", .04f, .255f, .96f, .76f,
                () => { if (owner == 0) ShowLocalProfile(ShowBattle); else ShowPolishBattleDesk(); }, ClassicBlack, 20, !locked);
            portrait.name = owner == 1 ? "Opponent information and battle desk" : "My battle information portrait";
            Artwork(portrait.transform, avatar, .018f, .025f, .982f, .975f);
            RenewalValueText(panel, "손패 " + player.hand.Count + "  덱 " + player.deck.Count, .04f, .14f, .96f, .265f, 19, ivory, "Player hand and deck " + owner);
            var discard = Button(panel, "LP " + player.levelPoints + "  소각 " + player.discard.Count, .035f, .012f, .965f, .145f,
                () => { battleArchivePage = 0; ShowBattleArchive(owner); }, slate, 19, !locked);
            if (owner == 0) BindDiscardDrop((RectTransform)discard.transform);
            BindTooltip(discard.gameObject, "소각장", owner == 0 ? "소각장을 보거나 준비 중 손패·아군 카드를 끌어와 소각합니다." : "공개된 상대 소각장을 확인합니다.");
        }
    }
}
