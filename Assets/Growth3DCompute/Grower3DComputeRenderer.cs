using Growth3DCompute;
using System.Runtime.InteropServices;
using UnityEditor;
using UnityEngine;

namespace Growth3DCompute
{
    public class Grower3DComputeRenderer : MonoBehaviour
    {
        public Grower3DComputeRunner computeRunner;

        [SerializeField, Range(0, 5)]
        private int subdivisions = 1;

        // rendering
        [Header("Rendering")]
        public BasicParticleBufferRenderer particleRenderer;
        public BasicEdgeBufferRenderer edgeRenderer;
        public MeshFilter nodeHoardMeshFilter;

        [Header("Debug")]
        public bool debugNodeTypes = true;

        private void Update()
        {
            // get the information back from the shader to render
            particleRenderer.RenderParticles(computeRunner.nodeBuffer, computeRunner.nodeCount);
            edgeRenderer.RenderEdges(computeRunner.nodeBuffer, computeRunner.halfEdgeBuffer, computeRunner.nodeCount, computeRunner.halfEdgeCount);

            if (Input.GetKeyDown(KeyCode.M))
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

                for (int i = 0; i < subdivisions; i++)
                    nodeHoardCompute = SubdivideTriangles(nodeHoardCompute);

                Mesh newMesh = NodeHoardMeshGenerator.GenerateMesh(nodeHoardCompute);

                nodeHoardMeshFilter.mesh = newMesh;
            }
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            if (!debugNodeTypes)
                return;

            if (computeRunner == null || computeRunner.nodeBuffer == null || computeRunner.nodeCount == 0)
                return;

            Node3D[] nodeData = new Node3D[computeRunner.nodeCount];
            computeRunner.nodeBuffer.GetData(nodeData);

            Handles.color = Color.white;

            foreach (Node3D node in nodeData)
            {
                Handles.Label(
                    node.position + Vector3.up * 0.02f,
                    $"Base: {node.baseType}\nAge: {node.age:F1}"
                );
            }
        }
#endif

        private NodeHoardCompute SubdivideTriangles(NodeHoardCompute baseData)
        {
            NodeHoardCompute nextData = new NodeHoardCompute();

            // We only need to track original vertices and new edge midpoints
            uint[] vertexNodes = new uint[baseData.allNodes.Count];
            uint[] edgeMidpoints = new uint[baseData.halfEdges.Count];

            // --- STEP 1: KEEP ORIGINAL VERTICES ---
            for (int i = 0; i < baseData.allNodes.Count; i++)
            {
                // For linear subdivision, we don't smooth the original vertices, just copy them.
                vertexNodes[i] = nextData.AddNode(baseData.allNodes[i].position, 0);
            }

            // --- STEP 2: EDGE MIDPOINTS ---
            for (int i = 0; i < baseData.halfEdges.Count; i++)
            {
                HalfEdge3D he = baseData.GetEdge((uint)i);

                if (he.isGhost == 1) continue;

                // Deduplicate: only skip if the twin is ALSO real and has a smaller ID
                if (he.twin != uint.MaxValue)
                {
                    HalfEdge3D twinEdge = baseData.GetEdge(he.twin);
                    if (twinEdge.isGhost == 0 && he.id > he.twin)
                        continue;
                }

                Vector3 p1 = baseData.GetNode(he.origin).position;
                Vector3 p2 = baseData.GetNode(he.target).position;

                // Just take the exact middle of the edge
                Vector3 midPoint = (p1 + p2) / 2f;

                uint midId = nextData.AddNode(midPoint, 2); // Type 2 = Edge Point
                edgeMidpoints[he.id] = midId;

                if (he.twin != uint.MaxValue)
                    edgeMidpoints[he.twin] = midId;
            }

            // --- STEP 3: WIRE UP 4 NEW TRIANGLES PER FACE ---
            for (int i = 0; i < baseData.faces.Count; i++)
            {
                Face3D face = baseData.faces[i];

                // Because it is a triangle, we hardcode the 3 steps instead of looping!
                HalfEdge3D e0 = baseData.GetEdge(face.halfEdge);
                HalfEdge3D e1 = baseData.GetEdge(e0.next);
                HalfEdge3D e2 = baseData.GetEdge(e1.next);

                // Safety Check: Does e2 actually connect back to e0? 
                if (e2.next != e0.id)
                {
                    Debug.LogWarning($"Compute Shader corrupted Face {face.id}: Not a closed triangle. Skipping.");
                    continue;
                }

                // Get the 3 original corners
                uint v0 = vertexNodes[e0.origin];
                uint v1 = vertexNodes[e1.origin];
                uint v2 = vertexNodes[e2.origin];

                // Get the 3 edge midpoints
                uint m0 = edgeMidpoints[e0.id];
                uint m1 = edgeMidpoints[e1.id];
                uint m2 = edgeMidpoints[e2.id];

                // Create the 4 new sub-triangles
                // Note: You will need to implement AddTriangle() in NodeHoardCompute 
                // if you previously only had AddQuad()
                nextData.AddTriangle(v0, m0, m2); // Corner A
                nextData.AddTriangle(m0, v1, m1); // Corner B
                nextData.AddTriangle(m2, m1, v2); // Corner C
                nextData.AddTriangle(m0, m1, m2); // The inverted center piece
            }

            // Seal boundaries of the new mesh to create the necessary ghost edges
            nextData.SealOpenBoundaries();

            return nextData;
        }

    }
}