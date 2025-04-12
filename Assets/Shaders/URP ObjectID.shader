Shader "Unlit/URP_ObjectID"
{
    Properties
    {
        [MainTexture] _Texture("Texture", 2D) = "white" {}
        [PerRendererData] _SSObjectId("Object or texture ID", Integer) = 0
    }
    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "RenderPipeline" = "UniversalPipeline"
            "UniversalMaterialType" = "Unlit"
            "IgnoreProjector" = "True"
        }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            #pragma shader_feature_local_fragment _ _ALPHATEST_ON
            #pragma multi_compile_fragment _ _ALPHATEST_ON
            
            #pragma multi_compile_instancing
            #include_with_pragmas "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DOTS.hlsl"

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            struct Attributes
            {
                float4 positionOS   : POSITION;
                float2 texcoord     : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                #if defined(_ALPHATEST_ON)
                    float2 uv       : TEXCOORD0;
                #endif
                float4 positionHCS  : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            CBUFFER_START(UnityPerMaterial)
                float4 _Texture_ST;
                uint _SSObjectId;
                //UNITY_TEXTURE_STREAMING_DEBUG_VARS;
            CBUFFER_END

            #if defined(DOTS_INSTANCING_ON)
            // DOTS instancing definitions
            UNITY_DOTS_INSTANCING_START(UserPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP_OVERRIDE_SUPPORTED(uint, _SSObjectId)
            UNITY_DOTS_INSTANCING_END(UserPropertyMetadata)
            // DOTS instancing usage macros
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(type, var)
            #elif defined(UNITY_INSTANCING_ENABLED)
            // Unity instancing definitions
            UNITY_INSTANCING_BUFFER_START(SGPerInstanceData)
                UNITY_DEFINE_INSTANCED_PROP(uint, _SSObjectId)
            UNITY_INSTANCING_BUFFER_END(SGPerInstanceData)
            // Unity instancing usage macros
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) UNITY_ACCESS_INSTANCED_PROP(SGPerInstanceData, var)
            #else
            #define UNITY_ACCESS_HYBRID_INSTANCED_PROP(var, type) var
            #endif

            #if defined(_ALPHATEST_ON)
            TEXTURE2D(_Texture);
            SAMPLER(sampler_Texture);
            #endif
            
            Varyings vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                
                #if defined(_ALPHATEST_ON)
                    output.uv = TRANSFORM_TEX(input.texcoord, _Texture);
                #endif
                output.positionHCS = TransformObjectToHClip(input.positionOS.xyz);
                return output;
            }

            uint frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                #if defined(_ALPHATEST_ON)
                    float index = SAMPLE_TEXTURE2D(_Texture, sampler_Texture, input.uv).r;
                    AlphaDiscard(index < 1.0/255.0 ? 0.0 : 1.0, 0.5);
                #endif
                
                return UNITY_ACCESS_HYBRID_INSTANCED_PROP(_SSObjectId, uint);
            }
            ENDHLSL
        }
    }
}