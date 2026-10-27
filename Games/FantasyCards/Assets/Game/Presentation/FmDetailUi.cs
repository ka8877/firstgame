using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FirstGame.Shared;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private const string ProfileNameKey = "FC.profile.name", ProfileAvatarKey = "FC.profile.avatar";
        private string profileDraftName;
        private Action profileTransferReturn;
        public string LocalProfileName => SafeProfileName(PlayerPrefs.GetString(ProfileNameKey, "소울 여행자"));
        public string LocalProfileAvatarId => LocalProfileAvatar().id;
        public int LocalProfileWins => PlayerPrefs.GetInt("FC.profile.wins", 0);
        public int LocalProfileLosses => PlayerPrefs.GetInt("FC.profile.losses", 0);
        public int LocalProfileDraws => PlayerPrefs.GetInt("FC.profile.draws", 0);

        private void DetailPanorama(bool quiet)
        {
            Panel(root, 0, 0, 1, 1, new Color(.025f, .045f, .038f));
            var texture = Resources.Load<Texture2D>("Art/FmDetail/title-keyart");
            if (texture != null)
                ImageFrame(root, texture, new Rect(0, 0, 1, 1), 0, 0, 1, 1, new Vector2(.58f, .48f));
            Gradient(root, 0, 0, 1, 1, new Color(.015f, .035f, .028f, quiet ? .70f : .15f),
                new Color(.015f, .035f, .028f, quiet ? .82f : .10f));
            Gradient(root, 0, 0, .69f, 1, new Color(.018f, .035f, .030f, .96f), Color.clear, true);
            Gradient(root, 0, 0, 1, .39f, new Color(.010f, .022f, .020f, .97f), Color.clear);
            var frame = Panel(root, .018f, .022f, .982f, .978f, Color.clear);
            frame.gameObject.name = "Remaster panorama frame";
            Frame(frame, new Color(.82f, .70f, .43f, .65f), 1);
        }

        private void ShowDetailTitle() { ShowArcanaTitle(); }

        private void DrawDetailElementSeals(Transform parent, float x0, float y0, float x1, float y1)
        {
            var elements = Enum.GetValues(typeof(Element)).Cast<Element>().ToArray();
            float span = (x1 - x0) / elements.Length;
            for (int i = 0; i < elements.Length; i++)
            {
                float x = x0 + span * i;
                var seal = Panel(parent, x, y0, x + span - .006f, y1, new Color(.045f, .085f, .065f, .83f));
                seal.gameObject.name = "Title element " + elements[i];
                Frame(seal, new Color(.72f, .62f, .38f, .46f), 1);
                ElementIcon(seal, elements[i], .065f, .22f, .32f, .78f);
                Text(seal, CardCatalog.ElementName(elements[i]), .35f, .04f, .98f, .96f, 22, ElementColor(elements[i]), TextAlignmentOptions.Center);
            }
        }

        private void DrawTitleCardFan()
        {
            string[] featured = { "fm_3121920887", "fm_3062720584", "fm_13031326761" };
            var cards = featured.Select(id => CardCatalog.All.FirstOrDefault(card => card.id == id))
                .Where(card => card != null).ToList();
            if (cards.Count < 3) cards = CardCatalog.All.Where(card => card.kind == CardKind.Unit).Take(3).ToList();
            for (int i = 0; i < cards.Count; i++)
            {
                float x = .680f + i * .070f, y = i == 1 ? .13f : .11f;
                var card = Panel(root, x, y, x + .109f, y + .235f, new Color(.035f, .055f, .045f, .96f));
                card.gameObject.name = "Title original card " + cards[i].id;
                PrintedImage(card, cards[i], .015f, .015f, .985f, .985f);
                Frame(card, gold, 1.5f);
                card.localRotation = Quaternion.Euler(0, 0, i == 0 ? 10 : i == 2 ? -10 : 0);
            }
        }

        private void ShowDetailLobby() { ShowArcanaLobby(); }

        private void DrawDetailClientRail(Action back, string heading)
        {
            DrawRestorationProfileRail(back, heading);
        }

        private CardDefinition LocalProfileAvatar()
        {
            string id = PlayerPrefs.GetString(ProfileAvatarKey, "fm_3010820331");
            return CardCatalog.All.FirstOrDefault(card => card.id == id && card.kind == CardKind.Unit)
                ?? CardCatalog.All.First(card => card.kind == CardKind.Unit);
        }

        private static string SafeProfileName(string value)
        {
            string name = new string((value ?? "").Where(ch => !char.IsControl(ch) && ch != '<' && ch != '>').Take(12).ToArray()).Trim();
            return string.IsNullOrWhiteSpace(name) ? "소울 여행자" : name;
        }

        private List<CardDefinition> ProfileAvatars()
        {
            string[] preferred = { "fm_3121920887", "fm_3062720584", "fm_6030222237", "fm_6070522429", "fm_4062521154", "fm_5050421743", "fm_11112425389" };
            var cards = preferred.Select(id => CardCatalog.All.FirstOrDefault(card => card.id == id && card.kind == CardKind.Unit)).Where(card => card != null).ToList();
            foreach (var card in CardCatalog.All.Where(card => card.kind == CardKind.Unit))
                if (cards.Count < 7 && !cards.Contains(card)) cards.Add(card);
            return cards;
        }

        private void ShowLocalProfile(Action back)
        {
            battleScreen = false; redraw = () => ShowLocalProfile(back); Clear(); DetailPanorama(true);
            if (profileDraftName == null) profileDraftName = LocalProfileName;
            Text(root, "나의 프로필", .06f, .875f, .94f, .97f, 43, ivory);
            Text(root, "닉네임과 아바타, 이 기기에 기록된 연습전 이야기", .06f, .81f, .94f, .87f, 23, gold);
            var portrait = Panel(root, .06f, .47f, .30f, .80f, new Color(.035f, .075f, .055f, .98f));
            portrait.gameObject.name = "Selected profile avatar";
            var avatar = LocalProfileAvatar(); Artwork(portrait, avatar, .035f, .035f, .965f, .965f); Frame(portrait, ElementColor(avatar.element), 2);
            Text(root, avatar.name + " · " + CardCatalog.ElementName(avatar.element), .06f, .405f, .30f, .465f, 22, ivory, TextAlignmentOptions.Center);
            Text(root, "닉네임", .35f, .77f, .94f, .83f, 24, gold);
            var inputArea = Panel(root, .35f, .65f, .75f, .755f, new Color(.045f, .080f, .065f, .98f));
            inputArea.gameObject.name = "Profile nickname input"; inputArea.GetComponent<Image>().raycastTarget = true; Frame(inputArea, gold, 1);
            var input = inputArea.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(inputArea, .04f, .12f, .96f, .88f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var nameText = Text(viewport, profileDraftName, 0, 0, 1, 1, 29, ivory);
            nameText.enableAutoSizing = false; nameText.overflowMode = TextOverflowModes.Overflow;
            input.textViewport = viewport; input.textComponent = nameText; input.characterLimit = 12;
            input.contentType = TMP_InputField.ContentType.Standard; input.lineType = TMP_InputField.LineType.SingleLine;
            input.text = profileDraftName; input.onValueChanged.AddListener(value => profileDraftName = value);
            var status = Text(root, "이 브라우저·기기의 로컬 프로필입니다.", .35f, .59f, .94f, .645f, 20, new Color(.73f, .82f, .74f));
            Button(root, "이름 저장", .77f, .65f, .94f, .755f, () =>
            {
                profileDraftName = SafeProfileName(input.text); PlayerPrefs.SetString(ProfileNameKey, profileDraftName); PlayerPrefs.Save();
                input.text = profileDraftName; status.text = "닉네임을 저장했습니다.";
            }, gold, 26);
            int wins = LocalProfileWins, losses = LocalProfileLosses, draws = LocalProfileDraws;
            string[] labels = { "기록한 대전", "승리", "패배 / 무승부" };
            string[] values = { (wins + losses + draws).ToString(), wins.ToString(), losses + " / " + draws };
            for (int i = 0; i < labels.Length; i++)
            {
                float x = .35f + i * .20f;
                var stat = Panel(root, x, .445f, x + .19f, .585f, new Color(.035f, .075f, .06f, .96f)); Frame(stat, gold, 1);
                Text(stat, labels[i], .05f, .60f, .95f, .95f, 20, new Color(.73f, .82f, .74f), TextAlignmentOptions.Center);
                Text(stat, values[i], .05f, .08f, .95f, .60f, 34, i == 1 ? gold : ivory, TextAlignmentOptions.Center);
            }
            Text(root, "아바타 선택 · 원작 카드 원화", .06f, .365f, .94f, .425f, 24, gold);
            var choices = ProfileAvatars();
            for (int i = 0; i < choices.Count; i++)
            {
                var choice = choices[i]; float x = .06f + i * .128f;
                var tile = Button(root, "", x, .13f, x + .113f, .36f,
                    () => { PlayerPrefs.SetString(ProfileAvatarKey, choice.id); PlayerPrefs.Save(); ShowLocalProfile(back); }, slate, 20);
                tile.gameObject.name = "Profile avatar " + choice.id;
                Artwork(tile.transform, choice, .025f, .23f, .975f, .97f);
                Text(tile.transform, choice.name, .03f, .025f, .97f, .22f, 20, ivory, TextAlignmentOptions.Center);
                Frame((RectTransform)tile.transform, avatar.id == choice.id ? gold : ElementColor(choice.element), avatar.id == choice.id ? 3 : 1);
                BindTooltip(tile.gameObject, choice.name, "이 원작 카드 원화를 프로필 아바타로 선택합니다. 카드 기술이나 전투 능력을 부여하지 않습니다.");
            }
            Button(root, "돌아가기", .06f, .02f, .27f, .115f, back, slate, 25);
            Button(root, "저장 옮기기", .31f, .025f, .55f, .105f, () =>
            { profileTransferReturn = () => ShowLocalProfile(back); ShowTransfer(); }, slate, 23);
            int completed = Enumerable.Range(0, DemoCatalog.Encounters.Length).Count(i => PlayerPrefs.GetInt("FC.demo.won." + i, 0) != 0);
            Text(root, "AI 승리 " + completed + "/" + DemoCatalog.Encounters.Length + " · " + CardCatalog.All.Length + "종", .57f, .025f, .94f, .105f, 22, ivory, TextAlignmentOptions.MidlineRight);
        }

        private void RecordLocalBattleResult()
        {
            if (engine == null || engine.State.phase != BattlePhase.Complete) return;
            // A completed save can be reopened or redrawn. Its stable finished-state
            // fingerprint prevents recording the same result more than once.
            string resultKey = "FC.profile.recorded." + Hash128.Compute(JsonUtility.ToJson(engine.State));
            if (PlayerPrefs.GetInt(resultKey, 0) != 0) return;
            string category = engine.State.winner == 0 ? "wins" : engine.State.winner == 1 ? "losses" : "draws";
            string key = "FC.profile." + category;
            PlayerPrefs.SetInt(key, PlayerPrefs.GetInt(key, 0) + 1);
            PlayerPrefs.SetInt(resultKey, 1); PlayerPrefs.Save();
        }

        private void DrawTerrainBackfield(PlayerState player, int owner, float bottom, float top)
        {
            bool mine = owner == 0, small = canvas.pixelRect.height < 600;
            var strip = Panel(root, .025f, bottom, .805f, top, new Color(.025f, .055f, .042f, .98f));
            strip.gameObject.name = mine ? "Terrain drop band" : "Opponent terrain backfield";
            strip.GetComponent<Image>().raycastTarget = true;
            Frame(strip, new Color(.58f, .50f, .31f, .65f), 1);
            Text(strip, (mine ? "내 지형" : "상대 지형") + "\n" + player.terrain.Count + "/" + BattleDisplayState.rules.terrainLimit,
                .01f, .06f, .17f, .94f, 21, mine ? gold : teal, TextAlignmentOptions.Center);
            int limit = BattleDisplayState.rules.terrainLimit;
            float width = .81f / Mathf.Max(1, limit);
            for (int i = 0; i < limit; i++)
            {
                float left = .18f + i * width;
                var tile = Panel(strip, left, .07f, left + width - .004f, .93f, new Color(.06f, .095f, .065f));
                tile.gameObject.name = "Backfield " + owner + " " + i;
                if (i >= player.terrain.Count)
                {
                    Frame(tile, new Color(.40f, .45f, .31f, .25f), 1);
                    if (!small) Text(tile, (i + 1).ToString("00"), .02f, .02f, .98f, .98f, 17, new Color(.30f, .38f, .31f), TextAlignmentOptions.Center);
                    continue;
                }
                var instance = player.terrain[i]; var card = CardCatalog.Get(instance.definitionId);
                var texture = OriginalCardTexture(card);
                if (texture != null) ImageFrame(tile, texture, new Rect(.13f, .36f, .805f, .56f), .02f, .03f, .98f, .98f, new Vector2(.5f, .5f), true);
                else Artwork(tile, card, .02f, .03f, .98f, .98f);
                if (!small)
                {
                    Panel(tile, .02f, .02f, .98f, .30f, new Color(.025f, .045f, .035f, .95f));
                    Text(tile, card.name, .035f, .03f, .965f, .30f, 16, ivory, TextAlignmentOptions.Center);
                }
                else
                {
                    var mark = Panel(tile, .025f, .02f, .33f, .38f, new Color(.02f, .04f, .03f, .90f));
                    ElementIcon(mark, card.element, .05f, .05f, .95f, .95f);
                }
                Frame(tile, ElementColor(card.element), 1.5f);
                tile.GetComponent<Image>().raycastTarget = true;
                BindCardHelp(tile.gameObject, card, () => ShowDetail(card, ShowBattle));
                tile.GetComponent<UiCardHover>().HoverScale = 1.02f;
                var button = tile.gameObject.AddComponent<Button>(); button.targetGraphic = tile.GetComponent<Image>(); button.interactable = !locked;
                button.onClick.AddListener(() => { if (!locked) ShowDetail(card, ShowBattle); });
                BindTooltip(tile.gameObject, card.name + " · 백필드 지형", CardCatalog.CostText(card) + "\n" + card.text + "\n클릭하거나 길게 눌러 원본 카드와 정보를 봅니다.");
            }
            if (mine)
            {
                BindTooltip(strip.gameObject, "지형 백필드 · 끌어놓기", "손패의 지형을 이 줄로 끌어놓거나 ‘지형 전개’를 누르세요. 최대 " + limit + "장입니다.\n" + TerrainSummary(player));
                BindTerrainDrop(strip);
            }
            else BindTooltip(strip.gameObject, "상대 백필드", TerrainSummary(player));
        }
    }
}
