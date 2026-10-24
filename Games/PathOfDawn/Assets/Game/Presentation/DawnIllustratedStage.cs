using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>
    /// A composed, painted stage driven by the existing collision-resolved world.
    /// Presentation never writes actor positions, story state, saves or random state.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class DawnIllustratedStage : MonoBehaviour
    {
        public enum ViewMode { Exploration, Dialogue, Intro }

        [Serializable] public sealed class ArtFrame
        {
            public string id;
            public float x, y, width = 1, height = 1, footX = .5f, footY;
            public Rect Uv => new Rect(x, y, width, height);
        }
        [Serializable] sealed class ArtLayout
        {
            public ArtFrame[] characters;
            public ArtFrame[] walkFrames;
        }
        sealed class ActorArt
        {
            public string id;
            public Transform actor;
            public RectTransform group, imageRect, shadowRect, marker;
            public DawnLivingImage image;
            public Image shadow;
            public CanvasGroup visibility;
            public Texture2D idle;
            public ArtFrame idleFrame;
            public Vector3 previous;
            public Vector2 screenPosition;
            public float height, alpha = 1, clock, phase;
            public bool initialized, rear;
            public float facing = 1, movementBlend, lean;
        }
        sealed class Mote
        {
            public RectTransform rect;
            public Image image;
            public Vector2 origin;
            public float phase, speed, size;
            public bool water;
        }

        readonly Dictionary<string, ActorArt> cast = new Dictionary<string, ActorArt>();
        readonly List<ActorArt> depthOrder = new List<ActorArt>();
        readonly List<Mote> motes = new List<Mote>();
        RectTransform stage, painting, castRoot, ambienceRoot;
        RawImage background, oldBackground;
        CanvasGroup stageVisibility;
        Texture2D shore, room, walkAtlas, rearIdle, motionFields;
        Shader livingShader;
        ArtLayout layout;
        Sprite softLight;
        Texture2D softLightTexture;
        Vector2 look, smoothedLook;
        float sceneFade = 1, dialogueBlend, introTime, sceneLight;
        bool visible, sharedBasket, preparedShelter, connectedPeople;
        string scene = "shore", speaker = "jesus";
        ViewMode mode;

        public bool IsReady { get; private set; }
        public RectTransform StageRoot => stage;
        public string CurrentScene => scene;
        public ViewMode CurrentMode => mode;
        public int ActorCount => cast.Count;
        public Texture2D BackgroundTexture => background != null ? background.texture as Texture2D : null;
        public bool TravelerIsMoving { get; private set; }
        public int TravelerWalkFrame { get; private set; } = -1;
        public float TravelerNormalizedSpeed { get; private set; }
        public Vector2 LookOffset => smoothedLook;

        public RectTransform ActorVisual(string id) => cast.TryGetValue(id, out var actor) ? actor.imageRect : null;
        public Texture ActorTexture(string id) => cast.TryGetValue(id, out var actor) ? actor.image.texture : null;
        public Vector2 ActorStagePosition(string id) => cast.TryGetValue(id, out var actor) ? actor.screenPosition : Vector2.zero;

        public void Initialize(RectTransform designFrame, Dictionary<string, Transform> actors)
        {
            if (stage != null) throw new InvalidOperationException("The illustrated stage is already initialized.");
            if (designFrame == null || actors == null) throw new ArgumentNullException();
            motionFields = DawnLivingImage.LoadMotionFields();
            livingShader = Resources.Load<Shader>("Art/Painted/DawnLivingSprite");
            shore = Resources.Load<Texture2D>("Art/Painted/shore");
            room = Resources.Load<Texture2D>("Art/Painted/room");
            walkAtlas = Resources.Load<Texture2D>("Art/Painted/traveler-walk");
            rearIdle = Resources.Load<Texture2D>("Art/Painted/traveler-back");
            var manifest = Resources.Load<TextAsset>("Art/Painted/layout");
            layout = manifest != null ? JsonUtility.FromJson<ArtLayout>(manifest.text) : new ArtLayout();

            stage = Rect(designFrame, "Illustrated Galilee Stage", Vector2.zero, new Vector2(1920, 1080));
            stage.anchorMin = stage.anchorMax = stage.pivot = new Vector2(.5f, .5f);
            stage.anchoredPosition = Vector2.zero;
            stage.gameObject.AddComponent<RectMask2D>();
            stageVisibility = stage.gameObject.AddComponent<CanvasGroup>();
            stageVisibility.interactable = false;
            stageVisibility.blocksRaycasts = false;
            var paper = stage.gameObject.AddComponent<Image>();
            paper.color = new Color(.91f, .88f, .77f);
            paper.raycastTarget = false;

            painting = Rect(stage, "Painted scene layers", new Vector2(960, 540), new Vector2(1920, 1080));
            oldBackground = Picture(painting, "Previous painting", shore);
            background = Picture(painting, "Current painting", shore);
            oldBackground.color = Color.clear;
            castRoot = Rect(stage, "Illustrated cast", new Vector2(960, 540), new Vector2(1920, 1080));
            ambienceRoot = Rect(stage, "Dawn light and lake reflections", new Vector2(960, 540), new Vector2(1920, 1080));
            BuildSoftLight();
            foreach (string id in new[] { "jesus", "miriam", "traveler" })
                if (actors.TryGetValue(id, out var actor)) BuildActor(id, actor);
            BuildAmbience();
            IsReady = livingShader != null && livingShader.isSupported && shore != null && room != null && walkAtlas != null && rearIdle != null && cast.Count == 3;
            foreach (var actor in cast.Values) IsReady &= actor.idle != null;
            if (!IsReady) Debug.LogError("Painted Galilee requires both paintings, three character cutouts, traveler-back rear idle and the traveler walk atlas.");
            EnsureBehindHud();
            SetVisible(false);
        }

        public void EnsureBehindHud()
        {
            if (stage != null) stage.SetAsFirstSibling();
        }

        public void SetVisible(bool value)
        {
            visible = value;
            if (stage != null) stage.gameObject.SetActive(value);
            if (value)
            {
                foreach (var actor in cast.Values) { actor.previous = actor.actor.position; actor.initialized = false; }
                ResetLook();
            }
            else
            {
                TravelerIsMoving = false;
                TravelerNormalizedSpeed = 0;
                TravelerWalkFrame = -1;
            }
        }

        public void SetScene(string pointId)
        {
            string next = pointId == "shore" ? "shore" : "room";
            if (scene != next && background != null)
            {
                oldBackground.texture = background.texture;
                oldBackground.color = Color.white;
                background.texture = next == "room" ? room : shore;
                sceneFade = 0;
                ResetLook();
                foreach (var actor in cast.Values) actor.initialized = false;
            }
            scene = next;
            // Applying a save/rewind is a position change, not a walking step.
            foreach (var actor in cast.Values) actor.previous = actor.actor.position;
        }

        public void SetMode(ViewMode value)
        {
            if (mode == value) return;
            mode = value;
            if (mode == ViewMode.Intro) introTime = 0;
            if (mode != ViewMode.Exploration)
            {
                TravelerIsMoving = false;
                TravelerNormalizedSpeed = 0;
                TravelerWalkFrame = -1;
            }
        }

        public void SetSpeaker(string id) => speaker = cast.ContainsKey(id ?? "") ? id : "traveler";

        public void SetLook(Vector2 delta)
        {
            // Deliberately bounded pan, rather than a camera orbit that exposes flat art.
            look = new Vector2(Mathf.Clamp(look.x + delta.x * 2.4f, -65, 65),
                Mathf.Clamp(look.y + delta.y * 1.5f, -25, 25));
        }

        public void ResetLook() { look = Vector2.zero; smoothedLook = Vector2.zero; }

        public void SetWorldFlags(bool basketShared, bool shelterPrepared, bool peopleConnected)
        {
            sharedBasket = basketShared;
            preparedShelter = shelterPrepared;
            connectedPeople = peopleConnected;
        }

        void LateUpdate()
        {
            if (!visible || stage == null) return;
            float dt = Mathf.Max(.0001f, Time.unscaledDeltaTime);
            float ease = 1 - Mathf.Exp(-dt * 7);
            smoothedLook = Vector2.Lerp(smoothedLook, look, ease);
            dialogueBlend = Mathf.Lerp(dialogueBlend, mode == ViewMode.Dialogue ? 1 : 0, ease);
            sceneFade = Mathf.MoveTowards(sceneFade, 1, dt * 2.5f);
            background.color = new Color(1, 1, 1, sceneFade);
            oldBackground.color = new Color(1, 1, 1, 1 - sceneFade);
            introTime += mode == ViewMode.Intro ? dt : 0;
            float introZoom = mode == ViewMode.Intro ? Mathf.Lerp(.095f, .035f, Mathf.Clamp01(introTime / 6)) : 0;
            float zoom = 1.075f + dialogueBlend * .055f + introZoom;
            painting.localScale = new Vector3(zoom, zoom, 1);
            painting.anchoredPosition = new Vector2(-smoothedLook.x * .36f, smoothedLook.y * .32f) +
                new Vector2(Mathf.Sin(Time.unscaledTime * .055f) * 2, Mathf.Sin(Time.unscaledTime * .073f) * 1.2f);

            depthOrder.Clear();
            foreach (var actor in cast.Values)
            {
                UpdateActor(actor, dt, ease);
                depthOrder.Add(actor);
            }
            // Higher ground positions are farther away; nearby feet draw last.
            depthOrder.Sort((a, b) => b.screenPosition.y.CompareTo(a.screenPosition.y));
            foreach (var actor in depthOrder) actor.group.SetAsLastSibling();
            UpdateAmbience(dt);
        }

        void UpdateActor(ActorArt art, float dt, float ease)
        {
            Vector3 position = art.actor.position;
            Vector3 displacement = position - art.previous;
            displacement.y = 0;
            art.previous = position;
            float actualSpeed = displacement.magnitude / Mathf.Max(.0001f, Time.deltaTime);
            bool discontinuity = displacement.magnitude > Mathf.Max(.8f, DawnMovement.Speed * DawnMovement.DepthScale * Time.deltaTime * 1.5f);
            bool walking = art.id == "traveler" && mode == ViewMode.Exploration && actualSpeed > .08f && !discontinuity;
            if (art.id == "traveler")
            {
                TravelerIsMoving = walking;
                TravelerNormalizedSpeed = walking ? Mathf.Clamp01(new Vector2(displacement.x, displacement.z / DawnMovement.DepthScale).magnitude / Mathf.Max(.0001f, Time.deltaTime) / DawnMovement.Speed) : 0;
                if (walking)
                {
                    art.clock += new Vector2(displacement.x, displacement.z / DawnMovement.DepthScale).magnitude / 1.8f;
                    if (Mathf.Abs(displacement.z) > Mathf.Abs(displacement.x) * .45f) art.rear = displacement.z > 0;
                    if (Mathf.Abs(displacement.x) > Mathf.Abs(displacement.z) * .18f && Mathf.Abs(displacement.x) > .001f) art.facing = Mathf.Sign(displacement.x);
                    TravelerWalkFrame = Mathf.FloorToInt(art.clock * 4) % 4;
                }
                else TravelerWalkFrame = -1;
            }

            ArtFrame frame = art.idleFrame;
            Texture2D texture = art.idle;
            if (art.id == "traveler" && dialogueBlend < .5f)
            {
                if (walking && walkAtlas != null)
                {
                    texture = walkAtlas;
                    frame = WalkFrame((art.rear ? 4 : 0) + TravelerWalkFrame);
                }
                else if (art.rear)
                {
                    if (rearIdle != null) { texture = rearIdle; frame = CharacterFrame("traveler-back"); }
                }
            }
            bool snapPose = !art.initialized || discontinuity;

            Vector2 foot = Project(position);
            float perspective = Mathf.Lerp(1.22f, .78f, Mathf.InverseLerp(220, 650, foot.y));
            float targetHeight = (art.id == "jesus" ? 425 : art.id == "miriam" ? 395 : 420) * perspective;
            float targetAlpha = scene == "room" && art.id == "jesus" ? 0 : 1;
            Vector2 composedFoot = foot;
            float composedHeight = targetHeight;
            if (mode == ViewMode.Dialogue || dialogueBlend > .001f)
            {
                bool focus = art.id == (speaker == "traveler" ? (scene == "room" ? "miriam" : "jesus") : speaker);
                if (scene == "shore")
                {
                    if (art.id == "jesus") { composedFoot = new Vector2(1165, 70); composedHeight = focus ? 830 : 795; }
                    else if (art.id == "miriam") { composedFoot = new Vector2(665, 115); composedHeight = focus ? 785 : 730; }
                    else { composedFoot = new Vector2(235, 65); composedHeight = 730; targetAlpha = 0; }
                }
                else
                {
                    // The transition into the room can still contain a line from Jesus.
                    // Keep the actual speaker in the composition, independent of location.
                    bool jesusSpeaking = speaker == "jesus";
                    if (art.id == "jesus")
                    {
                        composedFoot = new Vector2(1130, 70); composedHeight = 830;
                        targetAlpha = jesusSpeaking ? 1 : 0;
                    }
                    else if (art.id == "miriam")
                    {
                        composedFoot = new Vector2(jesusSpeaking ? 610 : 1130, 75);
                        composedHeight = jesusSpeaking ? 750 : 820;
                    }
                    else
                    {
                        composedFoot = new Vector2(550, 65); composedHeight = 810;
                        targetAlpha = jesusSpeaking ? 0 : 1;
                    }
                }
                foot = Vector2.Lerp(foot, composedFoot, dialogueBlend);
                targetHeight = Mathf.Lerp(targetHeight, composedHeight, dialogueBlend);
            }
            foot += new Vector2(-smoothedLook.x * .82f, smoothedLook.y * .7f) * (1 - dialogueBlend * .65f);
            if (!art.initialized)
            {
                art.screenPosition = foot;
                art.height = targetHeight;
                art.alpha = targetAlpha;
                art.initialized = true;
            }
            // Actor movement itself is direct; only cinematic composition is damped.
            art.screenPosition = Vector2.Lerp(art.screenPosition, foot, mode == ViewMode.Exploration && dialogueBlend < .01f ? 1 : ease);
            art.height = Mathf.Lerp(art.height, targetHeight, ease);
            art.alpha = Mathf.Lerp(art.alpha, targetAlpha, ease);
            art.visibility.alpha = art.alpha;
            art.group.anchoredPosition = art.screenPosition - new Vector2(960, 540);
            float facing = art.id == "traveler" && dialogueBlend < .5f ? art.facing : 1;
            int poseIndex = art.id != "traveler" ? -1 : texture == walkAtlas ? 2 + (art.rear ? 4 : 0) + TravelerWalkFrame : texture == rearIdle ? 1 : 0;
            art.image.ShowPose(texture, frame, facing, snapPose, dt, poseIndex);
            art.image.SetHeight(art.height);
            art.movementBlend = Mathf.Lerp(art.movementBlend, walking ? TravelerNormalizedSpeed : 0, 1 - Mathf.Exp(-dt * 14));
            float desiredLean = walking ? Mathf.Clamp(displacement.x / Mathf.Max(.0001f, Time.deltaTime) / DawnMovement.Speed, -1, 1) : 0;
            art.lean = Mathf.Lerp(art.lean, desiredLean, 1 - Mathf.Exp(-dt * 12));
            art.image.Animate(Time.unscaledTime * 1.55f + art.phase, art.clock, art.movementBlend, art.lean);
            // Only torso/cloth vertices move. The whole cutout no longer bobs
            // above the floor or snaps to a negative transform scale on turns.
            art.imageRect.anchoredPosition = Vector2.zero;
            art.imageRect.localScale = Vector3.one;
            art.imageRect.localRotation = Quaternion.identity;
            art.shadowRect.sizeDelta = new Vector2(art.height * .34f, art.height * .055f);
            art.shadow.color = new Color(.15f, .10f, .055f, Mathf.Lerp(.28f, .18f, dialogueBlend));
            bool isTarget = art.id == (scene == "shore" ? "jesus" : "miriam");
            bool interactionPromptVisible = isTarget && cast.TryGetValue("traveler", out var traveler) &&
                Vector3.Distance(position, traveler.actor.position) < (scene == "shore" ? 3 : 2.8f);
            // Nearby players already receive the named interaction prompt. Hiding
            // this marker also prevents its label from covering the player's face.
            art.marker.gameObject.SetActive(isTarget && !interactionPromptVisible &&
                mode == ViewMode.Exploration && dialogueBlend < .1f);
            // Keep the name below its diamond and below the HUD. Place the marker
            // beside the upper body so the lower label never covers the face.
            float markerY = Mathf.Min(art.height + 27 + Mathf.Sin(Time.unscaledTime * 2) * 3,
                890 - art.screenPosition.y);
            art.marker.anchoredPosition = new Vector2(art.height * .32f, markerY);
        }

        Vector2 Project(Vector3 position)
        {
            Vector2 origin = scene == "shore" ? Vector2.zero : new Vector2(-4, -3);
            float depth = position.z - origin.y;
            float y = 480 + (depth <= 0 ? depth * 33 : 33 * depth / (1 + depth * .16f));
            y = Mathf.Clamp(y, 190, 650);
            float perspective = Mathf.Lerp(1.13f, .86f, Mathf.InverseLerp(210, 650, y));
            float x = 1070 + (position.x - origin.x) * 93 * perspective;
            return new Vector2(Mathf.Clamp(x, 160, 1770), y);
        }

        void BuildActor(string id, Transform actor)
        {
            var group = Rect(castRoot, "Illustrated actor · " + id, Vector2.zero, Vector2.zero);
            var shadowRect = Rect(group, "Soft ground contact", Vector2.zero, new Vector2(140, 22));
            var shadow = shadowRect.gameObject.AddComponent<Image>();
            shadow.sprite = softLight;
            shadow.raycastTarget = false;
            var imageRect = Rect(group, "Painted full-body character", Vector2.zero, new Vector2(180, 420));
            imageRect.pivot = new Vector2(.5f, 0);
            var image = imageRect.gameObject.AddComponent<DawnLivingImage>();
            image.Initialize(livingShader, motionFields);
            var texture = Resources.Load<Texture2D>("Art/Painted/" + id);
            image.texture = texture;
            var marker = Rect(group, "Conversation marker", new Vector2(0, 450), new Vector2(180, 65));
            var diamond = Rect(marker, "Gold diamond", new Vector2(0, 8), new Vector2(12, 12));
            diamond.localRotation = Quaternion.Euler(0, 0, 45);
            var gem = diamond.gameObject.AddComponent<Image>();
            gem.color = new Color(1, .84f, .38f, .96f); gem.raycastTarget = false;
            var nameplate = new DawnUi().Card(marker, "NPC name background", 0, 0, 132, 42,
                new Color(.075f, .115f, .20f, .94f));
            nameplate.anchorMin = nameplate.anchorMax = nameplate.pivot = new Vector2(.5f, .5f);
            nameplate.anchoredPosition = new Vector2(0, -20);
            nameplate.GetComponent<Image>().raycastTarget = false;
            var labelRect = Rect(marker, "NPC name", new Vector2(0, -20), new Vector2(210, 45));
            var label = labelRect.gameObject.AddComponent<TextMeshProUGUI>();
            label.font = Resources.Load<TMP_FontAsset>("Fonts/Korean") ?? TMP_Settings.defaultFontAsset;
            label.text = id == "jesus" ? "예수님" : id == "miriam" ? "미리암" : "여행자";
            label.fontSize = 27; label.fontStyle = FontStyles.Bold; label.alignment = TextAlignmentOptions.Center;
            label.color = new Color(1, .96f, .79f); label.outlineWidth = .22f; label.outlineColor = new Color(.16f, .15f, .11f, .95f);
            label.raycastTarget = false;
            var item = new ActorArt { id = id, actor = actor, group = group, imageRect = imageRect,
                shadowRect = shadowRect, shadow = shadow, image = image, marker = marker,
                idle = texture, idleFrame = CharacterFrame(id), previous = actor.position,
                visibility = group.gameObject.AddComponent<CanvasGroup>(), phase = cast.Count * 1.3f };
            item.visibility.interactable = false; item.visibility.blocksRaycasts = false;
            cast.Add(id, item);
        }

        ArtFrame CharacterFrame(string id)
        {
            if (layout?.characters != null)
                foreach (var frame in layout.characters)
                    if (frame.id == id && Valid(frame)) return frame;
            return new ArtFrame { id = id };
        }

        ArtFrame WalkFrame(int index)
        {
            if (layout?.walkFrames != null && index < layout.walkFrames.Length && Valid(layout.walkFrames[index]))
                return layout.walkFrames[index];
            return new ArtFrame { x = (index % 4) * .25f, y = index < 4 ? .5f : 0, width = .25f, height = .5f };
        }

        static bool Valid(ArtFrame frame) => frame != null && frame.width > 0 && frame.height > 0 &&
            frame.x >= 0 && frame.y >= 0 && frame.x + frame.width <= 1.001f && frame.y + frame.height <= 1.001f;

        void BuildSoftLight()
        {
            const int size = 64;
            softLightTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "Painted stage soft contact", hideFlags = HideFlags.HideAndDontSave };
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++) for (int x = 0; x < size; x++)
            {
                float radius = new Vector2((x + .5f - size / 2f) / (size / 2f), (y + .5f - size / 2f) / (size / 2f)).magnitude;
                pixels[y * size + x] = new Color(1, 1, 1, Mathf.Pow(Mathf.Clamp01(1 - radius), 1.65f));
            }
            softLightTexture.SetPixels(pixels); softLightTexture.Apply(false, true);
            softLight = Sprite.Create(softLightTexture, new Rect(0, 0, size, size), new Vector2(.5f, .5f));
            softLight.hideFlags = HideFlags.HideAndDontSave;
        }

        void BuildAmbience()
        {
            // Deterministic decoration; the story's RNG is never consumed.
            for (int i = 0; i < 28; i++)
            {
                bool water = i < 12;
                float t = (i * .61803398875f) % 1;
                var origin = water ? new Vector2(1040 + t * 720, 650 + ((i * .37f) % 1) * 100) :
                    new Vector2(100 + t * 1720, 110 + ((i * .43f) % 1) * 680);
                var rect = Rect(ambienceRoot, water ? "Lake sunlight reflection" : "Drifting dawn mote", origin, Vector2.one);
                var image = rect.gameObject.AddComponent<Image>(); image.sprite = softLight; image.raycastTarget = false;
                motes.Add(new Mote { rect = rect, image = image, origin = origin, phase = i * 1.7f,
                    speed = .15f + (i % 5) * .035f, size = water ? 25 + (i % 4) * 9 : 6 + i % 4, water = water });
            }
        }

        void UpdateAmbience(float dt)
        {
            sceneLight = Mathf.MoveTowards(sceneLight, sharedBasket || preparedShelter || connectedPeople ? 1 : 0, dt * .7f);
            float time = Time.unscaledTime;
            foreach (var mote in motes)
            {
                bool enabledHere = !mote.water || scene == "shore";
                mote.rect.gameObject.SetActive(enabledHere);
                if (!enabledHere) continue;
                float wave = Mathf.Sin(time * (mote.water ? 1.3f : .7f) + mote.phase);
                float alpha = mote.water ? .16f + wave * .10f : .18f + wave * .12f + sceneLight * .06f;
                mote.image.color = mote.water ? new Color(.94f, .99f, 1, alpha) : new Color(1, .92f, .64f, alpha);
                mote.rect.sizeDelta = mote.water ? new Vector2(mote.size * 1.8f, 2.5f) : Vector2.one * mote.size;
                mote.rect.anchoredPosition = mote.origin - new Vector2(960, 540) + new Vector2(Mathf.Sin(time * mote.speed + mote.phase) * (mote.water ? 9 : 28),
                    Mathf.Cos(time * mote.speed * .8f + mote.phase) * (mote.water ? 1.5f : 25)) +
                    new Vector2(-smoothedLook.x * .5f, smoothedLook.y * .45f);
            }
        }

        static RectTransform Rect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform)); go.layer = 5;
            var rect = go.GetComponent<RectTransform>(); rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(.5f, .5f);
            rect.sizeDelta = size;
            // Stage's coordinates are a 1920x1080 sheet with its origin at bottom-left.
            bool sheetChild = parent is RectTransform p && p.sizeDelta == new Vector2(1920, 1080);
            rect.anchoredPosition = sheetChild ? position - new Vector2(960, 540) : position;
            return rect;
        }

        static RawImage Picture(RectTransform parent, string name, Texture2D texture)
        {
            var rect = Rect(parent, name, new Vector2(960, 540), new Vector2(1920, 1080));
            var image = rect.gameObject.AddComponent<RawImage>(); image.texture = texture; image.raycastTarget = false;
            if (texture != null)
            {
                float source = (float)texture.width / texture.height, target = 16f / 9f;
                image.uvRect = source > target ? new Rect((1 - target / source) * .5f, 0, target / source, 1) :
                    new Rect(0, (1 - source / target) * .5f, 1, source / target);
            }
            return image;
        }

        void OnDestroy()
        {
            if (stage != null) Destroy(stage.gameObject);
            if (softLight != null) Destroy(softLight);
            if (softLightTexture != null) Destroy(softLightTexture);
            if (motionFields != null) Destroy(motionFields);
        }
    }
}
