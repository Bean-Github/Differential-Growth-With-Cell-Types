using UnityEngine;

namespace Growth3DCompute
{
    // stores the base genotype / starting data of the grower,
    // which eventually makes the complex shape
    public class Grower3DGenotype : MonoBehaviour
    {
        // starting mesh
        public Mesh mesh;

        // types of nodes, instantiated once
        public NodeType[] baseNodeTypes;

        public Grower3DComputeRunner computeRunner;


        // assign the starting cell types to the compute runner
        public void AssignStartCellTypes(NodeHoardCompute nodeHoard)
        {
            for (int i = 0; i < nodeHoard.allNodes.Count; i++)
            {
                Node3D node = nodeHoard.allNodes[i];

                // change type based on condition
                if (i == 0) node.baseType = 1;

                // set the type of the node based on its baseType
                node.type = baseNodeTypes[node.baseType];

                nodeHoard.allNodes[i] = node;
            }
        }

        // set genotype in real time
        private void Update()
        {
            computeRunner.nodeTypesBuffer.SetData(baseNodeTypes);
        }

    }
}
