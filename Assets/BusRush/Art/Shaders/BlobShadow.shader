// Soft blob shadow under each passenger: one shared material, GPU instanced. Deliberately NOT SRP Batcher compatible
// (no UnityPerMaterial buffer), so URP draws all the blobs as instanced batches instead of one draw call each.
Shader "BusRush/BlobShadow"
{
    Properties
    {
        _MainTex ("Soft Circle", 2D) = "white" {}
        _Color ("Colour", Color) = (0.08, 0.1, 0.18, 0.42)
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-50" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Pass
        {
            Name "Blob"
            Tags { "LightMode" = "UniversalForward" }
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);
            half4 _Color; // global (not in UnityPerMaterial): keeps instancing instead of the SRP Batcher

            struct A { float4 positionOS : POSITION; float2 uv : TEXCOORD0; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; float2 uv : TEXCOORD0; };

            V Vert(A input)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                o.uv = input.uv;
                return o;
            }

            half4 Frag(V i) : SV_Target
            {
                half a = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, i.uv).a;
                return half4(_Color.rgb, _Color.a * a);
            }
            ENDHLSL
        }
    }
    FallBack Off
}
