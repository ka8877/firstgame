using System;
using System.Text;
using PathOfDawn.Core;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    public sealed partial class GospelLuminousApp
    {
        public GospelChronicle Chronicle => Journey?.Chronicle;
        public string RenderedChronicleChapter => renderedChronicleChapter ?? "";
        public string ChronicleNpcId => renderedChronicleNpcId ?? "";
        string renderedChronicleChapter, renderedChronicleQuest, renderedChronicleNpcId;
        Transform chronicleNpcRoot;
        Sprite johnSprite;
        int FoundReadingCount => Journey?.CodexCount ?? 0;
        int AvailableReadingCount => Journey?.CodexTotal ?? 19;
        string ChronicleObjective => Chronicle == null ? "" : Chronicle.IsComplete
            ? "요단강의 기록까지 여정을 마쳤습니다." : Chronicle.CurrentQuest.Objective;
        string HudReference => Chronicle != null ? Chronicle.IsComplete ? "요단강의 기록" : Chronicle.AgeLabel : VisualReading.Reference;
        string HudVerse => Chronicle != null ? Chronicle.IsComplete
            ? "발견한 말씀은 성경을 펼쳐 다시 읽을 수 있습니다." : ChronicleObjective : FirstLine(VisualReading.Text);

        void ResumeChronicle(bool initial = false)
        {
            if (Chronicle == null) return;
            Player.Enabled = false;
            bool changedChapter = renderedChronicleChapter != Chronicle.ChapterId;
            renderedChronicleChapter = Chronicle.ChapterId;
            World.SetChronicleChapter(Chronicle.ChapterId);
            Player.Actor.SetAdult(Chronicle.IsAdult);
            Player.Actor.SetLamp(Journey.LampEquipped);
            foreach (var person in meetingRoots) person.gameObject.SetActive(false);
            RefreshChronicleNpc();
            if ((initial || changedChapter) && Chronicle.ChapterId != "birth")
            {
                Player.SetPose(new Vector2(-31f, World.HeightAt(-31f) + .06f));
                Save(false);
                Ui.ShowChronicleChapter(Chronicle.IsAdult ? "요단강 · 공생애의 문턱" : "성전의 소년 · 유월절의 길",
                    Chronicle.AgeLabel,
                    Chronicle.IsAdult ? "나사렛에서 성장하신 뒤, 이야기는 요단강으로 이어집니다.\n성인이 된 예수님으로 세례 요한을 만나고 복음서의 기록을 따라가세요."
                        : "탄생의 서막을 지나 이제 열두 살 예수님으로 걷습니다.\n가족과 함께 길을 떠나고, 성전에서 듣고 묻던 만남을 따라가세요.",
                    () => ResumeChronicle());
                return;
            }
            if (Chronicle.Stage == GospelQuestStage.Reading)
            { ShowChronicleReading(); return; }
            Ui.ShowHud(ObjectiveName, HudReference, HudVerse, FoundReadingCount, AvailableReadingCount, DawnPlatformInput.UseTouchControls);
            Player.Enabled = true;
            World.SetObjective(ObjectiveX, !Chronicle.IsComplete);
        }

        void InteractChronicle()
        {
            if (Chronicle.IsComplete) { ShowBible(); return; }
            if (!CanInteract) { Ui.Toast(Chronicle.CurrentQuest.Objective); return; }
            var quest = Chronicle.CurrentQuest;
            if (Chronicle.Stage == GospelQuestStage.Travel && !Chronicle.TryArrive(quest.LandmarkId, Player.Position.x, out var arrival))
            { Ui.Toast(arrival); return; }
            if (Chronicle.Stage == GospelQuestStage.Talk && !Chronicle.TryTalk(quest.NpcId, out var response))
            { Ui.Toast(response); return; }
            Save(false);
            if (Chronicle.Stage == GospelQuestStage.Reading) ShowChronicleReading();
        }

        void ShowChronicleReading()
        {
            if (Chronicle == null || Chronicle.Stage != GospelQuestStage.Reading) return;
            Player.Enabled = false;
            Ui.ShowChronicleDialogue(Chronicle, () =>
            {
                int before = Chronicle.DiscoveredCount;
                var found = Chronicle.CurrentReading;
                Chronicle.AdvanceDialogue();
                Save(false);
                if (Chronicle.DiscoveredCount > before)
                {
                    var pages = Chronicle.BookPages;
                    for (int i = 0; i < pages.Count; i++) if (pages[i].ReadingId == found.Id) { Ui.FocusBiblePage(i); break; }
                    Ui.Toast("성경에 새 말씀이 기록되었습니다 · " + found.Title);
                }
                ResumeChronicle();
            });
        }

        void ShowBible()
        {
            if (Journey == null) return;
            Player.Enabled = false;
            if (Chronicle != null) Ui.ShowBible(Chronicle.BookPages, Chronicle.DiscoveredCount);
            else ShowJournal();
        }

        void ShowQuestLog()
        {
            if (Journey == null) return;
            Player.Enabled = false;
            if (Chronicle == null)
            {
                Ui.ShowChronicleQuestLog("이전 여정의 기록", "기존에 발견한 말씀과 여정은 그대로 보존됩니다.\n\n"
                    + Journey.ProgressText + "\n발견한 말씀 " + Journey.CodexCount + " / " + Journey.CodexTotal,
                    Journey.IsComplete ? (Action)ContinueChronicleFromLegacy : null);
                return;
            }
            var text = new StringBuilder();
            text.AppendLine(Chronicle.AgeLabel).AppendLine();
            if (Chronicle.IsComplete)
                text.AppendLine("요단강의 세례와 공생애의 시작까지 이번 이야기를 마쳤습니다.")
                    .AppendLine("발견한 말씀은 성경 책에 남습니다. 이후의 사역·십자가·부활은 다음 이야기입니다.");
            else text.AppendLine("현재 목표").AppendLine(Chronicle.CurrentQuest.Objective);
            text.AppendLine().AppendLine("지나온 만남");
            foreach (var entry in Chronicle.Journal)
                text.Append(entry.IsComplete ? "✓ " : "→ ").AppendLine(entry.Title);
            Ui.ShowChronicleQuestLog("예수님의 생애 · 여정 기록", text.ToString());
        }

        void ContinueChronicleFromLegacy()
        {
            if (Journey == null || Chronicle != null || !Journey.IsComplete) return;
            // Fork the slot: the completed seven/fifteen-reading save stays intact.
            var next = GospelJourney.FromSave(Journey.CaptureSave());
            next.EnableChronicle();
            Journey = next;
            activeSlot = GospelJourney.SaveSlot + "-chronicle-" + DateTime.UtcNow.Ticks;
            PlayerPrefs.SetString(ActiveSlotKey, activeSlot);
            renderedChronicleChapter = null;
            ResumeChronicle(true);
        }

        void RefreshChronicleNpc()
        {
            if (Chronicle == null || Chronicle.IsComplete || Chronicle.CurrentQuest.IsPrologue)
            { if (chronicleNpcRoot != null) chronicleNpcRoot.gameObject.SetActive(false); return; }
            var quest = Chronicle.CurrentQuest;
            if (renderedChronicleQuest == quest.Id && chronicleNpcRoot != null)
            { chronicleNpcRoot.gameObject.SetActive(true); return; }
            if (chronicleNpcRoot != null) { chronicleNpcRoot.gameObject.SetActive(false); Destroy(chronicleNpcRoot.gameObject); }
            chronicleNpcRoot = new GameObject("Chronicle encounter · " + quest.NpcName).transform;
            chronicleNpcRoot.SetParent(transform, false);
            chronicleNpcRoot.position = new Vector3(quest.X, World.HeightAt(quest.X) + .01f, 0);
            int variant = quest.Portrait == "mary" ? 1 : quest.Portrait == "joseph" ? 2 : 0;
            var npc = GospelLuminousActor.BuildNpc(chronicleNpcRoot, variant);
            if (npc != null)
            {
                npc.sortingOrder = 10;
                if (quest.NpcId == "john-baptist")
                {
                    var texture = Resources.Load<Texture2D>("Art/GospelChronicle/Actors/john-baptist");
                    if (texture != null)
                    {
                        if (johnSprite == null) johnSprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height),
                            new Vector2(.5f, 0), 100, 0, SpriteMeshType.FullRect);
                        npc.sprite = johnSprite;
                        npc.sharedMaterial.SetTexture("_MainTex", texture);
                        var bindings = new MaterialPropertyBlock();
                        npc.GetPropertyBlock(bindings); bindings.SetTexture("_MainTex", texture);
                        npc.SetPropertyBlock(bindings);
                        float scale = 2.1f / johnSprite.bounds.size.y;
                        npc.transform.localPosition = Vector3.zero;
                        npc.transform.localScale = Vector3.one * scale;
                        // The source is already facing left toward an approaching player.
                        npc.flipX = false;
                    }
                }
            }
            renderedChronicleQuest = quest.Id;
            renderedChronicleNpcId = quest.NpcId;
        }
    }
}
