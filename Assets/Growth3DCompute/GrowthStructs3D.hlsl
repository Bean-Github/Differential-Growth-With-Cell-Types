

// DATA STRUCTURES

struct Node3D
{
    float3 position;
    float3 velocity;
    
    float curvature;
    float mass;
    
    uint halfEdge;
        
    uint id;

    int debug_int;
};

struct HalfEdge3D
{
    uint origin; // at the start of this half-edge
    uint target; // the target node
    
    uint next; // The next half-edge in the face loop
    uint prev; // prev half-edge in the face loop
    uint twin; // twin half-edge, same edge but opposite direction
    
    uint face;
    
    uint id;
        
    bool wantsToSplit;
    int canSplit;
    
    bool wantsToFlip;
    bool canFlip;
    
    bool isBoundary;
    bool isGhost;
};

struct Face3D
{
    uint halfEdge; // ID of one of the half-edges bounding this face
    
    uint id;
};







