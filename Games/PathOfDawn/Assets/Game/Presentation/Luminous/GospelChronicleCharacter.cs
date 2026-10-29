using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;
using K = PathOfDawn.Core.GospelCharacterKinematics;

namespace PathOfDawn.Presentation
{
    public sealed partial class GospelLuminousActor
    {
        public bool UsesChroniclePainting { get; private set; }
        public bool UsesContinuousLimbRig { get; private set; }
        public bool UsesOriginalChildAtlas => UsesContinuousLimbRig && !adultStage;
        public float CurrentHeight => CurrentVisualHeight;
        public bool IsAdult => adultStage;
        public int ContinuousLimbCount => chronicleLimbs.Count;
        public float CurrentVisualHeight => adultStage ? 1.98f : 1.68f;
        bool adultStage;
        ChronicleLayout chronicleLayout;
        readonly List<WholeLimb> chronicleLimbs = new List<WholeLimb>();
        WholeLimb frontArm, backArm, frontLeg, backLeg;
        float chronicleHip, chronicleAnkle, farAnkle, chronicleUpperLeg, chronicleLowerLeg;
        Vector2 shoulderNear, shoulderFar, neckOffset, beltOffset;
        Vector2 smoothedNearWrist, smoothedFarWrist;
        float hemHeight;

        /// <summary>Only presentation changes. Physics position, journey and lamp equipment are retained.</summary>
        public void SetAdult(bool adult)
        {
            if (adultStage == adult && visual != null) return;
            bool held = LampEquipped;
            adultStage = adult;
            ClearOwnedRig();
            Create();
            SetLamp(held);
        }

        sealed class ChronicleLayout
        {
            public string Resource;
            public int Width, Height;
            public float HeadTop, FeetY, FarFeetY, OriginX;
            public Vector2 Hip, Neck;
            public Rect Head, Torso, Hem, NearArm, FarArm, NearLeg, FarLeg;
            public Vector2[] NearArmJoints, FarArmJoints, NearLegJoints, FarLegJoints;
            public Vector3[] NearArmCorridor, FarArmCorridor;
            public float PixelScale(float visualHeight) => visualHeight / (FeetY-HeadTop);
        }

