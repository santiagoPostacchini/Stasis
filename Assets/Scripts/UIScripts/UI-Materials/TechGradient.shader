Shader "UI/TechGradient"
{
    Properties
    {
        _MainTex ("Main Texture", 2D) = "white" {}
        _MaskTex ("Mask Texture", 2D) = "white" {}
        _MainColor ("Main Color", Color) = (1,1,1,1)
        _LineColor ("Line Color", Color) = (0, 1, 1, 1)
        _Speed ("Speed", Float) = 1
        _Thickness ("Line Thickness", Float) = 0.1
        _EffectActive ("Effect Active", Float) = 0
    }

    SubShader
    {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Lighting Off
        ZWrite Off
        Cull Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_MaskTex); SAMPLER(sampler_MaskTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _MaskTex_ST;
                float4 _MainColor;
                float4 _LineColor;
                float _Speed;
                float _Thickness;
                float _EffectActive;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target
            {
                float mask = SAMPLE_TEXTURE2D(_MaskTex, sampler_MaskTex, i.uv).a;
                if (mask < 0.1) discard;

                // El tiempo solo se mueve si _EffectActive > 0
                float t = fmod(_Time.y * _Speed * _EffectActive + i.uv.y, 1.0);
                float lineMask = smoothstep(0.0, _Thickness, abs(t - 0.5));

                half4 baseColor = _MainColor;
                half4 lineColor = _LineColor * (1.0 - lineMask);

                return baseColor + lineColor;
            }
            ENDHLSL
        }
    }
}
