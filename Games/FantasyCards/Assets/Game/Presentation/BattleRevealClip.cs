using UnityEngine;
using UnityEngine.Playables;

namespace FantasyCards.Presentation
{
    public sealed class BattleRevealClip : PlayableAsset
    {
        public ExposedReference<Transform> cameraTransform;
        public Vector3 from = new Vector3(0, 13, -15);
        public Vector3 to = new Vector3(0, 11, -13);
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<BattleRevealBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour(); behaviour.target = cameraTransform.Resolve(graph.GetResolver()); behaviour.from = from; behaviour.to = to; return playable;
        }
    }
    public sealed class BattleRevealBehaviour : PlayableBehaviour
    {
        public Transform target;
        public Vector3 from, to;
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (target == null) return;
            float duration = (float)playable.GetDuration(); float t = duration <= 0 ? 1f : Mathf.Clamp01((float)playable.GetTime() / duration);
            target.position = Vector3.Lerp(from, to, Mathf.SmoothStep(0, 1, t)); target.LookAt(new Vector3(0, 0, 1));
        }
    }
}
