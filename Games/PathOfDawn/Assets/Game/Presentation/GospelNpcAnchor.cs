using PathOfDawn.Core;
using UnityEngine;

namespace PathOfDawn.Presentation
{
    /// <summary>Identity on an actual world person; no narrative or save state lives here.</summary>
    public sealed class GospelNpcAnchor : MonoBehaviour
    {
        [SerializeField] string npcId;
        Transform conversationTarget;
        public string NpcId=>npcId;
        public string DisplayName=>GospelNpcCatalog.Get(npcId).Name;
        public string PortraitKey=>GospelNpcCatalog.Get(npcId).Portrait;
        public void FaceConversationPartner(Transform partner){conversationTarget=partner;}
        void LateUpdate()
        {
            if(conversationTarget==null)return;
            Vector3 direction=conversationTarget.position-transform.position;direction.y=0;
            if(direction.sqrMagnitude>.01f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),160*Time.deltaTime);
        }
        public static GospelNpcAnchor Attach(GameObject actor,string id)
        {
            GospelNpcCatalog.Get(id);
            var anchor=actor.GetComponent<GospelNpcAnchor>();if(anchor==null)anchor=actor.AddComponent<GospelNpcAnchor>();
            anchor.npcId=id;return anchor;
        }
    }
}