        bool TryCreateChronicleRig()
        {
            if (!adultStage) return TryCreateOriginalChildRig();
            chronicleLayout = AdultChronicleLayout();
            if (chronicleLayout == null) return false;
            atlas = Resources.Load<Texture2D>(chronicleLayout.Resource);
            if (atlas == null) return false;
            if (atlas.width != chronicleLayout.Width || atlas.height != chronicleLayout.Height)
                throw new InvalidOperationException("Chronicle rig must use the exact measured original painting.");
            UsesChroniclePainting = UsesContinuousLimbRig = true;
            UsesRadiancePainting = false;
            var spriteShader = Resources.Load<Shader>("Art/GospelRadiance/Rendering/RadianceSprite2D");
            var meshShader = Resources.Load<Shader>("Art/GospelRadiance/Rendering/RadianceCloth2D");
            if (spriteShader == null || meshShader == null) throw new InvalidOperationException("Missing approved painted character shaders.");
            material = new Material(spriteShader) { name = "Chronicle intact painting sprites", mainTexture = atlas };
            clothMaterial = new Material(meshShader) { name = "Chronicle continuous limb skin", mainTexture = atlas };
            TextureBindings(material, atlas);TextureBindings(clothMaterial, atlas);
            float scale = chronicleLayout.PixelScale(CurrentVisualHeight);
            chronicleHip = (chronicleLayout.FeetY-chronicleLayout.Hip.y)*scale;
            Vector2 origin = new Vector2(chronicleLayout.OriginX,chronicleLayout.FeetY);
            visual = Node("Chronicle continuous painted rig",transform,Vector2.zero);
            torso = Node("Chest and robe foundation",visual,new Vector2(0,chronicleHip));
            shoulderNear = SourcePoint(chronicleLayout.NearArmJoints[0],origin,scale)-new Vector2(0,chronicleHip);
            shoulderFar = SourcePoint(chronicleLayout.FarArmJoints[0],origin,scale)-new Vector2(0,chronicleHip);
            neckOffset = SourcePoint(chronicleLayout.Neck,origin,scale)-new Vector2(0,chronicleHip);
            beltOffset = new Vector2(.12f,0);
            PaintRegion("Unified ivory torso and shoulder mantle",chronicleLayout.Torso,torso,chronicleLayout.Hip,scale,22);
            head = Node("Stable neck and whole hair silhouette",torso,neckOffset);
            PaintRegion("Jesus face and shoulder-length hair",chronicleLayout.Head,head,chronicleLayout.Neck,scale,26);
            backArm=CreateLimb("Far continuous arm",chronicleLayout.FarArm,chronicleLayout.FarArmJoints,scale,18,false);
            frontArm=CreateLimb("Near continuous arm",chronicleLayout.NearArm,chronicleLayout.NearArmJoints,scale,25,false);
            backLeg=CreateLimb("Far continuous leg and sandal",chronicleLayout.FarLeg,chronicleLayout.FarLegJoints,scale,17,true);
            frontLeg=CreateLimb("Near continuous leg and sandal",chronicleLayout.NearLeg,chronicleLayout.NearLegJoints,scale,20,true);
            chronicleUpperLeg=frontLeg.Upper;chronicleLowerLeg=frontLeg.Lower;
            chronicleAnkle=Mathf.Max(.065f,(chronicleLayout.FeetY-chronicleLayout.NearLegJoints[2].y)*scale);
            farAnkle=Mathf.Max(.065f,(chronicleLayout.FarFeetY-chronicleLayout.FarLegJoints[2].y)*scale);
            float backReach=chronicleHip-farAnkle;
            float backRatio=backReach/backLeg.Reach;
            backLeg.SetLengths(backLeg.Upper*backRatio,backLeg.Lower*backRatio);
            // One connected robe surface covers the anatomical upper legs; no stacked skirts/cuffs.
            activeParts=new[]{chronicleLayout.Head,chronicleLayout.Torso,chronicleLayout.Hem};
            hemHeight=chronicleLayout.Hem.height*scale;
            var hemTop = new Vector2(chronicleLayout.Hem.center.x,chronicleLayout.Height-chronicleLayout.Hem.yMax);
            skirtRoot=Node("Continuous robe hem",torso,SourcePoint(hemTop,chronicleLayout.Hip,scale));
            skirtMesh=Cloth("Whole ivory robe and mantle hem",2,skirtRoot,chronicleLayout.Hem.width*scale,hemHeight,21,out skirtRest);
            skirtVertices=new Vector3[skirtRest.Length];
            CreateChronicleLamp();
            BuildContactShadow();
            smoothedNearWrist=new Vector2(0,-frontArm.Reach*.96f);
            smoothedFarWrist=new Vector2(0,-backArm.Reach*.96f);
            return true;
        }

