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
        GospelAuthoredProps authoredProps;
        Transform courtyard, manger, marker;
        Light sun;
        Material stone, paleStone, warmStone, darkStone, gold, wood, leaf, foliageLight, water, linen, pottery, ground, paving, pavingWarm, waterGlint, clayLight;
        System.Random random = new System.Random(41252);
        string currentLandmark;
        public Vector3 LandmarkPosition(string id) => landmarks.TryGetValue(id,out var p) ? p : Vector3.zero;
        public void Build()
        {
            var shader = Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new InvalidOperationException("The URP Lit shader is required.");
            stone=Mat("Warm Jerusalem limestone",new Color(.78f,.76f,.68f)); paleStone=Mat("Chalk stone",new Color(.94f,.92f,.84f));
            warmStone=Mat("Warm stone variation",new Color(.78f,.72f,.60f)); darkStone=Mat("Weathered recess",new Color(.34f,.35f,.32f));
            gold=Mat("Aged bronze detailing",new Color(.56f,.39f,.17f),.55f,.30f);wood=Mat("Cedar",new Color(.24f,.145f,.085f));
            leaf=Mat("Olive shaded leaves",new Color(.25f,.34f,.24f));foliageLight=Mat("Olive silver leaves",new Color(.43f,.49f,.34f));
            water=Mat("Pool water",new Color(.12f,.30f,.32f),.18f,.91f);waterGlint=Mat("Pool reflected daylight",new Color(.40f,.61f,.59f),.12f,.8f);
            waterGlint.SetFloat("_ReceiveShadows",0);waterGlint.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            linen=Mat("Unbleached linen canopy",new Color(.79f,.73f,.59f));
            pottery=Mat("Terracotta",new Color(.51f,.27f,.17f));clayLight=Mat("Pale fired clay",new Color(.68f,.48f,.31f));ground=Mat("Distant earth",new Color(.48f,.45f,.36f));
            paving=Mat("Limestone courtyard paving",Color.white);pavingWarm=Mat("Weathered courtyard paving",new Color(.94f,.91f,.84f));
            ApplyStone(stone,"limestone",new Color(.91f,.89f,.83f));ApplyStone(paleStone,"limestone",new Color(1,.985f,.93f));
            ApplyStone(warmStone,"limestone",new Color(.88f,.84f,.74f));ApplyStone(darkStone,"limestone",new Color(.50f,.49f,.44f));
            ApplyStone(paving,"paving",new Color(.88f,.91f,.92f));ApplyStone(pavingWarm,"paving",new Color(.92f,.89f,.81f));
            ConfigureLighting();
            var cam = new GameObject("Gospel camera",typeof(Camera),typeof(AudioListener)); View=cam.GetComponent<Camera>();
            View.tag="MainCamera";View.fieldOfView=52;View.nearClipPlane=.13f;View.farClipPlane=180;View.allowHDR=true;
            View.backgroundColor=new Color(.65f,.76f,.80f);View.clearFlags=CameraClearFlags.Skybox;
            var additional=cam.AddComponent<UniversalAdditionalCameraData>();additional.renderPostProcessing=true;additional.antialiasing=AntialiasingMode.FastApproximateAntialiasing;
            // A gentle, shadowless portrait fill makes the main face readable in
            // backlight. It follows the view and affects only the protagonist layer.
            var fillObject=new GameObject("Protagonist portrait fill",typeof(Light));
            fillObject.transform.SetParent(cam.transform,false);fillObject.transform.localPosition=new Vector3(0,-.20f,.65f);
            var fill=fillObject.GetComponent<Light>();fill.type=LightType.Point;fill.color=new Color(1,.98f,.95f);
            // Close zoom brings the camera light nearer the face. Keep the fill
            // gentle so skin and cloth retain their authored colour and detail.
            fill.intensity=.55f;fill.range=6;fill.shadows=LightShadows.None;fill.cullingMask=1<<8;
            courtyard=new GameObject("Temple courtyard · physical scene").transform;courtyard.SetParent(transform,false);
            authoredProps=gameObject.AddComponent<GospelAuthoredProps>();authoredProps.Initialize();
            BuildLandscape();BuildCourtyard();BuildTemple();BuildColonnades();BuildGarden();BuildCourtyardDetails();BuildQuietCourtyardLife();CombineArchitecture();BuildManger();
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
            Torus("Optional practice area · outside Scripture progression",courtyard,new Vector3(-8,.06f,-8),2.8f,.025f,gold,64,false);
            CreateNpc("Temple guide",new Vector3(1.2f,0,-5),220,1.08f,new Color(.65f,.62f,.48f));
            CreateNpc("Teacher reading",new Vector3(-5.8f,0,2.4f),120,1.15f,new Color(.58f,.48f,.32f));
            CreateNpc("Teacher listening",new Vector3(-4.4f,0,2.7f),210,1.1f,new Color(.65f,.57f,.42f));
            CreateNpc("Mary",new Vector3(4.6f,0,3.2f),210,1.18f,new Color(.35f,.48f,.55f));
            CreateNpc("Joseph",new Vector3(5.6f,0,3.4f),225,1.17f,new Color(.58f,.39f,.26f));
            marker=new GameObject("Current Scripture landmark").transform;marker.SetParent(courtyard,false);
            // The guide belongs to the ground plane. A floating bronze sphere
            // previously overlapped the basin and resembled a religious statue.
            Torus("Waypoint floor ring",marker,Vector3.up*.072f,.58f,.018f,gold,64,false);
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
            RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.56f,.64f,.75f);
            RenderSettings.ambientEquatorColor=new Color(.44f,.46f,.43f);RenderSettings.ambientGroundColor=new Color(.27f,.26f,.23f);
            RenderSettings.fog=true;RenderSettings.fogColor=new Color(.73f,.77f,.78f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.0048f;
            var lightObject=new GameObject("Afternoon sun",typeof(Light));lightObject.transform.SetParent(transform,false);
            sun=lightObject.GetComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.95f,.86f);sun.intensity=1.38f;sun.shadows=LightShadows.Soft;
            sun.shadowBias=.035f;sun.shadowNormalBias=.20f;lightObject.transform.rotation=Quaternion.Euler(43,-32,0);RenderSettings.sun=sun;
            var skyShader=Shader.Find("Skybox/Procedural");
            if(skyShader!=null){var sky=new Material(skyShader);sky.SetFloat("_SunSize",.025f);sky.SetFloat("_AtmosphereThickness",1.05f);sky.SetColor("_SkyTint",new Color(.53f,.64f,.78f));sky.SetColor("_GroundColor",new Color(.52f,.48f,.39f));RenderSettings.skybox=sky;ownedMaterials.Add(sky);}
            var volumeObject=new GameObject("Warm daylight grade",typeof(Volume));volumeObject.transform.SetParent(transform,false);
            var volume=volumeObject.GetComponent<Volume>();volume.isGlobal=true;var profile=ScriptableObject.CreateInstance<VolumeProfile>();ownedProfile=profile;volume.profile=profile;
            var tone=profile.Add<Tonemapping>();tone.mode.Override(TonemappingMode.ACES);
            var adjustment=profile.Add<ColorAdjustments>();adjustment.postExposure.Override(.03f);adjustment.contrast.Override(7);adjustment.saturation.Override(-5);
            var bloom=profile.Add<Bloom>();bloom.intensity.Override(.075f);bloom.threshold.Override(1.2f);
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
                Primitive("Limestone paving",PrimitiveType.Cube,courtyard,new Vector3(x*1.95f,.013f,z*1.95f),new Vector3(1.93f,.022f,1.93f),mat,true,false);
            }
            foreach(int approach in new[]{-1,1})for(int row=0;row<5;row++)for(int column=0;column<4;column++)
            {
                float z=approach*(3.48f+row*1.41f);
                Primitive("Broad processional paving stone",PrimitiveType.Cube,courtyard,new Vector3(-1.50f+column,.038f,z),new Vector3(.98f,.014f,1.38f),(row+column)%6==0?stone:paleStone,true,false);
            }
            foreach(int side in new[]{-1,1})foreach(int approach in new[]{-1,1})
                Primitive("Processional path inlay",PrimitiveType.Cube,courtyard,new Vector3(side*2.10f,.043f,approach*6.3f),new Vector3(.075f,.012f,7),warmStone,false,false);
            for(int i=-7;i<=7;i++)foreach(int edge in new[]{-1,1})
            {
                Primitive("Inlaid border",PrimitiveType.Cube,courtyard,new Vector3(i*1.95f,.035f,edge*11.1f),new Vector3(1.8f,.016f,.13f),darkStone,false,false);
                Primitive("Inlaid border",PrimitiveType.Cube,courtyard,new Vector3(edge*11.1f,.035f,i*1.95f),new Vector3(.13f,.016f,1.8f),darkStone,false,false);
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
            DrawWell();
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
                for(int i=0;i<7;i++)Column(new Vector3(side*12.8f,0,-10+i*3.0f),2.84f,.29f,courtyard);
                for(int bay=0;bay<6;bay++)
                    StoneArch("Portico voussoir arch",courtyard,new Vector3(side*12.8f,3.12f,-8.5f+bay*3),2.78f,.28f,.62f,90);
                Primitive("Colonnade shadow cornice",PrimitiveType.Cube,courtyard,new Vector3(side*13.2f,4.88f,-1),new Vector3(4.0f,.18f,22.5f),warmStone,false);
                Primitive("Colonnade roof",PrimitiveType.Cube,courtyard,new Vector3(side*13.2f,5.04f,-1),new Vector3(4.20f,.22f,22.75f),paleStone,false);
                Primitive("Colonnade upper lip",PrimitiveType.Cube,courtyard,new Vector3(side*13.2f,5.19f,-1),new Vector3(4.34f,.10f,22.90f),stone,false);
                // Recessed timber ceiling is visible through the open arch bays.
                for(int beam=0;beam<12;beam++)
                    Primitive("Portico cedar rafter",PrimitiveType.Cube,courtyard,new Vector3(side*13.45f,4.75f,-10.6f+beam*1.8f),new Vector3(3.0f,.13f,.14f),wood,false);
            }
        }
        void BuildGarden()
        {
            // Authored CC0 broadleaf meshes and photographic bark/leaf materials
            // replace procedural round crowns. These are artistic shade trees,
            // not a claim about a particular historical botanical species.
            var treePositions=new[]{new Vector3(-8,0,-6),new Vector3(8,0,-6),new Vector3(-8,0,6),new Vector3(8,0,7)};
            for(int i=0;i<treePositions.Length;i++)
            {
                var p=treePositions[i];
                Cylinder("Stone garden curb",courtyard,p+Vector3.up*.12f,1.20f,.22f,paleStone,true);
                Cylinder("Garden soil",courtyard,p+Vector3.up*.24f,1.05f,.025f,ground,false);
                authoredProps.AddTree(courtyard,p+Vector3.up*.25f,3.65f+(i%2)*.35f,i*61,i%2);
                for(int n=0;n<3;n++)
                {
                    float angle=(n*2.1f+i)*1.1f;
                    authoredProps.AddShrub(courtyard,p+new Vector3(Mathf.Sin(angle)*.75f,.25f,Mathf.Cos(angle)*.75f),.47f+n*.07f,i*43+n*21,n);
                }
            }
            var vessels=new[]{new Vector3(-6.6f,0,2.4f),new Vector3(6.8f,0,3.7f),new Vector3(-10,0,-9),new Vector3(10,0,-9)};
            for(int i=0;i<vessels.Length;i++)
            {
                authoredProps.AddCeramicVessel(courtyard,vessels[i],.74f+(i%2)*.12f,i*49);
                authoredProps.AddClayPlanter(courtyard,vessels[i]+new Vector3((i%2==0?1:-1)*.65f,0,.35f),.38f,i*29);
            }
            Primitive("Teacher bench",PrimitiveType.Cube,courtyard,new Vector3(-5.1f,.28f,3.4f),new Vector3(3.7f,.28f,.8f),paleStone,true);
            foreach(float x in new[]{-6.3f,-3.9f})Primitive("Bench leg",PrimitiveType.Cube,courtyard,new Vector3(x,.13f,3.4f),new Vector3(.3f,.26f,.55f),stone,false);
            var table=Primitive("Reading table",PrimitiveType.Cube,courtyard,new Vector3(-5.0f,.71f,2.45f),new Vector3(1.1f,.10f,.67f),wood,false);
            Primitive("Open Scripture scroll",PrimitiveType.Cube,courtyard,new Vector3(-5.0f,.775f,2.45f),new Vector3(.65f,.015f,.39f),linen,false);
        }
        void BuildCourtyardDetails()
        {
            // Quiet, modelled details break up the broad wall surfaces. They are
            // an illustrative setting, not an archaeological reconstruction.
            foreach(int side in new[]{-1,1})foreach(float z in new[]{-8f,-2f,4f})
            {
                float x=side*14.60f;
                Primitive("Carved portico panel",PrimitiveType.Cube,courtyard,new Vector3(x,1.55f,z),new Vector3(.08f,1.44f,1.26f),darkStone,false);
                foreach(float y in new[]{.79f,2.31f})Primitive("Panel limestone lintel",PrimitiveType.Cube,courtyard,new Vector3(x-side*.05f,y,z),new Vector3(.14f,.11f,1.53f),paleStone,false);
                foreach(float edge in new[]{-.71f,.71f})Primitive("Panel limestone jamb",PrimitiveType.Cube,courtyard,new Vector3(x-side*.05f,1.55f,z+edge),new Vector3(.14f,1.62f,.11f),paleStone,false);
                for(int bar=0;bar<5;bar++)Primitive("Cedar lattice",PrimitiveType.Cube,courtyard,new Vector3(x-side*.07f,1.55f,z-.47f+bar*.235f),new Vector3(.05f,1.28f,.034f),wood,false);
                for(int bar=0;bar<3;bar++)Primitive("Cedar lattice crossbar",PrimitiveType.Cube,courtyard,new Vector3(x-side*.09f,1.08f+bar*.47f,z),new Vector3(.04f,.034f,1.08f),wood,false);
            }
            foreach(int side in new[]{-1,1})
            {
                // Layered thresholds and bronze door edges give the entrance a
                // readable silhouette even with the new close camera distance.
                Primitive("Sanctuary doorway stone frame",PrimitiveType.Cube,courtyard,new Vector3(side*2.33f,3.65f,10.77f),new Vector3(.30f,5.90f,.38f),paleStone,false);
                Primitive("Sanctuary bronze door edge",PrimitiveType.Cube,courtyard,new Vector3(side*1.58f,3.5f,10.76f),new Vector3(.055f,5.10f,.055f),gold,false);
                foreach(float y in new[]{1.5f,3.5f,5.5f})Primitive("Cedar door raised panel",PrimitiveType.Cube,courtyard,new Vector3(side*.83f,y,10.73f),new Vector3(1.23f,1.40f,.075f),wood,false);
            }
            Primitive("Sanctuary lintel moulding",PrimitiveType.Cube,courtyard,new Vector3(0,6.64f,10.70f),new Vector3(5.10f,.24f,.48f),paleStone,false);
            // Distinct recessed joints and projecting courses read as dressed
            // masonry in grazing light, rather than a single featureless box.
            for(int course=0;course<9;course++)
            {
                float y=1.47f+course*.61f;
                for(int block=0;block<12;block++)
                {
                    float x=-8.1f+block*1.47f+(course%2)*.735f;
                    if(Mathf.Abs(x)<2.5f||x>8.5f)continue;
                    Primitive("Sanctuary dressed ashlar",PrimitiveType.Cube,courtyard,new Vector3(x,y,10.90f),new Vector3(1.42f,.56f,.16f),(course+block)%7==0?warmStone:paleStone,false);
                }
            }
            foreach(int side in new[]{-1,1})
            {
                // The side wings retain their existing collision envelope.
                var wing=courtyard;
                for(int storey=0;storey<2;storey++)
                {
                    Vector3 aperture=new Vector3(side*11,1.48f+storey*2.38f,8.94f);
                    Primitive("Wing recessed window",PrimitiveType.Cube,wing,aperture+Vector3.up*.47f,new Vector3(1.10f,1.29f,.12f),darkStone,false);
                    StoneArch("Wing window arch",wing,aperture+Vector3.up*1.10f,.99f,.18f,.20f,0);
                    foreach(int jamb in new[]{-1,1})Primitive("Wing window jamb",PrimitiveType.Cube,wing,aperture+new Vector3(jamb*.585f,.39f,-.03f),new Vector3(.17f,1.43f,.24f),paleStone,false);
                    for(int bar=0;bar<4;bar++)Primitive("Wing bronze lattice",PrimitiveType.Cube,wing,aperture+new Vector3(-.37f+bar*.245f,.42f,-.09f),new Vector3(.025f,1.12f,.025f),gold,false);
                    Primitive("Wing window sill",PrimitiveType.Cube,wing,aperture+new Vector3(0,-.34f,-.06f),new Vector3(1.52f,.16f,.44f),paleStone,false);
                }
                for(int i=0;i<6;i++)
                    Primitive("Wing corner ashlar",PrimitiveType.Cube,wing,new Vector3(side*12.76f,.52f+i*.94f,8.92f),new Vector3(.42f,.90f,.23f),paleStone,false);
            }
        }
        void BuildQuietCourtyardLife()
        {
            // Keep the central route and all Scripture landmarks free. Small
            // groups of hand-modelled props belong to the shaded portico edge.
            foreach(int side in new[]{-1,1})
            {
                for(int i=0;i<3;i++)
                {
                    var p=new Vector3(side*(11.05f+i*.52f),0,-3.2f+(i%2)*.55f);
                    if(i%2==0)authoredProps.AddCeramicVessel(courtyard,p,.54f+i*.12f,side*33+i*61);
                    else authoredProps.AddClayPlanter(courtyard,p,.53f,side*19+i*31);
                }
                Primitive("Portico stone seat",PrimitiveType.Cube,courtyard,new Vector3(side*13.65f,.49f,1.3f),new Vector3(.80f,.15f,3.0f),paleStone,false);
                foreach(float z in new[]{.12f,2.48f})Primitive("Portico seat pedestal",PrimitiveType.Cube,courtyard,new Vector3(side*13.65f,.24f,z),new Vector3(.60f,.48f,.34f),warmStone,false);
                for(int sprig=0;sprig<3;sprig++)
                    authoredProps.AddShrub(courtyard,new Vector3(side*(9.3f+sprig*.3f),.04f,6.4f),.35f+sprig*.08f,sprig*47,sprig);
                Primitive("Portico low threshold",PrimitiveType.Cube,courtyard,new Vector3(side*11.5f,.046f,-.5f),new Vector3(.13f,.028f,20),warmStone,false,false);
            }
        }
        void OliveTree(Vector3 p)
        {
            CurvedBranch("Gnarled olive trunk",courtyard,new[]{p,p+new Vector3(-.09f,.62f,.04f),p+new Vector3(.11f,1.30f,-.03f),p+new Vector3(.02f,1.97f,.08f)},new[]{.19f,.16f,.12f,.075f},wood,10);
            for(int i=0;i<11;i++)
            {
                float angle=i*2.4f;float reach=.63f+(i%4)*.18f;
                var end=p+new Vector3(Mathf.Sin(angle)*reach,2.12f+(i%3)*.29f,Mathf.Cos(angle)*reach);
                var start=p+new Vector3(.04f,1.25f+(i%4)*.14f,.03f);
                CurvedBranch("Tapering olive bough",courtyard,new[]{start,Vector3.Lerp(start,end,.50f)+Vector3.up*.13f,end},new[]{.072f,.044f,.017f},wood,7);
                var crown=OliveSpray(end,i%3==0?foliageLight:leaf,136,1.18f);
                crown.rotation=Quaternion.Euler(i*13,i*41,15);oliveCrowns.Add(crown);oliveRest.Add(crown.localRotation);
            }
        }
        Transform OliveSpray(Vector3 position,Material material,int count=160,float spread=1)
        {
            // Small curved, double-sided leaves give the canopy depth and open sky gaps.
            var vertices=new List<Vector3>();var normals=new List<Vector3>();var triangles=new List<int>();
            for(int i=0;i<count;i++)
            {
                var center=new Vector3((float)random.NextDouble()-.5f,((float)random.NextDouble()-.5f)*.68f,(float)random.NextDouble()-.5f)*spread;
                var rotation=Quaternion.Euler((float)random.NextDouble()*150,(float)random.NextDouble()*360,(float)random.NextDouble()*90);
                float length=.14f+(float)random.NextDouble()*.10f,width=length*.19f;int start=vertices.Count;
                foreach(var p in new[]{new Vector3(0,0,-length*.5f),new Vector3(-width,0,0),new Vector3(0,.025f,0),new Vector3(width,0,0),new Vector3(0,0,length*.5f)})
                { vertices.Add(center+rotation*p);normals.Add(rotation*Vector3.up); }
                foreach(int index in new[]{0,1,2,0,2,3,1,4,2,2,4,3,2,1,0,3,2,0,2,4,1,3,4,2})triangles.Add(start+index);
            }
            var mesh=new Mesh{name="Olive leaf spray"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetTriangles(triangles,0);mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject("Olive leaves",typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(courtyard,false);go.transform.position=position;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=material;return go.transform;
        }
        void DrawWell()
        {
            // A purpose-authored hollow draw well replaces the repurposed
            // fire-pit basin. The opening, shaded lining, recessed water,
            // timber winch, rope and bucket are actual three-dimensional parts.
            // This courtyard arrangement is illustrative, not a recovered site.
            authoredProps.AddWell(courtyard,new Vector3(0,.055f,0),18);
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
        GameObject CreatePerson(Transform parent,string name,Vector3 position,float scale,string resource=HeroResource)
        {
            var model=Resources.Load<GameObject>(resource);
            if(model==null)throw new InvalidOperationException("Required skinned character model is missing: "+resource);
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
            var actor=new GameObject(name);actor.transform.SetParent(courtyard,false);actor.transform.localPosition=position+Vector3.up*(name=="Temple guide"?.055f:.034f);actor.transform.localRotation=Quaternion.Euler(0,yaw,0);
            // Adults use an adult body, rather than enlarged copies of the boy.
            // Mary has her own veiled silhouette and facial proportions.
            string resource=name=="Mary"?"Art/Gospel3D/Mary":name=="Joseph"?"Art/Gospel3D/Joseph":"Art/Gospel3D/TempleTeacher";
            var body=CreatePerson(actor.transform,name+" model",Vector3.zero,scale*.84f,resource);
            var animator=body.GetComponentInChildren<Animator>();if(animator==null)animator=body.AddComponent<Animator>();animator.runtimeAnimatorController=Resources.Load<RuntimeAnimatorController>(AnimatorResource);animator.applyRootMotion=false;
            foreach(var renderer in body.GetComponentsInChildren<Renderer>())
            {
                var mats=renderer.sharedMaterials;
                for(int i=0;i<mats.Length;i++)if(mats[i]!=null&&(mats[i].name.ToLowerInvariant().Contains("robe")||mats[i].name.ToLowerInvariant().Contains("tunic")||mats[i].name.ToLowerInvariant().Contains("linen")))
                {var tint=new Material(mats[i]);tint.SetColor("_BaseColor",robeColor);mats[i]=tint;ownedMaterials.Add(tint);}
                renderer.sharedMaterials=mats;
            }
            var collider=actor.AddComponent<CapsuleCollider>();collider.center=Vector3.up*.8f;collider.radius=.27f;collider.height=1.6f;
            string id=name=="Mary"?"mary":name=="Joseph"?"joseph":name=="Teacher reading"?"teacher-reading":name=="Teacher listening"?"teacher-listening":"temple-guide";
            GospelNpcAnchor.Attach(actor,id);
        }
        public void SetCurrentLandmark(string id){currentLandmark=id;if(marker!=null){marker.gameObject.SetActive(landmarks.ContainsKey(id??""));if(landmarks.TryGetValue(id??"",out var p))marker.position=p;}}
        void Update()
        {
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
        GameObject Primitive(string name,PrimitiveType kind,Transform parent,Vector3 p,Vector3 scale,Material mat,bool collide,bool castShadows=true)
        {
            var go=GameObject.CreatePrimitive(kind);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=p;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            go.GetComponent<Renderer>().shadowCastingMode=castShadows?ShadowCastingMode.On:ShadowCastingMode.Off;
            // Small bevels catch daylight on cut stone and timber. Collision
            // remains the original simple box; the walkable layout is unchanged.
            if(kind==PrimitiveType.Cube&&Mathf.Min(scale.x,scale.y,scale.z)>.12f)
            {
                go.GetComponent<MeshFilter>().sharedMesh=SoftBox(scale);
                go.transform.localScale=Vector3.one;
                var box=go.GetComponent<BoxCollider>();if(box!=null)box.size=scale;
            }
            if(!collide)Destroy(go.GetComponent<Collider>());return go;
        }
        Mesh SoftBox(Vector3 size)
        {
            Vector3 half=size*.5f;float radius=Mathf.Min(.055f,Mathf.Min(size.x,Mathf.Min(size.y,size.z))*.13f);
            var vertices=new List<Vector3>(96);var normals=new List<Vector3>(96);var uv=new List<Vector2>(96);var triangles=new List<int>(324);
            foreach(var face in new[]{Vector3.right,Vector3.left,Vector3.up,Vector3.down,Vector3.forward,Vector3.back})
            {
                Vector3 u=Mathf.Abs(face.y)>.5f?Vector3.right:Vector3.up,v=Vector3.Cross(face,u);
                float hu=Vector3.Dot(half,new Vector3(Mathf.Abs(u.x),Mathf.Abs(u.y),Mathf.Abs(u.z)));
                float hv=Vector3.Dot(half,new Vector3(Mathf.Abs(v.x),Mathf.Abs(v.y),Mathf.Abs(v.z)));
                float hn=Vector3.Dot(half,new Vector3(Mathf.Abs(face.x),Mathf.Abs(face.y),Mathf.Abs(face.z)));
                float[] xs={-hu,-hu+radius,hu-radius,hu},ys={-hv,-hv+radius,hv-radius,hv};int start=vertices.Count;
                for(int y=0;y<4;y++)for(int x=0;x<4;x++)
                {
                    Vector3 point=face*hn+u*xs[x]+v*ys[y];
                    Vector3 inner=new Vector3(Mathf.Clamp(point.x,-half.x+radius,half.x-radius),Mathf.Clamp(point.y,-half.y+radius,half.y-radius),Mathf.Clamp(point.z,-half.z+radius,half.z-radius));
                    Vector3 normal=(point-inner).normalized;vertices.Add(inner+normal*radius);normals.Add(normal);uv.Add(new Vector2((xs[x]+hu)/(hu*2),(ys[y]+hv)/(hv*2)));
                }
                for(int y=0;y<3;y++)for(int x=0;x<3;x++){int a=start+y*4+x;triangles.AddRange(new[]{a,a+1,a+5,a,a+5,a+4});}
            }
            var mesh=new Mesh{name="Light-catching stone edges"};mesh.SetVertices(vertices);mesh.SetNormals(normals);mesh.SetUVs(0,uv);mesh.SetTriangles(triangles,0);mesh.RecalculateTangents();mesh.RecalculateBounds();ownedMeshes.Add(mesh);return mesh;
        }
        void Cylinder(string name,Transform parent,Vector3 p,float radius,float height,Material mat,bool collide)=>Primitive(name,PrimitiveType.Cylinder,parent,p,new Vector3(radius*2,height*.5f,radius*2),mat,collide);
        void Column(Vector3 p,float height,float radius,Transform parent)
        {
            Primitive("Column square plinth",PrimitiveType.Cube,parent,p+Vector3.up*.08f,new Vector3(radius*3.2f,.16f,radius*3.2f),paleStone,true);
            Lathe("Profiled Attic column base",parent,p,new[]{new Vector2(radius*1.38f,.16f),new Vector2(radius*1.43f,.20f),new Vector2(radius*1.22f,.25f),new Vector2(radius*1.18f,.30f),new Vector2(radius*1.30f,.34f),new Vector2(radius,.39f)},paleStone,40);
            // Continuous entasis and shallow flutes replace the rigid straight
            // primitive cylinder. Collision remains one simple capsule.
            const int rings=12,sides=48;var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int row=0;row<=rings;row++)
            {
                float t=row/(float)rings,y=.37f+t*(height-.25f);
                float r=radius*(1-.13f*t+.045f*Mathf.Sin(t*Mathf.PI));
                for(int i=0;i<=sides;i++)
                {
                    float a=i*Mathf.PI*2/sides;float flute=radius*.034f*(.5f+.5f*Mathf.Cos(a*16))*Mathf.Clamp01(t*8)*Mathf.Clamp01((1-t)*8);
                    vertices.Add(new Vector3(Mathf.Sin(a)*(r-flute),y,Mathf.Cos(a)*(r-flute)));
                }
            }
            for(int row=0;row<rings;row++)for(int i=0;i<sides;i++){int a=row*(sides+1)+i,b=a+sides+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            var shaft=MeshObject("Entasis fluted limestone shaft",parent,p,vertices,triangles,stone);
            var collision=shaft.AddComponent<CapsuleCollider>();collision.center=Vector3.up*(height*.5f+.24f);collision.height=height;collision.radius=radius;
            Lathe("Carved column neck and capital",parent,p,new[]{new Vector2(radius*.87f,height+.12f),new Vector2(radius*.99f,height+.16f),new Vector2(radius*.95f,height+.20f),new Vector2(radius*1.24f,height+.26f),new Vector2(radius*1.45f,height+.31f),new Vector2(radius*1.43f,height+.36f)},paleStone,40);
            Primitive("Column capital abacus",PrimitiveType.Cube,parent,p+Vector3.up*(height+.405f),new Vector3(radius*3.15f,.09f,radius*3.15f),paleStone,false);
            Torus("Column neck shadow bead",parent,p+Vector3.up*(height+.19f),radius*.975f,.018f,warmStone,40);
        }
        void Beam(string name,Vector3 from,Vector3 to,float radius,Material material,Transform parent)
        {var go=Primitive(name,PrimitiveType.Cylinder,parent,(from+to)*.5f,new Vector3(radius,(to-from).magnitude*.5f,radius),material,false);go.transform.up=(to-from).normalized;}
        GameObject MeshObject(string name,Transform parent,Vector3 p,List<Vector3> vertices,List<int> triangles,Material mat)
        {
            var mesh=new Mesh{name=name};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=p;
            go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
        }
        void StoneArch(string name,Transform parent,Vector3 spring,float opening,float thickness,float depth,float yaw)
        {
            // Twelve separate wedge-shaped stones make a real open arch. They
            // have front/back, intrados/extrados and end faces, not a flat decal.
            const int blocks=12;float inner=opening*.5f,outer=inner+thickness;
            Quaternion rotation=Quaternion.Euler(0,yaw,0);
            for(int block=0;block<blocks;block++)
            {
                float a=block*Mathf.PI/blocks+.006f,b=(block+1)*Mathf.PI/blocks-.006f;
                var points=new[]{new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,-depth*.5f),new Vector3(Mathf.Cos(b)*inner,Mathf.Sin(b)*inner,-depth*.5f),new Vector3(Mathf.Cos(b)*outer,Mathf.Sin(b)*outer,-depth*.5f),new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,-depth*.5f),
                    new Vector3(Mathf.Cos(a)*inner,Mathf.Sin(a)*inner,depth*.5f),new Vector3(Mathf.Cos(b)*inner,Mathf.Sin(b)*inner,depth*.5f),new Vector3(Mathf.Cos(b)*outer,Mathf.Sin(b)*outer,depth*.5f),new Vector3(Mathf.Cos(a)*outer,Mathf.Sin(a)*outer,depth*.5f)};
                var vertices=new List<Vector3>(24);var triangles=new List<int>(36);Vector3 center=Vector3.zero;foreach(var point in points)center+=point/8;
                foreach(var face in new[]{new[]{0,1,2,3},new[]{4,7,6,5},new[]{0,4,5,1},new[]{3,2,6,7},new[]{0,3,7,4},new[]{1,5,6,2}})
                {
                    var pa=points[face[0]];var pb=points[face[1]];var pc=points[face[2]];var pd=points[face[3]];
                    if(Vector3.Dot(Vector3.Cross(pb-pa,pc-pa),(pa+pb+pc+pd)*.25f-center)<0){var swap=pb;pb=pd;pd=swap;}
                    int start=vertices.Count;vertices.Add(rotation*pa);vertices.Add(rotation*pb);vertices.Add(rotation*pc);vertices.Add(rotation*pd);
                    triangles.AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
                }
                MeshObject(name+" · stone "+(block+1),parent,spring,vertices,triangles,block%7==0?warmStone:paleStone);
            }
        }
        void CurvedBranch(string name,Transform parent,Vector3[] points,float[] radii,Material mat,int sides)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();
            for(int row=0;row<points.Length;row++)
            {
                Vector3 tangent=(points[Mathf.Min(row+1,points.Length-1)]-points[Mathf.Max(row-1,0)]).normalized;
                Vector3 u=Vector3.Cross(tangent,Mathf.Abs(tangent.z)>.95f?Vector3.up:Vector3.forward).normalized,v=Vector3.Cross(tangent,u);
                for(int i=0;i<=sides;i++)
                {
                    float a=i*Mathf.PI*2/sides;
                    float bark=radii[row]*(1+.055f*Mathf.Sin(i*4.1f+row));vertices.Add(points[row]+(u*Mathf.Cos(a)+v*Mathf.Sin(a))*bark);
                }
            }
            for(int row=0;row<points.Length-1;row++)for(int i=0;i<sides;i++){int a=row*(sides+1)+i,b=a+sides+1;triangles.AddRange(new[]{a,a+1,b,a+1,b+1,b});}
            MeshObject(name,parent,Vector3.zero,vertices,triangles,mat);
        }
        void Lathe(string name,Transform parent,Vector3 p,Vector2[] profile,Material mat,int sides)
        {
            var vertices=new Vector3[(sides+1)*profile.Length];var uv=new Vector2[vertices.Length];var triangles=new int[sides*(profile.Length-1)*6];int t=0;
            for(int row=0;row<profile.Length;row++)for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;int index=row*(sides+1)+i;vertices[index]=new Vector3(Mathf.Sin(a)*profile[row].x,profile[row].y,Mathf.Cos(a)*profile[row].x);uv[index]=new Vector2(i/(float)sides,profile[row].y);}
            // At the front of a ring d(angle) points right and d(height)
            // points up. Their cross product faces out, so this winding is
            // visible with normal backface culling (the former order was inward).
            for(int row=0;row<profile.Length-1;row++)for(int i=0;i<sides;i++){int a=row*(sides+1)+i,b=a+sides+1;triangles[t++]=a;triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=a+1;triangles[t++]=b+1;triangles[t++]=b;}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.uv=uv;mesh.triangles=triangles;mesh.RecalculateNormals();mesh.RecalculateTangents();mesh.RecalculateBounds();ownedMeshes.Add(mesh);
            var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=mat;
        }
        void Torus(string name,Transform parent,Vector3 p,float radius,float thickness,Material mat,int segments,bool castShadows=true,bool receiveShadows=true)
        {
            const int rings=8;var vertices=new Vector3[(segments+1)*(rings+1)];var triangles=new int[segments*rings*6];int t=0;
            for(int i=0;i<=segments;i++)for(int j=0;j<=rings;j++){float a=i*Mathf.PI*2/segments,b=j*Mathf.PI*2/rings;vertices[i*(rings+1)+j]=new Vector3((radius+Mathf.Cos(b)*thickness)*Mathf.Sin(a),Mathf.Sin(b)*thickness,(radius+Mathf.Cos(b)*thickness)*Mathf.Cos(a));}
            for(int i=0;i<segments;i++)for(int j=0;j<rings;j++){int a=i*(rings+1)+j,b=a+rings+1;triangles[t++]=a;triangles[t++]=b;triangles[t++]=a+1;triangles[t++]=a+1;triangles[t++]=b;triangles[t++]=b+1;}
            var mesh=new Mesh{name=name};mesh.vertices=vertices;mesh.triangles=triangles;mesh.RecalculateNormals();ownedMeshes.Add(mesh);var go=new GameObject(name,typeof(MeshFilter),typeof(MeshRenderer));go.transform.SetParent(parent,false);go.transform.localPosition=p;go.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.GetComponent<Renderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=castShadows?ShadowCastingMode.On:ShadowCastingMode.Off;renderer.receiveShadows=receiveShadows;
        }
        void CombineArchitecture()
        {
            // Preserve shadow intent across batching. Millimetre-thick paving
            // overlays/ripples must not re-acquire shadow casting merely because
            // a wall uses the same limestone material.
            var groups=new Dictionary<(Material material,ShadowCastingMode casts,bool receives),List<CombineInstance>>();
            foreach(var renderer in courtyard.GetComponentsInChildren<MeshRenderer>())
            {
                var filter=renderer.GetComponent<MeshFilter>();
                if(renderer.GetComponentInParent<GospelAuthoredPropRoot>()!=null)continue;
                if(filter==null||filter.sharedMesh==null||renderer.sharedMaterial==null||renderer.sharedMaterial==leaf||renderer.sharedMaterial==foliageLight)continue;
                var key=(renderer.sharedMaterial,renderer.shadowCastingMode,renderer.receiveShadows);
                if(!groups.TryGetValue(key,out var list)){list=new List<CombineInstance>();groups.Add(key,list);}
                list.Add(new CombineInstance{mesh=renderer.sharedMaterial.GetTexture("_BaseMap")!=null?WorldUvMesh(filter.sharedMesh,renderer.transform):filter.sharedMesh,transform=courtyard.worldToLocalMatrix*renderer.transform.localToWorldMatrix});
                renderer.enabled=false;
            }
            foreach(var group in groups)
            {
                var mesh=new Mesh{name="Courtyard · "+group.Key.material.name+" · "+group.Key.casts,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(group.Value.ToArray(),true,true);ownedMeshes.Add(mesh);
                var combined=new GameObject(mesh.name,typeof(MeshFilter),typeof(MeshRenderer));combined.transform.SetParent(courtyard,false);
                combined.GetComponent<MeshFilter>().sharedMesh=mesh;var renderer=combined.GetComponent<MeshRenderer>();renderer.sharedMaterial=group.Key.material;renderer.shadowCastingMode=group.Key.casts;renderer.receiveShadows=group.Key.receives;
            }
        }
        void OnDestroy(){foreach(var material in ownedMaterials)if(material!=null)Destroy(material);foreach(var mesh in ownedMeshes)if(mesh!=null)Destroy(mesh);if(ownedProfile!=null)Destroy(ownedProfile);}
    }
}
