Shader "Hidden/CCTV_BlitBoost"
{
    Properties
    {
        _MainTex("Source", 2D) = "white" {}
        _Color("Multiply Color", Color) = (1,1,1,1)
        _Brightness("Brightness", Float) = 1.0
        _Contrast("Contrast", Float) = 1.0
        _Gamma("Gamma", Float) = 1.0
        _Saturation("Saturation", Float) = 1.0
    }
    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Cull Off ZTest Always ZWrite Off
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float _Brightness;
                float _Contrast;
                float _Gamma;
                float _Saturation;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; };
            struct Varyings  { float4 positionHCS:SV_POSITION; float2 uv:TEXCOORD0; };

            Varyings vert(Attributes v) {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            float3 ApplyContrast(float3 c, float k) { return (c - 0.5) * k + 0.5; }
            float3 ApplyGamma(float3 c, float g) { return pow(max(c, 1e-6), 1.0 / max(g, 1e-6)); }
            float3 ApplySaturation(float3 c, float s) {
                float l = dot(c, float3(0.2126, 0.7152, 0.0722));
                return lerp(l.xxx, c, s);
            }

            half4 frag(Varyings i) :SV_Target
            {
                half4 col = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv);
                col.rgb *= _Brightness;
                col.rgb = ApplyContrast(col.rgb, _Contrast);
                col.rgb = ApplyGamma(col.rgb, _Gamma);
                col.rgb = ApplySaturation(col.rgb, _Saturation);
                col *= _Color;
                return col;
            }
            ENDHLSL
        }
    }
}
