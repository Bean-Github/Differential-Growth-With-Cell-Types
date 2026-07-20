Shader "Custom/ColorParticle"
{
    Properties
    {
    }

    SubShader
    {
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "../Growth3DCompute/GrowthHelpers3D.hlsl"
            #include "../Growth3DCompute/GrowthStructs3D.hlsl"


            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
            };

            uniform float4x4 _ObjectToWorld;
            uniform float _NumInstances;

            uniform float _Radius;

            uniform float maxVelocity;
                        
            uniform float4 _TopColor;
            uniform float4 _MediumColor;
            uniform float4 _BottomColor;

            uniform float _TotalAuxin;

            StructuredBuffer<Node3D> particles;


            v2f vert(appdata_base v, uint instanceID : SV_InstanceID)
            {
                v2f o;

                float3 particlePos = particles[instanceID].position;
                float r = _Radius;
                float3 scaledVertex = v.vertex.xyz * r;

                float4 wpos = float4(particlePos + scaledVertex, 1.0);

                o.pos = mul(UNITY_MATRIX_VP, wpos);

                o.color = float4(particles[instanceID].type.color.rgb, 1.0f);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                return i.color;
            }
            ENDCG
        }
    }
}