        bool TryCreateOriginalChildRig()
        {
            atlas=Resources.Load<Texture2D>("Art/GospelRadiance/Actors/jesus-parts");
            if(atlas==null)return false;
            if(atlas.width!=1254||atlas.height!=1254)throw new InvalidOperationException("The preserved child atlas must retain its measured1254px source.");
            UsesContinuousLimbRig=UsesRadiancePainting=true;UsesChroniclePainting=false;
            activeParts=RadianceParts;
            var spriteShader=Resources.Load<Shader>("Art/GospelRadiance/Rendering/RadianceSprite2D");
            var meshShader=Resources.Load<Shader>("Art/GospelRadiance/Rendering/RadianceCloth2D");
            if(spriteShader==null||meshShader==null)throw new InvalidOperationException("Missing painted character rendering shaders.");
            material=new Material(spriteShader){name="Chronicle preserved child painting"};
            clothMaterial=new Material(meshShader){name="Chronicle child continuous skin"};
            TextureBindings(material,atlas);TextureBindings(clothMaterial,atlas);
            visual=Node("Chronicle continuous painted rig",transform,Vector2.zero);
            chronicleHip=.84f;chronicleAnkle=farAnkle=.10f;chronicleUpperLeg=.38f;chronicleLowerLeg=.36f;
            torso=Node("Chest and robe foundation",visual,new Vector2(0,chronicleHip));
            shoulderNear=new Vector2(.13f,.40f);shoulderFar=new Vector2(-.12f,.40f);neckOffset=new Vector2(.012f,.52f);
            SpritePart("Unified child tunic",1,torso,new Vector2(0,.015f),.54f,new Vector2(.5f,.06f),22,.425f);
            head=Node("Stable neck and whole hair silhouette",torso,neckOffset);
            hair=SpritePart("Shoulder hair behind child face",3,head,new Vector2(-.026f,.01f),.37f,new Vector2(.5f,.12f),23,.34f);
            SpritePart("Preserved child Jesus face",0,head,Vector2.zero,.37f,new Vector2(.48f,.12f),26,.345f);
            // Cells5/7 already contain one sleeve and the whole arm. Cells4/6 are
            // intentionally omitted: the previous rig visibly doubled the cuffs.
            frontArm=new WholeLimb("Near continuous original arm",visual,atlas,RadianceParts[5],
                new[]{new Vector2(500,326),new Vector2(478,449),new Vector2(471,555)},.54f/RadianceParts[5].height,clothMaterial,25,false,.72f);
            backArm=new WholeLimb("Far continuous original arm",visual,atlas,RadianceParts[7],
                new[]{new Vector2(1088,325),new Vector2(1091,453),new Vector2(1117,555)},.54f/RadianceParts[7].height,clothMaterial,18,false,.72f);
            chronicleLimbs.Add(frontArm);chronicleLimbs.Add(backArm);
            float nearScale=.235f/RadianceParts[9].width,farScale=.235f/RadianceParts[11].width;
            frontLeg=new WholeLimb("Near continuous child leg and sandal",visual,atlas,RadianceParts[9],
                new[]{new Vector2(470,645),new Vector2(466,725),new Vector2(465,806)},nearScale,clothMaterial,20,true);
            backLeg=new WholeLimb("Far continuous child leg and sandal",visual,atlas,RadianceParts[11],
                new[]{new Vector2(1072,640),new Vector2(1076,723),new Vector2(1078,800)},farScale,clothMaterial,17,true);
            // Only the length along each bone is adjusted under the robe. Limb
            // thickness and the original sandal proportions are preserved.
            frontLeg.SetLengths(.38f,.36f,.10f/((1254-RadianceParts[9].y-806)*nearScale));
            backLeg.SetLengths(.38f,.36f,.10f/((1254-RadianceParts[11].y-800)*farScale));
            chronicleLimbs.Add(frontLeg);chronicleLimbs.Add(backLeg);
            skirtRoot=Node("Continuous child robe hem",torso,new Vector2(0,.08f));hemHeight=.78f;
            skirtMesh=Cloth("One continuous ivory child robe",2,skirtRoot,.46f,hemHeight,21,out skirtRest);
            skirtVertices=new Vector3[skirtRest.Length];
            CreateChronicleLamp();BuildContactShadow();
            smoothedNearWrist=new Vector2(0,-frontArm.Reach*.96f);smoothedFarWrist=new Vector2(0,-backArm.Reach*.96f);
            return true;
        }

        static Vector2 SourcePoint(Vector2 source,Vector2 origin,float scale)
            =>new Vector2((source.x-origin.x)*scale,(origin.y-source.y)*scale);

