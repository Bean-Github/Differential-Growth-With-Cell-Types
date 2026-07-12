Shader "Hidden/SDFRaymarcher"
{
    Properties
    {
        _SphereColor ("Sphere Color", Color) = (1, 0.2, 0.3, 1)
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            Name "SDFRaymarch"

            HLSLPROGRAM
            // Use Unity's built-in Vertex Shader for full-screen blits!
            #pragma vertex Vert
            #pragma fragment Frag
            
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            // This include brings in the Vert function, Varyings struct, AND _BlitTexture definitions
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            #include "./SDFLib.hlsl"
            
            // Variables passed from your C# script and Material Properties
            float NearPlane;
            float FarPlane;
            float4x4 _CameraInvProjection; 
            float4 _Color;


            // THE MAIN FUNCTION (it gets an SDF of the sphere)
            float Map(float3 p)
            {
                return FinalSDF(p);
            }

            // normal calculation -> epsilon around to get a gradient
            float3 GetNormal(float3 p) 
            {
                float2 e = float2(0.001, 0);
                float3 n = float3(
                    Map(p + e.xyy) - Map(p - e.xyy),
                    Map(p + e.yxy) - Map(p - e.yxy),
                    Map(p + e.yyx) - Map(p - e.yyx)
                );
                return normalize(n);
            }

            // ==========================================
            // Fragment Shader
            // ==========================================
            // Note: Varyings is provided by Blit.hlsl. 
            // It provides positionCS and texcoord instead of uv.
            half4 Frag(Varyings input) : SV_Target
            {
                // 1. Read the existing screen color using Blit.hlsl's sampler
                half4 sourceColor = SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, input.texcoord);

                // 2. Reconstruct World Space Ray
                float2 ndc = input.texcoord * 2.0 - 1.0;

                float4 clipPos = float4(ndc, 1.0, 1.0);
                float4 viewPos = mul(_CameraInvProjection, clipPos);
                viewPos /= viewPos.w;

                float3 rayOrigin = _WorldSpaceCameraPos;
                float3 rayDir = normalize(mul((float3x3)UNITY_MATRIX_I_V, viewPos.xyz));

                // 3. The Raymarching Loop
                float t = 0.0;                  
                const int MAX_STEPS = 64;       
                const float MAX_DIST = 100.0;   
                const float SURF_DIST = 0.001;  

                for(int i = 0; i < MAX_STEPS; i++)
                {
                    float3 p = rayOrigin + rayDir * t; 
                    float d = Map(p); 

                    if(d < SURF_DIST)
                    {
                        float3 normal = GetNormal(p);

                        // get main light dir
                        Light mainLight = GetMainLight();
                        float3 lightDir = normalize(mainLight.direction);

                        float diffuseLighting = max(dot(normal, lightDir), 0.1); 
                        float alpha = _Color.a;

                        float3 surfaceColor = _Color.rgb * diffuseLighting;

                        float3 finalColor = lerp(sourceColor.rgb, surfaceColor, alpha);

                        return half4(finalColor, 1.0);
                    }

                    t += d; 
                    if(t > MAX_DIST) break; 
                }

                return sourceColor;
            }
            ENDHLSL
        }
    }
}



