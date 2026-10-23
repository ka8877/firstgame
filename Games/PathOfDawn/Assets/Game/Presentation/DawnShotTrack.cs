using UnityEngine;
using UnityEngine.Playables;
using UnityEngine.Timeline;

namespace PathOfDawn.Presentation
{
    public sealed class DawnShotMixer : PlayableBehaviour
    {
        public override void ProcessFrame(Playable playable, FrameData info, object playerData)
        {
            if (!(playerData is Transform cameraTransform)) return;
            Vector3 position = Vector3.zero, target = Vector3.zero; float total = 0;
            for (int i = 0; i < playable.GetInputCount(); ++i)
            {
                float weight = playable.GetInputWeight(i); if (weight <= 0) continue;
                var input = (ScriptPlayable<DawnShotBehaviour>)playable.GetInput(i); var shot = input.GetBehaviour();
                float t = Mathf.SmoothStep(0, 1, (float)(input.GetTime() / System.Math.Max(0.001, input.GetDuration())));
                position += Vector3.Lerp(shot.from, shot.to, t) * weight; target += shot.lookAt * weight; total += weight;
            }
            if (total <= 0) return;
            cameraTransform.position = position / total; cameraTransform.LookAt(target / total);
        }
    }
    [TrackClipType(typeof(DawnShotAsset))]
    [TrackBindingType(typeof(Transform))]
    public sealed class DawnShotTrack : TrackAsset
    {
        public override Playable CreateTrackMixer(PlayableGraph graph, GameObject go, int inputCount)
            => ScriptPlayable<DawnShotMixer>.Create(graph, inputCount);
    }
}
