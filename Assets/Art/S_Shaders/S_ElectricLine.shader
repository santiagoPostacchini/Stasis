Shader "Custom/S_ElectricLine"
{
    Properties
    {
        _Color ("Color Principal", Color) = (1,1,1,1)
        _NoiseTex ("Mapa de Ruido", 2D) = "white" {}
        _Intensity ("Intensidad", Range(0,10)) = 1
        _Speed ("Velocidad", Float) = 1
        _Tiling ("Repeticion UV", Float) = 1
        _Distortion ("Fuerza de Distorsion", Range(0,1)) = 0.1
    }
    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        Cull Off
        ZWrite Off
        Blend SrcAlpha One

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _NoiseTex_ST;
                float4 _Color;
                float _Intensity;
                float _Speed;
                float _Tiling;
                float _Distortion;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv     : TEXCOORD0;
                float4 color  : COLOR;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv  : TEXCOORD0;
                float4 col : COLOR;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = TRANSFORM_TEX(v.uv, _NoiseTex) * float2(_Tiling, 1);
                o.col = v.color * _Color;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, float2(i.uv.x + _Speed * _Time.y, i.uv.y)).r;
                float distort = (noise - 0.5) * _Distortion;
                float alpha = noise * _Intensity;
                half4 col = i.col;
                col.rgb += distort;
                col.a *= alpha;
                return col;
            }
            ENDHLSL
        }
    }
}
