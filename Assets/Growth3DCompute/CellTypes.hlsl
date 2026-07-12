struct NodeType
{
    float mass;
    float drag;
    
    float growthRate;
    
    float turgorPressure;
    
    float laplacianSmoothing;
    
    float switchTime;
    uint targetType;
    
    uint childType;
};


StructuredBuffer<NodeType> NodeTypes;

void GetCellInfo(uint cellTypeIndex, out NodeType nodeType)
{
    nodeType = NodeTypes[cellTypeIndex];
}





