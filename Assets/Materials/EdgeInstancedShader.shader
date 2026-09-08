Shader "Custom/ParticleEdge"
{
    Properties
    {
        _EdgeColor ("Edge Color", Color) = (0.5, 0.5, 0.5, 0.3)
    }

    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

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

            uniform float maxVelocity;
            uniform float4 _EdgeColor;

            StructuredBuffer<Node3D> particles;
            StructuredBuffer<HalfEdge3D> halfEdges; // Now we pass in the half-edge buffer

            v2f vert(appdata_base v, uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
            {
                v2f o;
                o.color = float4(0,0,0,0);

                // Each instance now cleanly maps to exactly ONE Half-Edge
                uint edgeIndex = instanceID;
                HalfEdge3D edge = halfEdges[edgeIndex];

                uint sourceIndex = edge.origin;

                uint targetIndex = edge.target;

                // Optimization: To stop dual-drawing identical lines (A->B and B->A),
                // only draw the line if the source node index is smaller than the target node index.
                if (!edge.isBoundary && sourceIndex >= targetIndex)
                {
                    o.pos = float4(0, 0, 0, 0);
                    return o;
                }

                Node3D source = particles[sourceIndex];
                Node3D target = particles[targetIndex];

                // If vertexID is 0, we position it at the source node. If 1, at the target node.
                float3 worldPos = (vertexID == 0) ? source.position : target.position;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                
                // Color by average curvature
                float colorT = saturate((source.curvature + target.curvature) * 0.5);

                //o.color = float4(_EdgeColor.rgb * colorT, 1.0f);

                o.color = _EdgeColor * (edge.conductivity);

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Discard execution for invalid/collapsed lines
                // Checking Z helps ensure we don't accidentally discard a valid node sitting perfectly at 0,0
                if (i.pos.x == 0 && i.pos.y == 0 && i.pos.z == 0) discard;
                return i.color;
            }
            ENDCG
        }
    }
}