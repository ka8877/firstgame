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
            new Rect(56,17,206,339), new Rect(396,132,197,143), new Rect(698,113,209,132), new Rect(1035,76,153,201)
        };
        Transform visual, torso, head, hair, nearArm, farArm, nearForearm, farForearm;
        Transform nearThigh, farThigh, nearCalf, farCalf, pouch;
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
            farThigh = Node("Far hip", visual, new Vector2(-.075f,.64f));
            SpritePart("Far upper leg",10,farThigh,Vector2.zero,.35f,new Vector2(.5f,.94f),16,.16f);
            farCalf = Node("Far knee",farThigh,new Vector2(0,-.27f));
            SpritePart("Far calf and sandal",11,farCalf,Vector2.zero,.40f,new Vector2(.40f,.93f),17,.25f);
            nearThigh = Node("Near hip", visual,new Vector2(.07f,.64f));
            SpritePart("Near upper leg",8,nearThigh,Vector2.zero,.35f,new Vector2(.5f,.94f),18,.16f);
            nearCalf = Node("Near knee",nearThigh,new Vector2(0,-.27f));
            SpritePart("Near calf and sandal",9,nearCalf,Vector2.zero,.40f,new Vector2(.40f,.93f),19,.25f);
            torso = Node("Chest",visual,new Vector2(0,.88f));
            farArm = Node("Far shoulder",torso,new Vector2(-.15f,.43f));
            SpritePart("Far sleeve",6,farArm,Vector2.zero,.34f,new Vector2(.5f,.90f),18,.15f);
            farForearm = Node("Far elbow",farArm,new Vector2(0,-.24f));
            SpritePart("Far forearm and hand",7,farForearm,Vector2.zero,.33f,new Vector2(.42f,.94f),18,.14f);
            skirtRoot = Node("Waist linen anchor",torso,new Vector2(0,.09f));
            skirtMesh = Cloth("Deforming ivory linen",2,skirtRoot,.63f,.77f,21,out skirtRest);
            skirtVertices = new Vector3[skirtRest.Length];
            SpritePart("Ivory upper tunic",1,torso,new Vector2(0,.03f),.58f,new Vector2(.5f,.06f),22,.53f);
            sashRoot = Node("Sash anchor",torso,new Vector2(-.18f,.38f));
            sashMesh = Cloth("Trailing blue sash",12,sashRoot,.20f,.69f,20,out sashRest);
            sashVertices = new Vector3[sashRest.Length];
            pouch = SpritePart("Belt pouch",15,torso,new Vector2(.16f,.02f),.19f,new Vector2(.5f,.85f),25,.14f);
            nearArm = Node("Near shoulder",torso,new Vector2(.16f,.43f));
            SpritePart("Near sleeve",4,nearArm,Vector2.zero,.34f,new Vector2(.5f,.90f),24,.15f);
            nearForearm = Node("Near elbow",nearArm,new Vector2(0,-.24f));
            SpritePart("Near forearm and hand",5,nearForearm,Vector2.zero,.33f,new Vector2(.42f,.94f),25,.14f);
            head = Node("Neck",torso,new Vector2(.015f,.56f));
            hair = SpritePart("Trailing hair",3,head,new Vector2(-.12f,.24f),.34f,new Vector2(.5f,.94f),23,.17f);
            SpritePart("Face and wavy hair",0,head,Vector2.zero,.43f,new Vector2(.48f,.10f),26,.45f);
            var hand = Node("Right hand lamp socket",nearForearm,new Vector2(.035f,-.285f));
            lamp = new GameObject("Held clay oil lamp"); lamp.transform.SetParent(hand,false);
            SpritePart("Clay lamp and flame",13,lamp.transform,new Vector2(.02f,.01f),.14f,new Vector2(.2f,.2f),29,.22f);
            var lightObject = new GameObject("Warm oil flame Light2D"); lightObject.transform.SetParent(lamp.transform,false);
            lightObject.transform.localPosition = new Vector3(.17f,.08f,0);
            lampLight = lightObject.AddComponent<Light2D>(); lampLight.lightType = Light2D.LightType.Point;
            lampLight.color = new Color(1,.64f,.20f); lampLight.intensity = .7f;
            lampLight.pointLightInnerRadius = .12f; lampLight.pointLightOuterRadius = 1.8f;
            lamp.SetActive(false);
        }

        void LateUpdate()
        {
            if (!visual) return;
            float dt = Time.deltaTime; time += dt;
            float speed = Mathf.Abs(velocity.x); speedBlend = Mathf.MoveTowards(speedBlend,Mathf.Clamp01(speed / 3.1f),dt * 6);
            bool running = speed > 4.0f;
            CurrentState = !grounded ? (velocity.y > .5f ? "Jump" : "Fall") : speed > .15f ? (running ? "Run" : "Walk") : "Idle";
            phase += speed * dt * (running ? 3.1f : 3.8f);
            float stride = Mathf.Sin(phase), opposite = Mathf.Sin(phase + Mathf.PI);
            float breathe = Mathf.Sin(time * 1.65f);
            visual.localScale = new Vector3(facing,1,1);
            float bob = grounded ? Mathf.Abs(Mathf.Sin(phase * 2)) * (running ? .045f : .021f) * speedBlend : .018f;
            visual.localPosition = new Vector3(0,bob + breathe * .006f,0);
            torso.localRotation = Quaternion.Euler(0,0,!grounded ? -Mathf.Clamp(velocity.y,-7,7) * .5f : -(running ? 5 : 1.8f) * speedBlend);
            torso.localScale = new Vector3(1 + breathe * .0035f,1 + breathe * .004f,1);
            head.localRotation = Quaternion.Euler(0,0,breathe * .9f - stride * speedBlend * .7f);
            hair.localRotation = Quaternion.Euler(0,0,Mathf.Sin(phase - .7f) * speedBlend * 7 + Mathf.Sin(time * 1.3f) * 2);
            float swing = running ? 40 : 24;
            nearThigh.localRotation = Quaternion.Euler(0,0,grounded ? stride * swing * speedBlend : 30);
            farThigh.localRotation = Quaternion.Euler(0,0,grounded ? opposite * swing * speedBlend : -24);
            nearCalf.localRotation = Quaternion.Euler(0,0,grounded ? -Mathf.Max(0,-stride) * (running ? 58 : 28) * speedBlend : -55);
            farCalf.localRotation = Quaternion.Euler(0,0,grounded ? -Mathf.Max(0,-opposite) * (running ? 58 : 28) * speedBlend : -30);
            nearArm.localRotation = Quaternion.Euler(0,0,grounded ? opposite * (running ? 35 : 20) * speedBlend - 3 : -35);
            farArm.localRotation = Quaternion.Euler(0,0,grounded ? stride * (running ? 35 : 20) * speedBlend + 3 : 25);
            nearForearm.localRotation = Quaternion.Euler(0,0,12 + (running ? 32 : 8) * speedBlend + Mathf.Max(0,stride) * 8 * speedBlend);
            farForearm.localRotation = Quaternion.Euler(0,0,10 + (running ? 28 : 7) * speedBlend + Mathf.Max(0,opposite) * 8 * speedBlend);
            if (LampEquipped) nearForearm.localRotation = Quaternion.Euler(0,0,20 + opposite * speedBlend * 8);
            Deform(skirtMesh,skirtRest,skirtVertices,.77f,.024f,.045f);
            Deform(sashMesh,sashRest,sashVertices,.69f,.07f,.10f);
            if (lampLight) lampLight.intensity = .72f + Mathf.Sin(time * 13) * .045f + Mathf.Sin(time * 19.2f) * .025f;
        }

        void Deform(Mesh mesh, Vector3[] rest, Vector3[] output, float height, float idle, float moving)
        {
            for (int i=0;i<rest.Length;i++)
            {
                var p = rest[i]; float down = -p.y / height;
                p.x += (Mathf.Sin(time * 2.1f - down * 2.6f) * idle + Mathf.Sin(phase - down * 2) * speedBlend * moving) * down * down;
                p.y += Mathf.Cos(phase - down) * speedBlend * .013f * down;
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
