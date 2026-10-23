using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace PathOfDawn.Presentation
{
    public sealed class DawnShotBehaviour : PlayableBehaviour
    {
        public Vector3 from, to, lookAt;
    }
    public sealed class DawnShotAsset : PlayableAsset, ITimelineClipAsset
    {
        public Vector3 from, to, lookAt;
        public ClipCaps clipCaps => ClipCaps.Blending;
        public override Playable CreatePlayable(PlayableGraph graph, GameObject owner)
        {
            var playable = ScriptPlayable<DawnShotBehaviour>.Create(graph);
            var behaviour = playable.GetBehaviour(); behaviour.from = from; behaviour.to = to; behaviour.lookAt = lookAt;
            return playable;
        }
    }
}
