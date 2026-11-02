Shader "FirstGame/Pilgrimage/AtmosphericLight"
{
    Properties
    {
        _Color("Light tint", Color) = (1, .84, .52, .15)
        _Style("0 mote, 1 shaft, 2 water glint", Float) = 0
        _Drift("Animated drift", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Name "AtmosphericForward"
            Tags { "LightMode"="UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Color;
                float _Style, _Drift;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float2 motion:TEXCOORD1; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; float phase:TEXCOORD1; half fog:TEXCOORD2; };
            Varyings Vert(Attributes input)
            {
                Varyings output;
                float3 p = input.positionOS.xyz;
                float phase = input.motion.x + _Time.y * input.motion.y;
                p.x += sin(phase * .73) * _Drift;
                p.y += sin(phase) * _Drift * .65;
                p.z += cos(phase * .57) * _Drift * .45;
                output.positionCS = TransformObjectToHClip(p);
                output.color = input.color * _Color;
                output.uv = input.uv; output.phase = phase;
                output.fog = ComputeFogFactor(output.positionCS.z);
                return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p = input.uv * 2 - 1;
                half alpha;
                if (_Style < .5)
                    alpha = pow(saturate(1 - dot(p,p)), 2) * (.62 + .38 * sin(input.phase));
                else if (_Style < 1.5)
                    alpha = pow(saturate(1 - abs(p.x)), 1.6) * sin(saturate(input.uv.y) * 3.141593) * (.82 + .18 * sin(input.phase * .25));
                else if (_Style < 2.5)
                    alpha = pow(saturate(1 - abs(p.y)), 3) * pow(saturate(1 - abs(p.x)), .75) * pow(saturate(.5 + .5 * sin(input.phase)), 3);
                else
                {
                    float organic = dot(p,p) + sin(p.x * 9 + p.y * 5) * .055 + sin(p.y * 12 - p.x * 3) * .04;
                    alpha = pow(saturate(1 - organic), .8);
                }
                alpha *= input.color.a * ComputeFogIntensity(input.fog);
                return half4(input.color.rgb, alpha);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
