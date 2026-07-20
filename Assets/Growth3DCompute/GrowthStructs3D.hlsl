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
    
    float auxinGenerationRate;
    float auxinTransportRate;
    float auxinDiffusionRate;
    float auxinStealRate;
    
    float auxinThreshold;
    
    float auxinGrowthFactor; // how much 1 auxin contributes to the growth rate
    
    float3 growthTensor;
    
    float4 color;
    
    uint useGravity;
};


StructuredBuffer<NodeType> NodeTypes;

void GetCellInfo(uint cellTypeIndex, out NodeType nodeType)
{
    nodeType = NodeTypes[cellTypeIndex];
}


NodeType BlendTypes(NodeType typeA, NodeType typeB, float blendFactor)
{
    NodeType blendedType;
 
    float hardBlendFactor = blendFactor <= 0.5 ? 0.0 : 1.0; // If blendFactor < 0.5, use typeA's values, else use typeB's values
    // Blend the float properties
    blendedType.mass = lerp(typeA.mass, typeB.mass, blendFactor);
    blendedType.drag = lerp(typeA.drag, typeB.drag, blendFactor);
    blendedType.growthRate = lerp(typeA.growthRate, typeB.growthRate, hardBlendFactor);
    blendedType.turgorPressure = lerp(typeA.turgorPressure, typeB.turgorPressure, blendFactor);
    blendedType.laplacianSmoothing = lerp(typeA.laplacianSmoothing, typeB.laplacianSmoothing, blendFactor);
    
    // based on inheritance weight, we can decide which type's switchTime, targetType, and childType to use
    blendedType.switchTime = lerp(typeA.switchTime, typeB.switchTime, hardBlendFactor);
    blendedType.targetType = hardBlendFactor < 0.5 ? typeA.targetType : typeB.targetType;
    blendedType.childType = hardBlendFactor < 0.5 ? typeA.childType : typeB.childType;
    
    blendedType.inheritanceWeight = lerp(typeA.inheritanceWeight, typeB.inheritanceWeight, hardBlendFactor);
    
    blendedType.auxinGenerationRate = lerp(typeA.auxinGenerationRate, typeB.auxinGenerationRate, hardBlendFactor);
    blendedType.auxinTransportRate = lerp(typeA.auxinTransportRate, typeB.auxinTransportRate, blendFactor);
    blendedType.auxinDiffusionRate = lerp(typeA.auxinDiffusionRate, typeB.auxinDiffusionRate, blendFactor);
    blendedType.auxinStealRate = lerp(typeA.auxinStealRate, typeB.auxinStealRate, blendFactor);
    
    blendedType.auxinThreshold = lerp(typeA.auxinThreshold, typeB.auxinThreshold, hardBlendFactor);
    blendedType.auxinGrowthFactor = lerp(typeA.auxinGrowthFactor, typeB.auxinGrowthFactor, blendFactor);
    
    blendedType.growthTensor = lerp(typeA.growthTensor, typeB.growthTensor, hardBlendFactor);
    
    blendedType.useGravity = hardBlendFactor < 0.5 ? typeA.useGravity : typeB.useGravity;
    
    // blend the colors based on the relative opacities
    float totalOpacity = max(0.001f, typeA.color.a + typeB.color.a);
    float weightA = typeA.color.a / totalOpacity;
    if (typeA.color.a + typeB.color.a < 0.001f) {
        weightA = 0.5f; // If both are fully transparent, blend equally
    }
    
    blendedType.color = lerp(typeB.color, typeA.color, weightA);
    
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
    
    float currAuxinLevel;
    
    
    // basis vectors defining local coordinate system
    float3 tangent; // up
    float3 normal; // forward
    float3 binormal; // right
    
    
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







