

// DATA STRUCTURES

struct Node3D
{
    // pointers
    uint halfEdge;
    uint id;
 
    // physics
    float age;
    
    float3 position;
    float3 velocity;
    
    float curvature;
    float mass;
    float drag;
    
    float growthRate;
    
    int debug_int;
};

struct HalfEdge3D
{
    // pointers
    uint origin; // at the start of this half-edge
    uint target; // the target node
    
    uint next; // The next half-edge in the face loop
    uint prev; // prev half-edge in the face loop
    uint twin; // twin half-edge, same edge but opposite direction
    
    uint face;
    
    uint id;
    
    // splitting info
    bool canSplit;
    
    bool isBoundary;
    bool isGhost;
    
    // physics
    float age;
    
    float springStiffness;

    float baseRestLength; // starting rest length for this edge
    float currRestLength;
    float splitDistanceThreshold;
};

struct Face3D
{
    uint halfEdge; // ID of one of the half-edges bounding this face
    
    uint id;
};







