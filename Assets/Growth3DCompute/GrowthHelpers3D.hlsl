
const uint UINT_MAX = 0xFFFFFFFF; // 2^32 - 1 = 4294967295

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


// (Based on Christer Ericson's Real-Time Collision Detection, chapter 5, page 139)
float3 ClosestPointOnTriangle(float3 p, float3 a, float3 b, float3 c)
{
    float3 ab = b - a;
    float3 ac = c - a;
    float3 bc = c - b;
// Compute parametric position s for projection P’ of P on AB,
// P’ = A + s*AB, s = snom/(snom+sdenom)
    float snom = dot(p - a, ab), sdenom = dot(p - b, a - b);
// Compute parametric position t for projection P’ of P on AC,
// P’ = A + t*AC, s = tnom/(tnom+tdenom)
    float tnom = dot(p - a, ac), tdenom = dot(p - c, a - c);
    if (snom <= 0.0f && tnom <= 0.0f)
        return a; // Vertex region early out
// Compute parametric position u for projection P’ of P on BC,
// P’ = B + u*BC, u = unom/(unom+udenom)
    float unom = dot(p - b, bc), udenom = dot(p - c, b - c);
    if (sdenom <= 0.0f && unom <= 0.0f)
        return b; // Vertex region early out
    if (tdenom <= 0.0f && udenom <= 0.0f)
        return c; // Vertex region early out
// P is outside (or on) AB if the triple scalar product [N PA PB] <= 0
    float3 n = cross(b - a, c - a);
    float vc = dot(n, cross(a - p, b - p));
// If P outside AB and within feature region of AB,
// return projection of P onto AB
    if (vc <= 0.0f && snom >= 0.0f && sdenom >= 0.0f)
        return a + snom / (snom + sdenom) * ab;
// P is outside (or on) BC if the triple scalar product [N PB PC] <= 0
    float va = dot(n, cross(b - p, c - p));
// If P outside BC and within feature region of BC,
// return projection of P onto BC
    if (va <= 0.0f && unom >= 0.0f && udenom >= 0.0f)
        return b + unom / (unom + udenom) * bc;
// P is outside (or on) CA if the triple scalar product [N PC PA] <= 0
    float vb = dot(n, cross(c - p, a - p));
// If P outside CA and within feature region of CA,
// return projection of P onto CA
    if (vb <= 0.0f && tnom >= 0.0f && tdenom >= 0.0f)
        return a + tnom / (tnom + tdenom) * ac;
// P must project inside face region. Compute Q using barycentric coordinates
    float u = va / (va + vb + vc);
    float v = vb / (va + vb + vc);
    float w = 1.0f - u - v; // = vc / (va + vb + vc)
    return u * a + v * b + w * c;
}

// Finds the closest point on a line segment AB to a point P
float3 ClosestPointOnLineSegment(float3 p, float3 a, float3 b)
{
    float3 ab = b - a;
    // Project P onto AB, computing the parameterized position t
    float t = dot(p - a, ab) / max(dot(ab, ab), 0.00001f);
    
    // Clamp t to the [0, 1] range to stay on the line segment
    t = saturate(t);
    
    return a + t * ab;
}

// Finds the closest points between two line segments: S1(p1 to q1) and S2(p2 to q2).
// Returns the parameterized positions (s and t) from 0.0 to 1.0, and the actual 3D points (c1 and c2).
void ClosestPointsOnTwoLineSegments(float3 p1, float3 q1, float3 p2, float3 q2,
                                    out float s, out float t,
                                    out float3 c1, out float3 c2)
{
    float3 d1 = q1 - p1; // Direction of S1
    float3 d2 = q2 - p2; // Direction of S2
    float3 r = p1 - p2;
    float a = dot(d1, d1);
    float e = dot(d2, d2);
    float f = dot(d2, r);

    // If both segments degenerate to points
    if (a <= 0.00001f && e <= 0.00001f)
    {
        s = t = 0.0f;
        c1 = p1;
        c2 = p2;
        return;
    }
    
    if (a <= 0.00001f)
    {
        s = 0.0f;
        t = saturate(f / e);
    }
    else
    {
        float c = dot(d1, r);
        if (e <= 0.00001f)
        {
            t = 0.0f;
            s = saturate(-c / a);
        }
        else
        {
            float b = dot(d1, d2);
            float denom = a * e - b * b;
            if (denom != 0.0f)
            {
                s = clamp((b * f - c * e) / denom, 0.0f, 1.0f);
            }
            else
            {
                s = 0.0f;
            }
            t = (b * s + f) / e;
            if (t < 0.0f)
            {
                t = 0.0f;
                s = saturate(-c / a);
            }
            else if (t > 1.0f)
            {
                t = 1.0f;
                s = saturate((b - c) / a);
            }
        }
    }
    
    c1 = p1 + d1 * s;
    c2 = p2 + d2 * t;
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


float3 GetRandomDirection3D(uint seed)
{
    float x = sin(seed * 12.9898);
    float y = cos(seed * 78.233);
    float z = sin(seed * 37.719);
    
    // Generate two angles from hashed values
    float theta = frac(x + y) * 6.2831853; // azimuthal angle [0, 2pi]
    float phi = acos(frac(z) * 2.0 - 1.0); // polar angle [0, pi]
    // Convert spherical to cartesian
    float sinPhi = sin(phi);
    return float3(
        cos(theta) * sinPhi,
        sin(theta) * sinPhi,
        cos(phi)
    );
}











