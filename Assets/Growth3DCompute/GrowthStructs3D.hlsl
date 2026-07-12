struct NodeType
{
    float mass;
    float drag;
    
    float growthRate;
    
    float turgorPressure;
    
    float laplacianSmoothing;
    
    float switchTime;
    uint targetType;
    
    float inheritanceWeight;
    
    uint childType;
};


StructuredBuffer<NodeType> NodeTypes;

void GetCellInfo(uint cellTypeIndex, out NodeType nodeType)
{
    nodeType = NodeTypes[cellTypeIndex];
}


NodeType BlendTypes(NodeType typeA, NodeType typeB, float blendFactor)
{
    NodeType blendedType;
 
    // Blend the float properties
    blendedType.mass = lerp(typeA.mass, typeB.mass, blendFactor);
    blendedType.drag = lerp(typeA.drag, typeB.drag, blendFactor);
    blendedType.growthRate = lerp(typeA.growthRate, typeB.growthRate, blendFactor);
    blendedType.turgorPressure = lerp(typeA.turgorPressure, typeB.turgorPressure, blendFactor);
    blendedType.laplacianSmoothing = lerp(typeA.laplacianSmoothing, typeB.laplacianSmoothing, blendFactor);
    
    // based on inheritance weight, we can decide which type's switchTime, targetType, and childType to use
    float hardBlendFactor = blendFactor <= 0.5 ? 0.0 : 1.0; // If blendFactor < 0.5, use typeA's values, else use typeB's values
    blendedType.switchTime = lerp(typeA.switchTime, typeB.switchTime, hardBlendFactor);
    blendedType.targetType = hardBlendFactor < 0.5 ? typeA.targetType : typeB.targetType;
    blendedType.childType = hardBlendFactor < 0.5 ? typeA.childType : typeB.childType;
    
    blendedType.inheritanceWeight = lerp(typeA.inheritanceWeight, typeB.inheritanceWeight, hardBlendFactor);
    
    return blendedType;
}

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
    
    // settings
    //uint type;
    uint baseType;
    
    NodeType type;
    
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







