Shader "Particles/Lightning" {
    Properties {
        [HDR]_TintColor ("Tint Color", Color) = (0.5,0.5,0.5,0.5)
        _MainTex ("Particle Texture", 2D) = "white" {}
        _Gradient("Gradient Texture", 2D) = "white" {}
        _Stretch("Stretch", Range(-2,2)) = 1.0
        _Offset("Offset", Range(-2,2)) = 1.0
        _Speed("Speed", Range(-2,2)) = 1.0
    }

    SubShader {
        Tags { "Queue"="Transparent" "IgnoreProjector"="True" "RenderType"="Transparent" "PreviewType"="Plane" "RenderPipeline"="UniversalPipeline" }
        Blend One OneMinusSrcAlpha
        ColorMask RGB
        Cull Off Lighting Off ZWrite Off

        Pass {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);  SAMPLER(sampler_MainTex);
            TEXTURE2D(_Gradient); SAMPLER(sampler_Gradient);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Gradient_ST;
                float4 _TintColor;
                float _Stretch;
                float _Offset;
                float _Speed;
            CBUFFER_END

            struct Attributes {
                float4 positionOS : POSITION;
                half4  color      : COLOR;
                float4 texcoord   : TEXCOORD0;
            };

            struct Varyings {
                float4 positionHCS : SV_POSITION;
                half4  color       : COLOR;
                float4 texcoord    : TEXCOORD0;
                float2 texcoord2   : TEXCOORD1;
            };

            Varyings vert (Attributes v)
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip(v.positionOS.xyz);
                o.color = v.color;
                o.texcoord.xy = TRANSFORM_TEX(v.texcoord.xy, _MainTex);
                o.texcoord2 = TRANSFORM_TEX(v.texcoord.xy, _Gradient);
                // Custom Data from particle system
                o.texcoord.z = v.texcoord.z;
                o.texcoord.w = v.texcoord.w;
                return o;
            }

            half4 frag (Varyings i) : SV_Target
            {
                // Custom Data from particle system
                float lifetime = i.texcoord.z;
                float randomOffset = i.texcoord.w;

                // fade the edges
                float gradientfalloff = smoothstep(0.99, 0.95, i.texcoord2.x) * smoothstep(0.99, 0.95, 1 - i.texcoord2.x);

                // moving UVs
                float2 movingUV = float2(i.texcoord.x + randomOffset + (_Time.x * _Speed), i.texcoord.y);
                half tex = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, movingUV).r * gradientfalloff;

                // cutoff for alpha
                float cutoff = step(lifetime, tex);

                // stretched uv for gradient map
                float2 uv = float2((tex * _Stretch) - lifetime + _Offset, 1);
                half4 colorMap = SAMPLE_TEXTURE2D(_Gradient, sampler_Gradient, uv);

                // everything together
                half4 col;
                col.rgb = colorMap.rgb * _TintColor.rgb * i.color.rgb;
                col.a = cutoff;
                col *= col.a;

                return col;
            }
            ENDHLSL
        }
    }
}