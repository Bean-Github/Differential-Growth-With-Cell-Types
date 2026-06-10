

// DATA STRUCTURES

struct Node3D
{
    float3 position;
    float3 velocity;
    
    float curvature;
    float mass;
    
    uint halfEdge;
    
    uint isLocked; // 0 = false, 1 = true
    
    uint id;
};

struct HalfEdge
{
    uint origin; // at the start of this half-edge
    uint target; // the target node
    
    uint next; // The next half-edge in the face loop
    uint prev; // prev half-edge in the face loop
    uint twin; // twin half-edge, same edge but opposite direction
    
    uint face;
    
    uint id;
};

struct Face
{
    uint edge; // ID of one of the half-edges bounding this face
    
    uint id;
};







