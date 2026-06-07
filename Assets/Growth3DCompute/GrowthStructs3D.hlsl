

// DATA STRUCTURES

struct Node3D
{
    float3 position;
    float3 velocity;
    
    float curvature;
    float mass;
    
    uint neighborCount;
    
    int neighbors[8];
    
    uint isLocked; // 0 = false, 1 = true
    
    uint halfEdgeIndex;
};


struct HalfEdge
{
    uint originVertexIndex; // Vertex at the start of this half-edge
    uint twinIndex; // The opposite half-edge
    uint nextIndex; // The next half-edge in the face loop
    uint faceIndex; // The face this half-edge belongs to
};

struct Face
{
    uint halfEdgeIndex; // ID of one of the half-edges bounding this face
};








