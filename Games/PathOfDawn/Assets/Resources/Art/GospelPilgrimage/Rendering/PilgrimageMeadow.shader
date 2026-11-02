Shader "FirstGame/Pilgrimage/LivingMeadow"
{
    Properties { _Wind("Leaf breeze", Range(0,1)) = 0.12 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "LivingMeadowForward"
            Tags { "LightMode"="UniversalForward" }
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float _Wind;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; half4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half4 color:COLOR; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                float flex = input.uv.y * input.uv.y * _Wind;
                p.x += sin(p.z * 1.9 + p.x * .8 + _Time.y * 1.4) * flex;
                p.z += cos(p.x * 1.4 + _Time.y * 1.1) * flex * .38;
                output.positionWS = TransformObjectToWorld(p);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.color = input.color;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input, FRONT_FACE_TYPE face:FRONT_FACE_SEMANTIC):SV_Target
            {
                half3 n = normalize(input.normalWS) * IS_FRONT_VFACE(face, 1, -1);
                Light sun = GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half diffuse = .34 + .66 * saturate(dot(n, sun.direction));
                half shadow = lerp(.40, 1, sun.shadowAttenuation);
                half3 illumination = SampleSH(n) * .70 + sun.color * diffuse * shadow * .65;
                half3 color = input.color.rgb * illumination;
                return half4(MixFog(color, input.fog), 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
