Shader "FirstGame/Pilgrimage/IntactPixelSprite"
{
    Properties
    {
        [PerRendererData] _MainTex("Original sprite pixels", 2D) = "white" {}
        _Color("Tint", Color) = (1,1,1,1)
        _Cutoff("Alpha cutoff", Range(0,1)) = 0.35
        [HideInInspector] _SrcBlend("Source blend", Float) = 1
        [HideInInspector] _DstBlend("Destination blend", Float) = 0
        [HideInInspector] _ZWrite("Depth write", Float) = 1
        [HideInInspector] _Cull("Cull", Float) = 0
        [HideInInspector] _UseVertexColor("Sprite vertex colors", Float) = 1
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="TransparentCutout" "Queue"="AlphaTest" }
        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        TEXTURE2D(_MainTex);
        SAMPLER(sampler_MainTex);
        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Cutoff;
            float4 _MainTex_ST;
            float _UseVertexColor;
        CBUFFER_END
        struct Attributes
        {
            float4 positionOS : POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
        };
        struct Varyings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            half fog : TEXCOORD1;
        };
        Varyings Vert(Attributes input)
        {
            Varyings output;
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.color = lerp(half4(1,1,1,1), input.color, saturate(_UseVertexColor)) * _Color;
            output.fog = ComputeFogFactor(output.positionCS.z);
            return output;
        }
        half4 SourcePixel(Varyings input)
        {
            half4 value = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * input.color;
            clip(value.a - _Cutoff);
            return value;
        }
        ENDHLSL
        Pass
        {
            Name "PixelForward"
            Tags { "LightMode"="UniversalForward" }
            Blend [_SrcBlend] [_DstBlend]
            ZWrite [_ZWrite]
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            half4 Frag(Varyings input) : SV_Target
            {
                half4 value = SourcePixel(input);
                value.rgb = MixFog(value.rgb, input.fog);
                return value;
            }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On
            ColorMask 0
            Cull [_Cull]
            HLSLPROGRAM
            #pragma target 2.0
            #pragma vertex Vert
            #pragma fragment DepthFrag
            half4 DepthFrag(Varyings input) : SV_Target
            {
                SourcePixel(input);
                return 0;
            }
            ENDHLSL
        }
    }
    Fallback Off
}
