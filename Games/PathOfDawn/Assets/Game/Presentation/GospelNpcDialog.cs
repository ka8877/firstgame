using System.Collections.Generic;
using PathOfDawn.Core;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace PathOfDawn.Presentation
{
    public sealed partial class GospelJourneyApp
    {
        string conversationNpcId,conversationTopicId;
        int conversationTurn;
        IReadOnlyList<GospelDialogueTurn> conversationTurns;
        GospelNpcAnchor[] npcAnchors;
        public string ActiveNpcId=>conversationNpcId;
        public string ActiveNpcTopic=>conversationTopicId;
        public int ActiveNpcTurn=>conversationTurn;
        public bool IsNpcConversation=>!string.IsNullOrEmpty(conversationNpcId);

        GospelNpcAnchor FindNearbyNpc()
        {
            if(World==null||Player==null)return null;
            GospelNpcAnchor nearest=null;float best=InteractionRadius;
            if(npcAnchors==null)npcAnchors=World.GetComponentsInChildren<GospelNpcAnchor>(false);
            foreach(var npc in npcAnchors)
            {
                if(npc==null||!npc.gameObject.activeInHierarchy)continue;
                float distance=Vector3.Distance(Player.transform.position,npc.transform.position);
                if(distance<best){best=distance;nearest=npc;}
            }
            return nearest;
        }
        void TryClickNpc()
        {
            if(modal||Mouse.current==null||!Mouse.current.leftButton.wasPressedThisFrame||TouchInterface)return;
            if(EventSystem.current!=null&&EventSystem.current.IsPointerOverGameObject())return;
            if(!Physics.Raycast(World.View.ScreenPointToRay(Mouse.current.position.ReadValue()),out var hit,30,~(1<<8)))return;
            var npc=hit.collider.GetComponentInParent<GospelNpcAnchor>();if(npc==null)return;
            if(Vector3.Distance(Player.transform.position,npc.transform.position)>=InteractionRadius){Toast(npc.DisplayName+"에게 가까이 다가가 대화해 보세요.");return;}
            OpenNpcConversation(npc.NpcId);
        }
        void ClearNpcConversation()
        {
            if(!string.IsNullOrEmpty(conversationNpcId)&&World!=null){var npc=FindNpc(conversationNpcId);if(npc!=null)npc.FaceConversationPartner(null);}
            conversationNpcId=null;conversationTopicId=null;conversationTurn=0;conversationTurns=null;
        }
        public void OpenNpcConversation(string npcId)
        {
            if(Journey==null||Journey.Phase!=GospelPhase.Exploring||modal)return;
            var npc=FindNpc(npcId);
            if(npc==null||Vector3.Distance(Player.transform.position,npc.transform.position)>=InteractionRadius){Toast("인물에게 가까이 다가가 주세요.");return;}
            conversationNpcId=npcId;conversationTopicId=null;conversationTurn=0;npc.FaceConversationPartner(Player.transform);ShowNpcTopics();
        }
        GospelNpcAnchor FindNpc(string id)
        {foreach(var npc in World.GetComponentsInChildren<GospelNpcAnchor>(false))if(npc.NpcId==id)return npc;return null;}
        void NpcModal()
        {modal=true;Player.InputEnabled=false;Player.StopInput();if(hud!=null)hud.gameObject.SetActive(false);ClearScreen();}
        void ShowNpcTopics()
        {
            conversationTopicId=null;conversationTurn=0;conversationTurns=null;
            var npc=GospelNpcCatalog.Get(conversationNpcId);NpcModal();
            Heading("CONVERSATION · "+npc.Role,npc.Name,"대화 주제를 선택하세요. 해설과 실제 성경 본문을 함께 읽습니다.");
            var portraitGround=ui.Card(screen,"NPC topic portrait",48,256,264,324,new Color(.88f,.89f,.83f,.95f));
            var portrait=ui.Art(portraitGround,"Art/GospelRemaster/portraits",8,8,248,248,false);if(portrait!=null)portrait.uvRect=PortraitUv(npc.Portrait);
            Text(portraitGround,npc.Role,14,264,236,42,21,DawnUi.ModernInk);
            Text(screen,npc.Greeting,340,250,870,86,24,DawnUi.ModernPaper);
            float y=354;
            if(GospelNpcCatalog.CanGuide(npc.Id,Journey.CurrentObjective?.Id))
            {
                Primary(screen,"여정 이어가기 · "+Journey.CurrentObjective.Title,340,y,866,64,BeginNpcStory);y+=80;
            }
            foreach(var topic in npc.Topics)
            {string topicId=topic.Id;Button(screen,topic.Title,340,y,866,60,()=>OpenNpcTopic(topicId));y+=74;}
            Button(screen,"대화 마치기",952,640,280,58,ResumeJourney);
            Text(screen,"학습 대화의 해설은 성경 인용과 구분해 표시합니다.",48,608,850,34,21,DawnUi.ModernGold);
        }
        void BeginNpcStory()
        {
            if(!GospelNpcCatalog.CanGuide(conversationNpcId,Journey.CurrentObjective?.Id))return;
            string npc=conversationNpcId;
            ClearNpcConversation();
            if(GospelNpcCatalog.TryBeginStory(Journey,npc,out var result)){Save(false);ShowReading(false);}else{Toast(result);ResumeJourney();}
        }
        void OpenNpcTopic(string topicId)
        {conversationTopicId=topicId;conversationTurn=0;conversationTurns=GospelNpcCatalog.ForTopic(conversationNpcId,topicId);ShowNpcTopicTurn();}
        void ShowNpcTopicTurn()
        {
            var npc=GospelNpcCatalog.Get(conversationNpcId);var topic=GospelNpcCatalog.GetTopic(npc.Id,conversationTopicId);
            var reading=GospelCatalog.GetReading(topic.ReadingId);var turn=conversationTurns[conversationTurn];NpcModal();
            Text(screen,npc.Name+"와의 대화 · "+topic.Title,36,28,1200,58,34,DawnUi.ModernPaper);
            Text(screen,"학습 대화 · 주제 "+(conversationTurn+1)+" / "+conversationTurns.Count,40,94,1180,34,21,DawnUi.ModernGold);
            var dialogue=ui.Card(screen,"NPC study dialogue",36,150,1208,288,new Color(.97f,.96f,.92f,.98f));
            var portrait=ui.Art(dialogue,"Art/GospelRemaster/portraits",16,15,258,258,false);if(portrait!=null)portrait.uvRect=PortraitUv(turn.PortraitKey);
            Text(dialogue,turn.Speaker,300,22,850,43,32,DawnUi.ModernInk);
            Text(dialogue,turn.Label,300,76,850,32,20,new Color(.22f,.43f,.42f));
            ScrollText(dialogue,"NPC 대화 내용",turn.Text,300,128,866,136,27,DawnUi.ModernInk);
            var scripture=ui.Card(screen,"NPC linked Scripture",36,456,1208,228,new Color(.075f,.105f,.12f,.97f));
            Text(scripture,reading.Reference+" / "+reading.Translation,26,12,1148,38,25,DawnUi.ModernGold);
            ScrollText(scripture,"NPC 성경 본문",reading.Text,26,58,1148,96,24,DawnUi.ModernPaper);
            Text(scripture,GospelCatalog.Attribution,26,176,750,32,20,new Color(.68f,.74f,.72f));
            Button(scripture,"다른 주제",660,166,236,48,ShowNpcTopics);
            bool more=conversationTurn+1<conversationTurns.Count;
            Primary(scripture,more?"다음 대화":"대화 마치기",912,166,264,48,()=>{if(more){conversationTurn++;ShowNpcTopicTurn();}else ResumeJourney();});
        }
    }
}
