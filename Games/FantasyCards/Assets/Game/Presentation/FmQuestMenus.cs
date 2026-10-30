using System;
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
        private string questStarterChoice = "";
        private int questStarterPage, questLobbyTab, questOwnedPage, questOwnedElement = -1, questOwnedRarity = -1;
        private bool questOwnedDeckOnly;
        private string questOwnedSearch = "", questOwnedSearchDraft = "";

        private void QuestScreen(string name, Action draw, string title, string subtitle)
        {
            questFreePracticeView = false; QuestCurrentScreen = name;
            battleScreen = false; redraw = draw; Clear(); UpdateWorld(); FmOriginalMusic.SetScene(name == "collection" || name == "deck" ? "deck" : "lobby");
            polishBack = name == "lobby" ? (Action)ShowTitle : ShowQuestLobby;
            RestorationBackdrop();
            Text(root, "FANTASY MASTERS", .045f, .92f, .74f, .975f, 33, gold);
            Text(root, title, .045f, .836f, .74f, .918f, 35, ivory);
            Text(root, subtitle, .045f, .765f, .744f, .835f, 23, new Color(.75f, .80f, .90f));
            DrawQuestRail();
        }

        private void DrawQuestRail()
        {
            var rail = RestorationPanel(root, .77f, .045f, .96f, .957f, new Color(.015f, .027f, .064f, .98f), "Quest wallet and profile rail");
            Text(rail, "FANTASY\nMASTERS", .04f, .85f, .96f, .98f, 31, gold, TextAlignmentOptions.Center);
            var orb = Panel(rail, .29f, .72f, .71f, .86f, Color.clear); RestorationTrim(orb, true);
            Text(rail, LocalProfileName, .04f, .65f, .96f, .73f, 24, ivory, TextAlignmentOptions.Center);
            var wallet = Panel(rail, .06f, .51f, .94f, .64f, new Color(.08f, .095f, .15f));
            wallet.gameObject.name = "Quest actual SP balance";
            Text(wallet, "SP · 게임머니", .05f, .56f, .95f, .98f, 20, RestorationMetal, TextAlignmentOptions.Center);
            Text(wallet, QuestSp.ToString("N0"), .03f, .01f, .97f, .57f, 35, gold, TextAlignmentOptions.Center);
            var cash = RestorationButton(rail, "GP  " + QuestGp + " · 준비 중", .06f, .415f, .94f, .495f, ShowQuestPaidInformation);
            cash.gameObject.name = "Quest GP future payments notice";
            BindTooltip(cash.gameObject, "GP · 유료 캐시", "GP 결제와 유료 상품은 추후 추가됩니다. 현재는 SP로 카드팩을 구입할 수 있습니다.");
            Text(rail, "보유 " + QuestOwnedTotal.ToString("N0") + "장\n" + (questEconomy == null ? 0 : CardCatalog.All.Count(card => questEconomy.OwnedCount(card.id) > 0)) + "종", .05f, .315f, .95f, .41f, 22, teal, TextAlignmentOptions.Center);
            RestorationButton(rail, "프로필", .06f, .235f, .94f, .31f, () => ShowLocalProfile(redraw ?? ShowLobby));
            RestorationButton(rail, "음악 / 효과음", .06f, .155f, .94f, .228f, () => ShowSoundSettings(redraw ?? ShowLobby));
            RestorationButton(rail, "타이틀", .06f, .065f, .94f, .145f, ShowTitle);
            Text(rail, "싱글 플레이", .03f, .003f, .97f, .06f, 18, RestorationMetal, TextAlignmentOptions.Center);
        }

        private void QuestNavigation()
        {
            string[] names = { "싱글", "카드 상점", "보유 카드", "덱 편집", "임무", "자유 연습" };
            Action[] actions = { ShowQuestLobby, ShowQuestShop, () => ShowQuestOwnedCards(false), () => ShowQuestOwnedCards(true), ShowQuestObjectives, ShowFreePracticeLobby };
            for (int i = 0; i < names.Length; i++)
            {
                float x = .045f + i * .117f;
                var action = actions[i];
                RestorationButton(root, names[i], x, .045f, x + .109f, .125f, () => { questNotice = ""; action(); });
            }
        }

        private void QuestFooterNotice(string fallback = "")
        {
            string value = string.IsNullOrEmpty(questNotice) ? fallback : questNotice;
            Text(root, value, .045f, .135f, .742f, .205f, 21, new Color(.77f, .83f, .92f));
        }

        private void ShowQuestLobby()
        {
            if (questEconomy == null || !questEconomy.HasStarter) { ShowQuestEntry(); return; }
            QuestScreen("lobby", ShowQuestLobby, "싱글 플레이 대기실", "기본 덱으로 시작하고, 대전 보상으로 카드를 모으세요.");
            RestorationButton(root, "시나리오", .045f, .672f, .27f, .752f, () => { questLobbyTab = 0; ShowQuestLobby(); }, questLobbyTab == 0);
            RestorationButton(root, "AI 대전", .285f, .672f, .51f, .752f, () => { questLobbyTab = 1; ShowQuestLobby(); }, questLobbyTab == 1);
            RestorationButton(root, "멀티플레이", .525f, .672f, .744f, .752f, ShowQuestMultiplayerNotice);
            var list = RestorationPanel(root, .045f, .215f, .744f, .655f, RestorationLeather, "Quest single player stage table");
            if (!string.IsNullOrEmpty(questEconomy.ActiveBattleTicketId))
            {
                Text(list, "진행 중인 싱글 대전이 있습니다.", .04f, .70f, .96f, .93f, 29, ivory);
                Text(list, "이어서 플레이하거나, 보상 없이 포기하고 새 대전을 시작하세요.", .04f, .47f, .96f, .72f, 24, RestorationMetal);
                RestorationButton(list, "싱글 대전 이어하기", .04f, .15f, .60f, .42f, ContinueQuestBattle, true,
                    HasQuestBattleSave && questEconomy.ActiveBattleTicketId == PlayerPrefs.GetString(QuestTicketKey, ""));
                RestorationButton(list, "대전 포기", .63f, .15f, .96f, .42f, () => ShowQuestAbandonConfirmation());
            }
            else if (questLobbyTab == 0)
            {
                var completed = questEconomy.CompletedScenarios;
                for (int i = 0; i < FmScenarioCatalog.Stages.Length; i++)
                {
                    var stage = FmScenarioCatalog.Stages[i];
                    bool won = completed.Contains(stage.id), available = FmScenarioCatalog.IsUnlocked(stage.id, completed);
                    float top = .96f - i * .238f;
                    var row = RestorationButton(list, "", .025f, top - .213f, .975f, top, () => ShowQuestStage(stage.id), won, available);
                    row.gameObject.name = "Quest scenario " + stage.id;
                    Text(row.transform, stage.tier.ToString("00"), .015f, .05f, .11f, .95f, 28, gold, TextAlignmentOptions.Center);
                    Text(row.transform, stage.name, .13f, .44f, .79f, .96f, 27, ivory);
                    Text(row.transform, won ? "다시 승리하면 SP를 받습니다." : "승리 SP + 첫 클리어 보너스", .13f, .03f, .79f, .47f, 21, RestorationMetal);
                    Text(row.transform, won ? "완료 · 입장" : available ? "입장" : "잠김", .80f, .05f, .98f, .95f, 23, available ? gold : RestorationMetal, TextAlignmentOptions.Center);
                    BindTooltip(row.gameObject, stage.name, available ? stage.description : "앞선 시나리오에서 승리하면 열립니다.");
                }
            }
            else
            {
                var practice = DemoCatalog.Encounters.Select((encounter, index) => new { encounter, index })
                    .Where(entry => !FmScenarioCatalog.Stages.Any(stage => stage.encounterIndex == entry.index)).Take(3).ToArray();
                for (int i = 0; i < practice.Length; i++)
                {
                    var entry = practice[i]; float top = .96f - i * .32f;
                    var row = RestorationButton(list, "", .025f, top - .29f, .975f, top,
                        () => ShowQuestEncounter(entry.index), false);
                    Text(row.transform, entry.encounter.name, .04f, .43f, .79f, .95f, 28, ivory);
                    Text(row.transform, "내 보유 덱으로 도전 · 대전 보상 SP", .04f, .04f, .79f, .45f, 21, RestorationMetal);
                    Text(row.transform, "입장", .81f, .06f, .98f, .94f, 25, gold, TextAlignmentOptions.Center);
                }
            }
            QuestFooterNotice("내 싱글 덱 " + questDeck.Count + "/50장 · 받은 카드와 카드팩에서 얻은 카드로 편집합니다.");
            QuestNavigation();
        }

        private void ShowQuestStage(string id)
        {
            var stage = FmScenarioCatalog.Get(id);
            ShowQuestEncounter(stage.encounterIndex, id);
        }

        private void ShowQuestEncounter(int encounterIndex, string scenarioId = null)
        {
            var encounter = DemoCatalog.GetEncounter(encounterIndex);
            var scenario = string.IsNullOrEmpty(scenarioId) ? null : FmScenarioCatalog.Get(scenarioId);
            QuestScreen("briefing", () => ShowQuestEncounter(encounterIndex, scenarioId), scenario?.name ?? encounter.name, "내 보유 덱 · 50장 · 원작 카드와 코인 공방");
            var panel = RestorationPanel(root, .045f, .23f, .744f, .74f, RestorationLeather, "Quest owned deck match briefing");
            Text(panel, "나  /  " + LocalProfileName, .045f, .73f, .955f, .92f, 29, ivory);
            Text(panel, "보유 덱 " + questDeck.Count + "장 · " + DeckKindCounts(questDeck), .045f, .56f, .955f, .75f, 23, gold);
            Text(panel, "상대  /  " + encounter.name + " · AI", .045f, .36f, .955f, .56f, 28, ivory);
            Text(panel, scenario?.objective ?? encounter.description, .045f, .10f, .955f, .36f, 24, RestorationMetal);
            string error = ""; bool valid = questEconomy != null && questEconomy.ValidateOwnedDeck(questDeck, out error);
            if (!valid) questNotice = error;
            RestorationButton(root, "대전 시작", .045f, .135f, .39f, .215f, () => BeginQuestBattle(encounterIndex, scenarioId), true, valid);
            RestorationButton(root, "덱 확인 / 편집", .405f, .135f, .744f, .215f, () => ShowQuestOwnedCards(true));
            QuestNavigation();
        }

        private void ShowQuestStarters()
        {
            if (questEconomy == null) { ShowQuestLoadError(); return; }
            if (questEconomy.HasStarter) { ShowQuestLobby(); return; }
            QuestCurrentScreen = "starter"; battleScreen = false; questFreePracticeView = false;
            redraw = ShowQuestStarters; Clear(); UpdateWorld(); FmOriginalMusic.SetScene("deck"); RestorationBackdrop();
            var choices = FmQuestCatalog.StarterChoices.ToArray();
            if (!choices.Any(deck => deck.id == questStarterChoice)) questStarterChoice = choices[0].id;
            int pages = (choices.Length + 3) / 4; questStarterPage = Mathf.Clamp(questStarterPage, 0, pages - 1);
            Text(root, "처음 만나는 판타지마스터즈", .045f, .88f, .955f, .975f, 43, ivory);
            Text(root, "일곱 속성 중 기본 덱 하나를 무료로 받으세요. 선택한 50장은 내 카드가 됩니다.", .045f, .79f, .955f, .88f, 25, gold);
            for (int i = 0; i < 4 && questStarterPage * 4 + i < choices.Length; i++)
            {
                var deck = choices[questStarterPage * 4 + i]; float x = i % 2 == 0 ? .045f : .515f, y = i < 2 ? .505f : .235f;
                var element = ArcanaDeckElements(deck)[0]; bool selected = questStarterChoice == deck.id;
                var tile = RestorationButton(root, "", x, y, x + .44f, y + .25f,
                    () => { questStarterChoice = deck.id; ShowQuestStarters(); }, selected);
                tile.gameObject.name = "Quest starter choice " + deck.id;
                var featured = deck.cards.Select(CardCatalog.Get).Where(card => card.kind == CardKind.Unit).OrderByDescending(card => card.level).First();
                ArcanaCardFace(tile.transform, featured, .012f, .04f, .29f, .96f, ArcanaCardMode.Album);
                Text(tile.transform, CardCatalog.ElementName(element) + " 기본 덱", .315f, .58f, .975f, .96f, 30, selected ? gold : ivory);
                Text(tile.transform, "무료 50장 · " + (selected ? "선택됨" : "선택하기"), .315f, .34f, .975f, .60f, 24, teal);
                Text(tile.transform, deck.description, .315f, .035f, .975f, .34f, 20, RestorationMetal);
                BindTooltip(tile.gameObject, deck.name, DeckKindCounts(deck.cards) + "\n" + deck.description);
            }
            Button(root, "이전", .045f, .153f, .20f, .225f, () => { questStarterPage--; ShowQuestStarters(); }, slate, 23, questStarterPage > 0);
            Text(root, (questStarterPage + 1) + " / " + pages, .21f, .153f, .37f, .225f, 23, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .38f, .153f, .535f, .225f, () => { questStarterPage++; ShowQuestStarters(); }, slate, 23, questStarterPage + 1 < pages);
            var selectedDeck = choices.First(deck => deck.id == questStarterChoice);
            Button(root, "이 덱을 무료로 받기", .55f, .153f, .955f, .225f, () => SelectQuestStarter(questStarterChoice), gold, 26);
            Text(root, string.IsNullOrEmpty(questNotice) ? selectedDeck.name + " · 처음 한 번, 한 속성만 선택할 수 있습니다." : questNotice,
                .045f, .082f, .955f, .15f, 23, ivory);
            Button(root, "타이틀", .045f, .012f, .25f, .078f, ShowTitle, slate, 23);
            Button(root, "모든 기본 덱 자유 연습", .54f, .012f, .955f, .078f, ShowFreePracticeLobby, slate, 23);
        }

        private void ShowQuestOwnedCards(bool edit)
        {
            if (questEconomy == null || !questEconomy.HasStarter) { ShowQuestEntry(); return; }
            QuestScreen(edit ? "deck" : "collection", () => ShowQuestOwnedCards(edit), edit ? "보유 덱 편집" : "내 카드 보관함",
                edit ? questDeck.Count + "/50장 · 보유한 수량과 카드별 덱 한도 안에서 편집합니다." : "기본 덱과 개봉한 카드팩에서 얻은 실제 보유 카드입니다.");
            var cards = CardCatalog.All.Where(card => questEconomy.OwnedCount(card.id) > 0
                && (questOwnedElement < 0 || (int)card.element == questOwnedElement)
                && (questOwnedRarity < 0 || (int)card.rarity == questOwnedRarity)
                && (!questOwnedDeckOnly || questDeck.Contains(card.id))
                && (string.IsNullOrWhiteSpace(questOwnedSearch) || card.name.IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0
                    || (card.text ?? "").IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0
                    || (card.sourceId ?? "").IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0
                    || CardCatalog.RarityName(card.rarity).IndexOf(questOwnedSearch, StringComparison.OrdinalIgnoreCase) >= 0))
                .OrderBy(card => card.element).ThenBy(card => card.level).ThenBy(card => card.name).ToArray();
            QuestOwnedSearchField(edit);
            Button(root, "속성 · " + (questOwnedElement < 0 ? "전체" : CardCatalog.ElementName((Element)questOwnedElement)), .045f, .615f, .265f, .675f,
                () => { questOwnedElement++; if (questOwnedElement >= 7) questOwnedElement = -1; questOwnedPage = 0; ShowQuestOwnedCards(edit); }, slate, 22);
            Button(root, "등급 · " + (questOwnedRarity < 0 ? "전체" : CardCatalog.RarityCode((CardRarity)questOwnedRarity)), .28f, .615f, .50f, .675f,
                () => { questOwnedRarity++; if (questOwnedRarity >= Enum.GetValues(typeof(CardRarity)).Length) questOwnedRarity = -1; questOwnedPage = 0; ShowQuestOwnedCards(edit); }, slate, 22);
            Button(root, questOwnedDeckOnly ? "현재 덱만" : "보유 카드 전체", .515f, .615f, .744f, .675f,
                () => { questOwnedDeckOnly = !questOwnedDeckOnly; questOwnedPage = 0; ShowQuestOwnedCards(edit); }, questOwnedDeckOnly ? gold : slate, 22);
            int pages = Mathf.Max(1, (cards.Length + 3) / 4); questOwnedPage = Mathf.Clamp(questOwnedPage, 0, pages - 1);
            for (int i = 0; i < 4 && questOwnedPage * 4 + i < cards.Length; i++)
            {
                var card = cards[questOwnedPage * 4 + i]; float left = .045f + i * .177f, right = left + .168f;
                int inDeck = questDeck.Count(id => id == card.id), owned = questEconomy.OwnedCount(card.id);
                DrawCard(root, card, left, .295f, right, .605f, inDeck > 0, () => ShowDetail(card, () => ShowQuestOwnedCards(edit)), true);
                Text(root, "보유 " + owned + " · 덱 " + inDeck, left, .252f, right, .293f, 20, RarityColor(card.rarity), TextAlignmentOptions.Center);
                if (edit)
                {
                    var minus = Button(root, "−", left, .205f, left + .048f, .252f, () => { questDeck.Remove(card.id); ShowQuestOwnedCards(true); }, slate, 27, inDeck > 0);
                    minus.GetComponentInChildren<UiCanvasTextSize>().ConfigureCard(27);
                    Text(root, inDeck + "/" + Math.Min(owned, card.deckLimit), left + .049f, .205f, right - .049f, .252f, 22, ivory, TextAlignmentOptions.Center);
                    var plus = Button(root, "+", right - .048f, .205f, right, .252f, () => { questDeck.Add(card.id); ShowQuestOwnedCards(true); }, slate, 27,
                        questDeck.Count < 50 && inDeck < Math.Min(owned, card.deckLimit));
                    plus.GetComponentInChildren<UiCanvasTextSize>().ConfigureCard(27);
                }
            }
            if (cards.Length == 0) Text(root, "이 조건에 맞는 보유 카드가 없습니다.", .07f, .35f, .72f, .53f, 29, ivory, TextAlignmentOptions.Center);
            Button(root, "이전", .045f, .135f, .16f, .195f, () => { questOwnedPage--; ShowQuestOwnedCards(edit); }, slate, 22, questOwnedPage > 0);
            Text(root, (questOwnedPage + 1) + "/" + pages, .17f, .135f, .28f, .195f, 23, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .29f, .135f, .405f, .195f, () => { questOwnedPage++; ShowQuestOwnedCards(edit); }, slate, 22, questOwnedPage + 1 < pages);
            if (edit)
            {
                Button(root, "받은 기본 덱", .42f, .135f, .575f, .195f,
                    () => { questDeck.Clear(); questDeck.AddRange(DemoCatalog.GetDeck(questEconomy.StarterDeckId).CopyCards()); ShowQuestOwnedCards(true); }, slate, 21);
                string reason; bool valid = questEconomy.ValidateOwnedDeck(questDeck, out reason);
                Button(root, "덱 저장", .59f, .135f, .744f, .195f, () => { if (SaveQuestDeck()) ShowQuestLobby(); else ShowQuestOwnedCards(true); }, gold, 23, valid);
                BindTooltip(root.GetChild(root.childCount - 1).gameObject, "보유 덱 저장", valid ? "50장 덱을 저장합니다." : reason);
            }
            else Button(root, "검색 초기화", .42f, .135f, .744f, .195f,
                () => { questOwnedElement = questOwnedRarity = -1; questOwnedSearch = questOwnedSearchDraft = ""; questOwnedDeckOnly = false; ShowQuestOwnedCards(false); }, slate, 22);
            QuestNavigation();
        }

        private void QuestOwnedSearchField(bool edit)
        {
            var area = Panel(root, .045f, .69f, .585f, .755f, new Color(.04f, .065f, .11f));
            area.gameObject.name = "Quest owned card search"; area.GetComponent<Image>().raycastTarget = true;
            var input = area.gameObject.AddComponent<TMP_InputField>();
            var viewport = Panel(area, .02f, .08f, .98f, .92f, Color.clear); viewport.gameObject.AddComponent<RectMask2D>();
            var label = Text(viewport, "", 0, 0, 1, 1, 23, ivory); label.enableAutoSizing = false; label.richText = false;
            var hint = Text(viewport, "보유 카드 이름 · 효과", 0, 0, 1, 1, 22, RestorationMetal);
            input.textViewport = viewport; input.textComponent = label; input.placeholder = hint;
            input.lineType = TMP_InputField.LineType.SingleLine; input.characterLimit = 48; input.SetTextWithoutNotify(questOwnedSearchDraft);
            input.onValueChanged.AddListener(value => questOwnedSearchDraft = value);
            void Search() { questOwnedSearch = questOwnedSearchDraft.Trim(); questOwnedPage = 0; ShowQuestOwnedCards(edit); }
            input.onSubmit.AddListener(_ => Search());
            Button(root, "검색", .60f, .69f, .744f, .755f, Search, gold, 23);
        }

        private void ShowQuestObjectives()
        {
            if (questEconomy == null || !questEconomy.HasStarter) { ShowQuestEntry(); return; }
            QuestScreen("missions", ShowQuestObjectives, "싱글 플레이 임무", "대전·카드 수집으로 달성한 보상을 받으세요. 일일 임무는 한국 시간 기준입니다.");
            var objectives = FmQuestCatalog.Objectives.ToArray();
            int perPage = 4, pages = Mathf.Max(1, (objectives.Length + perPage - 1) / perPage);
            questMissionPage = Mathf.Clamp(questMissionPage, 0, pages - 1);
            for (int i = 0; i < perPage && questMissionPage * perPage + i < objectives.Length; i++)
            {
                var objective = objectives[questMissionPage * perPage + i]; float top = .75f - i * .13f;
                bool claimable = questEconomy.CanClaimQuest(objective.id, QuestCalendarDay, out var reason);
                bool claimed = questEconomy.IsQuestClaimed(objective.id, QuestCalendarDay);
                var row = RestorationPanel(root, .045f, top - .118f, .744f, top, RestorationLeather, "Quest objective " + objective.id);
                Text(row, objective.name + (objective.isDaily ? " · 일일" : ""), .025f, .47f, .72f, .98f, 25, claimed ? RestorationMetal : ivory);
                Text(row, objective.description, .025f, .04f, .72f, .49f, 20, RestorationMetal);
                var button = RestorationButton(row, claimed ? "완료" : claimable ? "+" + objective.rewardSp + " SP 받기" : "진행 중", .735f, .12f, .98f, .88f,
                    () => ClaimQuestObjective(objective.id), claimable, claimable);
                BindTooltip(button.gameObject, objective.name, claimable ? objective.rewardSp + " SP를 받습니다." : reason);
            }
            Button(root, "이전", .045f, .135f, .20f, .205f, () => { questMissionPage--; ShowQuestObjectives(); }, slate, 23, questMissionPage > 0);
            Text(root, (questMissionPage + 1) + "/" + pages, .21f, .135f, .37f, .205f, 23, gold, TextAlignmentOptions.Center);
            Button(root, "다음", .38f, .135f, .535f, .205f, () => { questMissionPage++; ShowQuestObjectives(); }, slate, 23, questMissionPage + 1 < pages);
            Button(root, "오늘의 무료 카드팩", .55f, .135f, .744f, .205f, () => { questShopFilter = 1; ShowQuestShop(); }, gold, 23);
            QuestNavigation();
        }

        private int questMissionPage;
        private void ClaimQuestObjective(string id)
        {
            var before = questEconomy.CaptureSave();
            if (!questEconomy.ClaimQuest(id, QuestCalendarDay, out var reward, out var reason)) { questNotice = reason; ShowQuestObjectives(); return; }
            if (CommitQuestMutation(before)) { questNotice = reward.label + " · +" + reward.spAwarded + " SP"; FmBattleAudio.Play("card-draw"); }
            ShowQuestObjectives();
        }

        private void ShowQuestSimpleNotice(string title, string text, Action back)
        {
            QuestCurrentScreen = "notice"; battleScreen = false; redraw = () => ShowQuestSimpleNotice(title, text, back); Clear(); polishBack = back; RestorationBackdrop();
            var panel = RestorationPanel(root, .10f, .22f, .90f, .80f, RestorationLeather, "Quest information notice");
            Text(panel, title, .06f, .70f, .94f, .93f, 42, gold, TextAlignmentOptions.Center);
            Text(panel, text, .06f, .23f, .94f, .70f, 28, ivory, TextAlignmentOptions.Center);
            RestorationButton(panel, "확인", .30f, .045f, .70f, .22f, back, true);
        }

        private void ShowQuestMultiplayerNotice()
        { ShowQuestSimpleNotice("멀티플레이 · 추후 추가 예정", "현재는 싱글 플레이를 즐길 수 있습니다.\n온라인 대전과 매칭은 이후에 추가됩니다.", ShowQuestLobby); }

        private void ShowQuestPaidInformation()
        { var back = redraw ?? ShowLobby; ShowQuestSimpleNotice("GP · 추후 추가 예정", "유료 캐시 GP와 결제 기능은 아직 제공하지 않습니다.\n싱글 플레이에서 얻은 SP로 카드팩을 구입하세요.", back); }

        private void ShowQuestAbandonConfirmation()
        {
            battleScreen = false; redraw = ShowQuestAbandonConfirmation; Clear(); RestorationBackdrop();
            Text(root, "진행 중인 대전을 포기할까요?", .10f, .62f, .90f, .82f, 42, gold, TextAlignmentOptions.Center);
            Text(root, "포기한 대전의 SP와 첫 승리 보상은 지급되지 않습니다.\n보유 카드·재화·기존 클리어 기록은 유지됩니다.", .10f, .38f, .90f, .62f, 29, ivory, TextAlignmentOptions.Center);
            Button(root, "계속 플레이", .13f, .15f, .48f, .32f, ShowQuestLobby, slate, 27);
            Button(root, "보상 없이 포기", .52f, .15f, .87f, .32f, AbandonQuestBattle, gold, 27);
        }

        private void ShowQuestLoadError()
        {
            QuestCurrentScreen = "profile-error"; battleScreen = false; redraw = ShowQuestLoadError; Clear(); RestorationBackdrop();
            Text(root, "싱글 플레이 저장 확인", .07f, .75f, .93f, .92f, 42, gold, TextAlignmentOptions.Center);
            Text(root, questLoadError, .08f, .35f, .92f, .72f, 27, ivory, TextAlignmentOptions.Center);
            Button(root, "타이틀", .10f, .12f, .46f, .29f, ShowTitle, slate, 26);
            Button(root, "자유 연습", .54f, .12f, .90f, .29f, ShowFreePracticeLobby, gold, 26);
        }
    }
}
