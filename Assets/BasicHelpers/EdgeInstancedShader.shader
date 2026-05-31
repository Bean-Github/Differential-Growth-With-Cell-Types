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

            struct v2f
            {
                float4 pos : SV_POSITION;
                float4 color : COLOR0;
            };

            uniform float maxVelocity;
            uniform float4 _EdgeColor;

            struct Particle
            {
                float3 position;
                float3 velocity;
                float curvature; 
                float mass;
                int neighborCount;
                int neighbors[8];
            };

            StructuredBuffer<Particle> particles;

            v2f vert(appdata_base v, uint instanceID : SV_InstanceID, uint vertexID : SV_VertexID)
            {
                v2f o;
                o.color = float4(0,0,0,0);

                // Math trick: Decouple instance ID into source node and neighbor slot
                uint nodeIndex = instanceID / 8;
                uint neighborSlot = instanceID % 8;

                Particle source = particles[nodeIndex];

                // If this neighbor slot isn't active, collapse the line to avoid drawing ghosts
                if ((int)neighborSlot >= source.neighborCount)
                {
                    o.pos = float4(0,0,0,0);
                    return o;
                }

                uint targetIndex = source.neighbors[neighborSlot];
                Particle target = particles[targetIndex];

                // Optimization: To stop dual-drawing identical lines (A->B and B->A),
                // only draw the line if the source index is smaller.
                if (nodeIndex >= targetIndex)
                {
                    o.pos = float4(0,0,0,0);
                    return o;
                }

                // If vertexID is 0, we position it at the source node. If 1, at the target node.
                float3 worldPos = (vertexID == 0) ? source.position : target.position;

                o.pos = mul(UNITY_MATRIX_VP, float4(worldPos, 1.0));
                
                // Color mapping by average velocity of the edge connection
                float speed = length(source.velocity + target.velocity) * 0.5;
                float colorT = saturate(speed / maxVelocity);
                
                o.color = float4(_EdgeColor.rgb, _EdgeColor.a * (1.0 - colorT * 0.5));

                return o;
            }

            float4 frag(v2f i) : SV_Target
            {
                // Discard execution for invalid/collapsed lines
                if (i.pos.x == 0 && i.pos.y == 0) discard;
                return i.color;
            }
            ENDCG
        }
    }
}