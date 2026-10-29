using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>Painted, joint-articulated character with independently deforming linen and trailing cloth.</summary>
    public sealed class GospelLuminousActor : MonoBehaviour
    {
        public string CurrentState { get; private set; } = "Idle";
        public float Phase => phase;
        public bool LampEquipped => lamp && lamp.activeSelf;
        public const float Height = 1.84f;

        // Tight, read-only source rectangles measured from the original RGBA atlas, bottom-left coordinates.
        static readonly Rect[] Parts = {
            new Rect(25,953,282,272), new Rect(358,949,269,271), new Rect(687,948,242,289), new Rect(1029,953,181,259),
            new Rect(114,661,116,261), new Rect(434,657,107,258), new Rect(740,655,109,254), new Rect(1040,662,99,254),
            new Rect(103,366,147,273), new Rect(418,370,184,263), new Rect(715,369,160,260), new Rect(1040,374,180,255),
            new Rect(56,17,206,339), new Rect(396,132,197,143), new Rect(698,113,209,132), new Rect(1035,76,153,201),
            // Overlapping source windows separate the ankle from the shoe. No source pixels are edited.
            new Rect(432,438,110,195), new Rect(418,370,184,100),
            new Rect(1053,442,99,187), new Rect(1040,374,180,102)
        };
        Transform visual, torso, head, hair, nearArm, farArm, nearForearm, farForearm;
        Transform nearThigh, farThigh, nearCalf, farCalf, nearFoot, farFoot, pouch;
        const float UpperLeg = .38f, LowerLeg = .37f, RestHip = .84f, AnkleHeight = .102f;
        float groundSlope, runBlend, airBlend, pelvisOffset, torsoAngle;
        Transform skirtRoot, sashRoot;
        GameObject lamp;
        Light2D lampLight;
        Mesh skirtMesh, sashMesh;
        Vector3[] skirtRest, sashRest, skirtVertices, sashVertices;
        float phase, speedBlend, time;
        Vector2 velocity;
        bool grounded = true;
        int facing = 1;
        Material material, clothMaterial;
        Texture2D atlas;
        static Texture2D flatNormal;
        readonly List<Sprite> generatedSprites = new List<Sprite>();

        public static readonly Rect[] NpcRects = {
            new Rect(68,28,425,987), new Rect(577,37,365,935), new Rect(1051,26,418,974)
        };

        /// <summary>0 teacher, 1 Mary, 2 Joseph. Original distinct painted faces and costumes.</summary>
        public static SpriteRenderer BuildNpc(Transform parent, int variant, float height = 1.94f)
        {
            var texture = Resources.Load<Texture2D>("Art/GospelLuminous/Actors/npcs");
            if (!texture) return null;
            variant = Mathf.Clamp(variant,0,2); var rect = NpcRects[variant];
            var obj = new GameObject(new[] { "Temple teacher", "Mary", "Joseph" }[variant]);
            obj.transform.SetParent(parent,false);
            var renderer = obj.AddComponent<SpriteRenderer>();
            renderer.sprite = Sprite.Create(texture,rect,new Vector2(.5f,0),rect.height / height,0,SpriteMeshType.FullRect);
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (!shader) shader = Shader.Find("Sprites/Default");
            var npcMaterial = new Material(shader) { name = "Luminous original NPC painting" };
            TextureBindings(npcMaterial,texture);
            renderer.sharedMaterial = npcMaterial;
            TextureBindings(renderer,texture);
            renderer.sortingOrder = 19;
            return renderer;
        }

        static Texture2D FlatNormal
        {
            get
            {
                if (flatNormal) return flatNormal;
                flatNormal = new Texture2D(1,1,TextureFormat.RGBA32,false,true) { name = "Luminous explicit flat normal", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
                flatNormal.SetPixel(0,0,new Color(.5f,.5f,1,1));
                flatNormal.Apply(false,true);
                return flatNormal;
            }
        }

        static void TextureBindings(Material target, Texture2D texture)
        {
            target.SetTexture("_MainTex",texture);
            target.SetTexture("_MaskTex",Texture2D.whiteTexture);
            target.SetTexture("_NormalMap",FlatNormal);
        }

        static void TextureBindings(Renderer target, Texture2D texture)
        {
            var properties = new MaterialPropertyBlock();
            target.GetPropertyBlock(properties);
            properties.SetTexture("_MainTex",texture);
            properties.SetTexture("_MaskTex",Texture2D.whiteTexture);
            properties.SetTexture("_NormalMap",FlatNormal);
            target.SetPropertyBlock(properties);
        }

        public static GospelLuminousActor Build(Transform parent, bool adult = false)
        {
            var obj = new GameObject("Painted Jesus · articulated 2D");
            obj.transform.SetParent(parent, false);
            var result = obj.AddComponent<GospelLuminousActor>();
            result.Create();
            if (adult) Debug.LogWarning("The Luminous opening currently uses the twelve-year-old character; adult artwork is not implemented.");
            return result;
        }

        public void SetMotion(Vector2 measuredVelocity, bool onGround, int direction)
        { velocity = measuredVelocity; grounded = onGround; facing = direction < 0 ? -1 : 1; }

        /// <summary>Measured ground normal from the controller; presentation does not move the body.</summary>
        public void SetGroundSlope(float slope) => groundSlope = Mathf.Clamp(slope, -.35f, .35f);

        public void SetLamp(bool equipped)
        { if (lamp) lamp.SetActive(equipped); }

        static Transform Node(string name, Transform parent, Vector2 position)
        {
            var obj = new GameObject(name); obj.transform.SetParent(parent, false);
            obj.transform.localPosition = position; return obj.transform;
        }

        Transform SpritePart(string name, int index, Transform parent, Vector2 anchor, float height, Vector2 pivot, int order, float width = 0)
        {
            var node = Node(name, parent, anchor);
            var rect = Parts[index];
            var sprite = Sprite.Create(atlas, rect, pivot, rect.height / height, 0, SpriteMeshType.FullRect);
            generatedSprites.Add(sprite);
            sprite.name = "Painted " + name;
            var renderer = node.gameObject.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite; renderer.sharedMaterial = material; renderer.sortingOrder = order;
            TextureBindings(renderer,atlas);
            if (width > 0) node.localScale = new Vector3(width / (height * rect.width / rect.height), 1, 1);
            return node;
        }

        Mesh Cloth(string name, int index, Transform root, float width, float height, int order, out Vector3[] rest)
        {
            const int columns = 6, rows = 10;
            var obj = new GameObject(name); obj.transform.SetParent(root, false);
            var mesh = new Mesh { name = name + " · flowing linen grid" };
            var vertices = new Vector3[(columns + 1) * (rows + 1)];
            var uv = new Vector2[vertices.Length]; var colours = new Color[vertices.Length];
            var normals = new Vector3[vertices.Length]; var tangents = new Vector4[vertices.Length];
            var triangles = new int[columns * rows * 6]; var rect = Parts[index];
            for (int y = 0; y <= rows; y++) for (int x = 0; x <= columns; x++)
            {
                int k = y * (columns + 1) + x; float u = x / (float)columns, v = y / (float)rows;
                vertices[k] = new Vector3((u - .5f) * width, -v * height, 0);
                uv[k] = new Vector2((rect.x + u * rect.width) / atlas.width, (rect.yMax - v * rect.height) / atlas.height);
                colours[k] = Color.white; normals[k] = Vector3.back; tangents[k] = new Vector4(1, 0, 0, -1);
            }
            int t = 0;
            for (int y = 0; y < rows; y++) for (int x = 0; x < columns; x++)
            {
                int k = y * (columns + 1) + x;
                triangles[t++] = k; triangles[t++] = k + 1; triangles[t++] = k + columns + 1;
                triangles[t++] = k + 1; triangles[t++] = k + columns + 2; triangles[t++] = k + columns + 1;
            }
            mesh.vertices = vertices; mesh.uv = uv; mesh.colors = colours; mesh.normals = normals; mesh.tangents = tangents;
            mesh.triangles = triangles; mesh.RecalculateBounds(); mesh.MarkDynamic();
            obj.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = obj.AddComponent<MeshRenderer>(); renderer.sharedMaterial = clothMaterial; renderer.sortingOrder = order;
            TextureBindings(renderer,atlas);
            rest = vertices; return mesh;
        }

        void Create()
        {
            atlas = Resources.Load<Texture2D>("Art/GospelLuminous/Actors/jesus-parts");
            if (!atlas) { Debug.LogError("Missing original Luminous character atlas."); return; }
            var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (!shader) shader = Shader.Find("Sprites/Default");
            material = new Material(shader) { name = "Luminous painted character", mainTexture = atlas };
            TextureBindings(material,atlas);
            var meshShader = Resources.Load<Shader>("Art/GospelLuminous/Actors/LuminousCloth2D");
            if (!meshShader) meshShader = Shader.Find("Universal Render Pipeline/2D/Mesh2D-Lit-Default");
            if (!meshShader) meshShader = Shader.Find("Universal Render Pipeline/2D/Mesh2D-Unlit-Default");
            if (!meshShader) meshShader = Shader.Find("Sprites/Default");
            clothMaterial = new Material(meshShader) { name = "Luminous mesh-safe flowing linen" };
            TextureBindings(clothMaterial,atlas);
            visual = Node("Joint rig", transform, Vector2.zero);
            farThigh = Node("Far hip", visual, new Vector2(-.035f, RestHip));
            SpritePart("Far upper leg",10,farThigh,Vector2.zero,.435f,new Vector2(.5f,.94f),16,.15f);
            farCalf = Node("Far knee",farThigh,new Vector2(0,-UpperLeg));
            SpritePart("Far shin",18,farCalf,Vector2.zero,.405f,new Vector2(.52f,.96f),17,.135f);
            farFoot = Node("Far ankle",farCalf,new Vector2(0,-LowerLeg));
            SpritePart("Far leather sandal",19,farFoot,Vector2.zero,.144f,new Vector2(.30f,.71f),17,.255f);
            nearThigh = Node("Near hip", visual,new Vector2(.035f,RestHip));
            SpritePart("Near upper leg",8,nearThigh,Vector2.zero,.435f,new Vector2(.5f,.94f),18,.15f);
            nearCalf = Node("Near knee",nearThigh,new Vector2(0,-UpperLeg));
            SpritePart("Near shin",16,nearCalf,Vector2.zero,.405f,new Vector2(.52f,.96f),19,.145f);
            nearFoot = Node("Near ankle",nearCalf,new Vector2(0,-LowerLeg));
            SpritePart("Near leather sandal",17,nearFoot,Vector2.zero,.144f,new Vector2(.30f,.71f),20,.255f);
            torso = Node("Chest",visual,new Vector2(0,.88f));
            farArm = Node("Far shoulder",torso,new Vector2(-.15f,.43f));
            SpritePart("Far sleeve",6,farArm,Vector2.zero,.34f,new Vector2(.5f,.90f),18,.15f);
            farForearm = Node("Far elbow",farArm,new Vector2(0,-.24f));
            SpritePart("Far forearm and hand",7,farForearm,Vector2.zero,.33f,new Vector2(.42f,.94f),18,.14f);
            skirtRoot = Node("Waist linen anchor",torso,new Vector2(0,.09f));
            skirtMesh = Cloth("Deforming ivory linen",2,skirtRoot,.56f,.74f,21,out skirtRest);
            skirtVertices = new Vector3[skirtRest.Length];
            SpritePart("Ivory upper tunic",1,torso,new Vector2(0,.03f),.58f,new Vector2(.5f,.06f),22,.49f);
            sashRoot = Node("Sash anchor",torso,new Vector2(-.16f,.38f));
            sashMesh = Cloth("Trailing blue sash",12,sashRoot,.18f,.69f,20,out sashRest);
            sashVertices = new Vector3[sashRest.Length];
            pouch = SpritePart("Belt pouch",15,torso,new Vector2(.16f,.02f),.19f,new Vector2(.5f,.85f),25,.14f);
            nearArm = Node("Near shoulder",torso,new Vector2(.16f,.43f));
            SpritePart("Near sleeve",4,nearArm,Vector2.zero,.34f,new Vector2(.5f,.90f),24,.15f);
            nearForearm = Node("Near elbow",nearArm,new Vector2(0,-.24f));
            SpritePart("Near forearm and hand",5,nearForearm,Vector2.zero,.33f,new Vector2(.42f,.94f),25,.14f);
            head = Node("Neck",torso,new Vector2(.015f,.56f));
            hair = SpritePart("Trailing hair",3,head,new Vector2(-.10f,.22f),.29f,new Vector2(.5f,.94f),23,.14f);
            SpritePart("Face and wavy hair",0,head,Vector2.zero,.40f,new Vector2(.48f,.10f),26,.40f);
            var hand = Node("Right hand lamp socket",nearForearm,new Vector2(.035f,-.285f));
            lamp = new GameObject("Held clay oil lamp"); lamp.transform.SetParent(hand,false);
            SpritePart("Clay lamp and flame",13,lamp.transform,new Vector2(.02f,.01f),.14f,new Vector2(.2f,.2f),29,.22f);
            var lightPrefab = Resources.Load<GameObject>("Art/GospelLuminous/Rendering/NormalPointLight");
            if (!lightPrefab) throw new System.InvalidOperationException("Missing authored SoftGlow lamp light.");
            var lightObject = Instantiate(lightPrefab,lamp.transform,false);
            lightObject.name = "Warm oil flame · SoftGlow Sprite Light2D";
            lightObject.transform.localPosition = new Vector3(.17f,.08f,0);
            lightObject.transform.localScale = new Vector3(1.8f,1.8f,1);
            lampLight = lightObject.GetComponent<Light2D>();
            if (!lampLight || lampLight.lightType != Light2D.LightType.Sprite || !lampLight.lightCookieSprite || lampLight.normalMapQuality != Light2D.NormalMapQuality.Accurate)
                throw new System.InvalidOperationException("Lamp light must retain its authored SoftGlow sprite cookie and accurate normals.");
            lampLight.color = new Color(1,.64f,.20f); lampLight.intensity = .7f;
            lampLight.shadowsEnabled = false; lampLight.shadowIntensity = 0;
            lamp.SetActive(false);
        }

        void LateUpdate()
        {
            if (!visual) return;
            float dt = Mathf.Max(0, Time.deltaTime); time += dt;
            float speed = Mathf.Abs(velocity.x);
            float ease = 1f - Mathf.Exp(-12f * dt);
            speedBlend = Mathf.Lerp(speedBlend, Mathf.Clamp01(speed / 1.65f), ease);
            runBlend = Mathf.Lerp(runBlend, Mathf.InverseLerp(2.4f, 4.1f, speed), ease);
            airBlend = Mathf.Lerp(airBlend, grounded ? 0f : 1f, 1f - Mathf.Exp(-15f * dt));
            bool running = speed > 2.8f;
            CurrentState = !grounded ? (velocity.y > .5f ? "Jump" : "Fall") : speed > .15f ? (running ? "Run" : "Walk") : "Idle";

            // Advance by actual travelled distance. The stance foot travels backwards by
            // that same distance, unlike a time-driven leg pendulum which skates over the path.
            float span = Mathf.Lerp(.36f, .46f, runBlend);
            float stance = Mathf.Lerp(.58f, .48f, runBlend);
            float cycleDistance = span * 2f / stance;
            if (grounded) phase = Mathf.Repeat(phase + speed * dt / cycleDistance * Mathf.PI * 2f, Mathf.PI * 2f);
            float cycle = phase / (Mathf.PI * 2f);
            var near = Step(cycle, span, stance, out float nearPitch);
            var far = Step(Mathf.Repeat(cycle + .5f, 1f), span, stance, out float farPitch);
            near = Vector2.Lerp(new Vector2(.035f, AnkleHeight), near, speedBlend);
            far = Vector2.Lerp(new Vector2(-.035f, AnkleHeight), far, speedBlend);
            near.y += near.x * groundSlope * facing;
            far.y += far.x * groundSlope * facing;

            // A low, reachable pelvis at heel contact keeps knees anatomical and soles
            // on the actual ground plane. The torso follows it instead of stretching a leg.
            float hip = Mathf.Min(RestHip, Mathf.Min(ReachableHip(near, .035f), ReachableHip(far, -.035f)));
            hip = Mathf.Lerp(hip, RestHip - .035f, airBlend);
            pelvisOffset = hip - RestHip;
            near = Vector2.Lerp(near, new Vector2(.20f, .24f + Mathf.Clamp01(velocity.y / 8f) * .09f), airBlend);
            far = Vector2.Lerp(far, new Vector2(-.18f, .16f), airBlend);
            nearThigh.localPosition = new Vector3(.035f, RestHip + pelvisOffset, 0);
            farThigh.localPosition = new Vector3(-.035f, RestHip + pelvisOffset, 0);
            SolveLeg(nearThigh, nearCalf, nearFoot, near, Mathf.Lerp(nearPitch * speedBlend, 12, airBlend));
            SolveLeg(farThigh, farCalf, farFoot, far, Mathf.Lerp(farPitch * speedBlend, -10, airBlend));

            float stride = Mathf.Sin(phase), breathe = Mathf.Sin(time * 1.65f);
            visual.localScale = new Vector3(facing, 1, 1);
            visual.localPosition = Vector3.zero;
            torso.localPosition = new Vector3(0, .88f + pelvisOffset + breathe * .0035f, 0);
            float lean = Mathf.Lerp(-2.3f, -7.2f, runBlend) * speedBlend;
            torsoAngle = Mathf.Lerp(torsoAngle, Mathf.Lerp(lean, -Mathf.Clamp(velocity.y, -7, 7) * .45f, airBlend), ease);
            torso.localRotation = Quaternion.Euler(0, 0, torsoAngle);
            torso.localScale = new Vector3(1 + breathe * .002f, 1 + breathe * .002f, 1);
            // Counter-rotation stabilises the gaze; hair trails softly instead of whipping.
            SmoothAngle(head, -torsoAngle * .65f + breathe * .45f - stride * speedBlend * .45f, 12, dt);
            SmoothAngle(hair, Mathf.Sin(phase - .8f) * speedBlend * 2.8f + Mathf.Sin(time * 1.3f) * .8f, 8, dt);
            float armSwing = Mathf.Lerp(14f, 29f, runBlend) * speedBlend;
            SmoothAngle(nearArm, Mathf.Lerp(-stride * armSwing - 2f, -19f, airBlend), 14, dt);
            SmoothAngle(farArm, Mathf.Lerp(stride * armSwing + 2f, 17f, airBlend), 14, dt);
            float elbow = Mathf.Lerp(7f, 43f, runBlend) * speedBlend + 5f;
            SmoothAngle(nearForearm, LampEquipped ? 31f + stride * speedBlend * 3f : elbow + Mathf.Max(0, stride) * 5f * speedBlend, 14, dt);
            SmoothAngle(farForearm, elbow + Mathf.Max(0, -stride) * 5f * speedBlend, 14, dt);
            Deform(skirtMesh, skirtRest, skirtVertices, .74f, .008f, .028f, false);
            Deform(sashMesh, sashRest, sashVertices, .69f, .016f, .046f, true);
            if (lampLight) lampLight.intensity = .72f + Mathf.Sin(time * 13) * .045f + Mathf.Sin(time * 19.2f) * .025f;
        }

        Vector2 Step(float cycle, float span, float stance, out float footPitch)
        {
            if (cycle < stance)
            {
                float t = cycle / stance;
                // Mostly flat support, small heel roll only at the two ends.
                footPitch = Mathf.Lerp(7f, 0f, Mathf.Clamp01(t / .16f)) - Mathf.SmoothStep(0, 10f, (t - .8f) / .2f);
                return new Vector2(Mathf.Lerp(span, -span, t), AnkleHeight);
            }
            float swing = (cycle - stance) / (1f - stance);
            float travel = swing * swing * (3f - 2f * swing);
            float lift = Mathf.Sin(swing * Mathf.PI) * Mathf.Lerp(.105f, .205f, runBlend);
            footPitch = Mathf.Lerp(-10f, 7f, travel);
            return new Vector2(Mathf.Lerp(-span, span, travel), AnkleHeight + lift);
        }

        static float ReachableHip(Vector2 ankle, float hipX)
        {
            float dx = ankle.x - hipX, reach = (UpperLeg + LowerLeg) * .985f;
            return ankle.y + Mathf.Sqrt(Mathf.Max(.01f, reach * reach - dx * dx));
        }

        static void SolveLeg(Transform thigh, Transform calf, Transform foot, Vector2 ankle, float footPitch)
        {
            Vector2 target = ankle - (Vector2)thigh.localPosition;
            float distance = Mathf.Clamp(target.magnitude, .08f, UpperLeg + LowerLeg - .002f);
            float hipOffset = Mathf.Acos(Mathf.Clamp((UpperLeg * UpperLeg + distance * distance - LowerLeg * LowerLeg) / (2f * UpperLeg * distance), -1f, 1f)) * Mathf.Rad2Deg;
            float thighAngle = Mathf.Atan2(target.x, -target.y) * Mathf.Rad2Deg + hipOffset;
            float calfAngle = -Mathf.Acos(Mathf.Clamp((distance * distance - UpperLeg * UpperLeg - LowerLeg * LowerLeg) / (2f * UpperLeg * LowerLeg), -1f, 1f)) * Mathf.Rad2Deg;
            thigh.localRotation = Quaternion.Euler(0, 0, thighAngle);
            calf.localRotation = Quaternion.Euler(0, 0, calfAngle);
            // Sandals retain ground orientation independently from the shin angle.
            foot.localRotation = Quaternion.Euler(0, 0, footPitch - thighAngle - calfAngle);
        }

        static void SmoothAngle(Transform bone, float degrees, float rate, float dt)
        {
            bone.localRotation = Quaternion.Slerp(bone.localRotation, Quaternion.Euler(0, 0, degrees), 1f - Mathf.Exp(-rate * dt));
        }

        void Deform(Mesh mesh, Vector3[] rest, Vector3[] output, float height, float idle, float moving, bool trailing)
        {
            for (int i = 0; i < rest.Length; i++)
            {
                var p = rest[i]; float down = -p.y / height;
                // Top rows stay sewn to the waist/shoulder. Cloth follows the moving
                // legs subtly; asymmetric trailing cloth responds to the actual speed.
                float hem = down * down;
                float drift = trailing ? -Mathf.Abs(velocity.x) * .008f : 0;
                p.x += (Mathf.Sin(time * 1.8f - down * 2.6f) * idle + Mathf.Sin(phase - down * 1.5f) * speedBlend * moving + drift) * hem;
                p.y += Mathf.Cos(phase * 2f - down) * speedBlend * .006f * hem;
                output[i] = p;
            }
            mesh.vertices = output; mesh.RecalculateBounds();
        }

        void OnDestroy()
        {
            foreach (var sprite in generatedSprites) if (sprite) Destroy(sprite);
            if (skirtMesh) Destroy(skirtMesh);
            if (sashMesh) Destroy(sashMesh);
            if (material) Destroy(material);
            if (clothMaterial) Destroy(clothMaterial);
        }
    }
}
