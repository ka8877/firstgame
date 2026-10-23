using System;
using System.Collections.Generic;
using Ink.Runtime;

namespace PathOfDawn.Core
{
    /// <summary>Ink owns trust/choice/route. This model owns scene objects, inventory and RNG.
    /// MetaProgress is never rolled back. Checkpoints are taken before Ink generates choices.</summary>
    public sealed class NarrativeSession
    {
        public const int SchemaVersion = 2;
        public const string ContentVersion = "galilee-0.1.0";
        readonly string compiledInk;
        Story story;
        readonly List<Checkpoint> checkpoints = new List<Checkpoint>();
        public WorldState World { get; private set; }
        public MetaProgress Meta { get; private set; }
        public uint RandomState { get; private set; }
        public string ActiveCheckpoint { get; private set; }
        public List<string> Lines { get; private set; } = new List<string>();
        public IReadOnlyList<Checkpoint> Checkpoints => checkpoints.AsReadOnly();
        public IReadOnlyList<Choice> Choices => story.currentChoices.AsReadOnly();
        public int Trust => Convert.ToInt32(story.variablesState["trust"]);
        public string Route => Convert.ToString(story.variablesState["last_route"]);
        public string FirstChoice => Convert.ToString(story.variablesState["first_choice"]);
        public NarrativeSession(string compiledInk)
        {
            this.compiledInk = compiledInk ?? throw new ArgumentNullException(nameof(compiledInk));
            story = NewStory(); World = new WorldState(); Meta = new MetaProgress(); RandomState = 0x6D2B79F5u;
            Continue();
        }
        Story NewStory() => new Story(compiledInk);
        public void BeginShoreChoice()
        {
            if (World.pointId != "shore" || FirstChoice.Length != 0) throw new InvalidOperationException("첫 선택은 호숫가에서 시작합니다.");
            EnterCheckpoint("shore_before_choice", "shore_choice");
        }
        public void BeginRoomChoice()
        {
            if (World.pointId != "room" || !string.IsNullOrEmpty(Route)) throw new InvalidOperationException("두 번째 선택을 시작할 수 없습니다.");
            EnterCheckpoint("room_before_choice", "room_choice");
        }
        void EnterCheckpoint(string id, string knot)
        {
            if (story.currentChoices.Count > 0) return;
            ApplyMemories(story);
            story.ChoosePathString(knot);
            var checkpoint = new Checkpoint { id = id, choiceKnot = knot, inkState = story.state.ToJson(),
                world = World.Copy(), randomState = RandomState };
            checkpoints.RemoveAll(c => c.id == id); checkpoints.Add(checkpoint); ActiveCheckpoint = id;
            Continue();
        }
        public void Choose(int index)
        {
            if (index < 0 || index >= story.currentChoices.Count) throw new ArgumentOutOfRangeException(nameof(index));
            var choiceText = story.currentChoices[index].text;
            Meta.choiceHistory.Add(ActiveCheckpoint + " · " + choiceText);
            story.ChooseChoiceIndex(index); Continue();
            if (!string.IsNullOrEmpty(Route)) Meta.Route(Route);
        }
        void Continue()
        {
            Lines = new List<string>();
            int safety = 0;
            while (story.canContinue)
            {
                if (++safety > 500) throw new InvalidOperationException("Ink continued beyond safe slice boundary.");
                var line = story.Continue().Trim();
                if (line.Length > 0) Lines.Add(line);
                foreach (var tag in story.currentTags) ApplyTag(tag.Trim());
            }
        }
        void ApplyTag(string tag)
        {
            switch (tag)
            {
                case "world:help": World.waterCarried = true; World.Give("bread_miriam"); break;
                case "world:listen": World.worriesHeard = true; break;
                case "stage:room": World.pointId = "room";
                    var miriam = World.actors.Find(p => p.id == "miriam"); miriam.x = -4; miriam.z = -3; break;
                case "outcome:witness": World.basketShared = true; World.inventory.Remove("bread_miriam"); World.pointId = "ending"; break;
                case "outcome:shelter": World.shelterPrepared = true; World.pointId = "ending"; break;
                case "outcome:reconcile": World.peopleConnected = true; World.basketShared = true;
                    World.inventory.Remove("bread_miriam"); World.pointId = "ending"; break;
            }
        }
        public string Examine(string id)
        {
            string result;
            switch (id)
            {
                case "stopped_watch": result = "멈춘 시계. 선택 직전의 시간을 다시 찾아갈 수 있을 것 같다. [기억] 화면에서 회귀할 수 있다."; break;
                case "shore_basket": result = "바구니는 작지만 몇 사람의 손을 거쳐 이곳에 왔다. 빵을 나르는 일도 누군가를 알아 가는 일이다."; break;
                case "room_lamp": result = "돌벽에 새긴 작은 등불 자리. 밖으로 나아가는 길과 남아서 기다리는 길 모두 누군가를 향하고 있다."; break;
                default: throw new ArgumentException("알 수 없는 조사 대상입니다.", nameof(id));
            }
            if (!World.examined.Contains(id)) World.examined.Add(id);
            Meta.Discover(id); return result;
        }
        public uint NextRandom()
        {
            uint x = RandomState; x ^= x << 13; x ^= x >> 17; x ^= x << 5; return RandomState = x;
        }
        public void Rewind(string checkpointId)
        {
            var checkpoint = checkpoints.Find(c => c.id == checkpointId);
            if (checkpoint == null) throw new ArgumentException("체크포인트가 없습니다.", nameof(checkpointId));
            ValidateCheckpoint(checkpoint);
            var replacement = NewStory(); replacement.state.LoadJson(checkpoint.inkState);
            ApplyMemories(replacement); // MUST precede choice generation, never reuse old currentChoices.
            replacement.ChoosePathString(checkpoint.choiceKnot);
            World = checkpoint.world.Copy(); RandomState = checkpoint.randomState; story = replacement;
            ActiveCheckpoint = checkpoint.id;
            // Future checkpoints belong to the abandoned branch; never offer stale-world restores.
            int restoreIndex = checkpoints.IndexOf(checkpoint);
            if (restoreIndex + 1 < checkpoints.Count) checkpoints.RemoveRange(restoreIndex + 1, checkpoints.Count - restoreIndex - 1);
            Continue();
        }
        void ApplyMemories(Story target)
        {
            target.variablesState["memory_witness"] = Meta.routes.Contains("witness");
            target.variablesState["memory_shelter"] = Meta.routes.Contains("shelter");
            target.variablesState["memory_reconcile"] = Meta.routes.Contains("reconcile");
        }
        public NarrativeSave CaptureSave()
        {
            return new NarrativeSave { inkState = story.state.ToJson(), world = World.Copy(), meta = new MetaProgress {
                    discoveries = new List<string>(Meta.discoveries), routes = new List<string>(Meta.routes),
                    choiceHistory = new List<string>(Meta.choiceHistory) }, randomState = RandomState,
                activeCheckpoint = ActiveCheckpoint, checkpoints = checkpoints.ConvertAll(c => c.Copy()), displayedLines = new List<string>(Lines) };
        }
        public void RestoreSave(NarrativeSave save)
        {
            ValidateSave(save);
            var replacement = NewStory(); replacement.state.LoadJson(save.inkState);
            World = save.world.Copy(); Meta = new MetaProgress { discoveries = new List<string>(save.meta.discoveries),
                routes = new List<string>(save.meta.routes), choiceHistory = new List<string>(save.meta.choiceHistory) };
            RandomState = save.randomState; story = replacement; ActiveCheckpoint = save.activeCheckpoint;
            checkpoints.Clear(); checkpoints.AddRange(save.checkpoints.ConvertAll(c => c.Copy()));
            Lines = new List<string>(save.displayedLines ?? new List<string>());
            ApplyMemories(story);
        }
        public void ValidateSave(NarrativeSave save)
        {
            if (save == null) throw new FormatException("저장이 비어 있습니다.");
            if (save.schemaVersion == 1)
            {   // Version 1 had no explicit RNG. Migration is in memory and never writes the original.
                save.schemaVersion = 2; if (save.randomState == 0) save.randomState = 0x6D2B79F5u;
                foreach (var c in save.checkpoints ?? new List<Checkpoint>()) { c.schemaVersion = 2; if (c.randomState == 0) c.randomState = save.randomState; }
            }
            if (save.schemaVersion != SchemaVersion || save.contentVersion != ContentVersion) throw new FormatException("저장 스키마 또는 이야기 버전이 다릅니다. 원본을 보존했습니다.");
            ValidateWorld(save.world);
            if (save.randomState == 0 || save.meta == null || save.meta.routes == null || save.meta.discoveries == null || save.meta.choiceHistory == null || save.checkpoints == null)
                throw new FormatException("저장 필수 항목이 없습니다.");
            foreach (var route in save.meta.routes) if (route != "witness" && route != "shelter" && route != "reconcile") throw new FormatException("알 수 없는 발견 경로입니다.");
            if (save.checkpoints.Count > 2) throw new FormatException("체크포인트 수가 잘못되었습니다.");
            var ids = new HashSet<string>(); foreach (var c in save.checkpoints) { ValidateCheckpoint(c); if (!ids.Add(c.id)) throw new FormatException("중복 체크포인트입니다."); }
            if ((!string.IsNullOrEmpty(save.activeCheckpoint) && !ids.Contains(save.activeCheckpoint)) ||
                (save.checkpoints.Count > 0 && string.IsNullOrEmpty(save.activeCheckpoint))) throw new FormatException("현재 체크포인트가 유효하지 않습니다.");
            var loaded = ValidateInk(save.inkState); ValidateConsistency(loaded, save.world);
            string routeId = Convert.ToString(loaded.variablesState["last_route"]);
            if (routeId.Length > 0 && !save.meta.routes.Contains(routeId)) throw new FormatException("현재 결과의 기억이 없습니다.");
            if (new HashSet<string>(save.meta.routes).Count != save.meta.routes.Count || new HashSet<string>(save.meta.discoveries).Count != save.meta.discoveries.Count)
                throw new FormatException("기억 항목이 중복되었습니다.");
            foreach (var discovery in save.meta.discoveries) ValidateDiscovery(discovery);
        }
        void ValidateCheckpoint(Checkpoint c)
        {
            if (c == null || c.schemaVersion != SchemaVersion || c.contentVersion != ContentVersion || c.randomState == 0 ||
                !((c.id == "shore_before_choice" && c.choiceKnot == "shore_choice") || (c.id == "room_before_choice" && c.choiceKnot == "room_choice")))
                throw new FormatException("체크포인트가 호환되지 않습니다.");
            ValidateWorld(c.world); var loaded = ValidateInk(c.inkState); ValidateConsistency(loaded, c.world);
            if ((c.id == "shore_before_choice" && c.world.pointId != "shore") || (c.id == "room_before_choice" && c.world.pointId != "room"))
                throw new FormatException("체크포인트의 장소와 이야기 시점이 다릅니다.");
        }
        Story ValidateInk(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) throw new FormatException("Ink 상태가 없습니다.");
            var candidate = NewStory(); candidate.state.LoadJson(json);
            int trust = Convert.ToInt32(candidate.variablesState["trust"]);
            if (trust < 0 || trust > 10) throw new FormatException("관계 값이 유효하지 않습니다.");
            return candidate;
        }
        static void ValidateConsistency(Story candidate, WorldState world)
        {
            string first = Convert.ToString(candidate.variablesState["first_choice"]), route = Convert.ToString(candidate.variablesState["last_route"]);
            if ((first != "" && first != "help" && first != "listen") || (route != "" && route != "witness" && route != "shelter" && route != "reconcile") ||
                (world.pointId == "shore" && (first != "" || route != "")) || (world.pointId == "room" && (first == "" || route != "")) ||
                (world.pointId == "ending" && (first == "" || route == "")) || (route == "witness" && first != "help"))
                throw new FormatException("이야기와 월드 진행이 일치하지 않습니다.");
            int expectedTrust = first == "help" ? 2 : first == "listen" ? 1 : 0;
            expectedTrust += route == "witness" ? 1 : route == "reconcile" ? 2 : 0;
            if (Convert.ToInt32(candidate.variablesState["trust"]) != expectedTrust || world.waterCarried != (first == "help") ||
                world.worriesHeard != (first == "listen") || world.basketShared != (route == "witness" || route == "reconcile") ||
                world.shelterPrepared != (route == "shelter") || world.peopleConnected != (route == "reconcile") ||
                world.inventory.Contains("bread_miriam") != (first == "help" && !world.basketShared))
                throw new FormatException("이야기와 소지품·관계·오브젝트 상태가 일치하지 않습니다.");
        }
        static void ValidateDiscovery(string id)
        {
            if (id != "stopped_watch" && id != "shore_basket" && id != "room_lamp") throw new FormatException("알 수 없는 조사 항목입니다.");
        }
        static void ValidateWorld(WorldState world)
        {
            if (world == null || world.sceneId != "galilee_dawn" || (world.pointId != "shore" && world.pointId != "room" && world.pointId != "ending") ||
                world.inventory == null || world.examined == null || world.actors == null || world.actors.Count != 3)
                throw new FormatException("월드 상태가 유효하지 않습니다.");
            var inventoryIds = new HashSet<string>();
            foreach (var item in world.inventory) if (item != "bread_miriam" || !inventoryIds.Add(item)) throw new FormatException("소지품 상태가 유효하지 않습니다.");
            foreach (var id in world.examined) ValidateDiscovery(id);
            var actorIds = new HashSet<string>();
            foreach (var p in world.actors)
                if (p == null || (p.id != "traveler" && p.id != "jesus" && p.id != "miriam") || !actorIds.Add(p.id) ||
                    !Finite(p.x) || !Finite(p.y) || !Finite(p.z) || !Finite(p.yaw) || Math.Abs(p.x) > 100 || Math.Abs(p.y) > 100 || Math.Abs(p.z) > 100)
                    throw new FormatException("인물 위치가 유효하지 않습니다.");
        }
        static bool Finite(float v) => !float.IsNaN(v) && !float.IsInfinity(v);
    }
}
