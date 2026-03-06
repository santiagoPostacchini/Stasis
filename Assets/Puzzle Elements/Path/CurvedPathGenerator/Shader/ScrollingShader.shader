Shader "PathGenerator/ScrollingShader"
{
    Properties
    {
        _MainTex ( "Main Texture", 2D ) = "white" {}
        [IntRange] _Speed ( "Speed", Range ( -100, 100 ) ) = 30
        _Alpha ("Alpha", Range(0,1)) = 1
        _Fill("Fill Path", Range(0,1)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "RenderPipeline" = "UniversalPipeline" }
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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float _Alpha;
                float _Fill;
                float _Speed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float2 uv : TEXCOORD0;
                float4 positionHCS : SV_POSITION;
            };

            Varyings vert ( Attributes v )
            {
                Varyings o;
                o.positionHCS = TransformObjectToHClip( v.positionOS.xyz );
                o.uv = TRANSFORM_TEX ( v.uv, _MainTex );
                return o;
            }

            half4 frag ( Varyings i ) : SV_Target
            {
                // get scroll value
                float2 scroll = float2(0, (frac ( _Time.x * _Speed )));

                // sample texture
                half4 col = SAMPLE_TEXTURE2D( _MainTex, sampler_MainTex, (i.uv - scroll) );
                col.a *= _Alpha;

                // discard if uv.y is below cut value
                clip ( step ( i.uv.y, (_Fill - 0.5)* _MainTex_ST.y) - 0.1);

                return col;
            }
            ENDHLSL
        }
    }
}