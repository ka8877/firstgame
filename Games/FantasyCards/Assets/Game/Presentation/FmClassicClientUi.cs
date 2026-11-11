using System;
using System.Linq;
using FantasyCards.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    // The retained 640x480 December 2017 client defines window geometry.
    // The 2018 retrospective supplies the independently retained hero and
    // My Information window. Website artwork is not client-login evidence.
    public sealed partial class FantasyCardsApp
    {
        static readonly Color ClassicBlack = new Color(.012f, .013f, .014f);
        static readonly Color ClassicRed = new Color(.25f, .055f, .055f);
        static readonly Color ClassicSteel = new Color(.46f, .47f, .45f);
        static readonly Color ClassicBlue = new Color(.065f, .16f, .20f);
        const string ProfileGreetingKey = "FC.profile.greeting";
        public string LocalProfileGreeting => SafeProfileGreeting(PlayerPrefs.GetString(ProfileGreetingKey, ""));

        static string SafeProfileGreeting(string value) => new string((value ?? "").Where(c => !char.IsControl(c) && c != '<' && c != '>').Take(15).ToArray()).Trim();

        RectTransform ClassicClientRect(Transform parent, string name, float x, float top, float width, float height, Color tint)
        {
            var rect = Panel(parent, x / 640f, 1 - (top + height) / 480f, (x + width) / 640f, 1 - top / 480f, tint);
            rect.name = name; rect.GetComponent<UiRoundedImage>().Radius = 0;
            return rect;
        }

        RectTransform ClassicClientWindow(string name, float x, float top, float width, float height)
            => ClassicWindow(root, name, x / 640f, 1 - (top + height) / 480f, (x + width) / 640f, 1 - top / 480f);

        TMP_Text ClassicText(Transform parent, string value, float x, float y, float xx, float yy, float size, Color tint, TextAlignmentOptions alignment = TextAlignmentOptions.MidlineLeft)
        {
            var text = Text(parent, value, x, y, xx, yy, size, tint, alignment);
            text.font = font; text.textWrappingMode = TextWrappingModes.NoWrap;
            text.GetComponent<UiCanvasTextSize>().ConfigureCard(size);
            return text;
        }

        void ClassicDesktop()
        {
            Panel(root, 0, 0, 1, 1, ClassicBlack);
            var rim = Panel(root, .003f, .005f, .997f, .995f, Color.clear);
            rim.name = "Late FM1 black and red client frame";
            Frame(rim, ClassicRed, 2);
            Frame(Panel(rim, .004f, .004f, .996f, .996f, Color.clear), new Color(.14f, .15f, .15f), 1);
        }

        RectTransform ClassicWindow(Transform parent, string name, float x, float y, float xx, float yy, bool paper = false)
        {
            var panel = Panel(parent, x, y, xx, yy, paper ? new Color(.28f, .27f, .23f) : new Color(.021f, .023f, .024f));
            panel.name = name;
            panel.GetComponent<UiRoundedImage>().Radius = 0;
            if (AddClassicSkin(panel, false) == null) Frame(panel, ClassicSteel, 1);
            return panel;
        }

        FmClassicClientSkin AddClassicSkin(Transform parent, bool button)
        {
            var source = Resources.Load<Texture2D>("Art/FmClientMotion/late-client-interface-reference");
            if (source == null || source.width != 640 || source.height != 480) return null;
            var skin = new GameObject(button ? "Original 2017 graphite button skin" : "Original 2017 engraved window skin", typeof(RectTransform), typeof(FmClassicClientSkin)).GetComponent<FmClassicClientSkin>();
            skin.transform.SetParent(parent, false); Anchor((RectTransform)skin.transform, 0, 0, 1, 1); skin.transform.SetAsFirstSibling();
            skin.Source = source; skin.texture = source; skin.ButtonSkin = button; skin.raycastTarget = false; skin.color = Color.white; skin.SetAllDirty();
            var oldFrame = parent.Find("Fine rounded frame"); if (oldFrame != null) oldFrame.gameObject.SetActive(false);
            return skin;
        }

        Button ClassicCapsule(Transform parent, string label, float x, float y, float xx, float yy, Action action, bool enabled = true, bool selected = false)
        {
            var button = Button(parent, label, x, y, xx, yy, action, selected ? new Color(.27f, .11f, .10f) : ClassicBlue, 23, enabled);
            var shape = button.GetComponent<UiRoundedImage>(); shape.Radius = 22;
            shape.TopTint = new Color(1.6f, 1.6f, 1.55f); shape.BottomTint = new Color(.35f, .40f, .43f);
            var skin = AddClassicSkin(button.transform, true);
            if (skin != null)
            {
                shape.color = Color.clear; button.targetGraphic = skin;
                var colors = button.colors; colors.normalColor = selected ? new Color(1, .66f, .60f) : Color.white;
                colors.highlightedColor = selected ? new Color(1.15f, .78f, .72f) : new Color(1.15f, 1.15f, 1.15f);
                button.colors = colors;
            }
            else Frame((RectTransform)button.transform, new Color(.52f, .57f, .55f), 1);
            button.GetComponentInChildren<TMP_Text>().GetComponent<UiCanvasTextSize>().ConfigureCard(23);
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
            ClassicWordmark(root, 0, 424f / 480, 198f / 640, 1);
            string[] labels = { "대기실", owned ? "덱 편집" : "출전 카드", "카드 앨범", "카드 상점", "내정보" };
            Action[] actions = { owned ? (Action)ShowQuestLobby : ShowFreePracticeLobby,
                owned ? (Action)(() => ShowQuestOwnedCards(true)) : () => ShowCollection(false),
                ShowRestorationOriginalArchive, ShowQuestShop, () => ShowLocalProfile(redraw ?? ShowLobby) };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = (210 + i * 82) / 640f;
                ClassicCapsule(root, labels[i], x, 451f / 480, x + 77f / 640, 476f / 480, actions[i], !(i == 4 && QuestCurrentScreen == "profile"));
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
            var roomClient = Resources.Load<Texture2D>("Art/FmClientMotion/late-client-interface-reference");
            if (clientPortrait && roomClient != null && roomClient.width == 640 && roomClient.height == 480)
            {
                var portrait = Panel(bounds, 0, 0, 1, 1, Color.clear); portrait.name = "Original late client character artwork";
                var image = new GameObject("Bounded original hero art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                image.transform.SetParent(portrait, false); Anchor((RectTransform)image.transform, 0, 0, 1, 1);
                image.texture = roomClient; image.uvRect = new Rect(7f / 640, 115f / 480, 196f / 640, 307f / 480); image.raycastTarget = false;
                // The retained portrait contains an old matchmaking status.
                // Cover its complete rectangle with the actual disabled state.
                var status = Panel(portrait, 97f / 196, 273f / 307, 189f / 196, 305f / 307, new Color(.010f, .012f, .013f, 1));
                status.name = "Live disabled multiplayer status covering original capture";
                Frame(status, new Color(.29f, .31f, .30f), 1);
                ClassicText(status, "멀티 · 추후 업데이트", .025f, .05f, .975f, .95f, 21, new Color(.62f, .64f, .63f), TextAlignmentOptions.Center);
                return;
            }
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
        }

        void DrawClassicTitle()
        {
            QuestCurrentScreen = "title"; battleScreen = false; redraw = ShowTitle;
            Clear(); UpdateWorld(); FmOriginalMusic.SetScene("login"); ClassicDesktop();
            var source = Resources.Load<Texture2D>("Art/FmClientMotion/native-entry-reference");
            if (source != null && source.width == 530 && source.height == 398)
            {
                // These are unchanged, independently bounded painting regions
                // of the verified 2011 native client. The old credential box,
                // rating, cursor and version label are deliberately excluded.
                NativeEntryPainting(source, "left", 0, 0, 171, 398);
                NativeEntryPainting(source, "upper middle", 171, 0, 190, 81);
                NativeEntryPainting(source, "lower middle", 171, 195, 190, 203);
                NativeEntryPainting(source, "right above cursor", 361, 0, 101, 250);
                NativeEntryPainting(source, "right below cursor", 361, 273, 101, 125);
                NativeEntryPainting(source, "right beside cursor", 361, 250, 6, 23);
                NativeEntryPainting(source, "right outer edge", 393, 250, 69, 23);
                NativeEntryPainting(source, "far right", 462, 84, 68, 304);
                var badgePatch = NativeEntryPainting(source, "rating omission", 440, 0, 90, 100);
                badgePatch.uvRect = new Rect(362f / 530, 282f / 398, 80f / 530, 100f / 398);
                var badgeBlend = badgePatch.gameObject.AddComponent<FmArtworkFeather>();
                badgeBlend.Left = 22f / 90; badgeBlend.Bottom = 16f / 100;
                // The capture's mouse cursor is not part of the painting. Fill
                // its 26x23 region with clean purple cloth from just below,
                // blending over sixteen source pixels around the opaque core.
                var patch = NativeEntryPainting(source, "cursor omission", 351, 234, 58, 55);
                patch.uvRect = new Rect(351f / 530, 69f / 398, 58f / 530, 55f / 398);
                var cursorBlend = patch.gameObject.AddComponent<FmArtworkFeather>();
                cursorBlend.Left = cursorBlend.Right = 16f / 58;
                cursorBlend.Top = cursorBlend.Bottom = 16f / 55;
                var logo = NativeEntryPainting(source, "native wordmark", 178, 0, 180, 43);
                logo.name = "Original native FM login wordmark";
                logo.transform.parent.name = "Classic FM client wordmark";
            }
            else ClassicWordmark(root, 178f / 530, 355f / 398, 358f / 530, 1);
            var login = ClassicWindow(root, "Classic local client entry", 171f / 530, 203f / 398, 361f / 530, 317f / 398);
            var oldSkin = login.GetComponentInChildren<FmClassicClientSkin>(); if (oldSkin != null) oldSkin.gameObject.SetActive(false);
            NativeEntryWindowFrame(login, source);
            ClassicText(login, "게임접속", .15f, .83f, .85f, .96f, 22, ivory, TextAlignmentOptions.Center);
            ClassicText(login, "이름", .10f, .63f, .33f, .745f, 23, ivory);
            var player = ClassicProfileInput(login, "Saved local login identity", LocalProfileName, .337f, .63f, .895f, .745f, 12);
            ClassicText(login, "비밀번호", .10f, .465f, .33f, .58f, 21, RestorationMetal);
            var password = ClassicWindow(login, "Inactive original password row", .337f, .465f, .895f, .58f);
            ClassicText(password, "로컬 플레이", .05f, .02f, .95f, .98f, 21, RestorationMetal);
            ClassicText(login, "인증번호", .10f, .298f, .33f, .412f, 21, RestorationMetal);
            var auth = ClassicWindow(login, "Inactive original authentication row", .337f, .298f, .895f, .412f);
            ClassicText(auth, "멀티 · 추후 업데이트", .05f, .02f, .95f, .98f, 20, RestorationMetal);
            NativeEntryButton(login, "게임 시작", .147f, .357f, () =>
            {
                PlayerPrefs.SetString(ProfileNameKey, SafeProfileName(player.text)); PlayerPrefs.Save(); EnterQuestGame();
            });
            NativeEntryButton(login, "전투 이어하기", .379f, .595f, Continue, HasAnyBattleSave);
            NativeEntryButton(login, "자유 연습", .621f, .832f, ShowFreePracticeLobby);
            Panel(root, 0, 0, 1, 18f / 398, ClassicBlack);
            ClassicCapsule(root, "카드 앨범", 9f / 530, 2f / 398, 87f / 530, 16f / 398, ShowRestorationOriginalArchive);
            ClassicCapsule(root, "소리 설정", 96f / 530, 2f / 398, 174f / 530, 16f / 398, () => ShowSoundSettings(ShowTitle));
            ClassicText(root, "출전 " + CardCatalog.All.Length.ToString("N0") + "종  /  원본 카드 " + OriginalCardArchive.All.Length.ToString("N0") + "종", .40f, .004f, .98f, .040f, 21, RestorationMetal, TextAlignmentOptions.Right);
        }

        void NativeEntryWindowFrame(RectTransform window, Texture2D source)
        {
            if (source == null) return;
            // Actual Ver5.73 ornamental frame, excluding all original fields,
            // buttons and the old close glyph. The title remains live text.
            NativeWindowPatch(window, source, "left header ornament", 171, 81, 70, 21);
            NativeWindowPatch(window, source, "right header ornament", 287, 81, 54, 21);
            NativeWindowPatch(window, source, "upper centre edge", 241, 81, 46, 6);
            NativeWindowPatch(window, source, "top right corner", 341, 81, 20, 5);
            NativeWindowPatch(window, source, "right header edge", 355, 86, 6, 16);
            NativeWindowPatch(window, source, "left edge", 171, 102, 8, 85);
            NativeWindowPatch(window, source, "right edge", 351, 102, 10, 85);
            NativeWindowPatch(window, source, "bottom edge", 171, 187, 190, 8);
            NativeWindowPatch(window, source, "lower left corner", 179, 172, 17, 15);
            NativeWindowPatch(window, source, "lower right corner", 334, 172, 17, 15);
            NativeWindowPatch(window, source, "inner divider", 179, 164, 172, 7);
        }

        void NativeWindowPatch(Transform parent, Texture2D source, string name, float x, float top, float width, float height)
        {
            var image = new GameObject("Native 2011 live window " + name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false);
            Anchor((RectTransform)image.transform, (x - 171) / 190, 1 - (top + height - 81) / 114, (x + width - 171) / 190, 1 - (top - 81) / 114);
            image.texture = source; image.uvRect = new Rect(x / 530, (398 - top - height) / 398, width / 530, height / 398); image.raycastTarget = false;
        }

        Button NativeEntryButton(Transform parent, string label, float x, float xx, Action action, bool enabled = true)
        {
            var button = ClassicCapsule(parent, label, x, .079f, xx, .202f, action, enabled);
            var face = button.GetComponent<UiRoundedImage>(); face.Radius = 1;
            var graySkin = button.GetComponentInChildren<FmClassicClientSkin>(); if (graySkin != null) graySkin.gameObject.SetActive(false);
            button.targetGraphic = face;
            face.color = new Color(.16f, .045f, .037f); face.TopTint = new Color(1.45f, 1.42f, 1.35f);
            var source = Resources.Load<Texture2D>("Art/FmClientMotion/native-entry-reference");
            if (source != null)
            {
                NativeButtonPatch(button.transform, source, "unwritten centre", .075f, .133f, .925f, .867f, 199, 175, 1, 10);
                NativeButtonPatch(button.transform, source, "left bevel", 0, 0, .075f, 1, 197, 172, 3, 15);
                NativeButtonPatch(button.transform, source, "right bevel", .925f, 0, 1, 1, 238, 172, 3, 15);
                NativeButtonPatch(button.transform, source, "top bevel", .075f, .867f, .925f, 1, 201, 172, 36, 2);
                NativeButtonPatch(button.transform, source, "bottom bevel", .075f, 0, .925f, .133f, 201, 185, 36, 2);
            }
            var text = button.GetComponentInChildren<TMP_Text>(); text.characterWidthAdjustment = 30;
            return button;
        }

        void NativeButtonPatch(Transform parent, Texture2D source, string name, float x, float y, float xx, float yy, float sourceX, float sourceTop, float width, float height)
        {
            var image = new GameObject("Native 2011 live button " + name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(parent, false); Anchor((RectTransform)image.transform, x, y, xx, yy); image.transform.SetAsFirstSibling();
            image.texture = source; image.uvRect = new Rect(sourceX / 530, (398 - sourceTop - height) / 398, width / 530, height / 398); image.raycastTarget = false;
        }

        RawImage NativeEntryPainting(Texture2D source, string name, float x, float top, float width, float height)
        {
            var area = Panel(root, x / 530, 1 - (top + height) / 398, (x + width) / 530, 1 - top / 398, Color.clear);
            area.name = "Native entry painting region " + name;
            var image = new GameObject("Native entry source " + name, typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            image.transform.SetParent(area, false); Anchor((RectTransform)image.transform, 0, 0, 1, 1);
            image.texture = source; image.uvRect = new Rect(x / 530, (398 - top - height) / 398, width / 530, height / 398); image.raycastTarget = false;
            return image;
        }

        void DrawClassicLobby(bool owned)
        {
            if (owned && (questEconomy == null || !questEconomy.HasStarter)) { ShowQuestEntry(); return; }
            questFreePracticeView = !owned;
            QuestCurrentScreen = owned ? "lobby" : "free-practice";
            battleScreen = false; redraw = owned ? (Action)ShowQuestLobby : ShowLobby;
            Clear(); UpdateWorld(); FmOriginalMusic.SetScene("lobby"); ClassicDesktop(); ClassicTopbar(owned);
            ClassicOriginalCharacter(root, 7f / 640, 115f / 480, 203f / 640, 422f / 480);
            var name = ClassicClientWindow("Local player identity at waiting room", 25, 372, 158, 18);
            ClassicText(name, LocalProfileName, .025f, .03f, .975f, .97f, 24, new Color(.64f, .23f, .21f), TextAlignmentOptions.Center);
            ClassicCapsule(root, "게임 방법", 25f / 640, 50f / 480, 101f / 640, 87f / 480, () => ShowRulesHelp(redraw));
            ClassicCapsule(root, owned ? "자유 연습" : "싱글 게임", 107f / 640, 50f / 480, 183f / 640, 87f / 480, owned ? (Action)ShowFreePracticeLobby : EnterQuestGame);
            ClassicCapsule(root, "덱 편집", 25f / 640, 17f / 480, 101f / 640, 43f / 480, owned ? (Action)(() => ShowQuestOwnedCards(true)) : () => ShowCollection(true));
            ClassicCapsule(root, "타이틀", 107f / 640, 17f / 480, 183f / 640, 43f / 480, ShowTitle);

            var rooms = ClassicClientWindow("Late FM1 live waiting room", 205, 59, 425, 282);
            string[] tabs = { owned ? "싱글 게임" : "연습 대전", owned ? "AI 대전" : "시나리오", "멀티 · 추후 업데이트" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int tab = i;
                ClassicCapsule(rooms, tabs[i], (10 + i * 138) / 425f, 249f / 282, (143 + i * 138) / 425f, 273f / 282,
                    () => { if (tab == 2) ShowQuestMultiplayerNotice(); else if (owned) { questLobbyTab = tab; ShowQuestLobby(); } else { restorationLobbyMode = tab; ShowLobby(); } }, i != 2,
                    (owned ? questLobbyTab : restorationLobbyMode) == i);
            }
            // Six thin room sockets are part of the retained client. Empty
            // sockets remain empty: they do not imply online players/rooms.
            for (int i = 0; i < 6; i++)
            {
                float left = i % 2 == 0 ? 13f : 214f, top = 242 - i / 2 * 72;
                var slot = Panel(rooms, left / 425, (top - 65) / 282, (left + 195) / 425, top / 282, new Color(.008f, .009f, .010f));
                slot.name = "Classic empty room socket " + i;
                if (AddClassicSkin(slot, false) == null) Frame(slot, new Color(.17f, .18f, .18f), 1);
                var line = Panel(slot, .035f, .70f, .965f, .94f, Color.clear); Frame(line, new Color(.13f, .14f, .14f), 1);
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
            ClassicCapsule(rooms, "<", 176f / 425, 8f / 282, 193f / 425, 25f / 282, () => { }, false);
            ClassicText(rooms, "1", 195f / 425, 8f / 282, 228f / 425, 25f / 282, 23, ivory, TextAlignmentOptions.Center);
            ClassicCapsule(rooms, ">", 230f / 425, 8f / 282, 247f / 425, 25f / 282, () => { }, false);
            var log = ClassicClientWindow("Classic system and local record window", 205, 354, 425, 116);
            var heading = Panel(log, .006f, .86f, .994f, .99f, new Color(.18f, .055f, .052f));
            ClassicText(heading, "시스템 / 기록", .025f, .02f, .975f, .98f, 21, ivory);
            string status = owned ? "보유 " + QuestOwnedTotal + "장  ·  SP " + QuestSp.ToString("N0") + "  ·  미개봉 " + QuestUnopenedPackCount + "팩"
                : "자유 연습  ·  기본 덱 " + DemoCatalog.StarterDecks.Length + "개  ·  출전 " + CardCatalog.All.Length + "종";
            ClassicText(log, status, .035f, .68f, .97f, .83f, 23, new Color(.77f, .80f, .69f));
            ClassicText(log, LocalProfileWins + "승 " + LocalProfileLosses + "패 " + LocalProfileDraws + "무" + (owned ? "  /  SP " + QuestSp.ToString("N0") + "  /  보유 " + QuestOwnedTotal + "장" : "  /  출전 " + CardCatalog.All.Length + "종"), .035f, .50f, .97f, .66f, 23, new Color(.75f, .72f, .59f));
            ClassicText(log, string.IsNullOrEmpty(LocalProfileGreeting) ? "" : "대전 한마디  " + LocalProfileGreeting, .035f, .31f, .97f, .48f, 21, RestorationMetal);
        }

        void ClassicRoom(RectTransform rooms, int index, string title, string description, bool available, Action enter)
        {
            float x = (index % 2 == 0 ? 13f : 214f) / 425;
            float top = (242f - index / 2 * 72f) / 282;
            var room = Button(rooms, "", x, top - 65f / 282, x + 195f / 425, top, enter, new Color(.013f, .015f, .016f), 22, available);
            room.name = "Classic live room " + index;
            room.GetComponent<UiRoundedImage>().Radius = 0;
            AddClassicSkin(room.transform, false);
            ClassicText(room.transform, (index + 1).ToString("00") + "  " + title, .045f, .65f, .955f, .94f, 25, ivory);
            ClassicText(room.transform, description, .045f, .25f, .79f, .55f, 22, RestorationMetal);
            ClassicText(room.transform, available ? "입장" : "잠김", .75f, .06f, .96f, .39f, 22, available ? new Color(.67f, .65f, .51f) : RestorationMetal, TextAlignmentOptions.Right);
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
            var text = ClassicText(viewport, value, 0, 0, 1, 1, 23, ivory); text.enableAutoSizing = false; text.overflowMode = TextOverflowModes.Overflow;
            field.textViewport = viewport; field.textComponent = text; field.characterLimit = limit; field.lineType = TMP_InputField.LineType.SingleLine;
            field.contentType = TMP_InputField.ContentType.Standard; field.text = value; return field;
        }

        void DrawClassicProfile(Action back)
        {
            // My Information is an actual small window over the waiting room,
            // with the same room list, hero and system log still visible.
            DrawClassicLobby(!questFreePracticeView && questEconomy != null && questEconomy.HasStarter);
            QuestCurrentScreen = "profile"; battleScreen = false; redraw = () => ShowLocalProfile(back);
            var shield = Panel(root, 0, 0, 1, 1, Color.clear);
            shield.name = "Profile modal input shield"; shield.GetComponent<Image>().raycastTarget = true;
            var window = ClassicClientWindow("Late FM1 My Information window", 331, 42, 287, 217);
            var bar = Panel(window, .015f, .90f, .985f, .98f, new Color(.035f, .09f, .12f));
            ClassicText(bar, "내정보", .04f, .02f, .90f, .98f, 23, ivory, TextAlignmentOptions.Center);
            ClassicCapsule(bar, "X", .93f, .05f, .99f, .95f, back);
            var portrait = ClassicWindow(window, "Selected profile avatar", .062f, .18f, .38f, .855f);
            var avatar = LocalProfileAvatar(); Artwork(portrait, avatar, .025f, .025f, .975f, .975f);
            ClassicCapsule(window, "초상 선택", .062f, .055f, .38f, .14f, () => ShowClassicAvatarPicker(() => ShowLocalProfile(back)));
            var nickname = ClassicProfileInput(window, "Profile nickname input", LocalProfileName, .565f, .79f, .945f, .865f, 12);
            string[] labels = { "승 / 패 / 무", "SP", "보유 카드", "히어로 속성" };
            string[] values = { LocalProfileWins + " / " + LocalProfileLosses + " / " + LocalProfileDraws, QuestSp.ToString("N0"), QuestOwnedTotal + "장", CardCatalog.ElementName(avatar.element) };
            for (int i = 0; i < labels.Length; i++)
            {
                float top = .765f - i * .10f;
                var row = ClassicWindow(window, "My Information " + labels[i], .555f, top - .085f, .945f, top);
                ClassicText(row, labels[i], .025f, .03f, .53f, .97f, 20, RestorationMetal);
                ClassicText(row, values[i], .54f, .03f, .975f, .97f, 21, ivory, TextAlignmentOptions.Right);
            }
            ClassicText(window, "대전 한마디 (15자)", .42f, .285f, .945f, .37f, 22, RestorationMetal);
            var greeting = ClassicProfileInput(window, "Profile battle greeting input", LocalProfileGreeting, .42f, .18f, .945f, .275f, 15);
            ClassicCapsule(window, "저장", .42f, .035f, .60f, .125f, () =>
            {
                PlayerPrefs.SetString(ProfileNameKey, SafeProfileName(nickname.text)); PlayerPrefs.SetString(ProfileGreetingKey, SafeProfileGreeting(greeting.text));
                PlayerPrefs.Save(); profileDraftName = LocalProfileName; ShowLocalProfile(back);
            });
            ClassicCapsule(window, "돌아가기", .62f, .035f, .80f, .125f, back);
            ClassicCapsule(root, "저장 옮기기", 480f / 640, 198f / 480, 618f / 640, 216f / 480, () => { profileTransferReturn = () => ShowLocalProfile(back); ShowTransfer(); });
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
            var rail = ClassicClientWindow("Battle resource rail", 520, 0, 120, 480);
            // The late client puts the seven colour counters around its total
            // soul orb; no battle RNG or private opponent hand is inspected.
            var soul = ClassicWindow(rail, "Classic radial soul display", .025f, .81f, .975f, .995f);
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
            var phase = ClassicWindow(rail, "Classic turn and phase", .025f, .727f, .975f, .805f);
            RenewalValueText(phase, "Turn " + state.turn, .035f, .50f, .97f, .98f, 21, ivory, "Classic turn number");
            RenewalValueText(phase, locked ? "Wait" : engine.PreparingPlayer == 1 ? "상대 준비" : PhaseName(state.phase), .035f, .02f, .97f, .51f, 20, gold, "Actual battle phase");
            ClassicBattlePlayer(rail, 1, 184f / 480, 303f / 480);
            RenewalValueText(rail, "VS", .1f, 176f / 480, .9f, 185f / 480, 20, new Color(.66f, .15f, .12f), "Classic versus divider");
            ClassicBattlePlayer(rail, 0, 64f / 480, 176f / 480);

            var selected = player.hand.Find(card => card.instanceId == selectedCard);
            var definition = selected == null ? null : CardCatalog.Get(selected.definitionId);
            bool mine = engine.IsPreparing(0), fieldDiscard = selected == null && !locked && engine.CanDiscardField(0, selectedAttacker, out _);
            string label = fieldDiscard ? "선택 카드 소각" : definition != null && definition.kind == CardKind.Terrain ? "지형 전개"
                : definition != null && definition.kind == CardKind.Spell && !CardCatalog.RequiresUnitTarget(definition) ? "마법 예약" : "카드 상세";
            ClassicCapsule(rail, "기록 / 메뉴", 7f / 120, 11f / 480, 56f / 120, 35f / 480, ShowRevivalBattleMenu, !locked);
            ClassicCapsule(rail, label, .045f, 309f / 480, .955f, 334f / 480,
                () => { if (fieldDiscard) DiscardSelectedField(selectedAttacker); else if (label == "카드 상세") ShowDetail(CardCatalog.Get(inspectedId), ShowBattle); else Play(-1); }, !locked && mine);
            bool canShuffle = engine.CanShuffleHand(0, out string reason);
            var shuffle = ClassicCapsule(rail, "손패 셔플", 7f / 120, 38f / 480, 56f / 120, 62f / 480, () =>
            {
                if (!engine.ShuffleHand(0, out message)) { ShowBattle(); return; }
                selectedCard = -1; Save(); locked = true; StartCoroutine(ShuffleAndDeal(false));
            }, !locked && canShuffle);
            BindTooltip(shuffle.gameObject, "손패 셔플", canShuffle ? "손패를 덱으로 되돌리고 6장을 다시 뽑습니다." : reason);
            if (!battlePlaybackActive) ClassicCapsule(rail, "준비 완료", 63f / 120, 38f / 480, 112f / 120, 62f / 480, EndTurn, !locked && mine, true);
        }

        void ClassicBattlePlayer(RectTransform rail, int owner, float bottom, float top)
        {
            var state = BattleDisplayState; var player = state.players[owner];
            var encounter = DemoCatalog.GetEncounter(Mathf.Clamp(state.encounterIndex, 0, DemoCatalog.Encounters.Length - 1));
            var avatar = owner == 0 ? LocalProfileAvatar() : DemoCatalog.GetDeck(encounter.deckId).cards.Select(CardCatalog.Get).First(card => card.kind == CardKind.Unit);
            var panel = ClassicWindow(rail, "Classic actual player information " + owner, .025f, bottom, .975f, top);
            string nickname = owner == 0 ? LocalProfileName : encounter.name + " · AI";
            var name = RenewalValueText(panel, nickname, .025f, .84f, .975f, .985f, 22, ivory, "Battle nickname " + owner);
            name.overflowMode = TextOverflowModes.Ellipsis;
            var portrait = Button(panel, "", .04f, .035f, .96f, .66f,
                () => { if (owner == 0) ShowLocalProfile(ShowBattle); else ShowPolishBattleDesk(); }, ClassicBlack, 20, !locked);
            portrait.name = owner == 1 ? "Opponent information and battle desk" : "My battle information portrait";
            Artwork(portrait.transform, avatar, .018f, .025f, .982f, .975f);
            RenewalValueText(panel, "손패 " + player.hand.Count + "  덱 " + player.deck.Count, .04f, .755f, .96f, .84f, 19, ivory, "Player hand and deck " + owner);
            var discard = Button(panel, "LP " + player.levelPoints + "  소각 " + player.discard.Count, .035f, .67f, .965f, .755f,
                () => { battleArchivePage = 0; ShowBattleArchive(owner); }, slate, 19, !locked);
            discard.GetComponentInChildren<TMP_Text>().GetComponent<UiCanvasTextSize>().ConfigureCard(19);
            if (owner == 0) BindDiscardDrop((RectTransform)discard.transform);
            BindTooltip(discard.gameObject, "소각장", owner == 0 ? "소각장을 보거나 준비 중 손패·아군 카드를 끌어와 소각합니다." : "공개된 상대 소각장을 확인합니다.");
        }
    }
}
