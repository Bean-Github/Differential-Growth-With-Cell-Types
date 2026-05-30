
const uint UINT_MAX = 0xFFFFFFFF; // 2^32 - 1 = 4294967295



struct Node3D
{
    float3 position;
    float curvature;
    
    float3 velocity;
    float mass;
    
    int neighborStartIndex;
    int neighborCount;
};



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


//                OPTIMIZATION

// hash a cell coordinate to a hashed value (by multiplying by a large prime number), 
// which will wrap around based on 
static const int3 Offsets3D[27] =
{
    int3(-1, -1, -1),
	int3(-1, -1, 0),
	int3(-1, -1, 1),
	int3(-1, 0, -1),
	int3(-1, 0, 0),
	int3(-1, 0, 1),
	int3(-1, 1, -1),
	int3(-1, 1, 0),
	int3(-1, 1, 1),
	int3(0, -1, -1),
	int3(0, -1, 0),
	int3(0, -1, 1),
	int3(0, 0, -1),
	int3(0, 0, 0),
	int3(0, 0, 1),
	int3(0, 1, -1),
	int3(0, 1, 0),
	int3(0, 1, 1),
	int3(1, -1, -1),
	int3(1, -1, 0),
	int3(1, -1, 1),
	int3(1, 0, -1),
	int3(1, 0, 0),
	int3(1, 0, 1),
	int3(1, 1, -1),
	int3(1, 1, 0),
	int3(1, 1, 1)
};

struct Entry
{
    uint particleIndex;
    uint hash;
    uint cellKey;
    uint pad;
};

float3 PseudoRandomDir3D(int seed)
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

static const uint hashK1 = 15823;
static const uint hashK2 = 9737333;
static const uint hashK3 = 440817757;

uint HashCell(int cellX, int cellY, int cellZ)
{
    int3 cell = int3(cellX, cellY, cellZ); // safe offset
    
    const uint blockSize = 50;
    uint3 ucell = (uint3) (cell + blockSize / 2);

    uint3 localCell = ucell % blockSize;
    uint3 blockID = ucell / blockSize;
    uint blockHash = blockID.x * hashK1 + blockID.y * hashK2 + blockID.z * hashK3;
    return localCell.x + blockSize * (localCell.y + blockSize * localCell.z) + blockHash;

}

uint KeyFromHash(uint hash, uint tableSize)
{
    return hash % (tableSize);
}


int3 PositionToCellCoord(float3 position, float cellSize, float3 boundsCenter, float3 boundsExtents) 
{
    return (int3) floor(position / cellSize);
    
    float3 offset = (boundsCenter - boundsExtents);

    int3 numCells = (int3) ceil((boundsExtents * 2) / cellSize);
    int3 cell = (int3) floor((position - offset) / cellSize);
    
    cell.x = clamp(cell.x, 0, numCells.x - 1);
    cell.y = clamp(cell.y, 0, numCells.y - 1);
    cell.z = clamp(cell.z, 0, numCells.z - 1);
        
    return cell;
    
    //return (int3) floor(position / cellSize);
}










