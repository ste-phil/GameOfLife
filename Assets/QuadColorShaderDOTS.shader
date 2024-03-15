Shader "GameOfLife/QuadColorShaderDOTS"
{
    Properties
    {
        _CellStates ("Cell States", Integer) = 1.0
        _CellIndices ("Cell Indices", Integer) = 1.0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        
        Pass
        {
            HLSLPROGRAM
            #pragma exclude_renderers gles gles3 glcore
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 4.5
            #pragma multi_compile_instancing
            #pragma multi_compile _ DOTS_INSTANCING_ON
            #pragma instancing_options renderinglayer
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/UnityDOTSInstancing.hlsl"
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            CBUFFER_START(UnityPerMaterial)
                uint _CellStates;
                uint _CellIndices;
            CBUFFER_END

            #ifdef DOTS_INSTANCING_ON
                UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                    UNITY_DOTS_INSTANCED_PROP(uint, _CellStates)
                    UNITY_DOTS_INSTANCED_PROP(uint, _CellIndices)
                UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)
            #endif

            v2f vert(appdata v)
            {
                v2f o;
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_TRANSFER_INSTANCE_ID(v, o);

                uint instanceId = 0;
                uint alive = 0;
                #ifdef DOTS_INSTANCING_ON
                instanceId = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(uint, _CellIndices);
                alive = UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(uint, _CellStates);
                #endif

                o.pos = v.vertex;
                // o.instanceID = instanceId;
                return o;
            }

            half4  frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);

                return half4(0.0f, 0.0f, 0.0f, 1.0f);
            }
            ENDHLSL
        }
    }
}