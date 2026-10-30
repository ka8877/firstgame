using System;
using System.Collections.Generic;
using System.Linq;
using FantasyCards.Core;
using FirstGame.Shared;
using UnityEngine;

namespace FantasyCards.Presentation
{
    public sealed partial class FantasyCardsApp
    {
        private const string QuestBattleSlot = "fantasy-quest-battle-v1";
        private const string QuestDeckSlot = "fantasy-quest-deck-v1";
        private const string QuestModeKey = "FC.quest.lastBattleMode";
        private const string QuestTicketKey = "FC.quest.battleTicket";
        private FmQuestEconomy questEconomy;
        private readonly List<string> questDeck = new List<string>();
        private bool questBattleMode, questFreePracticeView;
        private string questLoadError = "", questNotice = "", questBattleTicket = "";
        private FmQuestRewardReceipt questBattleReward;

        public bool IsQuestBattle => questBattleMode;
        public bool QuestHasStarter => questEconomy != null && questEconomy.HasStarter;
        public int QuestSp => questEconomy?.Sp ?? 0;
        public int QuestGp => questEconomy?.PaidPoints ?? 0;
        public string QuestStarterDeckId => questEconomy?.StarterDeckId ?? "";
        public int QuestOwnedCount(string cardId) => questEconomy?.OwnedCount(cardId) ?? 0;
        public IReadOnlyList<string> QuestDeckCards => questDeck.AsReadOnly();
        public string QuestCurrentScreen { get; private set; } = "title";
        public string QuestCurrentTicketId => questBattleTicket;
        public FmQuestProfile QuestProfileSnapshot => questEconomy?.CaptureSave();

        private string ActiveBattleSlot => questBattleMode ? QuestBattleSlot : Slot;
        private bool HasQuestBattleSave => questEconomy != null && PersistentSaves.Exists(QuestBattleSlot)
            && questEconomy.CaptureSave().battles.Any(ticket => ticket.id == PlayerPrefs.GetString(QuestTicketKey, "")
                && ticket.status != FmQuestBattleStatus.Abandoned);
        private bool HasAnyBattleSave => HasQuestBattleSave || PersistentSaves.Exists(Slot);
        private static string QuestCalendarDay => DateTime.UtcNow.AddHours(9).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);
        private long QuestOwnedTotal => questEconomy == null ? 0 : CardCatalog.All.Sum(card => (long)questEconomy.OwnedCount(card.id));

        private void InitializeQuestProfile()
        {
            questLoadError = "";
            try
            {
                if (PersistentSaves.Exists(FmQuestEconomy.SaveSlot))
                {
                    if (!PersistentSaves.TryLoad(FmQuestEconomy.SaveSlot, FmQuestEconomy.GameId,
                        FmQuestEconomy.SchemaVersion, FmQuestEconomy.ContentVersion, out var payload, out var error))
                        throw new ArgumentException(error);
                    questEconomy = FmQuestEconomy.FromSave(JsonUtility.FromJson<FmQuestProfile>(payload));
                }
                else
                {
                    uint seed = unchecked((uint)DateTime.UtcNow.Ticks);
                    questEconomy = FmQuestEconomy.New(seed == 0 ? 1u : seed);
                }
                LoadQuestDeck();
            }
            catch (Exception exception)
            {
                questEconomy = null;
                questLoadError = "싱글 플레이 저장을 열 수 없습니다. 원래 저장은 보존했습니다.\n" + exception.Message;
            }
        }

        private bool SaveQuestProfile()
        {
            if (questEconomy == null) return false;
            bool saved = PersistentSaves.TrySave(FmQuestEconomy.SaveSlot, FmQuestEconomy.GameId,
                FmQuestEconomy.SchemaVersion, FmQuestEconomy.ContentVersion,
                JsonUtility.ToJson(questEconomy.CaptureSave()), out var error);
            if (!saved) questNotice = "싱글 플레이 저장 실패: " + error;
            return saved;
        }

        private bool CommitQuestMutation(FmQuestProfile before)
        {
            if (SaveQuestProfile()) return true;
            questEconomy = FmQuestEconomy.FromSave(before);
            return false;
        }

        private void EnterQuestGame()
        {
            questFreePracticeView = false;
            ShowLobby();
        }

        private void ShowQuestEntry()
        {
            if (questEconomy == null) { ShowQuestLoadError(); return; }
            if (!questEconomy.HasStarter) { ShowQuestStarters(); return; }
            ShowQuestLobby();
        }

        private void ShowFreePracticeLobby()
        {
            questFreePracticeView = true;
            ShowLobby();
        }

        private void LoadQuestDeck()
        {
            questDeck.Clear();
            if (questEconomy == null || !questEconomy.HasStarter) return;
            questDeck.AddRange(DemoCatalog.GetDeck(questEconomy.StarterDeckId).CopyCards());
            if (!PersistentSaves.Exists(QuestDeckSlot)) return;
            try
            {
                if (!PersistentSaves.TryLoad(QuestDeckSlot, FmQuestEconomy.GameId, FmQuestEconomy.SchemaVersion,
                    FmQuestEconomy.ContentVersion, out var payload, out var error)) throw new ArgumentException(error);
                var saved = JsonUtility.FromJson<DeckSave>(payload);
                if (saved == null || !questEconomy.ValidateOwnedDeck(saved.cards, out error)) throw new ArgumentException(error);
                questDeck.Clear(); questDeck.AddRange(saved.cards);
            }
            catch (Exception exception)
            { questNotice = "기존 싱글 덱 저장은 보존했습니다. 받은 기본 덱으로 시작합니다. " + exception.Message; }
        }

