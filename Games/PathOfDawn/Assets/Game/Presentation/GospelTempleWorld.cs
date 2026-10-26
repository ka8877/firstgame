using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace PathOfDawn.Presentation
{
    /// <summary>A physical courtyard assembled from actual meshes, with continuous depth and collision.</summary>
    public sealed class GospelTempleWorld : MonoBehaviour
    {
        public const string HeroResource = "Art/Gospel3D/YoungJesus";
        public const string AnimatorResource = "Art/Gospel3D/YouthLocomotion";
        public GospelThirdPersonMotor Player { get; private set; }
        public Camera View { get; private set; }
        public IReadOnlyDictionary<string,Vector3> Landmarks => landmarks;
        readonly Dictionary<string,Vector3> landmarks = new Dictionary<string,Vector3> {
            {"temple-gate",new Vector3(0,0,-5.5f)}, {"teachers",new Vector3(-5,0,2)},
            {"parents",new Vector3(5,0,3)}, {"nazareth-gate",new Vector3(0,0,-9)}
        };
        readonly List<Material> ownedMaterials = new List<Material>();
        readonly List<Mesh> ownedMeshes = new List<Mesh>();
        readonly List<Transform> oliveCrowns = new List<Transform>();
        readonly List<Quaternion> oliveRest = new List<Quaternion>();
        VolumeProfile ownedProfile;
        Transform courtyard, manger, marker;
        Light sun;
        Material stone, paleStone, warmStone, darkStone, gold, wood, leaf, foliageLight, water, linen, pottery, ground, paving, pavingWarm;
        System.Random random = new System.Random(41252);
        string currentLandmark;
        public Vector3 LandmarkPosition(string id) => landmarks.TryGetValue(id,out var p) ? p : Vector3.zero;
        public void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader is required.");
            stone=Mat("Warm Jerusalem limestone",new Color(.73f,.62f,.45f)); paleStone=Mat("Chalk stone",new Color(.91f,.82f,.64f));
            warmStone=Mat("Warm stone variation",new Color(.82f,.68f,.47f)); darkStone=Mat("Weathered recess",new Color(.40f,.35f,.27f));
            gold=Mat("Bronze detailing",new Color(.70f,.47f,.18f),.65f,.45f);wood=Mat("Cedar",new Color(.29f,.17f,.085f));
            leaf=Mat("Olive leaves",new Color(.29f,.40f,.23f));foliageLight=Mat("Olive sunlit leaves",new Color(.43f,.51f,.27f));
            water=Mat("Pool water",new Color(.18f,.44f,.46f),.15f,.86f);linen=Mat("Linen canopy",new Color(.90f,.79f,.56f));
            pottery=Mat("Terracotta",new Color(.61f,.30f,.16f));ground=Mat("Distant earth",new Color(.52f,.46f,.31f));
            paving=Mat("Limestone courtyard paving",Color.white);pavingWarm=Mat("Weathered courtyard paving",new Color(.94f,.91f,.84f));
            ApplyStone(stone,"limestone",new Color(.93f,.86f,.72f));ApplyStone(paleStone,"limestone",new Color(1,.96f,.85f));
            ApplyStone(warmStone,"limestone",new Color(.94f,.82f,.64f));ApplyStone(darkStone,"limestone",new Color(.52f,.48f,.40f));
            ApplyStone(paving,"paving",Color.white);ApplyStone(pavingWarm,"paving",new Color(.94f,.91f,.84f));
            ConfigureLighting();
            var cam = new GameObject("Gospel camera",typeof(Camera),typeof(AudioListener)); View=cam.GetComponent<Camera>();
            View.tag="MainCamera";View.fieldOfView=52;View.nearClipPlane=.13f;View.farClipPlane=180;View.allowHDR=true;
            View.backgroundColor=new Color(.65f,.76f,.80f);View.clearFlags=CameraClearFlags.Skybox;
            var additional=cam.AddComponent<UniversalAdditionalCameraData>();additional.renderPostProcessing=true;additional.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            // A gentle, shadowless portrait fill makes the main face readable in
            // backlight. It follows the view and affects only the protagonist layer.
            var fillObject=new GameObject("Protagonist portrait fill",typeof(Light));
            fillObject.transform.SetParent(cam.transform,false);fillObject.transform.localPosition=new Vector3(0,-.20f,.65f);
            var fill=fillObject.GetComponent<Light>();fill.type=LightType.Point;fill.color=new Color(1,.95f,.87f);
            fill.intensity=2.8f;fill.range=8;fill.shadows=LightShadows.None;fill.cullingMask=1<<8;
            courtyard=new GameObject("Temple courtyard · physical scene").transform;courtyard.SetParent(transform,false);
            BuildLandscape();BuildCourtyard();BuildTemple();BuildColonnades();BuildGarden();CombineArchitecture();BuildManger();
            var playerRoot=new GameObject("Jesus · playable protagonist");playerRoot.transform.SetParent(courtyard,false);playerRoot.layer=8;
            var body=CreatePerson(playerRoot.transform,"Young Jesus",Vector3.zero,1);
            foreach(var node in body.GetComponentsInChildren<Transform>(true))node.gameObject.layer=8;
            var animator=body.GetComponentInChildren<Animator>();
            if(animator==null)animator=body.AddComponent<Animator>();
            animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>(AnimatorResource);animator.applyRootMotion=false;
            if(animator.runtimeAnimatorController==null)throw new InvalidOperationException("Missing youth locomotion AnimatorController.");
            playerRoot.AddComponent<CharacterController>();Player=playerRoot.AddComponent<GospelThirdPersonMotor>();Player.Initialize(View,animator);
            Player.SetPose(new Vector3(0,.08f,-9),0);
            Player.Actions.ConfigureTrainingArea(new Vector3(-8,0,-8),3);
            Torus("Optional practice area · outside Scripture progression",courtyard,new Vector3(-8,.06f,-8),2.8f,.025f,gold,64);
            CreateNpc("Teacher reading",new Vector3(-5.8f,0,2.4f),120,1.15f,new Color(.58f,.48f,.32f));
            CreateNpc("Teacher listening",new Vector3(-4.4f,0,2.7f),210,1.1f,new Color(.65f,.57f,.42f));
            CreateNpc("Mary",new Vector3(4.6f,0,3.2f),210,1.06f,new Color(.35f,.48f,.55f));
            CreateNpc("Joseph",new Vector3(5.6f,0,3.4f),225,1.17f,new Color(.58f,.39f,.26f));
            marker=new GameObject("Current Scripture landmark").transform;marker.SetParent(courtyard,false);
            var gem=Primitive("Waypoint light",PrimitiveType.Sphere,marker,new Vector3(0,1.9f,0),new Vector3(.14f,.25f,.14f),gold,false);
            gem.transform.rotation=Quaternion.Euler(0,0,25);
            Torus("Waypoint floor ring",marker,Vector3.up*.045f,.58f,.035f,gold,48);
            SetOverview(false);SetCurrentLandmark("temple-gate");
        }
        Material Mat(string name,Color color,float metallic=0,float smooth=.24f)
        {
            var mat=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name,enableInstancing=true};
            mat.SetColor("_BaseColor",color);mat.SetFloat("_Metallic",metallic);mat.SetFloat("_Smoothness",smooth);ownedMaterials.Add(mat);return mat;
        }
        void ApplyStone(Material material,string texture,Color tint)
        {
            string folder="Art/Gospel3D/TempleMaterials/"+texture;
            var albedo=Resources.Load<Texture2D>(folder+"-albedo");var normal=Resources.Load<Texture2D>(folder+"-normal");
            if(albedo==null||normal==null)throw new InvalidOperationException("Missing courtyard PBR texture maps: "+texture);
            material.SetColor("_BaseColor",tint);material.SetTexture("_BaseMap",albedo);material.SetTexture("_BumpMap",normal);
            material.EnableKeyword("_NORMALMAP");material.SetFloat("_BumpScale",.32f);material.SetFloat("_Smoothness",.12f);
            var ao=Resources.Load<Texture2D>(folder+"-ao");if(ao!=null){material.SetTexture("_OcclusionMap",ao);material.SetFloat("_OcclusionStrength",.5f);material.EnableKeyword("_OCCLUSIONMAP");}
        }
        Mesh WorldUvMesh(Mesh source,Transform objectTransform)
        {
            var mesh=Instantiate(source);mesh.name=source.name+" · world stone UV";var vertices=source.vertices;var normals=source.normals;var uv=new Vector2[vertices.Length];
            for(int i=0;i<vertices.Length;i++)
            {
                Vector3 p=objectTransform.TransformPoint(vertices[i]);Vector3 n=objectTransform.TransformDirection(normals[i]);
                uv[i]=(Mathf.Abs(n.y)>Mathf.Abs(n.x)&&Mathf.Abs(n.y)>Mathf.Abs(n.z)?new Vector2(p.x,p.z):Mathf.Abs(n.x)>Mathf.Abs(n.z)?new Vector2(p.z,p.y):new Vector2(p.x,p.y))*.52f;
            }
            mesh.uv=uv;mesh.RecalculateTangents();ownedMeshes.Add(mesh);return mesh;
        }
        void ConfigureLighting()
        {
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.67f,.74f,.83f);
            RenderSettings.ambientEquatorColor=new Color(.57f,.53f,.43f);RenderSettings.ambientGroundColor=new Color(.25f,.25f,.23f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.72f,.73f,.65f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0068f;
            var lightObject=new GameObject("Afternoon sun",typeof(Light));lightObject.transform.SetParent(transform,false);
            sun=lightObject.GetComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.87f,.65f);sun.intensity=2.0f;sun.shadows=LightShadows.Soft;
            sun.shadowBias=.045f;sun.shadowNormalBias=.28f;lightObject.transform.rotation=Quaternion.Euler(38,-32,0);RenderSettings.sun=sun;
            var skyShader=Shader.Find("Skybox/Procedural");
            if(skyShader!=null){var sky=new Material(skyShader);sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",.75f);sky.SetColor("_SkyTint",new Color(.46f,.57f,.66f));sky.SetColor("_GroundColor",new Color(.58f,.51f,.38f));RenderSettings.skybox=sky;ownedMaterials.Add(sky);}
            var volumeObject=new GameObject("Warm daylight grade",typeof(Volume));volumeObject.transform.SetParent(transform,false);
            var volume=volumeObject.GetComponent<Volume>();volume.isGlobal=true;var profile=ScriptableObject.CreateInstance<VolumeProfile>();ownedProfile=profile;volume.profile=profile;
            var tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            var adjustment=profile.Add<ColorAdjustments>();adjustment.postExposure.Override(.05f);adjustment.contrast.Override(7);adjustment.saturation.Override(-4);
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(.12f);bloom.threshold.Override(1.1f);
        }
        void BuildLandscape()
        {
            Primitive("Ground foundation",PrimitiveType.Cube,courtyard,new Vector3(0,-.31f,0),new Vector3(130,.5f,130),ground,true);
            for(int i=0;i<28;i++)
            {
                float angle=i*Mathf.PI*2/28;float radius=48+(float)random.NextDouble()*22;
                var p=new Vector3(Mathf.Sin(angle)*radius,-3,Mathf.Cos(angle)*radius);
                Primitive("Distant Judean ridge",PrimitiveType.Sphere,courtyard,p,new Vector3(28+(float)random.NextDouble()*15,10+(float)random.NextDouble()*9,22),ground,false);
            }
            for(int i=0;i<35;i++)
            {
                float x=-38+(i%9)*8.8f,z=28+(i/9)*7;float h=2+(float)random.NextDouble()*4;
                Primitive("Old city house",PrimitiveType.Cube,courtyard,new Vector3(x,h*.5f,z),new Vector3(5.5f,h,5),i%3==0?warmStone:stone,false);
                if(i%3==0)Primitive("Old city dome",PrimitiveType.Sphere,courtyard,new Vector3(x,h,z),new Vector3(4,2.5f,4),paleStone,false);
            }
        }
        void BuildCourtyard()
        {
            Primitive("Courtyard slab",PrimitiveType.Cube,courtyard,new Vector3(0,-.12f,0),new Vector3(30,.24f,30),stone,true);
            for(int x=-7;x<=7;x++)for(int z=-7;z<=7;z++)
            {
                var mat=(x+z)%5==0?pavingWarm:paving;
                Primitive("Limestone paving",PrimitiveType.Cube,courtyard,new Vector3(x*1.95f,.008f,z*1.95f),new Vector3(1.90f,.028f,1.90f),mat,false);
            }
            for(int i=-7;i<=7;i++)foreach(int edge in new[]{-1,1})
            {
                Primitive("Inlaid border",PrimitiveType.Cube,courtyard,new Vector3(i*1.95f,.028f,edge*11.1f),new Vector3(1.8f,.02f,.13f),darkStone,false);
                Primitive("Inlaid border",PrimitiveType.Cube,courtyard,new Vector3(edge*11.1f,.028f,i*1.95f),new Vector3(.13f,.02f,1.8f),darkStone,false);
            }
            // The surrounding wall keeps the first teaching area coherent and physically bounded.
            foreach(int side in new[]{-1,1})
                Primitive("Courtyard outer wall",PrimitiveType.Cube,courtyard,new Vector3(side*15,1.2f,0),new Vector3(.7f,2.4f,30),stone,true);
            Primitive("South wall left",PrimitiveType.Cube,courtyard,new Vector3(-9,1.2f,-15),new Vector3(12,2.4f,.7f),stone,true);
            Primitive("South wall right",PrimitiveType.Cube,courtyard,new Vector3(9,1.2f,-15),new Vector3(12,2.4f,.7f),stone,true);
            Primitive("Cedar courtyard gate",PrimitiveType.Cube,courtyard,new Vector3(0,1.3f,-15),new Vector3(5.9f,2.6f,.35f),wood,true);
            foreach(float gateX in new[]{-1.5f,1.5f})foreach(float y in new[]{.55f,1.85f})
                Primitive("Gate bronze hinge",PrimitiveType.Cube,courtyard,new Vector3(gateX,y,-14.79f),new Vector3(2.5f,.12f,.06f),gold,false);
            Primitive("North perimeter wall",PrimitiveType.Cube,courtyard,new Vector3(0,1.25f,16.9f),new Vector3(30,2.5f,.5f),stone,true);
            Fountain();
        }
        void BuildTemple()
        {
            for(int step=0;step<5;step++)Primitive("Temple stair",PrimitiveType.Cube,courtyard,new Vector3(0,.12f+step*.20f,9.2f+step*.45f),new Vector3(19-step*.35f,.24f,3.3f-step*.45f),paleStone,true);
            Primitive("Sanctuary body",PrimitiveType.Cube,courtyard,new Vector3(0,5.0f,14),new Vector3(18,8,6),paleStone,true);
            Primitive("Dark sanctuary doorway",PrimitiveType.Cube,courtyard,new Vector3(0,3.65f,10.94f),new Vector3(4.3f,5.5f,.12f),darkStone,false);
            Primitive("Sanctuary cedar doors",PrimitiveType.Cube,courtyard,new Vector3(0,3.5f,10.84f),new Vector3(3.35f,5.2f,.10f),wood,false);
            for(int i=-3;i<=3;i++)if(i!=0)Column(new Vector3(i*2.45f,1,10),5.9f,.40f,courtyard);
            Primitive("Entablature",PrimitiveType.Cube,courtyard,new Vector3(0,7.25f,10.3f),new Vector3(19,1.1f,2.1f),paleStone,false);
            for(int i=-7;i<=7;i++)Primitive("Frieze bronze inset",PrimitiveType.Cube,courtyard,new Vector3(i*1.14f,7.25f,9.18f),new Vector3(.27f,.52f,.05f),gold,false);
            Primitive("Temple upper coping",PrimitiveType.Cube,courtyard,new Vector3(0,8.0f,12.5f),new Vector3(19.7f,.35f,8),warmStone,false);
            // Repeated carved stone courses give broad surfaces depth without an image backdrop.
            for(int i=0;i<11;i++)Primitive("Temple stone course",PrimitiveType.Cube,courtyard,new Vector3(0,1.5f+i*.59f,10.97f),new Vector3(17.8f,.035f,.09f),stone,false);
            foreach(int side in new[]{-1,1})
            {
                Primitive("Temple side wing",PrimitiveType.Cube,courtyard,new Vector3(side*11,3.1f,13),new Vector3(4,6.2f,8),warmStone,true);
                Primitive("Wing cornice",PrimitiveType.Cube,courtyard,new Vector3(side*11,6.3f,13),new Vector3(4.5f,.3f,8.4f),paleStone,false);
            }
        }
        void BuildColonnades()
        {
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<7;i++)Column(new Vector3(side*12.8f,0,-10+i*3.0f),3.6f,.28f,courtyard);
                Primitive("Colonnade roof",PrimitiveType.Cube,courtyard,new Vector3(side*13.2f,3.95f,-1),new Vector3(4.0f,.36f,22.5f),paleStone,false);
                Primitive("Colonnade frieze",PrimitiveType.Cube,courtyard,new Vector3(side*12.0f,3.6f,-1),new Vector3(.25f,.4f,22.5f),warmStone,false);
            }
        }
        void BuildGarden()
        {
            foreach(var p in new[]{new Vector3(-8,0,-6),new Vector3(8,0,-6),new Vector3(-8,0,6),new Vector3(8,0,7)})
            {
                Cylinder("Raised garden bed",courtyard,p+Vector3.up*.16f,1.25f,.3f,paleStone,true);
                Cylinder("Garden earth",courtyard,p+Vector3.up*.33f,1.05f,.035f,ground,false);OliveTree(p+Vector3.up*.35f);
            }
            foreach(var p in new[]{new Vector3(-6.6f,0,2.4f),new Vector3(6.8f,0,3.7f),new Vector3(-10,0,-9),new Vector3(10,0,-9)})
            {
                Lathe("Terracotta water jar",courtyard,p,new[]{new Vector2(.18f,0),new Vector2(.36f,.16f),new Vector2(.43f,.58f),new Vector2(.27f,.85f),new Vector2(.20f,.92f),new Vector2(.23f,.98f)},pottery,28);
                Torus("Jar rim",courtyard,p+Vector3.up*.98f,.23f,.035f,pottery,28);
            }
            Primitive("Teacher bench",PrimitiveType.Cube,courtyard,new Vector3(-5.1f,.28f,3.4f),new Vector3(3.7f,.28f,.8f),paleStone,true);
            foreach(float x in new[]{-6.3f,-3.9f})Primitive("Bench leg",PrimitiveType.Cube,courtyard,new Vector3(x,.13f,3.4f),new Vector3(.3f,.26f,.55f),stone,false);
            var table=Primitive("Reading table",PrimitiveType.Cube,courtyard,new Vector3(-5,.65f,1.65f),new Vector3(1.5f,.09f,.75f),wood,true);
            Primitive("Open scroll",PrimitiveType.Cube,courtyard,new Vector3(-5,.705f,1.65f),new Vector3(1.1f,.02f,.55f),linen,false);
            foreach(float x in new[]{-5.57f,-4.43f})
            {var roller=Primitive("Scroll roller",PrimitiveType.Cylinder,courtyard,new Vector3(x,.74f,1.65f),new Vector3(.09f,.32f,.09f),wood,false);roller.transform.rotation=Quaternion.Euler(90,0,0);}
            for(int i=0;i<5;i++)Primitive("Ink marks on scroll",PrimitiveType.Cube,courtyard,new Vector3(-5,.72f,1.47f+i*.075f),new Vector3(.65f,.006f,.012f),darkStone,false);
        }
        void OliveTree(Vector3 p)
        {
            var trunk=Primitive("Olive trunk",PrimitiveType.Cylinder,courtyard,p+Vector3.up*1.1f,new Vector3(.28f,1.12f,.28f),wood,false);trunk.transform.rotation=Quaternion.Euler(4,15,-8);
            for(int i=0;i<8;i++)
            {
                float angle=i*2.4f;var end=p+new Vector3(Mathf.Sin(angle)*(.7f+i*.06f),2.3f+(i%3)*.3f,Mathf.Cos(angle)*(.7f+i*.06f));
                Beam("Olive branch",p+Vector3.up*1.5f,end,.10f,wood,courtyard);
                var crown=OliveSpray(end,i%2==0?leaf:foliageLight);
                crown.rotation=Quaternion.Euler(i*13,i*41,15);oliveCrowns.Add(crown);oliveRest.Add(crown.localRotation);
            }
        }
        Transform OliveSpray(Vector3 position,Material material)
        {
            // Small curved, double-sided leaves give the canopy depth and open sky gaps.
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<160;i++)
            {
                var center=new Vector3((float)random.NextDouble()-.5f,((float)random.NextDouble()-.5f)*.65f,(float)random.NextDouble()-.5f);
                var rotation=Quaternion.Euler((float)random.NextDouble()*150,(float)random.NextDouble()*360,(float)random.NextDouble()*90);
                float length=.16f+(float)random.NextDouble()*.13f,width=length*.22f;int start=vertices.Count;
                foreach(var p in new[]{new Vector3(0,0,-length*.5f),new Vector3(-width,0,0),new Vector3(0,.025f,0),new Vector3(width,0,0),new Vector3(0,0,length*.5f)})
                { vertices.Add(center+rotation*p);normals.Add(rotation*Vector3.up); }
                foreach(int index in new[]{0,1,2,0,2,3,1,4,2,2,4,3,2,1,0,3,2,0,2,4,1,3,4,2})triangles.Add(start+index);
            }
            var mesh=new Mesh{name="Olive leaf spray"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject("Olive leaves",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(courtyard,false);go.transform.position=position;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        void Fountain()
        {
            Cylinder("Pool pedestal",courtyard,new Vector3(0,.10f,0),2.05f,.20f,paleStone,true);
            Cylinder("Pool shadow well",courtyard,new Vector3(0,.22f,0),1.87f,.08f,darkStone,false);
            Torus("Rounded fountain rim",courtyard,new Vector3(0,.32f,0),1.95f,.13f,paleStone,64);
            Cylinder("Reflective pool",courtyard,new Vector3(0,.28f,0),1.80f,.025f,water,false);
            Lathe("Carved fountain stem",courtyard,new Vector3(0,.30f,0),new[]{new Vector2(.42f,0),new Vector2(.29f,.15f),new Vector2(.18f,.6f),new Vector2(.36f,.8f),new Vector2(.65f,.9f),new Vector2(.68f,1.02f)},paleStone,40);
            Cylinder("Upper water bowl",courtyard,new Vector3(0,1.34f,0),.57f,.025f,water,false);
            for(int i=0;i<6;i++){float a=i*Mathf.PI/3;Primitive("Water trickle",PrimitiveType.Cylinder,courtyard,new Vector3(Mathf.Sin(a)*.63f,.9f,Mathf.Cos(a)*.63f),new Vector3(.025f,.40f,.025f),water,false);}
        }
        void BuildManger()
        {
            manger=new GameObject("Bethlehem opening · staged grotto").transform;manger.SetParent(transform,false);manger.position=new Vector3(90,0,0);
            Primitive("Stable floor",PrimitiveType.Cube,manger,new Vector3(0,-.06f,0),new Vector3(15,.12f,12),ground,true);
            Primitive("Stable wall",PrimitiveType.Cube,manger,new Vector3(0,2.1f,3),new Vector3(13,4.2f,.7f),darkStone,false);
            foreach(float x in new[]{-5f,5f}){Primitive("Stable timber post",PrimitiveType.Cube,manger,new Vector3(x,1.8f,0),new Vector3(.28f,3.6f,.28f),wood,false);}
            Primitive("Stable timber beam",PrimitiveType.Cube,manger,new Vector3(0,3.5f,0),new Vector3(10.6f,.3f,.32f),wood,false);
            var cradle=Primitive("Manger",PrimitiveType.Cube,manger,new Vector3(0,.48f,.7f),new Vector3(1.65f,.7f,.9f),wood,false);cradle.transform.rotation=Quaternion.Euler(0,-15,0);
            Primitive("Linen in manger",PrimitiveType.Capsule,manger,new Vector3(0,.89f,.7f),new Vector3(.52f,.25f,.85f),linen,false);
            Primitive("Infant wrapped in linen",PrimitiveType.Capsule,manger,new Vector3(0,1.02f,.7f),new Vector3(.30f,.20f,.60f),paleStone,false);
            var lampGo=new GameObject("Manger lamplight",typeof(Light));lampGo.transform.SetParent(manger,false);lampGo.transform.localPosition=new Vector3(-1.2f,2.4f,-.5f);
            var lamp=lampGo.GetComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(1,.61f,.27f);lamp.intensity=12;lamp.range=11;
            manger.gameObject.SetActive(false);
        }
        GameObject CreatePerson(Transform parent,string name,Vector3 position,float scale)
        {
            var model=Resources.Load<GameObject>(HeroResource);
            if(model==null)throw new InvalidOperationException("Required skinned youth model is missing: "+HeroResource);
            var body=Instantiate(model,parent);body.name=name;body.transform.localPosition=position;body.transform.localRotation=Quaternion.identity;body.transform.localScale=Vector3.one*scale;
            // FBX import may prefix embedded material IDs with the model name.
            // Resolve the authored Gospel key explicitly instead of displaying
            // Standard/URP fallback materials without their face/eye textures.
            foreach(var renderer in body.GetComponentsInChildren<Renderer>(true))
            {
                var materials=renderer.sharedMaterials;
                for(int i=0;i<materials.Length;i++)
                {
                    string importedName=materials[i]!=null?materials[i].name:"";
                    int keyStart=importedName.IndexOf("Gospel_",StringComparison.Ordinal);
                    if(keyStart<0)throw new InvalidOperationException("Unknown protagonist material: "+importedName);
                    string key=importedName.Substring(keyStart).Replace(" (Instance)","");
                    var authored=Resources.Load<Material>("Art/Gospel3D/"+key);
                    if(authored==null)throw new InvalidOperationException("Missing authored protagonist material: "+key);
                    materials[i]=authored;
                }
                renderer.sharedMaterials=materials;
            }
            foreach(var camera in body.GetComponentsInChildren<Camera>())Destroy(camera.gameObject);
            return body;
        }
        void CreateNpc(string name,Vector3 position,float yaw,float scale,Color robeColor)
        {
            var actor=new GameObject(name);actor.transform.SetParent(courtyard,false);actor.transform.localPosition=position;actor.transform.localRotation=Quaternion.Euler(0,yaw,0);
            var body=CreatePerson(actor.transform,name+" model",Vector3.zero,scale);
            var animator=body.GetComponentInChildren<Animator>();if(animator==null)animator=body.AddComponent<Animator>();animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>(AnimatorResource);animator.applyRootMotion=false;
            foreach(var renderer in body.GetComponentsInChildren<Renderer>())
            {
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++)if(mats[i]!=null&&(mats[i].name.ToLowerInvariant().Contains("robe")||mats[i].name.ToLowerInvariant().Contains("tunic")||mats[i].name.ToLowerInvariant().Contains("linen")))
                {var tint=new Material(mats[i]);tint.SetColor("_BaseColor",robeColor);mats[i]=tint;ownedMaterials.Add(tint);}
                renderer.sharedMaterials=mats;
            }
            var collider=actor.AddComponent<CapsuleCollider>();collider.center=Vector3.up*.8f;collider.radius=.27f;collider.height=1.6f;
        }
        public void SetCurrentLandmark(string id){currentLandmark=id;if(marker!=null){marker.gameObject.SetActive(landmarks.ContainsKey(id??""));if(landmarks.TryGetValue(id??"",out var p))marker.position=p;}}
        void Update()
        {
            if(marker!=null&&marker.gameObject.activeSelf)
            {
                // Keep the floor guide and HUD at reading distance; the floating guide
                // must never sit between the orbiting camera and the protagonist's face.
                var gem=marker.Find("Waypoint light");
                if(gem!=null)
                {
                    bool distant=Player!=null&&Vector3.Distance(Player.transform.position,marker.position)>3.1f;
                    gem.gameObject.SetActive(distant);
                    if(distant){gem.localPosition=new Vector3(0,1.9f+Mathf.Sin(Time.time*2)*.08f,0);gem.Rotate(0,36*Time.deltaTime,0,Space.World);}
                }
            }
            for(int i=0;i<oliveCrowns.Count;i++)if(oliveCrowns[i]!=null)oliveCrowns[i].localRotation=oliveRest[i]*Quaternion.Euler(Mathf.Sin(Time.time*.65f+i)*.55f,0,Mathf.Sin(Time.time*.9f+i*.7f)*.8f);
        }
        public void SetOverview(bool birth)
        {
            if(Player!=null){Player.InputEnabled=false;Player.CameraFollow=false;Player.StopInput();}
            courtyard.gameObject.SetActive(!birth);manger.gameObject.SetActive(birth);
            if(birth){View.transform.position=new Vector3(95.5f,3.1f,-5.3f);View.transform.LookAt(new Vector3(90,.9f,.7f));}
            else{View.transform.position=new Vector3(13,7.8f,-14);View.transform.LookAt(new Vector3(0,2.4f,2));}
        }
        public void BeginExploration(){courtyard.gameObject.SetActive(true);manger.gameObject.SetActive(false);Player.CameraFollow=true;Player.SnapCamera();}
        GameObject Primitive(string name,PrimitiveType kind,Transform parent,Vector3 p,Vector3 scale,Material mat,bool collide)
        {
            var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!collide)Destroy(go.GetComponent<Collider>());return go;
        }
        void Cylinder(string name,Transform parent,Vector3 p,float radius,float height,Material mat,bool collide)=>Primitive(name,PrimitiveType.Cylinder,parent,p,new Vector3(radius*2,height*.5f,radius*2),mat,collide);
        void Column(Vector3 p,float height,float radius,Transform parent)
        {
            Cylinder("Column base",parent,p+Vector3.up*.12f,radius*1.6f,.24f,paleStone,true);
            Cylinder("Column shaft",parent,p+Vector3.up*(height*.5f+.24f),radius,height,stone,true);
            Cylinder("Column capital",parent,p+Vector3.up*(height+.24f),radius*1.55f,.24f,paleStone,false);
            for(int i=1;i<5;i++)Torus("Column carved band",parent,p+Vector3.up*(.24f+i*height/5),radius*1.015f,.018f,warmStone,24);
        }
        void Beam(string name,Vector3 from,Vector3 to,float radius,Material material,Transform parent)
        {var go=Primitive(name,PrimitiveType.Cylinder,parent,(from+to)*.5f,new Vector3(radius,(to-from).magnitude*.5f,radius),material,false);go.transform.up=(to-from).normalized;}
        void Lathe(string name,Transform parent,Vector3 p,Vector2[] profile,Material mat,int sides)
        {
            var vertices=new Vector3[(sides+1)*profile.Length];var triangles=new int[sides*(profile.Length-1)*6];int t=0;
            for(int row=0;row<profile.Length;row++)for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;vertices[row*(sides+1)+i]=new Vector3(Mathf.Sin(a)*profile[row].x,profile[row].y,Mathf.Cos(a)*profile[row].x);}
            for(int row=0;row<profile.Length-1;row++)for(int i=0;i<sides;i++){int a=row*(sides+1)+i,b=a+sides+1;triangles[t++]=a;triangles[t++]=b;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=b+1;}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        void Torus(string name,Transform parent,Vector3 p,float radius,float thickness,Material mat,int segments)
        {
            const int rings=8;var vertices=new Vector3[(segments+1)*(rings+1)];var triangles=new int[segments*rings*6];int t=0;
            for(int i=0;i<=segments;i++)for(int j=0;j<=rings;j++){float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/rings;vertices[i*(rings+1)+j]=new Vector3((radius+Mathf.Cos(b)*thickness)*Mathf.Sin(a),Mathf.Sin(b)*thickness,(radius+Mathf.Cos(b)*thickness)*Mathf.Cos(a));}
            for(int i=0;i<segments;i++)for(int j=0;j<rings;j++){int a=i*(rings+1)+j,b=a+rings+1;triangles[t++]=a;triangles[t++]=b;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=b+1;}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();ownedMeshes.Add(mesh);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        void CombineArchitecture()
        {
            var groups=new Dictionary<Material,List<CombineInstance>>();
            foreach(var renderer in courtyard.GetComponentsInChildren<MeshRenderer>())
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(filter==null||filter.sharedMesh==null||renderer.sharedMaterial==null||renderer.sharedMaterial==leaf||renderer.sharedMaterial==foliageLight)continue;
                if(!groups.TryGetValue(renderer.sharedMaterial,out var list)){list=new List<CombineInstance>();groups.Add(renderer.sharedMaterial,list);}
                list.Add(new CombineInstance{mesh=renderer.sharedMaterial.GetTexture("_BaseMap")!=null?WorldUvMesh(filter.sharedMesh,renderer.transform):filter.sharedMesh,transform=courtyard.worldToLocalMatrix*renderer.transform.localToWorldMatrix});
                renderer.enabled=false;
            }
            foreach(var group in groups)
            {
                var mesh=new Mesh{name="Courtyard · "+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Value.ToArray(),true,true);ownedMeshes.Add(mesh);
                var combined=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));combined.transform.SetParent(courtyard,false);
                combined.GetComponent<MeshFilter>().sharedMesh=mesh;combined.GetComponent<MeshRenderer>().sharedMaterial=group.Key;
            }
        }
        void OnDestroy(){foreach(var material in ownedMaterials)if(material!=null)Destroy(material);foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);if(ownedProfile!=null)Destroy(ownedProfile);}
    }
}
