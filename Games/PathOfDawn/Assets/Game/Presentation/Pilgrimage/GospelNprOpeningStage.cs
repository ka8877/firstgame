using System;
using System.Collections.Generic;
using PathOfDawn.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// Presentation-only opening sets. The Chronicle owns every word and cursor;
    /// Motor/People keep every physical position. These models are never NPCs.
    /// </summary>
    [DefaultExecutionOrder(110)]
    public sealed class GospelNprOpeningStage : MonoBehaviour
    {
        const string VillageArt = "Art/GalileeAtelier/";
        const string ArtPrefix = "Art/GospelStorybook/";
        readonly Dictionary<string, Transform> sets = new Dictionary<string, Transform>(StringComparer.Ordinal);
        readonly Dictionary<string, GospelNprCharacterView> models = new Dictionary<string, GospelNprCharacterView>(StringComparer.Ordinal);
        readonly List<Material> materials = new List<Material>();
        readonly List<Mesh> meshes = new List<Mesh>();
        Transform content, actors;
        GospelNprEnvironment environment;
        Camera cameraView;
        GospelPilgrimageCamera exploration;
        GameObject travelerVisual;
        Material plaster, stone, timber, linen, blue, earth, brass, masonry, tapestry, vista;
        Vector3 savedCameraPosition;
        Quaternion savedCameraRotation, savedSunRotation;
        float savedFov, savedFar;
        bool savedRigEnabled, savedTravelerActive;
        CameraClearFlags savedClear;
        Color savedBackground, savedAmbient, savedAmbientSky, savedAmbientEquator, savedAmbientGround;
        AmbientMode savedAmbientMode;
        Light observedSun;
        Color savedSunColor;
        float savedSunIntensity;
        GUIStyle errorStyle;

        public bool IsVisible { get; private set; }
        public string CurrentSet { get; private set; } = "";
        public string LoadError { get; private set; } = "";
        public int VisualModelCount => models.Count;

        public void Initialize(Camera view, GospelPilgrimageCamera rig, GameObject traveler)
        {
            cameraView = view != null ? view : throw new ArgumentNullException(nameof(view));
            exploration = rig != null ? rig : throw new ArgumentNullException(nameof(rig));
            travelerVisual = traveler != null ? traveler : throw new ArgumentNullException(nameof(traveler));
        }

        /// <summary>Show only a real prologue. No Core mutation or saved pose write occurs here.</summary>
        public bool Show(GospelChronicle story)
        {
            if (story == null || story.IsComplete || !story.CurrentQuest.IsPrologue)
                throw new InvalidOperationException("A current prologue is required for the NPR opening stage.");
            if (cameraView == null || exploration == null || travelerVisual == null)
                throw new InvalidOperationException("Initialize the NPR opening stage with the actual game camera and visual actor.");
            try
            {
                EnsureContent();
                string key = SetFor(story);
                if (!sets.TryGetValue(key, out var selected))
                {
                    selected = BuildSet(key);
                    sets.Add(key, selected);
                }
                if (!IsVisible)
                {
                    savedCameraPosition = cameraView.transform.position;
                    savedCameraRotation = cameraView.transform.rotation;
                    savedFov = cameraView.fieldOfView; savedFar = cameraView.farClipPlane;
                    savedClear = cameraView.clearFlags; savedBackground = cameraView.backgroundColor;
                    savedAmbient = RenderSettings.ambientLight; savedAmbientMode = RenderSettings.ambientMode;
                    savedAmbientSky = RenderSettings.ambientSkyColor;
                    savedAmbientEquator = RenderSettings.ambientEquatorColor;
                    savedAmbientGround = RenderSettings.ambientGroundColor;
                    observedSun = RenderSettings.sun;
                    if (observedSun != null)
                    {
                        savedSunColor = observedSun.color; savedSunIntensity = observedSun.intensity;
                        savedSunRotation = observedSun.transform.rotation;
                    }
                    savedRigEnabled = exploration.enabled;
                    savedTravelerActive = travelerVisual.activeSelf;
                    IsVisible = true;
                }
                content.gameObject.SetActive(true);
                foreach (var set in sets) set.Value.gameObject.SetActive(set.Key == key);
                exploration.enabled = false;
                travelerVisual.SetActive(false); // Hide only the future-age visual child, never its motor/root.
                CurrentSet = key;
                PlaceModels(key, story);
                SetLight(key, story);
                Frame(key, GospelPilgrimageUi.ModelSpeakerIdentity(story.CurrentStoryBeat));
                LoadError = "";
                return true;
            }
            catch (Exception error)
            {
                LoadError = error.Message;
                Hide();
                Debug.LogError("FIRSTGAME_NPR_OPENING_LOAD_FAILED " + error.Message);
                throw;
            }
        }

        public GospelNprCharacterView ModelFor(string identity)
        {
            if (!IsVisible || string.IsNullOrWhiteSpace(identity)) return null;
            string key = identity.Trim().ToLowerInvariant();
            if (key == "temple-guide" || key == "teacher-listening" || key == "teacher-reading") key = "teacher";
            return models.TryGetValue(key, out var view) && view != null && view.gameObject.activeInHierarchy ? view : null;
        }

        public void Hide()
        {
            foreach (var view in models.Values)
                if (view != null) { view.SetSpeaking(false); view.SetPortraitVisible(false); }
            if (content != null) content.gameObject.SetActive(false);
            if (!IsVisible) return;
            IsVisible = false;
            CurrentSet = "";
            if (travelerVisual != null) travelerVisual.SetActive(savedTravelerActive);
            if (cameraView != null)
            {
                cameraView.fieldOfView = savedFov; cameraView.farClipPlane = savedFar;
                cameraView.clearFlags = savedClear; cameraView.backgroundColor = savedBackground;
                cameraView.transform.SetPositionAndRotation(savedCameraPosition, savedCameraRotation);
            }
            RestoreLight();
            if (exploration != null)
            {
                exploration.enabled = savedRigEnabled;
                if (savedRigEnabled) exploration.Snap();
            }
        }

        void EnsureContent()
        {
            if (content != null) return;
            content = Node("NPR opening sets · visual-only, no gameplay population", transform);
            content.position = new Vector3(300, 0, 300);
            environment = content.gameObject.AddComponent<GospelNprEnvironment>();
            environment.Initialize();
            plaster = Mat("Opening warm lime plaster", new Color(.87f, .82f, .70f));
            stone = Mat("Opening pale limestone", new Color(.77f, .73f, .63f));
            timber = Mat("Opening cedar", new Color(.29f, .20f, .13f));
            linen = Mat("Opening undyed woven linen", new Color(.90f, .87f, .76f));
            blue = Mat("Opening indigo linen", new Color(.20f, .34f, .44f));
            earth = Mat("Opening warm earth", new Color(.62f, .56f, .40f));
            brass = Mat("Opening aged brass", new Color(.58f, .44f, .22f), .28f);
            RetainedSurface(plaster, "Art/Gospel3D/TempleMaterials/limestone-albedo", null, .10f);
            RetainedSurface(timber, "Art/GospelWell/cedar-albedo", null, 0);
            masonry = Mat("Village sandstone walls", new Color(.96f, .91f, .80f));
            RetainedSurface(masonry, VillageArt + "GateMasonry-albedo", VillageArt + "GateMasonry-normal", .36f);
            RetainedSurface(stone, VillageArt + "VillageFlagstone-albedo", VillageArt + "VillageFlagstone-normal", .40f);
            RetainedSurface(earth, VillageArt + "stony_dirt_path-diffuse", VillageArt + "stony_dirt_path-normal", .28f);
            tapestry = Mat("Linen dyed with madder and indigo", Color.white);
            RetainedSurface(tapestry, VillageArt + "Household-weave", null, 0);
            var horizonMaterial = Resources.Load<Material>(VillageArt + "Terraces");
            if (!horizonMaterial) throw new InvalidOperationException("Missing Galilean terrace panorama");
            vista = new Material(horizonMaterial);
            materials.Add(vista);
            DistantValley();
            actors = Node("Opening model-only performers", content);
            AddModel("mary", "Mary"); AddModel("joseph", "Joseph"); AddModel("teacher", "Teacher");
            content.gameObject.SetActive(false);
        }

        void AddModel(string key, string identity)
        {
            var root = Node("Opening visual · " + identity, actors);
            var view = root.gameObject.AddComponent<GospelNprCharacterView>();
            view.LoadModel(identity, GospelNprCharacterView.HeightFor(identity), Color.white);
            view.SetMotion(Vector3.zero, true, 0);
            models.Add(key, view);
        }

        static string SetFor(GospelChronicle story)
        {
            string art = story.CurrentStoryScene?.ArtKey ?? "";
            if (art.StartsWith(ArtPrefix, StringComparison.Ordinal)) art = art.Substring(ArtPrefix.Length);
            switch (art)
            {
                case "nativity-annunciation": return "nazareth";
                case "nativity-visitation": return "hill-house";
                case "nativity-joseph": return "workshop";
                case "birth-road": return "road";
                case "birth-manger": return "stable";
                case "birth-shepherds": return "hillside";
                case "birth-temple": return "temple";
                case "nazareth": return "nazareth-family";
                case "nativity-magi":
                    // The same catalog art key spans five distinct details.
                    // The loss of the children has an empty village setting;
                    // never place a cheerful family or a graphic reenactment over it.
                    if (story.DialogueIndex == 0 && story.StoryDetailIndex == 2) return "silent-village";
                    if (story.DialogueIndex == 0 && (story.StoryDetailIndex == 1 || story.StoryDetailIndex == 3)) return "road";
                    return story.DialogueIndex == 0 && story.StoryDetailIndex == 0 ? "magi-house" : "nazareth-family";
                default: throw new InvalidOperationException("No authored NPR opening set for the current scene: " + art);
            }
        }

        Transform BuildSet(string key)
        {
            var root = Node("Opening spatial set · " + key, content);
            Terrain(root, key == "hillside" || key == "road" ? earth : stone);
            bool outdoor = key == "road" || key == "hillside" || key == "silent-village";
            if (outdoor)
            {
                VillageApproach(root, key);
            }
            else if (key == "temple")
            {
                Box(root, "Temple rear wall", new Vector3(0, 1.7f, -3), new Vector3(10, 3.4f, .35f), plaster);
                for (int i = 0; i < 6; i++)
                {
                    float x = -4.7f + i * 1.88f;
                    Scenery("Column_Round", root, new Vector3(x, 0, -1.9f), 3.8f);
                    if (i < 5) Scenery("Arch_Round", root, new Vector3(x + .94f, 1.3f, -1.9f), 2.15f);
                }
                for (int x = -2; x <= 2; x++)
                    for (int z = -1; z <= 2; z++)
                        SizedScenery("Floor_Standard", root, new Vector3(x * 1.8f, -.04f, z * 1.8f), new Vector3(1.78f, .10f, 1.78f));
                Scenery("PottersJar", root, new Vector3(-3.35f, .04f, -.5f), .68f, 26);
                DrapedLinen(root, new Vector3(3.02f, 3.30f, -2.74f), 1.35f, 2.44f);
                Box(root, "Temple stone seat", new Vector3(2.2f, .38f, -.7f), new Vector3(1.6f, .76f, .62f), stone);
            }
            else
            {
                bool stable = key == "stable";
                RoomRearWall(root);
                DressRoom(root, key);
                Box(root, "Open room left return", new Vector3(-4.1f, 1.55f, -.3f), new Vector3(.32f, 3.1f, 5.1f), plaster);
                Box(root, "Low side wall", new Vector3(4.1f, .55f, -.3f), new Vector3(.32f, 1.1f, 5.1f), plaster);
                Scenery("Arch_Round", root, new Vector3(2.45f, 0, -2.45f), 2.8f);
                for (int i = 0; i < 5; i++)
                    Box(root, "Exposed cedar ceiling beam", new Vector3(-3.7f + i * 1.85f, 3.05f, -.15f), new Vector3(.18f, .19f, 5.2f), timber);
                Box(root, "Linen rug", new Vector3(-.15f, .045f, .25f), new Vector3(3.4f, .025f, 2.25f), stable ? linen : tapestry);
                Box(root, "Cedar bench", new Vector3(-2.4f, .48f, -.65f), new Vector3(1.4f, .12f, .55f), timber);
                for (int i = 0; i < 4; i++)
                    Box(root, "Bench joinery", new Vector3(-2.9f + i % 2 * 1.0f, .24f, -.86f + i / 2 * .42f), new Vector3(.09f, .48f, .09f), timber);
                Jar(root, new Vector3(2.6f, 0, -.3f), .35f, .65f);
                Jar(root, new Vector3(2.05f, 0, -.56f), .24f, .45f);
                if (stable) Manger(root);
                if (stable || key == "workshop")
                {
                    var glow = Node("Interior warm oil-light fill", root);
                    glow.localPosition = new Vector3(-1.4f, 1.55f, .7f);
                    var light = glow.gameObject.AddComponent<Light>(); light.type = LightType.Point;
                    light.color = new Color(1, .70f, .36f); light.intensity = 1.2f;
                    light.range = 4.7f; light.shadows = LightShadows.None;
                }
                if (key == "workshop")
                {
                    Box(root, "Joiner's work table", new Vector3(1.65f, .90f, -.9f), new Vector3(2.1f, .12f, .75f), timber);
                    for (int i = 0; i < 3; i++) Box(root, "Timber awaiting work", new Vector3(1.2f + i * .25f, .99f, -.9f), new Vector3(.12f, .08f, 1.0f), timber, i * 7);
                }
                if (key == "magi-house")
                    for (int i = 0; i < 3; i++) Box(root, "Closed gift vessel", new Vector3(1.1f + i * .38f, .16f, .5f), new Vector3(.27f, .30f, .24f), i == 1 ? brass : timber, i * 15);
                Scenery("OliveTree_Galilee", root, new Vector3(3.7f, 0, -6.2f), 4.15f, 55);
                Scenery("MasticBush", root, new Vector3(1.8f, 0, -5.2f), 1.05f, 148);
            }
            environment.FinishChapter(root);
            root.gameObject.SetActive(false);
            return root;
        }

        void DistantValley()
        {
            // All opening cameras look toward the far side of the same three-dimensional stage.
            const int strips = 40;
            var positions = new Vector3[(strips + 1) * 2];
            var uv = new Vector2[positions.Length];
            var triangles = new int[strips * 6];
            for (int i = 0; i <= strips; i++)
            {
                float u = i / (float)strips, angle = Mathf.Lerp(-1.35f, 1.35f, u);
                var basePoint = new Vector3(Mathf.Sin(angle) * 44, -10, -Mathf.Cos(angle) * 44);
                int k = i * 2;
                positions[k] = basePoint;
                positions[k + 1] = basePoint + Vector3.up * 24;
                uv[k] = new Vector2(u, 0); uv[k + 1] = new Vector2(u, 1);
                if (i == strips) continue;
                int t = i * 6;
                triangles[t] = k; triangles[t + 1] = k + 1; triangles[t + 2] = k + 2;
                triangles[t + 3] = k + 2; triangles[t + 4] = k + 1; triangles[t + 5] = k + 3;
            }
            var mesh = new Mesh { name = "Terraced hills beyond the opening", vertices = positions, uv = uv, triangles = triangles };
            mesh.RecalculateBounds(); meshes.Add(mesh);
            var ridge = Node(mesh.name, content);
            ridge.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = ridge.gameObject.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = vista;
            renderer.shadowCastingMode = ShadowCastingMode.Off; renderer.receiveShadows = false;
        }

        void VillageApproach(Transform root, string key)
        {
            bool street = key == "silent-village";
            if (street)
            {
                House(root, new Vector3(-5.2f, 0, -5.8f), 24, true);
                House(root, new Vector3(5.5f, 0, -8.6f), -16, false);
                House(root, new Vector3(1.5f, .1f, -16.1f), -18, true);
                House(root, new Vector3(-2.4f, .2f, -18.3f), 9, false);
                Scenery("MarketBasket", root, new Vector3(-3.23f, .02f, -3.1f), .42f, 23);
                Scenery("PottersJar", root, new Vector3(3.7f, .02f, -4.2f), .67f, 73);
            }
            else if (key == "road")
            {
                House(root, new Vector3(-5.0f, .1f, -12.4f), 33, false);
                House(root, new Vector3(4.4f, .15f, -17.2f), -14, true);
            }
            for (int i = 0; i < 7; i++)
            {
                float side = i % 2 == 0 ? -1 : 1;
                float z = 2.2f - i * 2.85f;
                Scenery("OliveTree_Galilee", root, new Vector3(side * (4.8f + i % 3 * .93f), .12f, z),
                    3.3f + (i % 4) * .53f, 63 * i);
                Scenery("MasticBush", root, new Vector3(side * (3.55f + i % 2), .08f, z - .85f),
                    .69f + .13f * (i % 3), 81 * i);
            }
            for (int i = 0; i < 11; i++)
            {
                float z = -1.2f - i * 1.08f;
                float x = -3.13f - .75f * Mathf.Sin(i * .33f) + z * .079f;
                Scenery("TerraceStone", root, new Vector3(x, .015f, z), .37f + (i % 3) * .095f, i * 51);
            }
            if (!street)
                Scenery("TerraceStone", root, new Vector3(-1.65f, 0, -1.15f), .55f, 52);
        }

        void DressRoom(Transform root, string key)
        {
            bool stable = key == "stable", workshop = key == "workshop";
            if (!stable)
            {
                float width = workshop ? .85f : 1.52f;
                DrapedLinen(root, new Vector3(-1.29f, 2.72f, -2.37f), width, workshop ? 1.34f : 1.95f);
                Box(root, "Linen hanging rail", new Vector3(-1.29f, 2.75f, -2.39f),
                    new Vector3(width + .17f, .056f, .08f), timber);
            }
            Box(root, "Potter's cedar shelf", new Vector3(.59f, 1.15f, -2.33f), new Vector3(1.44f, .10f, .45f), timber);
            for (int i = 0; i < 2; i++)
            {
                Box(root, "Shelf bracket", new Vector3(.13f + i * .89f, 1.01f, -2.39f), new Vector3(.10f, .24f, .29f), timber);
                Scenery("PottersJar", root, new Vector3(.14f + i * .46f, 1.21f, -2.29f), .35f - i * .11f, i * 27);
            }
            Scenery("MarketBasket", root, new Vector3(-1.70f, .07f, -.95f), .48f, -24);
            Scenery("PottersJar", root, new Vector3(-2.30f, .04f, -1.61f), .71f, 107);
            if (!stable && !workshop)
            {
                Scenery("MarketBasket", root, new Vector3(.62f, .05f, -1.73f), .38f, 38);
                for (int i = 0; i < 4; i++)
                    Scenery("MarketPomegranate", root, new Vector3(.52f + (i % 2) * .112f, .355f, -1.68f - (i / 2) * .11f), .118f, i * 67);
            }
        }

        void DrapedLinen(Transform root, Vector3 top, float width, float length)
        {
            const int across = 23, down = 16;
            var points = new Vector3[(across + 1) * (down + 1)];
            var uv = new Vector2[points.Length];
            var triangles = new int[across * down * 6];
            for (int y = 0; y <= down; y++)
                for (int x = 0; x <= across; x++)
                {
                    float u = x / (float)across, v = y / (float)down;
                    float fold = Mathf.Sin(u * 29.5f + .8f * v) * (.039f + .033f * v);
                    int j = y * (across + 1) + x;
                    points[j] = new Vector3((u - .5f) * width + .018f * Mathf.Sin(v * 4 + u * 8),
                        -v * length - .044f * Mathf.Sin(Mathf.PI * u) + .016f * v * Mathf.Cos(u * 27), fold + .02f * v * v);
                    uv[j] = new Vector2(u, 1 - v);
                    if (x == across || y == down) continue;
                    int t = (y * across + x) * 6, nextRow = j + across + 1;
                    triangles[t] = j; triangles[t + 1] = nextRow; triangles[t + 2] = j + 1;
                    triangles[t + 3] = j + 1; triangles[t + 4] = nextRow; triangles[t + 5] = nextRow + 1;
                }
            var mesh = new Mesh { name = "Linen on a hanging rod", vertices = points, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateTangents(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var cloth = Node(mesh.name, root); cloth.localPosition = top;
            cloth.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            cloth.gameObject.AddComponent<MeshRenderer>().sharedMaterial = tapestry;
        }

        void PlaceModels(string key, GospelChronicle story)
        {
            foreach (var model in models.Values) { model.gameObject.SetActive(false); model.SetSpeaking(false); }
            bool silent = key == "silent-village";
            if (silent) return;
            bool mary = key != "workshop" && key != "hillside";
            bool joseph = key != "nazareth" && key != "hill-house" && key != "hillside";
            bool teacher = key == "hillside" || key == "magi-house" || key == "temple" && story.CurrentQuest.Id == "birth-simeon-peace";
            float feet = key == "road" || key == "hillside" ? .006f : .055f;
            Place("mary", mary, new Vector3(-.62f, feet, .15f), 12);
            Place("joseph", joseph, new Vector3(.72f, feet, -.20f), 346);
            Place("teacher", teacher, new Vector3(key == "hillside" ? -.15f : 1.90f, feet, key == "hillside" ? 0 : -.55f), 338);
        }

        void Place(string key, bool visible, Vector3 localFoot, float yaw)
        {
            var view = models[key];
            view.transform.localPosition = localFoot;
            view.gameObject.SetActive(visible);
            view.SetMotion(Vector3.zero, true, yaw);
            if (visible) view.Tick(0);
        }

        void Frame(string key, string speaker)
        {
            bool wide = key == "road" || key == "hillside" || key == "temple" || key == "silent-village";
            bool stable = key == "stable";
            Vector3 look = new Vector3(0, stable ? .86f : wide ? 1.10f : 1.25f, 0);
            // Single-person rooms stay on their actual performer, including
            // narration turns. Family rooms retain the existing speaker bias.
            if (key == "nazareth" || key == "hill-house") look.x = -.50f;
            else if (key == "workshop") look.x = .58f;
            else if (speaker == "mary") look.x = -.30f;
            else if (speaker == "joseph") look.x = .28f;
            else if (speaker == "teacher") look.x = key == "hillside" ? -.12f : .8f;
            float distance = wide ? 5.4f : stable ? 4.0f : 3.25f;
            Vector3 eye = look + new Vector3(.28f, .65f, distance);
            cameraView.fieldOfView = 44;
            if (speaker != null && models.TryGetValue(speaker, out var performer) && performer.gameObject.activeSelf && performer.Head != null)
            {
                // Cut to the actual speaking face. Keep establishing views for the narrated beats.
                look = content.InverseTransformPoint(performer.Head.position) + Vector3.up * .025f;
                look.y -= .17f;
                eye = look + new Vector3(speaker == "joseph" ? -.24f : .24f, .09f, 1.37f);
                cameraView.fieldOfView = 40;
            }
            cameraView.farClipPlane = 90;
            cameraView.transform.position = content.TransformPoint(eye);
            cameraView.transform.LookAt(content.TransformPoint(look), Vector3.up);
        }

        void SetLight(string key, GospelChronicle story)
        {
            RestoreLight();
            // The opening camera observes +Z-facing actors from +Z. Light
            // through the open front of these sets, then restore the actual
            // exploration sun's complete world rotation on stage exit.
            if (observedSun != null) observedSun.transform.rotation = Quaternion.Euler(43, 147, 0);
            bool night = key == "hillside" || key == "workshop"
                || key == "stable" && story.CurrentQuest.Id != "birth-name-jesus"
                || key == "road" && story.CurrentQuest.Id == "birth-nazareth-growth"
                    && story.DialogueIndex == 0 && story.StoryDetailIndex == 1;
            vista.SetColor("_Tint", night ? new Color(.22f, .29f, .46f) : Color.white);
            cameraView.clearFlags = night ? CameraClearFlags.SolidColor : savedClear;
            cameraView.backgroundColor = night ? new Color(.045f, .068f, .12f) : savedBackground;
            if (!night) return;
            // Grade the sky, distant hills and the same scene sun together.
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(.22f, .26f, .34f);
            if (observedSun != null)
            {
                observedSun.color = new Color(.74f, .80f, 1);
                observedSun.intensity = .38f;
            }
        }

        void RestoreLight()
        {
            RenderSettings.ambientMode = savedAmbientMode;
            RenderSettings.ambientLight = savedAmbient;
            RenderSettings.ambientSkyColor = savedAmbientSky;
            RenderSettings.ambientEquatorColor = savedAmbientEquator;
            RenderSettings.ambientGroundColor = savedAmbientGround;
            if (observedSun != null)
            {
                observedSun.color = savedSunColor; observedSun.intensity = savedSunIntensity;
                observedSun.transform.rotation = savedSunRotation;
            }
        }

        void Update()
        {
            if (!IsVisible) return;
            float dt = Mathf.Min(Time.deltaTime, Time.maximumDeltaTime);
            foreach (var view in models.Values) if (view != null && view.isActiveAndEnabled) view.Tick(dt);
        }

        void Terrain(Transform root, Material material)
        {
            const int count = 18;
            var vertices = new Vector3[(count + 1) * (count + 1)];
            var uv = new Vector2[vertices.Length];
            var triangles = new int[count * count * 6];
            for (int z = 0; z <= count; z++) for (int x = 0; x <= count; x++)
            {
                int i = z * (count + 1) + x;
                float px = (x - count * .5f) * 1.4f, pz = (z - count * .5f) * 1.4f;
                float edge = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(2.5f, 10, Mathf.Max(Mathf.Abs(px), Mathf.Abs(pz))));
                vertices[i] = new Vector3(px, edge * (.18f + Mathf.Sin(px * .53f) * Mathf.Cos(pz * .47f) * .18f), pz);
                uv[i] = new Vector2(px, pz) * .49f;
            }
            int cursor = 0;
            for (int z = 0; z < count; z++) for (int x = 0; x < count; x++)
            {
                int i = z * (count + 1) + x;
                triangles[cursor++] = i; triangles[cursor++] = i + count + 1; triangles[cursor++] = i + 1;
                triangles[cursor++] = i + 1; triangles[cursor++] = i + count + 1; triangles[cursor++] = i + count + 2;
            }
            var mesh = new Mesh { name = "Opening spatial terrain", vertices = vertices, uv = uv, triangles = triangles };
            mesh.RecalculateNormals(); mesh.RecalculateBounds(); meshes.Add(mesh);
            var node = Node("Opening continuous terrain", root);
            node.gameObject.AddComponent<MeshFilter>().sharedMesh = mesh;
            node.gameObject.AddComponent<MeshRenderer>().sharedMaterial = material;
        }

        void RoomRearWall(Transform root)
        {
            // Four real wall segments leave an actual aperture behind the
            // imported arch. The view reaches the spatial courtyard/palm,
            // rather than another closed wall or a pasted window picture.
            Box(root, "Rear plaster left of opening", new Vector3(-1.325f, 1.55f, -2.7f), new Vector3(5.85f, 3.1f, .34f), plaster);
            Box(root, "Rear plaster right of opening", new Vector3(3.775f, 1.55f, -2.7f), new Vector3(.95f, 3.1f, .34f), plaster);
            Box(root, "Rear plaster below opening", new Vector3(2.45f, .30f, -2.7f), new Vector3(1.70f, .60f, .34f), plaster);
            Box(root, "Rear plaster above opening", new Vector3(2.45f, 2.625f, -2.7f), new Vector3(1.70f, .95f, .34f), plaster);
            Box(root, "Stone window sill", new Vector3(2.45f, .62f, -2.51f), new Vector3(1.95f, .10f, .48f), stone);
        }

        void House(Transform root, Vector3 foot, float yaw, bool shuttered)
        {
            var house = Node("Distant stone house", root); house.localPosition = foot; house.localRotation = Quaternion.Euler(0, yaw, 0);
            Box(house, "Lime plaster volume", new Vector3(0, 1.2f, 0), new Vector3(3.2f, 2.4f, 2.7f), masonry);
            Box(house, "Flat timber roof", new Vector3(0, 2.46f, 0), new Vector3(3.5f, .16f, 3), timber);
            Box(house, "Deep doorway", new Vector3(0, .85f, 1.37f), new Vector3(.80f, 1.70f, .045f), shuttered ? timber : earth);
            Scenery("Arch_Round", house, new Vector3(0, 0, 1.41f), 1.95f);
            Box(house, "Shaded street window", new Vector3(1.01f, 1.44f, 1.36f), new Vector3(.63f, .78f, .09f), timber);
            for (int i = 0; i < 5; i++)
                Box(house, "Window slat", new Vector3(.77f + i * .12f, 1.43f, 1.43f), new Vector3(.078f, .71f, .055f), blue);
            Box(house, "Limestone window sill", new Vector3(1.01f, 1.02f, 1.44f), new Vector3(.79f, .10f, .25f), stone);
            Box(house, "Earthen roof coping", new Vector3(0, 2.57f, 0), new Vector3(3.41f, .14f, 2.94f), stone);
            for (int i = 0; i < 5; i++)
                Box(house, "Exposed roof joist", new Vector3(-1.22f + i * .61f, 2.33f, 1.37f), new Vector3(.13f, .14f, .46f), timber);
            Scenery("PottersJar", house, new Vector3(-1.12f, .03f, 1.77f), .59f, 18);
            environment.FinishChapter(house);
        }

        void Manger(Transform root)
        {
            var manger = Node("Bethlehem manger · no child proxy mesh", root);
            manger.localPosition = new Vector3(.04f, 0, .96f);
            Box(manger, "Manger timber trough", new Vector3(0, .48f, 0), new Vector3(.94f, .26f, .52f), timber);
            for (int i = 0; i < 4; i++) Box(manger, "Manger timber support", new Vector3((i % 2 == 0 ? -1 : 1) * .31f, .23f, (i / 2 == 0 ? -1 : 1) * .17f), new Vector3(.09f, .46f, .09f), timber);
            // This is folded fabric resting in a trough. It has no head, face,
            // body, bones or animation and is never presented as a baby model.
            Box(manger, "Folded undyed swaddling cloth", new Vector3(0, .64f, 0), new Vector3(.72f, .07f, .40f), linen, -6);
            for (int i = 0; i < 5; i++) Box(manger, "Loose woven cloth fold", new Vector3(-.28f + i * .14f, .68f, -.03f), new Vector3(.025f, .022f, .31f), linen, i - 2);
        }

        void Jar(Transform root, Vector3 foot, float radius, float height)
        {
            SizedScenery("PottersJar", root, foot, new Vector3(radius * 2, height, radius * 2));
        }

        void Scenery(string key, Transform root, Vector3 localFoot, float height, float yaw = 0) =>
            environment.Add(key, root, root.TransformPoint(localFoot), height, yaw);
        void SizedScenery(string key, Transform root, Vector3 localFoot, Vector3 size) =>
            environment.AddSized(key, root, root.TransformPoint(localFoot), size);

        static Transform Node(string name, Transform parent)
        {
            var node = new GameObject(name).transform; node.SetParent(parent, false); return node;
        }
        void Box(Transform parent, string name, Vector3 center, Vector3 size, Material material, float yaw = 0)
        {
            var node = GameObject.CreatePrimitive(PrimitiveType.Cube); node.name = name;
            node.transform.SetParent(parent, false); node.transform.localPosition = center;
            node.transform.localScale = size; node.transform.localRotation = Quaternion.Euler(0, yaw, 0);
            var collider = node.GetComponent<Collider>(); collider.enabled = false; Destroy(collider);
            var renderer = node.GetComponent<MeshRenderer>(); renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.On; renderer.receiveShadows = true;
        }
        Material Mat(string name, Color color, float smoothness = .12f)
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The actual retained URP Lit shader is required for opening scenery.");
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.SetColor("_BaseColor", color); material.SetFloat("_Smoothness", smoothness);
            materials.Add(material); return material;
        }
        static void RetainedSurface(Material material, string albedoPath, string normalPath, float normalStrength)
        {
            var albedo = Resources.Load<Texture2D>(albedoPath);
            if (albedo == null) throw new InvalidOperationException("Missing retained opening surface: " + albedoPath);
            material.SetTexture("_BaseMap", albedo);
            if (normalPath == null) return;
            var normal = Resources.Load<Texture2D>(normalPath);
            if (normal == null) throw new InvalidOperationException("Missing retained opening surface normal: " + normalPath);
            material.SetTexture("_BumpMap", normal); material.SetFloat("_BumpScale", normalStrength);
            material.EnableKeyword("_NORMALMAP");
        }

        void OnGUI()
        {
            if (string.IsNullOrEmpty(LoadError)) return;
            if (errorStyle == null) errorStyle = new GUIStyle(GUI.skin.box) { fontSize = 18, wordWrap = true, alignment = TextAnchor.MiddleLeft };
            GUI.Box(new Rect(20, 20, Mathf.Min(Screen.width - 40, 1000), 110), "NPR opening scene could not load: " + LoadError, errorStyle);
        }
        void OnDisable() => Hide();
        void OnDestroy()
        {
            Hide();
            foreach (var material in materials) if (material != null) Destroy(material);
            foreach (var mesh in meshes) if (mesh != null) Destroy(mesh);
        }
    }
}
