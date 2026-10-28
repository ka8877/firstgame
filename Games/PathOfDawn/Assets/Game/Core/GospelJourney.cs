using System;
using System.Collections.Generic;

namespace PathOfDawn.Core
{
    public enum GospelPhase { Prologue, Exploring, Reading, Completed }

    [Serializable]
    public sealed class GospelSave
    {
        // Missing version fields must remain invalid when JSON is decoded.
        public int schemaVersion;
        public string contentVersion;
        public GospelPhase phase;
        public int prologuePage, objectiveIndex;
        // Optional in schema 1: older saves resume at the first turn of the open reading.
        public int dialoguePage;
        public List<string> collectedReadings = new List<string>();
        public float playerX, playerY, playerZ, playerYaw;
        // Optional schema-1 extension: old saves have no equipped utility.
        public string equippedUtility;
        // Optional schema-1 metadata. Empty identifies the preserved 3D coordinate space.
        public string presentationSpace;

        public GospelSave Copy() => new GospelSave
        {
            schemaVersion = schemaVersion, contentVersion = contentVersion, phase = phase,
            prologuePage = prologuePage, objectiveIndex = objectiveIndex, dialoguePage = dialoguePage,
            collectedReadings = collectedReadings == null ? null : new List<string>(collectedReadings),
            playerX = playerX, playerY = playerY, playerZ = playerZ, playerYaw = playerYaw,
            equippedUtility = equippedUtility, presentationSpace = presentationSpace
        };
    }

    /// <summary>Owns Scripture-study progression independently of rendering, input and storage.</summary>
    public sealed class GospelJourney
    {
        public const int SchemaVersion = 1;
        public const string ContentVersion = "gospel-0.1.0";
        public const string GameId = "gospel-journey";
        public const string SaveSlot = "gospel-journey-v1";
        public const string LuminousSpace = "luminous-side-v1";
        GospelSave state;

        GospelJourney() { state = new GospelSave { schemaVersion = SchemaVersion,
            contentVersion = ContentVersion, phase = GospelPhase.Prologue, playerZ = -9f }; }
        public static GospelJourney New() => new GospelJourney();
        public GospelPhase Phase => state.phase;
        public int ProloguePage => state.prologuePage;
        public int ObjectiveIndex => state.objectiveIndex;
        public int DialogueIndex => state.dialoguePage;
        public int DialogueCount => GospelConversations.ForReading(CurrentReading.Id).Count;
        public GospelDialogueTurn CurrentDialogue => GospelConversations.ForReading(CurrentReading.Id)[state.dialoguePage];
        public bool IsComplete => Phase == GospelPhase.Completed;
        public float PlayerX => state.playerX;
        public float PlayerY => state.playerY;
        public float PlayerZ => state.playerZ;
        public float PlayerYaw => state.playerYaw;
        public string EquippedUtility => state.equippedUtility ?? "";
        public string PresentationSpace => state.presentationSpace ?? "";
        public void SetPresentationSpace(string space)
        {
            if (!ValidPresentationSpace(space)) throw new ArgumentException("저장된 화면 좌표 공간을 확인할 수 없습니다.", nameof(space));
            state.presentationSpace = space ?? "";
        }
        public bool LampEquipped => EquippedUtility == GospelBelongings.LampId;
        public void SetUtility(string id)
        {
            if(!GospelBelongings.IsValidUtility(id))throw new ArgumentException("장착할 수 없는 도구입니다.",nameof(id));
            state.equippedUtility=id??"";
        }
        public IReadOnlyList<string> CollectedReadings => state.collectedReadings.AsReadOnly();
        public GospelObjective CurrentObjective => state.objectiveIndex < GospelCatalog.Objectives.Count
            ? GospelCatalog.Objectives[state.objectiveIndex] : null;
        public GospelEpisode CurrentEpisode => GospelCatalog.Episodes[Phase == GospelPhase.Prologue ? 0 : 1];
        public GospelReading CurrentReading => Phase == GospelPhase.Prologue
            ? GospelCatalog.PrologueReadings[state.prologuePage]
            : GospelCatalog.GetReading(GospelCatalog.Objectives[Math.Min(state.objectiveIndex,
                GospelCatalog.Objectives.Count - 1)].ReadingId);
        public string ProgressText => Phase == GospelPhase.Prologue
            ? "탄생 · " + (state.prologuePage + 1) + "/" + GospelCatalog.PrologueReadings.Count
            : IsComplete ? "첫 여정 완료 · 나사렛으로"
            : "성전의 소년 · " + (state.objectiveIndex + 1) + "/" + GospelCatalog.Objectives.Count;

        public bool IsEpisodeUnlocked(string id) => id == "birth" || (id == "temple" && Phase != GospelPhase.Prologue);

        public bool AdvanceDialogue()
        {
            if (Phase != GospelPhase.Prologue && Phase != GospelPhase.Reading) return false;
            if (state.dialoguePage + 1 < DialogueCount) { state.dialoguePage++; return true; }
            return AdvanceReading();
        }

        /// <summary>Acknowledges the displayed Scripture reading; never invents a narrative branch.</summary>
        public bool AdvanceReading()
        {
            if (Phase == GospelPhase.Prologue)
            {
                state.dialoguePage = 0;
                state.collectedReadings.Add(CurrentReading.Id);
                state.prologuePage++;
                if (state.prologuePage == GospelCatalog.PrologueReadings.Count) state.phase = GospelPhase.Exploring;
                return true;
            }
            if (Phase != GospelPhase.Reading) return false;
            state.dialoguePage = 0;
            state.collectedReadings.Add(CurrentReading.Id);
            state.objectiveIndex++;
            state.phase = state.objectiveIndex == GospelCatalog.Objectives.Count
                ? GospelPhase.Completed : GospelPhase.Exploring;
            return true;
        }

