using Growth3DCompute;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Growth3DCompute
{
    public class Grower3DComputeRenderer : MonoBehaviour
    {
        public Grower3DComputeRunner computeRunner;

        // rendering
        [Header("Rendering")]
        public BasicParticleBufferRenderer particleRenderer;
        public BasicEdgeBufferRenderer edgeRenderer;
        public MeshFilter nodeHoardMeshFilter;


        private void Update()
        {
            // get the information back from the shader to render
            particleRenderer.RenderParticles(computeRunner.nodeBuffer, computeRunner.nodeCount);
            edgeRenderer.RenderEdges(computeRunner.nodeBuffer, computeRunner.halfEdgeBuffer, computeRunner.nodeCount, computeRunner.halfEdgeCount);

            if (Input.GetKey(KeyCode.M))
            {
                // convert to mesh
                // extract the data back to CPU and log it for debugging
                Node3D[] nodeData = new Node3D[computeRunner.nodeCount];
                computeRunner.nodeBuffer.GetData(nodeData, 0, 0, computeRunner.nodeCount);

                HalfEdge3D[] halfEdgeData = new HalfEdge3D[computeRunner.halfEdgeCount];
                computeRunner.halfEdgeBuffer.GetData(halfEdgeData, 0, 0, computeRunner.halfEdgeCount);

                Face3D[] faceData = new Face3D[computeRunner.faceCount];
                computeRunner.faceBuffer.GetData(faceData, 0, 0, computeRunner.faceCount);

                NodeHoardCompute nodeHoardCompute = new NodeHoardCompute(nodeData, halfEdgeData, faceData);

                Mesh newMesh = NodeHoardMeshGenerator.GenerateMesh(nodeHoardCompute);

                nodeHoardMeshFilter.mesh = newMesh;
            }
        }

    }

}