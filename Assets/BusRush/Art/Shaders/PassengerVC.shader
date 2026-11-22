// Passenger v2 (single skinned mesh): colours come from the vertex colours. vc.rgb is the part colour (sRGB, so it is
// converted to linear here: the project is Linear); vc.a masks the team parts, which get tinted by _TeamColor:
//   albedo = lerp(vc.rgb, vc.rgb * _TeamColor, vc.a)
// Main light (wrapped Lambert) + ambient (SH) + a soft Blinn-Phong highlight. No shadows cast or received.
// SRP Batcher compatible: all material properties live in UnityPerMaterial.
Shader "BusRush/PassengerVC"
{
    Properties
    {
        _TeamColor ("Team Colour", Color) = (1, 0, 0, 1)
        _Gloss ("Highlight Size (exponent)", Range(4, 128)) = 36
        _SpecStrength ("Highlight Strength", Range(0, 1)) = 0.35
        _Wrap ("Light Wrap", Range(0, 1)) = 0.35
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TeamColor;
                half _Gloss;
                half _SpecStrength;
                half _Wrap;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 normalWS : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                half4 color : COLOR;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings o;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
                VertexPositionInputs pos = GetVertexPositionInputs(input.positionOS.xyz);
                o.positionCS = pos.positionCS;
                o.positionWS = pos.positionWS;
                o.normalWS = TransformObjectToWorldNormal(input.normalOS);
                o.color = input.color;
                return o;
            }

            half4 Frag(Varyings i) : SV_Target
            {
                half3 vc = SRGBToLinear(i.color.rgb);
                half3 albedo = lerp(vc, vc * _TeamColor.rgb, i.color.a);

                float3 n = normalize(i.normalWS);
                Light light = GetMainLight();
                half ndl = saturate((dot(n, light.direction) + _Wrap) / (1.0 + _Wrap));
                half3 diffuse = albedo * (light.color * ndl + SampleSH(n));

                float3 v = normalize(GetWorldSpaceViewDir(i.positionWS));
                float3 h = normalize(light.direction + v);
                half spec = pow(saturate(dot(n, h)), _Gloss) * _SpecStrength;

                return half4(diffuse + spec * light.color, 1.0);
            }
            ENDHLSL
        }

        // Depth for the depth texture / depth prepass (keeps the passengers correct if URP needs it).
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _TeamColor;
                half _Gloss;
                half _SpecStrength;
                half _Wrap;
            CBUFFER_END

            struct A { float4 positionOS : POSITION; UNITY_VERTEX_INPUT_INSTANCE_ID };
            struct V { float4 positionCS : SV_POSITION; };

            V DepthVert(A input)
            {
                V o;
                UNITY_SETUP_INSTANCE_ID(input);
                o.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                return o;
            }

            half DepthFrag(V i) : SV_Target { return 0; }
            ENDHLSL
        }
    }
    FallBack Off
}