        public bool TryInteract(string landmarkId, out string message)
        {
            if (Phase != GospelPhase.Exploring)
            {
                message = IsComplete ? "첫 여정을 마쳤습니다. 말씀 기록에서 읽은 구절을 다시 볼 수 있습니다."
                    : "현재 표시된 말씀을 먼저 읽어 주세요.";
                return false;
            }
            if (!string.Equals(CurrentObjective.Id, landmarkId, StringComparison.Ordinal))
            {
                message = CurrentObjective.Prompt;
                return false;
            }
            state.phase = GospelPhase.Reading;
            state.dialoguePage = 0;
            message = CurrentReading.Reference;
            return true;
        }

        public void SetPlayerPose(float x, float y, float z, float yaw)
        {
            if (!ValidPosition(x, y, z) || !Finite(yaw)) throw new ArgumentOutOfRangeException(nameof(x), "저장할 인물 위치가 유효하지 않습니다.");
            state.playerX = x; state.playerY = y; state.playerZ = z;
            state.playerYaw = ((yaw % 360f) + 360f) % 360f;
        }

        public GospelSave CaptureSave() => state.Copy();
        public static GospelJourney FromSave(GospelSave save)
        { var result = New(); result.RestoreSave(save); return result; }
        public void RestoreSave(GospelSave save)
        { ValidateSave(save); state = save.Copy(); }

        public void ValidateSave(GospelSave save)
        {
            if (save == null) throw new ArgumentException("저장 내용이 없습니다.");
            if (save.schemaVersion != SchemaVersion || save.contentVersion != ContentVersion)
                throw new ArgumentException("호환되지 않는 복음서 여정 저장입니다. 기존 파일을 보존해 주세요.");
            if(!GospelBelongings.IsValidUtility(save.equippedUtility))
                throw new ArgumentException("저장된 장착 도구를 확인할 수 없습니다. 기존 파일을 보존해 주세요.");
            if (!ValidPresentationSpace(save.presentationSpace))
                throw new ArgumentException("저장된 화면 좌표 공간을 확인할 수 없습니다. 기존 파일을 보존해 주세요.");
            if (!Enum.IsDefined(typeof(GospelPhase), save.phase) || save.prologuePage < 0
                || save.prologuePage > GospelCatalog.PrologueReadings.Count || save.objectiveIndex < 0
                || save.objectiveIndex > GospelCatalog.Objectives.Count)
                throw new ArgumentException("저장된 장 진행 범위가 잘못되었습니다.");
            bool atPrologue = save.phase == GospelPhase.Prologue;
            bool atEnd = save.phase == GospelPhase.Completed;
            if ((atPrologue && (save.prologuePage >= GospelCatalog.PrologueReadings.Count || save.objectiveIndex != 0))
                || (!atPrologue && save.prologuePage != GospelCatalog.PrologueReadings.Count)
                || (atEnd && save.objectiveIndex != GospelCatalog.Objectives.Count)
                || (!atEnd && !atPrologue && save.objectiveIndex >= GospelCatalog.Objectives.Count))
                throw new ArgumentException("저장된 장면과 목표가 일치하지 않습니다.");
            string openReading = atPrologue ? GospelCatalog.PrologueReadings[save.prologuePage].Id
                : GospelCatalog.Objectives[Math.Min(save.objectiveIndex, GospelCatalog.Objectives.Count - 1)].ReadingId;
            bool readingOpen = atPrologue || save.phase == GospelPhase.Reading;
            if (save.dialoguePage < 0 || (readingOpen && save.dialoguePage >= GospelConversations.ForReading(openReading).Count)
                || (!readingOpen && save.dialoguePage != 0))
                throw new ArgumentException("저장된 대화 위치가 유효하지 않습니다.");
            var expected = new List<string>();
            for (int i = 0; i < save.prologuePage; i++) expected.Add(GospelCatalog.PrologueReadings[i].Id);
            for (int i = 0; i < save.objectiveIndex; i++) expected.Add(GospelCatalog.Objectives[i].ReadingId);
            if (save.collectedReadings == null || save.collectedReadings.Count != expected.Count)
                throw new ArgumentException("읽은 말씀 기록과 진행 상태가 일치하지 않습니다.");
            for (int i = 0; i < expected.Count; i++)
                if (save.collectedReadings[i] != expected[i]) throw new ArgumentException("말씀 기록의 순서 또는 식별자가 잘못되었습니다.");
            if (!ValidPosition(save.playerX, save.playerY, save.playerZ) || !Finite(save.playerYaw)
                || save.playerYaw < 0 || save.playerYaw >= 360)
                throw new ArgumentException("저장된 인물 위치가 유효하지 않습니다.");
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static bool ValidPresentationSpace(string space) => string.IsNullOrEmpty(space) || space == LuminousSpace;
        static bool ValidPosition(float x, float y, float z) => Finite(x) && Finite(y) && Finite(z)
            && Math.Abs(x) <= 100f && Math.Abs(z) <= 100f && y >= -5f && y <= 30f;
    }
}
