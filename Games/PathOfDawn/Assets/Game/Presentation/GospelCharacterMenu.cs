using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    public sealed partial class GospelJourneyApp
    {
        string characterTab="character",selectedBelonging="travel-lamp";
        GameObject utilityVisual;
        Transform utilityHand;
        public string CharacterMenuTab => statusVisible?characterTab:"";
        public string SelectedBelongingId => selectedBelonging;
        public bool UtilityLightVisible => utilityVisual!=null&&utilityVisual.activeInHierarchy;

        void ShowCharacterMenu(string tab)
        {
            if(Journey==null)return;
            modal=true;Player.InputEnabled=false;ClearScreen();statusVisible=true;characterTab=tab;
            if(hud!=null)hud.gameObject.SetActive(false);
            ui.Fill(screen,"Character menu veil",new Color(.025f,.045f,.065f,.93f));
            Text(screen,"JOURNEY COMPANION",42,24,980,28,23,DawnUi.ModernGold).characterSpacing=3;
            Text(screen,tab=="inventory"?"여행 가방":tab=="map"?"성전의 뜰":tab=="journey"?"걸어온 말씀":"예수님 · 성전의 소년",40,66,1120,57,38);
            string[] ids={"character","inventory","map","journey"},labels={"인물","소지품","안내도","여정"};
            for(int i=0;i<ids.Length;i++)
            {
                string target=ids[i];var b=Button(screen,labels[i],42+i*298,138,280,54,()=>ShowCharacterMenu(target));
                if(target==tab){b.GetComponent<Image>().color=DawnUi.ModernGold;b.GetComponentInChildren<TextMeshProUGUI>().color=DawnUi.ModernInk;}
            }
            switch(tab){case "inventory":BuildBelongings();break;case "map":BuildCourtyardMap();break;case "journey":BuildJourneySummary();break;default:BuildCharacterStatus();break;}
            Text(screen,"I 인물 · B 가방 · M 안내도 · Esc 닫기",44,668,750,30,23,new Color(.69f,.77f,.79f));
            Button(screen,"저장",846,644,170,56,()=>Save());Primary(screen,"여정으로",1032,644,204,56,ResumeJourney);
        }

        void BuildCharacterStatus()
        {
            var portraitCard=ui.Card(screen,"Character portrait panel",42,212,330,414,new Color(.79f,.84f,.82f));
            var portrait=ui.Art(portraitCard,"Art/GospelRemaster/portraits",12,8,306,300,false);if(portrait!=null)portrait.uvRect=PortraitUv("jesus");
            Text(portraitCard,"열두 살 · 예루살렘",22,326,292,34,25,DawnUi.ModernInk);
            Text(portraitCard,"누가복음 2:41–52",22,370,292,30,23,new Color(.24f,.39f,.40f));
            var detail=ui.Card(screen,"Character status",394,212,842,414);
            Text(detail,"현재 여정",24,18,790,30,23,DawnUi.ModernGold);
            Text(detail,Journey.CurrentObjective?.Title??"첫 여정 완료",24,59,790,43,32);
            Text(detail,Journey.CurrentReading.Reference+" · "+Journey.CurrentReading.Translation,24,116,790,34,24,new Color(.73f,.81f,.81f));
            StatusValue(detail,"의복","아마포 튜닉",24,180,380);
            StatusValue(detail,"신발","가죽 샌들",426,180,390);
            StatusValue(detail,"여행 도구",Journey.LampEquipped?"토기 등잔 · 켜짐":"토기 등잔 · 보관 중",24,260,380);
            StatusValue(detail,"말씀 기록",Journey.CollectedReadings.Count+" / "+(GospelCatalog.PrologueReadings.Count+GospelCatalog.Objectives.Count)+" 구간",426,260,390);
            Button(detail,"소지품 살펴보기",24,348,380,48,()=>ShowCharacterMenu("inventory"));
            Button(detail,"말씀 기록 열기",426,348,390,48,ShowJournal);
        }
        void StatusValue(Transform parent,string label,string value,float x,float y,float width)
        {
            Text(parent,label,x,y,width,28,23,DawnUi.ModernGold);
            var text=Text(parent,value,x,y+32,width,34,25);text.overflowMode=TextOverflowModes.Ellipsis;
        }

        void BuildBelongings()
        {
            for(int i=0;i<GospelBelongings.All.Count;i++)
            {
                var item=GospelBelongings.All[i];string id=item.Id;
                float x=42+(i%3)*228,y=212+(i/3)*174;
                var b=Button(screen,"",x,y,212,158,()=>{selectedBelonging=id;ShowCharacterMenu("inventory");});
                b.gameObject.name="Inventory item · "+id;
                b.GetComponent<Image>().color=id==selectedBelonging?new Color(.22f,.32f,.36f):new Color(.085f,.14f,.18f);
                var icon=ui.Box(b.transform,"Item icon",72,14,68,68).gameObject.AddComponent<GospelBelongingIcon>();icon.ItemId=id;icon.color=DawnUi.ModernGold;icon.raycastTarget=false;
                var label=Text(b.transform,item.Name,8,92,196,34,24);label.alignment=TextAlignmentOptions.Center;label.raycastTarget=false;
                string state=item.Id==GospelBelongings.LampId?(Journey.LampEquipped?"장착 중":"도구"):i<2?"착용 중":"소지 중";
                var stateLabel=Text(b.transform,state,8,129,196,26,23,new Color(.64f,.77f,.79f));stateLabel.alignment=TextAlignmentOptions.Center;stateLabel.raycastTarget=false;
            }
            var selected=GospelBelongings.Find(selectedBelonging);
            var panel=ui.Card(screen,"Selected belonging",742,212,494,414,new Color(.11f,.17f,.20f));
            Text(panel,selected.Category,24,18,446,30,23,DawnUi.ModernGold);
            Text(panel,selected.Name,24,62,446,48,34);
            ScrollText(panel,"Item description",selected.Description+"\n\n"+selected.Detail,24,126,446,194,24,DawnUi.ModernPaper);
            if(selected.Id==GospelBelongings.LampId)
                Primary(panel,Journey.LampEquipped?"등잔 끄고 보관":"등잔 켜고 장착",24,342,446,54,()=>{Journey.SetUtility(Journey.LampEquipped?"":GospelBelongings.LampId);Save(false);SyncUtilityVisual();ShowCharacterMenu("inventory");});
            else if(selected.Id=="reading-notes")Primary(panel,"말씀 기록 열기",24,342,446,54,ShowJournal);
            else if(selected.Id=="courtyard-map")Primary(panel,"안내도 펼치기",24,342,446,54,()=>ShowCharacterMenu("map"));
            else Text(panel,"여행에 함께하는 소지품",24,350,446,32,23,DawnUi.ModernGold);
            Text(screen,"소지품과 장착 연출은 학습용 게임 설정입니다.",44,576,672,58,23,new Color(.69f,.77f,.79f));
        }

        void BuildCourtyardMap()
        {
            var panel=ui.Card(screen,"Courtyard map",42,212,720,414,new Color(.82f,.85f,.78f));
            var map=ui.Box(panel,"Map drawing",120,18,480,360);
            // World x,z are mapped directly; north is the temple and top of the diagram.
            MapRect(map,"Courtyard outline",new Vector2(0,0),new Vector2(30,30),new Color(.25f,.38f,.35f,.15f));
            MapRect(map,"Sanctuary",new Vector2(0,13),new Vector2(18,5),new Color(.29f,.39f,.36f,.60f));
            MapRect(map,"West colonnade",new Vector2(-12,0),new Vector2(3,24),new Color(.50f,.56f,.47f,.65f));
            MapRect(map,"East colonnade",new Vector2(12,0),new Vector2(3,24),new Color(.50f,.56f,.47f,.65f));
            foreach(var anchor in World.GetComponentsInChildren<GospelNpcAnchor>())
                MapDot(map,"NPC · "+anchor.NpcId,anchor.transform.position,new Color(.40f,.49f,.39f),10);
            if(Journey.CurrentObjective!=null)MapDot(map,"Current objective",World.LandmarkPosition(Journey.CurrentObjective.Id),new Color(.72f,.44f,.12f),20);
            MapDot(map,"Player position",Player.transform.position,new Color(.12f,.42f,.56f),16);
            Text(panel,"북쪽 · 성전",250,18,240,32,24,DawnUi.ModernInk).alignment=TextAlignmentOptions.Center;
            var info=ui.Card(screen,"Map legend",784,212,452,414);
            Text(info,"현재 위치",24,18,404,30,23,DawnUi.ModernGold);
            Text(info,"예루살렘 · 성전의 뜰",24,62,404,70,29);
            Text(info,"파랑  ·  나의 위치\n금색  ·  다음 말씀\n녹색  ·  대화할 인물",24,150,404,126,25);
            string next=Journey.CurrentObjective!=null?"다음 장소까지 "+Mathf.CeilToInt(Vector3.Distance(Player.transform.position,World.LandmarkPosition(Journey.CurrentObjective.Id)))+" m":"성전의 여정을 마쳤습니다.";
            Text(info,next,24,298,404,42,25,DawnUi.ModernGold);
            Text(info,"이동을 돕는 게임 공간 안내도",24,360,404,34,23,new Color(.68f,.77f,.78f));
        }
        Vector2 MapPosition(Vector3 world)=>new Vector2((world.x+16)*15,(16-world.z)*10.8f);
        void MapDot(Transform parent,string name,Vector3 world,Color color,float size)
        {var point=MapPosition(world);ui.Card(parent,name,point.x-size/2,point.y-size/2,size,size,color);}
        void MapRect(Transform parent,string name,Vector2 center,Vector2 size,Color color)
        {var point=MapPosition(new Vector3(center.x,0,center.y));ui.Card(parent,name,point.x-size.x*7.5f,point.y-size.y*5.4f,size.x*15,size.y*10.8f,color);}

        void BuildJourneySummary()
        {
            var panel=ui.Card(screen,"Journey progress",42,212,1194,414);
            Text(panel,Journey.ProgressText,24,18,1146,40,30,DawnUi.ModernGold);
            float completed=Journey.CollectedReadings.Count,total=GospelCatalog.PrologueReadings.Count+GospelCatalog.Objectives.Count;
            ui.Card(panel,"Progress track",24,84,1146,8,new Color(.25f,.35f,.38f));
            if(completed>0)ui.Card(panel,"Read progress",24,84,1146*completed/total,8,DawnUi.ModernGold);
            Text(panel,"읽은 말씀  "+completed+" / "+total+" 구간",24,112,1146,34,25);
            Text(panel,"지금 따라가는 말씀",24,178,1146,30,23,DawnUi.ModernGold);
            Text(panel,Journey.CurrentReading.Reference+" · "+Journey.CurrentReading.Title,24,220,1146,48,30);
            Text(panel,"현재 체험 구간  ·  탄생의 말씀 → 열두 살 예수님의 성전 방문",24,287,1146,40,24,new Color(.72f,.80f,.80f));
            Button(panel,"읽은 말씀 다시 보기",24,348,548,48,ShowJournal);Button(panel,"전체 여정 살펴보기",598,348,572,48,ShowChapters);
        }

        void SyncUtilityVisual()
        {
            bool visible=inGame&&Journey!=null&&Journey.LampEquipped&&Journey.Phase!=GospelPhase.Prologue;
            if(visible&&utilityVisual==null&&Player!=null)
            {
                utilityVisual=new GameObject("Equipped travel lamp");
                utilityVisual.transform.SetParent(Player.transform,false);
                foreach(var bone in Player.Animator.GetComponentsInChildren<Transform>())if(bone.name=="hand_r"){utilityHand=bone;break;}
                if(utilityHand==null)throw new System.InvalidOperationException("The authored hand_r joint is required to hold the travel lamp.");
                World.GetComponent<GospelAuthoredProps>().AddTravelLamp(utilityVisual.transform,Vector3.zero);
                var lightRoot=new GameObject("Oil lamp warm light",typeof(Light));lightRoot.transform.SetParent(utilityVisual.transform,false);lightRoot.transform.localPosition=new Vector3(.05f,.10f,0);
                var lamp=lightRoot.GetComponent<Light>();lamp.type=LightType.Point;lamp.color=new Color(1,.65f,.29f);lamp.intensity=.8f;lamp.range=2.6f;lamp.shadows=LightShadows.None;
            }
            if(utilityVisual!=null)utilityVisual.SetActive(visible);
        }
        void LateUpdate()
        {
            if(utilityVisual==null||!utilityVisual.activeSelf||utilityHand==null)return;
            // Follow the actual animated hand while keeping an open oil bowl
            // upright; it must not hover at a fixed point ahead of the torso.
            utilityVisual.transform.position=utilityHand.TransformPoint(Vector3.up*.06f)+Player.transform.forward*.02f-Vector3.up*.025f;
            utilityVisual.transform.rotation=Player.transform.rotation;
        }
    }
}
