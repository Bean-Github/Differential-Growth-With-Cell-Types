using UnityEngine;


[System.Serializable]
public struct NodeType
{
    public float mass;
    public float drag;

    public float growthRate;
    public float turgorPressure;

    public float laplacianSmoothing;

    public float switchTime;
    public uint targetType;

    public float inheritanceWeight;
    public uint childType;

    public float auxinGenerationRate;
    public float auxinTransportRate;
    public float auxinDiffusionRate;
    public float auxinStealRate;

    public float auxinThreshold;

    public float auxinGrowthFactor;

    public Vector3 growthTensor;

    public Color color;
    
    public uint useGravity;
}

// ALL STRUCTS
public unsafe struct Node3D
{
    // pointers
    public uint halfEdge; // ID of one of the half-edges originating from this vertex
    public uint id;

    // physics
    public float age;

    public Vector3 position;
    public Vector3 velocity;

    public float curvature;

    public uint baseType;

    // settings
    public NodeType type;

    // auxin
    public float currAuxinLevel;

    // basis vectors defining local coordinate system
    public Vector3 tangent; // up
    public Vector3 normal; // forward
    public Vector3 binormal; // right

    public int debug_int; // for debugging purposes only
}

public unsafe struct HalfEdge3D
{
    // pointers
    public uint origin; // node refs
    public uint target;

    public uint next; // edge refs
    public uint prev;
    public uint twin;

    // the face this half-edge belongs to
    public uint face;

    public uint id; // this index

    // splitting info
    public int canSplit;

    public int isBoundary;
    public int isGhost;

    // physics
    public float age;

    public float springStiffness;

    public float baseRestLength; // starting rest length for this edge
    public float currRestLength;
    public float splitDistanceThreshold;
};

public unsafe struct Face3D
{
    public uint halfEdge; // ID of one of the half-edges bounding this face

    // this index
    public uint id;
};


