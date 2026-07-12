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
                uint typeIndex = nodeHoard.allNodes[i].type;

                Node3D node = nodeHoard.allNodes[i];

                // change type based on condition
                if (i == 0 || i == 1 || i == 2 || i == 3) node.type = 1;

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
