using Growth3DCompute;
using UnityEngine;

namespace Growth3DCompute
{
    // stores the base genotype / starting data of the grower,
    // which eventually makes the complex shape.

    [CreateAssetMenu(fileName = "New Grower Genotype", menuName = "Growth3D/Grower Genotype")]
    public class Grower3DGenotype : ScriptableObject
    {
        // starting mesh
        public Mesh mesh;

        // types of nodes, instantiated once
        public NodeType[] baseNodeTypes;
    }
}