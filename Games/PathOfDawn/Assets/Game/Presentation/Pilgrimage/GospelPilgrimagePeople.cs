using System;
using System.Collections.Generic;
using PathOfDawn.Core;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    [Serializable] public sealed class GospelPilgrimagePersonPose
    {
        public string id;
        public float x, y, z, yaw;
        public int routePoint;
    }

    /// <summary>Optional presentation metadata; Scripture and quest state remain in the unchanged Core save.</summary>
    [Serializable] public sealed class GospelPilgrimagePeopleSave
    {
        public int version = 1;
        public string chapter;
        public int questIndex;
        public List<GospelPilgrimagePersonPose> people = new List<GospelPilgrimagePersonPose>();
    }

    public sealed class GospelPersonMarker
    {
        public string Id { get; internal set; }
        public string DisplayName { get; internal set; }
        public Transform RootTransform { get; internal set; }
        public Vector3 Position => RootTransform != null ? RootTransform.position : Vector3.zero;
        public bool IsAmbient { get; internal set; }
        public bool IsCurrentObjective { get; internal set; }
        public bool IsWalking { get; internal set; }
        public float FacingYaw { get; internal set; }
    }

    /// <summary>
    /// A chapter keeps its people, rather than replacing the one visible quest actor.
    /// Completed conversations send the same person along a collision-checked path.
    /// These background routes are scene staging, never additional Biblical events.
    /// </summary>
    public sealed class GospelPilgrimagePeople : MonoBehaviour
    {
        sealed class Person
        {
            public GospelPersonMarker Marker;
            public GospelPilgrimageActor Actor;
            public CharacterController Controller;
            public readonly List<Vector3> Path = new List<Vector3>();
            public int PathPoint, GoalQuest = -1, AmbientPoint;
            public Vector2[] AmbientRoute;
            public float Speed, Pause;
            public Vector3 Goal;
        }

        sealed class Population
        {
            public Transform Root;
            public string Chapter;
            public int QuestIndex = -1;
            public readonly List<Person> People = new List<Person>();
            public readonly List<GospelPersonMarker> Markers = new List<GospelPersonMarker>();
            public GospelPeopleNavigation Navigation;
        }

        readonly Dictionary<string, Population> chapters = new Dictionary<string, Population>();
        static readonly IReadOnlyList<GospelPersonMarker> noMarkers = Array.AsReadOnly(new GospelPersonMarker[0]);
        IReadOnlyList<GospelPersonMarker> markers = noMarkers;
        GospelPilgrimageWorld world;
        GospelPilgrimageMotor player;
        Camera camera;
        Population current;
        GospelChronicle chronicle;
        bool resetLayout = true;

        public IReadOnlyList<GospelPersonMarker> Markers => markers;
        public GospelPersonMarker CurrentPerson { get; private set; }
        public string Chapter => current == null ? "" : current.Chapter;
        public int Count => markers.Count;
        public int AmbientCount { get; private set; }
        public int NamedCount => Count - AmbientCount;
        public double LastNavigationBuildMilliseconds { get; private set; }

        public void Initialize(Transform parent, GospelPilgrimageWorld scene, Camera view, GospelPilgrimageMotor traveler)
        {
            transform.SetParent(parent, false);
            world = scene; camera = view; player = traveler;
        }

        public void ResetJourney() { resetLayout = true; chronicle = null; }

        public void Synchronize(GospelChronicle story)
        {
            chronicle = story;
            string chapter = story == null || !story.IsComplete && story.CurrentQuest.IsPrologue ? "" : story.ChapterId;
            foreach (var pair in chapters) pair.Value.Root.gameObject.SetActive(pair.Key == chapter);
            if (chapter == "")
            {
                current = null; CurrentPerson = null; markers = noMarkers; AmbientCount = 0;
                return;
            }
            if (!chapters.TryGetValue(chapter, out var population))
            {
                population = BuildPopulation(chapter);
                chapters.Add(chapter, population);
            }
            bool changedChapter = current != population;
            current = population; current.Root.gameObject.SetActive(true);
            markers = current.Markers.AsReadOnly();
            AmbientCount = 0; CurrentPerson = null;
            string npc = story.IsComplete ? "" : story.CurrentQuest.NpcId;
            foreach (var person in current.People)
            {
                person.Marker.IsCurrentObjective = !person.Marker.IsAmbient && person.Marker.Id == npc;
                if (person.Marker.IsCurrentObjective) CurrentPerson = person.Marker;
                if (person.Marker.IsAmbient) AmbientCount++;
            }
            if (resetLayout || changedChapter || current.QuestIndex != story.QuestIndex)
                Arrange(story, resetLayout || changedChapter);
            current.QuestIndex = story.QuestIndex;
            resetLayout = false;
        }

        Population BuildPopulation(string chapter)
        {
            var population = new Population { Chapter = chapter };
            population.Root = new GameObject(chapter == "baptism" ? "People · Jordan riverbank" : "People · pilgrimage and temple").transform;
            population.Root.SetParent(transform, false);
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (var quest in GospelChronicleCatalog.Quests)
            {
                if (quest.ChapterId != chapter || !seen.Add(quest.NpcId)) continue;
                var person = CreatePerson(population, quest.NpcId, quest.NpcName, false,
                    quest.Portrait == "mary" ? 1.63f : 1.78f);
                person.Actor = GospelPilgrimageActor.BuildNpc(person.Marker.RootTransform, camera, quest.NpcId,
                    quest.Portrait == "mary" ? 1.63f : 1.78f);
                person.Actor.SetGroundSampler(world.HeightAt);
                person.Speed = 2.05f;
            }
            Vector2[][] routes = chapter == "baptism" ? JordanRoutes() : TempleRoutes();
            for (int i = 0; i < routes.Length; i++)
            {
                bool woman = i % 3 == 1;
                Color tint = i % 3 == 0 ? new Color(.94f, .98f, 1f) : i % 3 == 1
                    ? new Color(1f, .97f, .92f) : new Color(.93f, .96f, .91f);
                var person = CreatePerson(population, "pilgrim-" + i, "순례자", true, 1.65f + i % 3 * .065f);
                person.Actor = GospelPilgrimageActor.BuildAmbient(person.Marker.RootTransform, camera,
                    "Art/GospelRpg/Actors/pilgrims", woman, 1.65f + i % 3 * .065f, tint);
                person.Actor.SetGroundSampler(world.HeightAt);
                person.AmbientRoute = routes[i]; person.Speed = 1.08f + i % 3 * .13f;
                person.Pause = .5f + i * .23f;
            }
            Physics.SyncTransforms();
            var buildClock = System.Diagnostics.Stopwatch.StartNew();
            population.Navigation = new GospelPeopleNavigation(world, transform, player.transform);
            buildClock.Stop(); LastNavigationBuildMilliseconds = buildClock.Elapsed.TotalMilliseconds;
            return population;
        }

        Person CreatePerson(Population population, string id, string name, bool ambient, float height)
        {
            var root = new GameObject(ambient ? "Passer-by · " + id : "Encounter · " + name).transform;
            root.SetParent(population.Root, false);
            var controller = root.gameObject.AddComponent<CharacterController>();
            controller.enabled = false;
            controller.height = height; controller.center = Vector3.up * (height * .5f);
            controller.radius = .21f; controller.skinWidth = .025f;
            controller.minMoveDistance = 0; controller.stepOffset = .26f; controller.slopeLimit = 55;
            var marker = new GospelPersonMarker { Id = id, DisplayName = name, RootTransform = root,
                IsAmbient = ambient, FacingYaw = 180 };
            var person = new Person { Marker = marker, Controller = controller };
            population.People.Add(person); population.Markers.Add(marker);
            return person;
        }

        void Arrange(GospelChronicle story, bool reset)
        {
            foreach (var person in current.People)
            {
                if (person.Marker.IsAmbient)
                {
                    if (!reset) continue;
                    Place(person, Ground(person.AmbientRoute[0])); person.AmbientPoint = 1;
                    SetPath(person, Ground(person.AmbientRoute[1]));
                    continue;
                }
                int next = -1, last = -1;
                for (int i = 0; i < GospelChronicleCatalog.Quests.Count; i++)
                {
                    var quest = GospelChronicleCatalog.Quests[i];
                    if (quest.ChapterId != current.Chapter || quest.NpcId != person.Marker.Id) continue;
                    last = i;
                    if (i >= story.QuestIndex) { next = i; break; }
                }
                int goalIndex = next >= 0 ? next : last;
                Vector3 goal = world.PositionForQuest(GospelChronicleCatalog.Quests[goalIndex]);
                if (next < 0)
                {
                    // Stay nearby after the final meeting instead of vanishing.
                    float side = person.Marker.Id == "mary" || person.Marker.Id == "teacher-listening" ? -1 : 1;
                    goal += new Vector3(side * 1.55f, 0, -1.2f);
                    goal.y = world.HeightAt(goal.x, goal.z);
                }
                if (reset) Place(person, goal);
                if (reset || person.GoalQuest != goalIndex || next < 0 && person.Goal != goal)
                    SetPath(person, goal);
                person.GoalQuest = goalIndex;
            }
        }

        Vector3 Ground(Vector2 point) => new Vector3(point.x, world.HeightAt(point), point.y);

        void Place(Person person, Vector3 point, bool restoreHeight = false)
        {
            if (!restoreHeight)
            {
                string blocker = current.Navigation.ClearanceProblem(point, .15f);
                if (blocker != null)
                    throw new InvalidOperationException("Living-world authored spawn intersects scenery: " + person.Marker.Id + " / " + blocker);
            }
            person.Controller.enabled = false;
            person.Marker.RootTransform.position = new Vector3(point.x,
                restoreHeight ? point.y : world.HeightAt(point.x, point.z) + .035f, point.z);
            person.Controller.enabled = true;
            person.Marker.IsWalking = false;
            person.Actor.SetMotion(Vector3.zero, true, person.Marker.FacingYaw);
        }

        void SetPath(Person person, Vector3 goal)
        {
            person.Goal = goal;
            person.Path.Clear(); person.PathPoint = 0;
            if (Planar(person.Marker.Position, goal) < .14f) return;
            current.Navigation.FindPath(person.Marker.Position, goal, person.Path);
        }

        public void Tick(bool running, float deltaTime)
        {
            if (current == null || chronicle == null) return;
            float dt = Mathf.Clamp(deltaTime, 0, .12f);
            foreach (var person in current.People)
            {
                bool talking = person.Marker.IsCurrentObjective && chronicle.Stage == GospelQuestStage.Reading;
                if (!running || talking || dt <= 0)
                {
                    person.Marker.IsWalking = false;
                    person.Actor.SetMotion(Vector3.zero, true, person.Marker.FacingYaw);
                    if (talking) person.Actor.FaceConversationPartner(player.Position);
                    continue;
                }
                if (person.Pause > 0)
                {
                    person.Pause -= dt; person.Marker.IsWalking = false;
                    person.Actor.SetMotion(Vector3.zero, true, person.Marker.FacingYaw);
                    continue;
                }
                if (person.PathPoint >= person.Path.Count)
                {
                    person.Marker.IsWalking = false; person.Actor.SetMotion(Vector3.zero, true, person.Marker.FacingYaw);
                    if (person.Marker.IsAmbient)
                    {
                        person.AmbientPoint = (person.AmbientPoint + 1) % person.AmbientRoute.Length;
                        person.Pause = 1.4f + person.AmbientPoint * .5f;
                        SetPath(person, Ground(person.AmbientRoute[person.AmbientPoint]));
                    }
                    continue;
                }
                Vector3 before = person.Marker.Position;
                Vector3 delta = person.Path[person.PathPoint] - before; delta.y = 0;
                if (delta.magnitude < .18f) { person.PathPoint++; continue; }
                Vector3 direction = delta.normalized;
                Vector3 avoidance = Avoidance(person, before);
                direction = (direction + avoidance).normalized;
                Vector3 step = direction * Mathf.Min(person.Speed * dt, delta.magnitude);
                person.Controller.Move(step + Vector3.down * (2.5f * dt));
                Vector3 actual = (person.Marker.Position - before) / dt; actual.y = 0;
                person.Marker.IsWalking = actual.sqrMagnitude > .003f;
                if (person.Marker.IsWalking) person.Marker.FacingYaw = Mathf.Atan2(actual.x, actual.z) * Mathf.Rad2Deg;
                person.Actor.SetMotion(actual, true, person.Marker.FacingYaw);
            }
        }

        Vector3 Avoidance(Person person, Vector3 position)
        {
            Vector3 result = Vector3.zero;
            foreach (var other in current.People)
            {
                if (other == person) continue;
                result += Separation(position, other.Marker.Position);
            }
            result += Separation(position, player.Position);
            return Vector3.ClampMagnitude(result, .8f);
        }

        static Vector3 Separation(Vector3 from, Vector3 other)
        {
            Vector3 away = from - other; away.y = 0;
            float distance = away.magnitude;
            if (distance >= .85f) return Vector3.zero;
            if (distance < .015f) return Vector3.right * .4f;
            return away / distance * ((.85f - distance) * 1.5f) + new Vector3(-away.z, 0, away.x).normalized * .11f;
        }

        public GospelPilgrimagePeopleSave Capture()
        {
            // A newly awarded last page may already advance Core to the next
            // chapter while its book reward is still open over the old scenery.
            // That valid boundary save uses the next chapter's default layout.
            if (current == null || chronicle == null || current.Chapter != chronicle.ChapterId) return null;
            var result = new GospelPilgrimagePeopleSave { chapter = current.Chapter, questIndex = chronicle.QuestIndex };
            foreach (var person in current.People)
            {
                Vector3 p = person.Marker.Position;
                result.people.Add(new GospelPilgrimagePersonPose { id = person.Marker.Id, x = p.x, y = p.y,
                    z = p.z, yaw = person.Marker.FacingYaw, routePoint = person.AmbientPoint });
            }
            return result;
        }

        public void Restore(GospelPilgrimagePeopleSave saved)
        {
            if (saved == null) return; // Saves made before the living-world extension keep their story checkpoint layout.
            if (current == null || saved.version != 1 || saved.chapter != current.Chapter ||
                saved.questIndex != chronicle.QuestIndex || saved.people == null || saved.people.Count != current.People.Count)
            {
                Debug.Log("FIRSTGAME_PEOPLE_RESTORE_REJECTED stage=chapter-metadata savedChapter=" + saved.chapter +
                    " activeChapter=" + Chapter + " version=" + saved.version + " quest=" + saved.questIndex +
                    " activeQuest=" + chronicle?.QuestIndex + " people=" + (saved.people == null ? -1 : saved.people.Count) + " activePeople=" + Count);
                throw new ArgumentException("사람들의 위치 기록이 현재 여정과 맞지 않습니다. 기존 파일을 보존했습니다.");
            }
            var rows = new Dictionary<string, GospelPilgrimagePersonPose>(StringComparer.Ordinal);
            foreach (var row in saved.people)
            {
                if (row == null || string.IsNullOrEmpty(row.id) || rows.ContainsKey(row.id) ||
                    !Finite(row.x) || !Finite(row.y) || !Finite(row.z) || !Finite(row.yaw) || Mathf.Abs(row.yaw) > 360 ||
                    !world.Bounds.Contains(new Vector2(row.x, row.z)) ||
                    Mathf.Abs(row.y - world.HeightAt(row.x, row.z)) > .2f)
                {
                    string reason = row == null ? "missing-row" : string.IsNullOrEmpty(row.id) ? "missing-id"
                        : rows.ContainsKey(row.id) ? "duplicate-id" : !Finite(row.x) || !Finite(row.y) || !Finite(row.z) || !Finite(row.yaw)
                        ? "nonfinite" : Mathf.Abs(row.yaw) > 360 ? "yaw-range"
                        : !world.Bounds.Contains(new Vector2(row.x, row.z)) ? "bounds" : "ground-height";
                    Debug.Log("FIRSTGAME_PEOPLE_RESTORE_REJECTED stage=" + reason + " id=" + row?.id + " position=" +
                        (row == null ? "null" : new Vector3(row.x, row.y, row.z).ToString("F4")));
                    throw new ArgumentException("사람들의 위치 기록이 잘못되었습니다. 기존 파일을 보존했습니다.");
                }
                string blocker = current.Navigation.ClearanceProblem(new Vector3(row.x, row.y, row.z), .15f);
                if (blocker != null)
                {
                    Debug.Log("FIRSTGAME_PEOPLE_RESTORE_REJECTED stage=static-clearance id=" + row.id + " position=" +
                        new Vector3(row.x, row.y, row.z).ToString("F4") + " collider=" + blocker);
                    throw new ArgumentException("사람들의 위치 기록이 잘못되었습니다. 기존 파일을 보존했습니다.");
                }
                rows.Add(row.id, row);
            }
            foreach (var person in current.People)
            {
                if (!rows.TryGetValue(person.Marker.Id, out var row) || row.routePoint < 0 ||
                    (person.Marker.IsAmbient ? row.routePoint >= person.AmbientRoute.Length : row.routePoint != 0))
                {
                    Debug.Log("FIRSTGAME_PEOPLE_RESTORE_REJECTED stage=identity-or-route id=" + person.Marker.Id);
                    throw new ArgumentException("사람들의 여정 기록이 잘못되었습니다. 기존 파일을 보존했습니다.");
                }
            }
            if (CurrentPerson != null && chronicle.Stage == GospelQuestStage.Reading)
            {
                var row = rows[CurrentPerson.Id];
                if (Planar(new Vector3(row.x, row.y, row.z), player.Position) > GospelChronicle.InteractionRadius + .35f)
                {
                    Debug.Log("FIRSTGAME_PEOPLE_RESTORE_REJECTED stage=conversation-proximity id=" + CurrentPerson.Id +
                        " position=" + new Vector3(row.x, row.y, row.z).ToString("F4") + " player=" + player.Position.ToString("F4"));
                    throw new ArgumentException("대화 상대의 위치 기록이 맞지 않습니다. 기존 파일을 보존했습니다.");
                }
            }
            // Validate every row before changing any live actor.
            foreach (var person in current.People)
            {
                var row = rows[person.Marker.Id];
                person.Marker.FacingYaw = row.yaw;
                Place(person, new Vector3(row.x, row.y, row.z), true);
                person.AmbientPoint = row.routePoint;
                SetPath(person, person.Marker.IsAmbient ? Ground(person.AmbientRoute[person.AmbientPoint]) : person.Goal);
            }
        }

        static bool Finite(float value) => !float.IsNaN(value) && !float.IsInfinity(value);
        static float Planar(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        static Vector2[][] TempleRoutes() => new[] {
            // The handled vessel at (-32.4,-10.1) has a real box collider.
            // Begin in the open courtyard rather than inside its broad handle.
            new[] { new Vector2(-30.9f, -9.3f), new Vector2(-23.5f, -9.1f) },
            new[] { new Vector2(-30.2f, -4.7f), new Vector2(-24, -4.7f) },
            new[] { new Vector2(-17, -10), new Vector2(-7, -9.5f) },
            new[] { new Vector2(-.3f, 2.2f), new Vector2(6.5f, 2.8f) },
            new[] { new Vector2(12, 6.5f), new Vector2(23, 7.5f) },
            new[] { new Vector2(21, 12), new Vector2(28, 14.5f) }
        };
        static Vector2[][] JordanRoutes() => new[] {
            new[] { new Vector2(-30, -9), new Vector2(-20, -10) },
            new[] { new Vector2(-24, -4), new Vector2(-15, -3) },
            new[] { new Vector2(-15, -.6f), new Vector2(-6, -.6f) },
            new[] { new Vector2(-3, 3.3f), new Vector2(6, 2) },
            new[] { new Vector2(13, -4), new Vector2(22, -3) },
            new[] { new Vector2(26, 3), new Vector2(34, 5) }
        };
    }
}
