using System;
using System.Collections.Generic;

namespace PathOfDawn.Core
{
    [Serializable] public sealed class PositionState
    {
        public string id; public float x, y, z, yaw;
        public PositionState Copy() => (PositionState)MemberwiseClone();
    }
    [Serializable] public sealed class WorldState
    {
        public string sceneId = "galilee_dawn";
        public string pointId = "shore";
        public bool waterCarried, worriesHeard, basketShared, shelterPrepared, peopleConnected;
        public List<string> inventory = new List<string>();
        public List<string> examined = new List<string>();
        public List<PositionState> actors = new List<PositionState>
        {
            new PositionState { id = "traveler", x = 0, y = 0.1f, z = -5 },
            new PositionState { id = "jesus", x = 0, y = 0.1f, z = 0, yaw = 180 },
            new PositionState { id = "miriam", x = -2, y = 0.1f, z = 1, yaw = 145 }
        };
        public WorldState Copy()
        {
            var copy = (WorldState)MemberwiseClone();
            copy.inventory = new List<string>(inventory); copy.examined = new List<string>(examined);
            copy.actors = actors.ConvertAll(p => p.Copy()); return copy;
        }
        public void Give(string id) { if (!inventory.Contains(id)) inventory.Add(id); }
    }
    [Serializable] public sealed class MetaProgress
    {
        public List<string> discoveries = new List<string>();
        public List<string> routes = new List<string>();
        public List<string> choiceHistory = new List<string>();
        public void Discover(string id) { if (!discoveries.Contains(id)) discoveries.Add(id); }
        public void Route(string id) { if (!routes.Contains(id)) routes.Add(id); }
    }
    [Serializable] public sealed class Checkpoint
    {
        public int schemaVersion = NarrativeSession.SchemaVersion;
        public string contentVersion = NarrativeSession.ContentVersion;
        public string id, choiceKnot, inkState;
        public WorldState world;
        public uint randomState;
        public Checkpoint Copy() => new Checkpoint { schemaVersion = schemaVersion, contentVersion = contentVersion,
            id = id, choiceKnot = choiceKnot, inkState = inkState, world = world.Copy(), randomState = randomState };
    }
    [Serializable] public sealed class NarrativeSave
    {
        public int schemaVersion = NarrativeSession.SchemaVersion;
        public string contentVersion = NarrativeSession.ContentVersion;
        public string inkState;
        public WorldState world;
        public MetaProgress meta;
        public uint randomState;
        public string activeCheckpoint;
        public List<Checkpoint> checkpoints = new List<Checkpoint>();
        public List<string> displayedLines = new List<string>();
    }
    public interface ISnapshotCodec
    {
        string Encode(NarrativeSave value);
        NarrativeSave Decode(string json);
    }
    public interface ITextSaveStorage
    {
        bool Exists { get; }
        string Read();
        void Write(string value);
    }
    /// <summary>Never writes during read/import failure. Export remains available for recovery.</summary>
    public sealed class NarrativeSaveSlot
    {
        readonly ITextSaveStorage storage; readonly ISnapshotCodec codec;
        public bool WriteBlocked { get; private set; }
        public bool Exists => storage.Exists;
        public NarrativeSaveSlot(ITextSaveStorage storage, ISnapshotCodec codec) { this.storage = storage; this.codec = codec; }
        public bool LoadInto(NarrativeSession session, out string error)
        {
            try { var save = codec.Decode(storage.Read()); session.RestoreSave(save); WriteBlocked = false; error = null; return true; }
            catch (Exception ex) { WriteBlocked = true; error = ex.Message; return false; }
        }
        public void Save(NarrativeSession session)
        {
            if (WriteBlocked) throw new InvalidOperationException("기존 저장을 읽지 못했습니다. 원본을 내보낸 뒤 새 슬롯으로 시작하세요.");
            storage.Write(codec.Encode(session.CaptureSave()));
        }
        public string ExportRaw() => storage.Read();
        public bool ImportInto(NarrativeSession session, string raw, out string error)
        {
            try { var save = codec.Decode(raw); session.ValidateSave(save); storage.Write(raw); session.RestoreSave(save);
                WriteBlocked = false; error = null; return true; }
            catch (Exception ex) { error = ex.Message; return false; }
        }
    }
}
