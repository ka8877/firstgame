using System;
using UnityEngine;
using UnityEngine.UI;

namespace PathOfDawn.Presentation
{
    /// <summary>Ground-anchored cloth motion and alpha-correct transitions between authored poses.</summary>
    public sealed class DawnLivingImage : RawImage
    {
        const float SheetWidth = 1.5f;
        Material poseMaterial;
        Texture currentTexture;
        Rect currentUv;
        float currentFacing;
        float blend = 1, breath, stride, motion, lean;
        bool hasPose;
        int currentPose = -1;
        public float PoseBlend => blend;

        public void Initialize(Shader shader, Texture2D motionFields)
        {
            poseMaterial = new Material(shader) { name = "Living painted pose", hideFlags = HideFlags.HideAndDontSave };
            material = poseMaterial;
            poseMaterial.SetTexture("_MotionTex", motionFields);
            raycastTarget = false;
            rectTransform.pivot = new Vector2(.5f, 0);
        }

        public void ShowPose(Texture2D next, DawnIllustratedStage.ArtFrame frame, float facing, bool snap, float dt, int poseIndex)
        {
            if (poseMaterial == null || next == null) return;
            bool changed = !hasPose || currentTexture != next || currentUv != frame.Uv || currentFacing != facing;
            if (changed)
            {
                if (hasPose)
                {
                    poseMaterial.SetTexture("_PreviousTex", currentTexture);
                    poseMaterial.SetVector("_PreviousUV", poseMaterial.GetVector("_PoseUV"));
                    poseMaterial.SetVector("_PreviousBounds", poseMaterial.GetVector("_PoseBounds"));
                    poseMaterial.SetFloat("_PreviousFacing", currentFacing);
                }
                float aspect = next.width * frame.width / (next.height * frame.height);
                poseMaterial.SetVector("_PoseUV", new Vector4(frame.x, frame.y, frame.width, frame.height));
                poseMaterial.SetVector("_PoseBounds", new Vector4(-frame.footX * aspect, -frame.footY, aspect, 1));
                poseMaterial.SetFloat("_Facing", facing);
                bool sameSide = currentPose >= 0 && poseIndex >= 0 &&
                    (currentPose == 1 || currentPose >= 6) == (poseIndex == 1 || poseIndex >= 6);
                poseMaterial.SetFloat("_UseMotion", sameSide && currentFacing == facing ? 1 : 0);
                poseMaterial.SetVector("_MotionCell", new Vector4(poseIndex * .1f, currentPose * .1f, .1f, .1f));
                blend = hasPose && !snap ? 0 : 1;
                currentTexture = next; currentUv = frame.Uv; currentFacing = facing;
                currentPose = poseIndex;
                texture = next;
                hasPose = true;
            }
            if (snap) blend = 1;
            else blend = Mathf.MoveTowards(blend, 1, dt / .095f);
            poseMaterial.SetFloat("_PoseBlend", Mathf.SmoothStep(0, 1, blend));
        }

        public void Animate(float breathingPhase, float gaitPhase, float moving, float lateralLean)
        {
            breath = Mathf.Sin(breathingPhase);
            stride = gaitPhase;
            motion = moving;
            lean = lateralLean;
            SetVerticesDirty();
        }

        protected override void OnPopulateMesh(VertexHelper vertices)
        {
            vertices.Clear();
            Rect rect = GetPixelAdjustedRect();
            const int columns = 16, rows = 28;
            var vertex = UIVertex.simpleVert;
            vertex.color = color;
            for (int y = 0; y <= rows; ++y)
                for (int x = 0; x <= columns; ++x)
                {
                    float u = (float)x / columns, v = (float)y / rows;
                    float edge = Mathf.Clamp01(Mathf.Abs(u - .5f) * 4);
                    float grounded = Mathf.SmoothStep(0, 1, Mathf.Clamp01(v / .18f));
                    float torso = Mathf.SmoothStep(0, 1, Mathf.InverseLerp(.38f, .85f, v));
                    float cloth = Mathf.Sin(Mathf.PI * Mathf.Clamp01((v - .14f) / .66f)) * edge * edge;
                    float flutter = Mathf.Sin(stride * Mathf.PI * 2 - v * 4) * motion;
                    float dx = rect.height * (lean * .012f * grounded * v + flutter * .006f * cloth + breath * .0009f * torso);
                    float dy = rect.height * (breath * .0016f * torso - motion * .002f * (1 - Mathf.Cos(stride * Mathf.PI * 4)) * grounded);
                    vertex.position = new Vector3(rect.xMin + u * rect.width + dx, rect.yMin + v * rect.height + dy);
                    // Coordinates are in character-height units, shared by every
                    // pose. Changing an alpha bounding box never stretches a body.
                    vertex.uv0 = new Vector2((u - .5f) * SheetWidth, v);
                    vertices.AddVert(vertex);
                }
            for (int y = 0; y < rows; ++y)
                for (int x = 0; x < columns; ++x)
                {
                    int i = y * (columns + 1) + x;
                    vertices.AddTriangle(i, i + columns + 1, i + 1);
                    vertices.AddTriangle(i + 1, i + columns + 1, i + columns + 2);
                }
        }

        public void SetHeight(float height) => rectTransform.sizeDelta = new Vector2(height * SheetWidth, height);

        public static Texture2D LoadMotionFields()
        {
            var source = Resources.Load<TextAsset>("Art/Painted/traveler-motion");
            if (source == null) throw new InvalidOperationException("Traveler motion fields are missing.");
            byte[] bytes = source.bytes;
            if (bytes.Length < 12 || bytes[0] != 'D' || bytes[1] != 'W' || bytes[2] != 'M' || bytes[3] != '1')
                throw new InvalidOperationException("Invalid traveler motion fields.");
            int width = BitConverter.ToInt32(bytes, 4), height = BitConverter.ToInt32(bytes, 8);
            if (width != 640 || height != 960 || bytes.Length != 12 + width * height * 8)
                throw new InvalidOperationException("Traveler motion field dimensions do not match the pose contract.");
            var data = new byte[bytes.Length - 12];
            Buffer.BlockCopy(bytes, 12, data, 0, data.Length);
            var result = new Texture2D(width, height, TextureFormat.RGBAHalf, false, true)
            { name = "Measured pose correspondence", hideFlags = HideFlags.HideAndDontSave, filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
            result.LoadRawTextureData(data); result.Apply(false, true);
            return result;
        }

        protected override void OnDestroy()
        {
            if (poseMaterial != null) Destroy(poseMaterial);
            base.OnDestroy();
        }
    }
}
