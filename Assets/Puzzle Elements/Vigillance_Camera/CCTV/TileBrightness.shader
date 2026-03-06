Shader "Hidden/CCTV/TileBrightness"
{
    Properties{
        _MainTex("Texture", 2D) = "white" {}
        _Color("Color", Color) = (1,1,1,1)
        _Brightness("Brightness", Float) = 1.0
    }
    SubShader{
        Tags { "RenderType" = "Opaque" "Queue" = "Overlay" }
        Pass {
            ZTest Always Cull Off ZWrite Off
            Blend One Zero

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _Brightness;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                float2 uv     : TEXCOORD0;
            };
            struct Varyings {
                float4 positionHCS : SV_POSITION;
                float2 uv     : TEXCOORD0;
            };

            Varyings vert(Attributes v) {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.uv = v.uv;
                return o;
            }

            half4 frag(Varyings i) : SV_Target {
                half4 c = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv) * _Color;
                c.rgb *= _Brightness;
                return c;
            }
            ENDHLSL
        }
    }
}
