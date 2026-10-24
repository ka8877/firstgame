Shader "Dawn/Living Illustrated Sprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Current pose", 2D) = "white" {}
        _PreviousTex ("Previous pose", 2D) = "white" {}
        _MotionTex ("Pose correspondence", 2D) = "black" {}
        _MotionCell ("Pose pair", Vector) = (0,0,.1,.1)
        _UseMotion ("Align limbs", Float) = 0
        _PoseUV ("Current UV", Vector) = (0,0,1,1)
        _PreviousUV ("Previous UV", Vector) = (0,0,1,1)
        _PoseBounds ("Current bounds", Vector) = (-.5,0,1,1)
        _PreviousBounds ("Previous bounds", Vector) = (-.5,0,1,1)
        _Facing ("Facing", Float) = 1
        _PreviousFacing ("Previous facing", Float) = 1
        _PoseBlend ("Pose transition", Float) = 1
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }
    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "CanUseSpriteAtlas"="True" }
        Stencil { Ref [_Stencil] Comp [_StencilComp] Pass [_StencilOp] ReadMask [_StencilReadMask] WriteMask [_StencilWriteMask] }
        Cull Off Lighting Off ZWrite Off ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_local _ UNITY_UI_CLIP_RECT
            #include "UnityCG.cginc"
            #include "UnityUI.cginc"
            struct appdata { float4 vertex : POSITION; float4 color : COLOR; float2 uv : TEXCOORD0; };
            struct v2f { float4 vertex : SV_POSITION; fixed4 color : COLOR; float2 uv : TEXCOORD0; float4 world : TEXCOORD1; };
            sampler2D _MainTex, _PreviousTex, _MotionTex;
            float4 _PoseUV, _PreviousUV, _PoseBounds, _PreviousBounds, _ClipRect;
            float4 _MotionCell;
            float _Facing, _PreviousFacing, _PoseBlend, _UseMotion;
            v2f vert(appdata v)
            {
                v2f o; o.world = v.vertex; o.vertex = UnityObjectToClipPos(v.vertex); o.uv = v.uv; o.color = v.color; return o;
            }
            fixed4 pose(sampler2D tex, float2 coord, float4 bounds, float4 region, float facing)
            {
                coord.x *= facing;
                float2 p = (coord - bounds.xy) / bounds.zw;
                float inside = step(0,p.x) * step(p.x,1) * step(0,p.y) * step(p.y,1);
                fixed4 c = tex2D(tex, region.xy + saturate(p) * region.zw);
                c.a *= inside; c.rgb *= c.a; return c;
            }
            float4 motionField(float2 coord)
            {
                float2 cell = float2(coord.x * _Facing / 1.5 + .5, coord.y);
                cell = clamp(cell, float2(.5/64,.5/96), float2(1-.5/64,1-.5/96));
                float4 flow = tex2D(_MotionTex, _MotionCell.xy + cell * _MotionCell.zw);
                flow.xz *= _Facing;
                return flow;
            }
            fixed4 frag(v2f i) : SV_Target
            {
                float2 oldCoord = i.uv, newCoord = i.uv;
                if (_UseMotion > .5 && _PoseBlend < .999)
                {
                    // Inverse-warp each drawing toward the same intermediate
                    // limb locations before blending, avoiding doubled feet.
                    float4 flow = motionField(i.uv);
                    oldCoord -= _PoseBlend * flow.xy;
                    newCoord -= (1 - _PoseBlend) * flow.zw;
                    oldCoord = i.uv - _PoseBlend * motionField(oldCoord).xy;
                    newCoord = i.uv - (1 - _PoseBlend) * motionField(newCoord).zw;
                }
                fixed4 current = pose(_MainTex, newCoord, _PoseBounds, _PoseUV, _Facing);
                fixed4 previous = pose(_PreviousTex, oldCoord, _PreviousBounds, _PreviousUV, _PreviousFacing);
                // Blend premultiplied samples once: no translucent double-image
                // opacity dip from stacking two ordinary UI Images.
                fixed4 c = lerp(previous, current, _PoseBlend);
                c.rgb = c.a > .0001 ? c.rgb / c.a : 0;
                c *= i.color;
                #ifdef UNITY_UI_CLIP_RECT
                c.a *= UnityGet2DClipping(i.world.xy, _ClipRect);
                #endif
                return c;
            }
            ENDCG
        }
    }
}
