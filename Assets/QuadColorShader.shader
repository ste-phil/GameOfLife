Shader "GameOfLife/QuadColorShader"
{
    Properties
    {
        // Other properties...
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"
            
            struct appdata
            {
                float4 vertex : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID 
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                uint instanceID : BLENDINDICES;
            };

            StructuredBuffer<int> _CellStates;

            v2f vert(appdata v)
            {
                UNITY_SETUP_INSTANCE_ID(v);

                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
            #ifdef INSTANCING_ON
                o.instanceID = unity_InstanceID;
            #endif
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(i);
                return fixed4(0.0f, _CellStates[i.instanceID], 0.0f, 1.0f);
            }
            ENDCG
        }
    }
}