        private bool SaveQuestDeck()
        {
            if (questEconomy == null) { questNotice = "먼저 기본 덱을 받으세요."; return false; }
            if (!questEconomy.ValidateOwnedDeck(questDeck, out var reason))
            { questNotice = reason; return false; }
            bool saved = PersistentSaves.TrySave(QuestDeckSlot, FmQuestEconomy.GameId, FmQuestEconomy.SchemaVersion,
                FmQuestEconomy.ContentVersion, JsonUtility.ToJson(new DeckSave { cards = questDeck.ToList() }), out var error);
            questNotice = saved ? "보유 카드로 구성한 50장 덱을 저장했습니다." : "덱 저장 실패: " + error;
            return saved;
        }

        private void SelectQuestStarter(string id)
        {
            if (questEconomy == null) return;
            var before = questEconomy.CaptureSave();
            if (!questEconomy.TrySelectStarter(id, out var reason)) { questNotice = reason; ShowQuestStarters(); return; }
            if (!CommitQuestMutation(before)) { ShowQuestStarters(); return; }
            LoadQuestDeck();
            questNotice = "기본 덱 50장을 받았습니다. 싱글 플레이로 SP를 모아 카드 상점에서 덱을 넓혀 보세요.";
            FmBattleAudio.Play("card-draw"); ShowQuestLobby();
        }

        private void BeginQuestBattle(int encounter, string scenarioId)
        {
            if (questEconomy == null) return;
            if (!SaveQuestDeck()) { ShowQuestOwnedCards(true); return; }
            var before = questEconomy.CaptureSave();
            if (!questEconomy.TryBeginBattle(questEconomy.StarterDeckId, encounter, scenarioId, questDeck,
                out var created, out var ticket, out var reason))
            { questNotice = reason; ShowQuestLobby(); return; }
            if (!CommitQuestMutation(before)) { ShowQuestLobby(); return; }
            engine = created; questBattleMode = true; questFreePracticeView = false;
            questBattleTicket = ticket; questBattleReward = null;
            selectedCard = selectedAttacker = -1; openingReplacements.Clear(); ResetPolishSession();
            PlayerPrefs.SetInt(QuestModeKey, 1); PlayerPrefs.SetString(QuestTicketKey, ticket); PlayerPrefs.Save();
            if (!Save()) { questNotice = saveError; ShowQuestLobby(); return; }
            locked = true; StartCoroutine(ShuffleAndDeal());
        }

        private void AbandonQuestBattle()
        {
            if (questEconomy == null || string.IsNullOrEmpty(questEconomy.ActiveBattleTicketId)) return;
            var before = questEconomy.CaptureSave();
            if (!questEconomy.TryAbandonBattle(questEconomy.ActiveBattleTicketId, out var reason))
            { questNotice = reason; ShowQuestLobby(); return; }
            if (!CommitQuestMutation(before)) { ShowQuestLobby(); return; }
            questNotice = "진행 중인 싱글 대전을 포기했습니다. 보상 없이 새 대전을 시작할 수 있습니다.";
            ShowQuestLobby();
        }

        private void ActivatePracticeBattle()
        {
            questBattleMode = false; questBattleReward = null;
            PlayerPrefs.SetInt(QuestModeKey, 0); PlayerPrefs.Save();
        }

        private void RestoreQuestBattleMode()
        {
            questBattleMode = HasQuestBattleSave
                && (PlayerPrefs.GetInt(QuestModeKey, 1) == 1 || !PersistentSaves.Exists(Slot));
            questBattleTicket = questBattleMode ? PlayerPrefs.GetString(QuestTicketKey, "") : "";
            questBattleReward = null;
            questFreePracticeView = !questBattleMode;
        }

        private void ContinueQuestBattle()
        {
            PlayerPrefs.SetInt(QuestModeKey, 1); PlayerPrefs.Save(); Continue();
        }

        private void ContinuePracticeBattle()
        {
            PlayerPrefs.SetInt(QuestModeKey, 0); PlayerPrefs.Save(); Continue();
        }

        private void ValidateQuestContinuedBattle(BattleState restored)
        {
            if (!questBattleMode) return;
            if (questEconomy == null) throw new ArgumentException(questLoadError);
            if (!questEconomy.ValidateBattleBinding(questBattleTicket, restored, out var error))
                throw new ArgumentException(error);
        }

        private void ResolveQuestResult()
        {
            if (!questBattleMode || questEconomy == null || engine == null || engine.State.phase != BattlePhase.Complete) return;
            var before = questEconomy.CaptureSave();
            if (!questEconomy.TryResolveBattle(questBattleTicket, engine.State, QuestCalendarDay, out var reward, out var reason))
            { questNotice = reason; return; }
            if (reward.alreadyClaimed)
            {
                if (questBattleReward == null) questBattleReward = reward;
                return;
            }
            if (CommitQuestMutation(before)) questBattleReward = reward;
        }

        private string QuestResultText()
        {
            if (!questBattleMode) return "자유 연습전 · SP 보상은 싱글 플레이에서 받을 수 있습니다.";
            if (questBattleReward == null) return string.IsNullOrEmpty(questNotice) ? "싱글 플레이 보상을 확인하고 있습니다." : questNotice;
            if (questBattleReward.alreadyClaimed) return "이미 받은 대전 보상입니다. 현재 SP " + questEconomy.Sp.ToString("N0");
            return questBattleReward.label + "  +" + questBattleReward.spAwarded.ToString("N0") + " SP"
                + (questBattleReward.firstClearSp > 0 ? " · 첫 승리 보너스 포함" : "")
                + "\n현재 SP " + questEconomy.Sp.ToString("N0") + " · 상점에서 카드팩을 구입하세요.";
        }
    }
}
