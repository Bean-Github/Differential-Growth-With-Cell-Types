// HASHING OPTIMIZATION

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
    uint index;
    uint hash;
    uint cellKey;
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
static const uint hashK2 = 213562;
static const uint hashK3 = 177723;

uint HashCell(int cellX, int cellY, int cellZ)
{
    int3 cell = int3(cellX, cellY, cellZ); // safe offset
    
    const uint blockSize = 10;
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


int3 PositionToCellCoord(float3 position, float cellSize) 
{    
    return (int3) floor((position) / cellSize);
}