        void PaintRegion(string name,Rect rect,Transform parent,Vector2 sourcePivot,float scale,int order)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,false);
            go.transform.localPosition=SourcePoint(new Vector2(rect.center.x,chronicleLayout.Height-rect.center.y),sourcePivot,scale);
            var sprite=Sprite.Create(atlas,rect,new Vector2(.5f,.5f),1f/scale,0,SpriteMeshType.FullRect);
            generatedSprites.Add(sprite);
            var renderer=go.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;
            renderer.sortingOrder=order;TextureBindings(renderer,atlas);
        }

        WholeLimb CreateLimb(string name,Rect rect,Vector2[] joints,float scale,int order,bool foot)
        {
            Vector3[] corridor=foot?null:(name.StartsWith("Near",StringComparison.Ordinal)?chronicleLayout.NearArmCorridor:chronicleLayout.FarArmCorridor);
            var limb=new WholeLimb(name,visual,atlas,rect,joints,scale,clothMaterial,order,foot,1f,corridor,foot ? .05f : .105f);
            chronicleLimbs.Add(limb);return limb;
        }

        void CreateChronicleLamp()
        {
            lamp=new GameObject("Held lamp · world-upright wrist socket");lamp.transform.SetParent(visual,false);
            var source=Resources.Load<Texture2D>("Art/GospelRadiance/Actors/jesus-parts");
            if(source==null)throw new InvalidOperationException("Missing preserved oil-lamp painting.");
            var rect=RadianceParts[13];
            var sprite=Sprite.Create(source,rect,new Vector2(.22f,.17f),rect.height/.14f,0,SpriteMeshType.FullRect);
            generatedSprites.Add(sprite);var renderer=lamp.AddComponent<SpriteRenderer>();renderer.sprite=sprite;
            renderer.sharedMaterial=material;renderer.sortingOrder=29;TextureBindings(renderer,source);
            var prefab=Resources.Load<GameObject>("Art/GospelLuminous/Rendering/NormalPointLight");
            if(prefab==null)throw new InvalidOperationException("Missing authored lamp light.");
            var lightObject=Instantiate(prefab,lamp.transform,false);lightObject.transform.localPosition=new Vector3(.10f,.08f,0);
            lightObject.transform.localScale=new Vector3(1.8f,1.8f,1);lampLight=lightObject.GetComponent<Light2D>();
            lampLight.color=new Color(1,.64f,.20f);lampLight.intensity=.72f;lampLight.shadowsEnabled=false;lamp.SetActive(false);
        }

        void UpdateChronicleRig()
        {
            float dt=Mathf.Max(0,Time.deltaTime);time+=dt;
            float speed=Mathf.Abs(velocity.x),ease=1-Mathf.Exp(-12*dt);
            speedBlend=Mathf.Lerp(speedBlend,Mathf.Clamp01(speed/1.65f),ease);
            runBlend=Mathf.Lerp(runBlend,Mathf.InverseLerp(2.4f,4.1f,speed),ease);
            airBlend=Mathf.Lerp(airBlend,grounded?0:1,1-Mathf.Exp(-15*dt));
            CurrentState=!grounded?(velocity.y>.5f?"Jump":"Fall"):speed>.15f?(speed>2.8f?"Run":"Walk"):"Idle";
            float span=Mathf.Lerp(.28f,.39f,runBlend)*(adultStage?1.12f:1f);
            float stance=Mathf.Lerp(.60f,.49f,runBlend),cycleDistance=span*2/stance;
            if(grounded)phase=Mathf.Repeat(phase+speed*dt/cycleDistance*Mathf.PI*2,Mathf.PI*2);
            var a=K.Foot(phase/(Mathf.PI*2),span,stance,chronicleAnkle,Mathf.Lerp(.09f,.17f,runBlend),out float pitchA);
            var b=K.Foot(phase/(Mathf.PI*2)+.5f,span,stance,farAnkle,Mathf.Lerp(.09f,.17f,runBlend),out float pitchB);
            Vector2 near=Vector2.Lerp(new Vector2(adultStage ? .10f : .075f,chronicleAnkle),new Vector2(a.X,a.Y),speedBlend);
            Vector2 far=Vector2.Lerp(new Vector2(adultStage ? -.085f : -.065f,farAnkle),new Vector2(b.X,b.Y),speedBlend);
            near.y+=near.x*groundSlope*facing;far.y+=far.x*groundSlope*facing;
            float hip=Mathf.Min(chronicleHip,Mathf.Min(ReachHip(near,.025f,frontLeg.Reach*.99f),ReachHip(far,-.025f,backLeg.Reach*.99f)));
            hip=Mathf.Lerp(hip,chronicleHip-.03f,airBlend);pelvisOffset=hip-chronicleHip;
            near=Vector2.Lerp(near,new Vector2(.15f,.22f),airBlend);
            far=Vector2.Lerp(far,new Vector2(-.13f,.18f),airBlend);
            frontLeg.Pose(K.Solve(new K.Point(.025f,hip),Point(near),frontLeg.Upper,frontLeg.Lower,1),pitchA*speedBlend+Mathf.Atan(groundSlope)*Mathf.Rad2Deg*facing);
            backLeg.Pose(K.Solve(new K.Point(-.025f,hip),Point(far),backLeg.Upper,backLeg.Lower,1),pitchB*speedBlend+Mathf.Atan(groundSlope)*Mathf.Rad2Deg*facing);
            float breathe=Mathf.Sin(time*1.5f),swing=Mathf.Cos(phase)*speedBlend;
            torso.localPosition=new Vector3(0,hip+breathe*.0028f,0);
            torsoAngle=Mathf.Lerp(torsoAngle,Mathf.Lerp(-1.5f,-5.8f,runBlend)*speedBlend,ease);
            torso.localRotation=Quaternion.Euler(0,0,torsoAngle);
            visual.localScale=new Vector3(facing,1,1);
            SmoothAngle(head,-torsoAngle*.75f+breathe*.22f,12,dt);
            if(hair!=null)SmoothAngle(hair,Mathf.Sin(time*.9f)*.5f,8,dt);
            float armTravel=Mathf.Lerp(.105f,.19f,runBlend)*(adultStage?1.12f:1f);
            Vector2 nearWrist=new Vector2(-swing*armTravel+.025f,-frontArm.Reach*.95f+runBlend*.10f*(1-swing)*.5f);
            Vector2 farWrist=new Vector2(swing*armTravel-.015f,-backArm.Reach*.95f+runBlend*.10f*(1+swing)*.5f);
            nearWrist=Vector2.Lerp(nearWrist,new Vector2(.14f,-frontArm.Reach*.65f),airBlend);
            farWrist=Vector2.Lerp(farWrist,new Vector2(-.11f,-backArm.Reach*.70f),airBlend);
            if(LampEquipped)nearWrist=adultStage?new Vector2(.17f,-frontArm.Reach*.82f):new Vector2(.19f,-frontArm.Reach*.66f);
            smoothedNearWrist=Vector2.Lerp(smoothedNearWrist,nearWrist,ease);
            smoothedFarWrist=Vector2.Lerp(smoothedFarWrist,farWrist,ease);
            Vector2 nearRoot=TorsoPoint(shoulderNear),farRoot=TorsoPoint(shoulderFar);
            var nearChain=K.Solve(Point(nearRoot),Point(nearRoot+smoothedNearWrist),frontArm.Upper,frontArm.Lower,-1);
            var farChain=K.Solve(Point(farRoot),Point(farRoot+smoothedFarWrist),backArm.Upper,backArm.Lower,-1);
            frontArm.Pose(nearChain,LampEquipped?0:nearChain.LowerDegrees);
            backArm.Pose(farChain,farChain.LowerDegrees);
            if(lamp!=null){lamp.transform.localPosition=new Vector3(nearChain.Tip.X+.055f,nearChain.Tip.Y+.025f,0);lamp.transform.localRotation=Quaternion.identity;}
            if(lampLight!=null)lampLight.intensity=.72f+Mathf.Sin(time*13)*.035f;
            if(skirtMesh!=null)Deform(skirtMesh,skirtRest,skirtVertices,hemHeight,.006f,.024f,false);
            UpdateChronicleShadow();
        }

        static float ReachHip(Vector2 ankle,float hipX,float reach)
        {float x=ankle.x-hipX;return ankle.y+Mathf.Sqrt(Mathf.Max(.01f,reach*reach-x*x));}
        static K.Point Point(Vector2 value)=>new K.Point(value.x,value.y);
        Vector2 TorsoPoint(Vector2 local)=> (Vector2)torso.localPosition+(Vector2)(torso.localRotation*(Vector3)local);
        void UpdateChronicleShadow()
        {
            if(contactShadow==null)return;
            float ground=world!=null?world.HeightAt(transform.position.x):transform.position.y;
            float altitude=Mathf.Max(0,transform.position.y-ground),spread=1+Mathf.Min(1,altitude)*.35f;
            contactShadow.transform.position=new Vector3(transform.position.x,ground+.035f,0);
            contactShadow.transform.localScale=new Vector3(.56f*spread/contactShadow.sprite.bounds.size.x,.08f*spread/contactShadow.sprite.bounds.size.y,1);
            contactShadow.color=new Color(.015f,.027f,.045f,.45f/(1+altitude*1.3f));
        }

        void ClearOwnedRig()
        {
            if(visual!=null){visual.gameObject.SetActive(false);Destroy(visual.gameObject);}
            if(contactShadow!=null)Destroy(contactShadow.gameObject);
            DestroyChronicleMeshes();
            foreach(var sprite in generatedSprites)if(sprite!=null)Destroy(sprite);generatedSprites.Clear();
            if(skirtMesh!=null)Destroy(skirtMesh);if(sashMesh!=null)Destroy(sashMesh);
            if(material!=null)Destroy(material);if(clothMaterial!=null)Destroy(clothMaterial);if(shadowMaterial!=null)Destroy(shadowMaterial);
            visual=null;contactShadow=null;skirtMesh=sashMesh=null;material=clothMaterial=shadowMaterial=null;UsesChroniclePainting=UsesContinuousLimbRig=false;
        }
        void DestroyChronicleMeshes(){foreach(var limb in chronicleLimbs)limb.Dispose();chronicleLimbs.Clear();}

        sealed class WholeLimb
        {
            readonly Mesh mesh;
            readonly Vector3[] output;
            readonly K.Point[] rest;
            K.Point[] stretchedUpper, stretchedLower;
            float tipScale = 1f;
            readonly K.Weights[] weights;
            readonly K.Point bindRoot,bindJoint,bindTip;
            readonly float bindUpperAngle,bindLowerAngle,bindTipAngle;
            readonly float bindUpperLength,bindLowerLength;
            public float Upper{get;private set;}public float Lower{get;private set;}public float Reach=>Upper+Lower;
            public WholeLimb(string name,Transform parent,Texture2D atlas,Rect rect,Vector2[] sourceJoints,float scale,Material material,int order,bool foot,float horizontalScale=1f,Vector3[] corridor=null,float jointBlendFraction=.05f)
            {
                const int cols=6,rows=32;
                var go=new GameObject(name);go.transform.SetParent(parent,false);
                var origin=sourceJoints[0];
                bindRoot=new K.Point(0,0);
                var j=SourcePoint(sourceJoints[1],origin,scale);var t=SourcePoint(sourceJoints[2],origin,scale);j.x*=horizontalScale;t.x*=horizontalScale;
                bindJoint=Point(j);bindTip=Point(t);
                bindUpperLength=Upper=bindJoint.Length;bindLowerLength=Lower=(bindTip-bindJoint).Length;
                bindUpperAngle=Mathf.Atan2(j.y,j.x)*Mathf.Rad2Deg;bindLowerAngle=Mathf.Atan2(t.y-j.y,t.x-j.x)*Mathf.Rad2Deg;
                bindTipAngle=foot?0:bindLowerAngle;
                int count=(cols+1)*(rows+1);rest=new K.Point[count];weights=new K.Weights[count];output=new Vector3[count];
                var uv=new Vector2[count];var colors=new Color[count];var normals=new Vector3[count];var tangents=new Vector4[count];
                float top=atlas.height-rect.yMax;
                for(int y=0;y<=rows;y++)for(int x=0;x<=cols;x++)
                {
                    int k=y*(cols+1)+x;float u=x/(float)cols,v=y/(float)rows;
                    float sourceY=top+v*rect.height;
                    Vector2 limits=CorridorAt(corridor,sourceY,rect.x,rect.xMax);
                    Vector2 source=new Vector2(Mathf.Lerp(limits.x,limits.y,u),sourceY),p=SourcePoint(source,origin,scale);
                    p.x*=horizontalScale;rest[k]=Point(p);output[k]=p;uv[k]=new Vector2(source.x/atlas.width,1-source.y/atlas.height);
                    float down=source.y-origin.y,first=sourceJoints[1].y-origin.y,second=sourceJoints[2].y-origin.y;
                    weights[k]=K.BindWeights(down,first,second,Mathf.Max(8,rect.height*jointBlendFraction));
                    colors[k]=Color.white;normals[k]=Vector3.back;tangents[k]=new Vector4(1,0,0,-1);
                }
                var triangles=new int[cols*rows*6];int at=0;
                for(int y=0;y<rows;y++)for(int x=0;x<cols;x++)
                {int k=y*(cols+1)+x;triangles[at++]=k;triangles[at++]=k+1;triangles[at++]=k+cols+1;triangles[at++]=k+1;triangles[at++]=k+cols+2;triangles[at++]=k+cols+1;}
                mesh=new Mesh{name=name+" · continuous weighted surface"};mesh.vertices=output;mesh.uv=uv;mesh.colors=colors;mesh.normals=normals;mesh.tangents=tangents;mesh.triangles=triangles;mesh.MarkDynamic();mesh.RecalculateBounds();
                go.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=go.AddComponent<MeshRenderer>();renderer.sharedMaterial=material;renderer.sortingOrder=order;TextureBindings(renderer,atlas);
            }
            static Vector2 CorridorAt(Vector3[] corridor,float sourceY,float left,float right)
            {
                if(corridor==null||corridor.Length==0)return new Vector2(left,right);
                if(sourceY<=corridor[0].x)return new Vector2(corridor[0].y,corridor[0].z);
                for(int i=1;i<corridor.Length;i++)
                    if(sourceY<=corridor[i].x)
                    {
                        float t=Mathf.InverseLerp(corridor[i-1].x,corridor[i].x,sourceY);
                        return new Vector2(Mathf.Lerp(corridor[i-1].y,corridor[i].y,t),Mathf.Lerp(corridor[i-1].z,corridor[i].z,t));
                    }
                var last=corridor[corridor.Length-1];return new Vector2(last.y,last.z);
            }
            public void SetLengths(float upper,float lower,float endScale=1f)
            {
                Upper=upper;Lower=lower;tipScale=endScale;
                stretchedUpper=new K.Point[rest.Length];stretchedLower=new K.Point[rest.Length];
                for(int i=0;i<rest.Length;i++)
                {
                    stretchedUpper[i]=Along(rest[i],bindRoot,bindJoint-bindRoot,upper/bindUpperLength);
                    stretchedLower[i]=Along(rest[i],bindJoint,bindTip-bindJoint,lower/bindLowerLength);
                }
            }
            static K.Point Along(K.Point value,K.Point origin,K.Point axis,float scale)
            {
                var direction=axis*(1f/axis.Length);var delta=value-origin;
                float projection=delta.X*direction.X+delta.Y*direction.Y;
                return value+direction*(projection*(scale-1));
            }
            public void Pose(K.Chain chain,float tipAngle)
            {
                var upper=new K.SkinTransform(bindRoot,bindUpperAngle,chain.Root,chain.UpperDegrees);
                var lower=new K.SkinTransform(bindJoint,bindLowerAngle,chain.Joint,chain.LowerDegrees);
                var tip=new K.SkinTransform(bindTip,bindTipAngle,chain.Tip,tipAngle,tipScale);
                for(int i=0;i<rest.Length;i++)
                {
                    K.Point p=stretchedUpper==null?K.Skin(rest[i],weights[i],upper,lower,tip):
                        upper.Apply(stretchedUpper[i])*weights[i].Root+lower.Apply(stretchedLower[i])*weights[i].Joint+tip.Apply(rest[i])*weights[i].Tip;
                    output[i]=new Vector3(p.X,p.Y,0);
                }
                mesh.vertices=output;mesh.RecalculateBounds();
            }
            public void Dispose(){if(mesh!=null)UnityEngine.Object.Destroy(mesh);}
        }

        static ChronicleLayout AdultChronicleLayout()=>new ChronicleLayout {
            Resource="Art/GospelChronicle/Actors/jesus-adult",Width=1024,Height=1536,
            HeadTop=16,FeetY=1495,FarFeetY=1442,OriginX=510,Hip=new Vector2(510,740),Neck=new Vector2(548,245),
            Head=new Rect(398,1288,252,235),Torso=new Rect(382,906,328,392),Hem=new Rect(305,308,421,638),
            NearArm=new Rect(228,696,177,560),FarArm=new Rect(645,696,155,514),
            NearLeg=new Rect(320,38,137,768),FarLeg=new Rect(515,86,210,720),
            NearArmJoints=new[]{new Vector2(391,289),new Vector2(305,547),new Vector2(307,724)},
            FarArmJoints=new[]{new Vector2(654,326),new Vector2(723,590),new Vector2(759,746)},
            NearLegJoints=new[]{new Vector2(401,740),new Vector2(382,1040),new Vector2(368,1389)},
            FarLegJoints=new[]{new Vector2(552,740),new Vector2(560,1040),new Vector2(571,1365)},
            // These are UV geometry limits, not edits to source pixels. A plain
            // rectangle also captured the dark mantle/background beside each hand.
            NearArmCorridor=new[]{new Vector3(280,330,405),new Vector3(390,300,405),
                new Vector3(540,250,393),new Vector3(610,228,386),new Vector3(650,252,374),
                new Vector3(670,275,374),new Vector3(700,276,329),new Vector3(740,273,348),
                new Vector3(790,273,359),new Vector3(820,306,344),new Vector3(840,325,340)},
            FarArmCorridor=new[]{new Vector3(326,645,710),new Vector3(480,680,755),
                new Vector3(610,697,779),new Vector3(660,710,785),new Vector3(700,729,790),
                new Vector3(750,738,798),new Vector3(800,735,798),new Vector3(840,744,789)}
        };
    }
}
