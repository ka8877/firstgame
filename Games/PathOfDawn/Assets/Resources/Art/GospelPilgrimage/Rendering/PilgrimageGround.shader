Shader "FirstGame/Pilgrimage/PaintedGround"
{
    Properties
    {
        _GrassMap("Preserved meadow grain", 2D) = "white" {}
        _StoneMap("Preserved limestone paving", 2D) = "white" {}
        _EarthMap("Preserved earth grain", 2D) = "white" {}
        _BlendMap("World path, court, grove and shore", 2D) = "black" {}
        _PaintedAtlas("Optional original painted ground atlas", 2D) = "white" {}
        _UsePainted("Use original painted atlas", Float) = 0
        _WorldRect("Ground mask origin and reciprocal extent", Vector) = (-64,-48,.007352941,.009803922)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry" }
        Pass
        {
            Name "PaintedGroundForward"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_GrassMap); SAMPLER(sampler_GrassMap);
            TEXTURE2D(_StoneMap); SAMPLER(sampler_StoneMap);
            TEXTURE2D(_EarthMap); SAMPLER(sampler_EarthMap);
            TEXTURE2D(_BlendMap); SAMPLER(sampler_BlendMap);
            TEXTURE2D(_PaintedAtlas); SAMPLER(sampler_PaintedAtlas);
            CBUFFER_START(UnityPerMaterial)
                float4 _WorldRect;
                float _UsePainted;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; half3 normalWS:TEXCOORD1; half fog:TEXCOORD2; };
            float Hash(float2 p) { p=frac(p*float2(123.34,456.21)); p+=dot(p,p+45.32); return frac(p.x*p.y); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half3 Atlas(float2 metres, float2 tile)
            {
                // Inset every tile to keep neighbouring paint out of the mip edge.
                // Mirrored repeat joins the very same painted edge on either
                // side of a tile boundary without modifying the original PNG.
                float2 repeat=1-abs(frac(metres*.095)*2-1);
                float2 uv=tile*.5+.003+repeat*.494;
                return SAMPLE_TEXTURE2D_LOD(_PaintedAtlas,sampler_PaintedAtlas,uv,0).rgb;
            }
            Varyings Vert(Attributes input)
            {
                Varyings output;
                output.positionWS=TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS=TransformWorldToHClip(output.positionWS);
                output.normalWS=TransformObjectToWorldNormal(input.normalOS);
                output.fog=ComputeFogFactor(output.positionCS.z); return output;
            }
            half4 Frag(Varyings input):SV_Target
            {
                float2 p=input.positionWS.xz;
                half4 mask=SAMPLE_TEXTURE2D_LOD(_BlendMap,sampler_BlendMap,saturate((p-_WorldRect.xy)*_WorldRect.zw),0);
                half broad=Noise(p*.11), small=Noise(p*1.7), fine=Noise(p*7.0);
                half grain=SAMPLE_TEXTURE2D_LOD(_GrassMap,sampler_GrassMap,p*.12,1).g;
                half3 meadow=lerp(half3(.15,.25,.18),half3(.39,.46,.25),broad)*(.82+grain*.23);
                half3 earth=lerp(half3(.36,.28,.17),half3(.66,.53,.32),small*.35+broad*.65);
                half3 stone=SAMPLE_TEXTURE2D_LOD(_StoneMap,sampler_StoneMap,p*.43,0).rgb*half3(.86,.78,.59);
                stone=lerp(stone,half3(.63,.61,.45),.16+small*.13);
                if (_UsePainted>.5)
                {
                    meadow=lerp(meadow,Atlas(p,float2(0,1))*half3(.68,.81,.66),.72);
                    earth=lerp(earth,Atlas(p,float2(1,1))*half3(.89,.84,.72),.72);
                    earth=lerp(earth,Atlas(p,float2(1,0))*half3(.77,.82,.66),mask.b*.36);
                    stone=lerp(stone,Atlas(p,float2(0,0))*half3(.91,.87,.74),.74);
                }
                half3 base=lerp(meadow,earth,saturate(mask.r*(.82+small*.18)));
                half paving=saturate((mask.g-.13+small*.16)*1.18);
                base=lerp(base,stone,paving);
                // Moss islands interrupt the limestone at the edge of every court.
                half moss=(1-smoothstep(.12,.58,small))*paving*(1-paving)*1.7;
                base=lerp(base,meadow*.83,moss);
                base*=lerp(1,.63,mask.b*(.65+broad*.35));
                base=lerp(base,half3(.34,.45,.40),mask.a*.38);
                base*=.97+fine*.055;
                // Broad translucent foliage, dappled sun and cool shadow share
                // the floor's world coordinates: no separate rectangular decals.
                half leaf=Noise(p*.69+float2(sin(p.y*.3),cos(p.x*.27)));
                half canopy=smoothstep(.48,.72,leaf)*mask.b;
                base*=1-canopy*.15;
                Light sun=GetMainLight(TransformWorldToShadowCoord(input.positionWS));
                half3 n=normalize(input.normalWS);
                half direct=.48+.52*saturate(dot(n,sun.direction));
                half shadow=lerp(.58,1,sun.shadowAttenuation);
                half3 light=SampleSH(n)*.78+sun.color*direct*shadow*.62;
                half3 color=max(0,base*light);
                return half4(MixFog(color,input.fog),1);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
    Fallback Off
}
