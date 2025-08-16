Shader "URP/Outline/BackfaceExtrude"
{
    Properties
    {
        _OutlineColor ("Outline Color", Color) = (1,1,0,1)
        _OutlineWidth ("Outline Width", Range(0,0.05)) = 0.01
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "Outline"
            Tags { "LightMode"="UniversalForward" }
            Cull Front        // 뒷면만 렌더 → 테두리만 보이게
            ZWrite Off
            ZTest LEqual
            Blend SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            // SkinnedMeshRenderer 지원
            #pragma multi_compile _ SKINNING_ON
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityInput.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/SpaceTransforms.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                // 스킨드 메시에 필요
                #if defined(SKINNING_ON)
                float4 tangentOS  : TANGENT;
                float4 weights    : BLENDWEIGHT;
                uint4  indices    : BLENDINDICES;
                #endif
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            // 간단 스킨닝(URP 포함 매크로 사용)
            float3 SkinPosition(float3 pos, float4 weights, uint4 indices)
            {
                #if defined(SKINNING_ON)
                float4x4 m0 = unity_Bones[indices.x];
                float4x4 m1 = unity_Bones[indices.y];
                float4x4 m2 = unity_Bones[indices.z];
                float4x4 m3 = unity_Bones[indices.w];
                float4 skinned =
                    mul(m0, float4(pos,1)) * weights.x +
                    mul(m1, float4(pos,1)) * weights.y +
                    mul(m2, float4(pos,1)) * weights.z +
                    mul(m3, float4(pos,1)) * weights.w;
                return skinned.xyz;
                #else
                return pos;
                #endif
            }

            float3 SkinNormal(float3 nrm, float4 weights, uint4 indices)
            {
                #if defined(SKINNING_ON)
                float3 n0 = mul((float3x3)unity_Bones[indices.x], nrm) * weights.x;
                float3 n1 = mul((float3x3)unity_Bones[indices.y], nrm) * weights.y;
                float3 n2 = mul((float3x3)unity_Bones[indices.z], nrm) * weights.z;
                float3 n3 = mul((float3x3)unity_Bones[indices.w], nrm) * weights.w;
                return normalize(n0+n1+n2+n3);
                #else
                return nrm;
                #endif
            }

            Varyings vert (Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                float3 posOS = IN.positionOS.xyz;
                float3 nrmOS = IN.normalOS;

                #if defined(SKINNING_ON)
                posOS = SkinPosition(posOS, IN.weights, IN.indices);
                nrmOS = SkinNormal(nrmOS, IN.weights, IN.indices);
                #endif

                // 노멀 방향으로 살짝 확장 → 외곽선 두께
                posOS += normalize(nrmOS) * _OutlineWidth;

                float4 posWS = mul(GetObjectToWorldMatrix(), float4(posOS,1));
                OUT.positionCS = TransformWorldToHClip(posWS.xyz);
                return OUT;
            }

            half4 frag (Varyings IN) : SV_Target
            {
                return _OutlineColor;
            }
            ENDHLSL
        }
    }
}
