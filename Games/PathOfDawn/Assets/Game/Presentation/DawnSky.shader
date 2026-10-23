Shader "PathOfDawn/Storybook Sky"
{
    Properties
    {
        _Zenith("Clear azure", Color) = (.26,.55,.86,1)
        _Horizon("Pearl horizon", Color) = (.79,.91,.96,1)
        _CloudColor("Sunlit cloud", Color) = (1,.98,.9,1)
    }
    SubShader
    {
        Tags { "RenderType"="Background" "Queue"="Background" "PreviewType"="Skybox" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Zenith, _Horizon, _CloudColor;
            CBUFFER_END
            struct A { float4 positionOS : POSITION; };
            struct V { float4 positionCS : SV_POSITION; float3 direction : TEXCOORD0; };
            V vert(A v) { V o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz);o.direction=v.positionOS.xyz;return o; }
            float hash21(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
            float StoryNoise(float2 p)
            {
                float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);
                return lerp(lerp(hash21(i),hash21(i+float2(1,0)),f.x),lerp(hash21(i+float2(0,1)),hash21(i+1),f.x),f.y);
            }
            half4 frag(V i) : SV_Target
            {
                float3 d=normalize(i.direction);float elevation=saturate(d.y);
                half3 col=lerp(_Horizon.rgb,_Zenith.rgb,pow(elevation,.55));
                float2 uv=d.xz/(max(.15,d.y)+.3)*2.4+float2(_Time.y*.0013,0);
                float n=StoryNoise(uv)+.45*StoryNoise(uv*2.13)+.18*StoryNoise(uv*4.7);
                float clouds=smoothstep(.88,1.15,n)*smoothstep(.02,.19,elevation)*(1-smoothstep(.72,.96,elevation));
                col=lerp(col,_CloudColor.rgb,clouds*.92);
                float sun=pow(saturate(dot(d,normalize(float3(-.45,.47,-.45)))),180);
                col+=half3(1,.78,.38)*sun*.38;
                return half4(col,1);
            }
            ENDHLSL
        }
    }
}
