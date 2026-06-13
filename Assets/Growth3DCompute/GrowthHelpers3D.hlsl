
const uint UINT_MAX = 0xFFFFFFFF; // 2^32 - 1 = 4294967295
const uint INVALID_FACE_ID = 0xFFFFFFFF; 
const uint INVALID_TWIN_ID = -1; 

// HELPERS

float2 worldToScreenPos(float4 worldPos, float4x4 projMatrix, float4x4 viewMatrix, float2 screenDims)
{
    float4x4 vpMatrix = projMatrix * viewMatrix;
    
    float4 clip = mul(vpMatrix, worldPos);
    
    // perspective divide
    float3 ndc = clip.xyz / clip.w;
    
    float2 screenUV = ndc.xy * 0.5 + 0.5;
    float2 screenPos = screenUV * float2(screenDims.x, screenDims.y);
    
    return screenPos;
}

float remap01(float t, float oldMin, float oldMax)
{
    return (t - oldMin) * (1.0f / (oldMax - oldMin));
}

//                      Smoothing Kernel calculations
/* 
Smoothing kernel equation extracted from 
Particle-Based Fluid Simulation for Interactive Applications by Matthias Muller, David Charypar and Markus Gross

 example Poly6 smoothing kernel used in soSPH (Smoothed Particle Hydrodynamics):
 W_poly6(r, h) = (315 / (64 * pi * h^9)) * (h^2 - r^2)^3, for 0 <= r <= h
              = 0, otherwise

 where:
 - r is the distance to the particle (r = ||r_i - r_j||)
 - h is the smoothing length (defines the kernel support radius)
 - The kernel smoothly goes to 0 at r = h and is normalized over its support
*/

// 3d conversion: done
float SmoothingKernelPoly6(float dst, float radius)
{
    if (dst < radius)
    {
        float scale = 315.0f / (64.0f * 3.14159265f * pow(max(0.0001f, radius), 9));
        float v = radius * radius - dst * dst;
        return v * v * v * scale;
    }
    return 0;
}


// 3d conversion: done
//Integrate[(h-r)^2 r^2 Sin[theta], {r, 0, h}, {theta, 0, pi}, {phi, 0, 2*pi}]
float SpikyKernelPow2(float dst, float radius)
{
    if (dst < radius)
    {
        float scale = 15.0f / (2.0f * 3.14159265f * pow(max(0.0001f, radius), 5));
        float v = radius - dst;
        return v * v * scale;
    }
    return 0;
}


// 3d conversion: done
float SpikyKernelPow3(float dst, float radius)
{
    if (dst < radius)
    {
        float scale = 15.0f / (3.14159265f * pow(max(0.0001f, radius), 6));
        float v = radius - dst;
        return v * v * v * scale;
    }
    return 0;
}


// 3d conversion: done
float DerivativeSpikyPow2(float dst, float radius)
{
    if (dst <= radius)
    {
        float scale = 15.0f / (pow(max(0.0001f, radius), 5.0f) * 3.14159265f);
        float v = radius - dst;
        return -v * scale;
    }
    return 0;
}

// 3d conversion: done
float DerivativeSpikyPow3(float dst, float radius)
{
    if (dst <= radius)
    {
        float scale = 45.0f / (pow(max(0.0001f, radius), 6.0f) * 3.14159265f);
        float v = radius - dst;
        return -v * v * scale;
    }
    return 0;
}

float GetParticleInfluence(float dst, float radius)
{
    return SmoothingKernelPoly6(dst, radius);
}

float GetParticleInfluenceSharp(float r, float h)
{
    return SpikyKernelPow2(r, h);
}

float GetParticleInfluenceSlopeSharp(float r, float h)
{
    return DerivativeSpikyPow2(r, h);
}

float GetParticleInfluenceSharpV3(float r, float h)
{
    return SpikyKernelPow3(r, h);
}

float GetParticleInfluenceSlopeSharpV3(float r, float h)
{
    return DerivativeSpikyPow3(r, h);
}










