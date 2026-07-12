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

        private NodeHoardCompute SubdivideTopology(NodeHoardCompute baseData)
        {
            NodeHoardCompute nextData = new NodeHoardCompute();

            // We need to track the new Node IDs generated for Faces, Edges, and Vertices
            uint[] facePointNodes = new uint[baseData.faces.Count];
            uint[] edgePointNodes = new uint[baseData.halfEdges.Count];
            uint[] vertexPointNodes = new uint[baseData.allNodes.Count];

            // --- STEP 1: FACE POINTS ---
            // Average of all original points of the face
            Vector3[] rawFacePoints = new Vector3[baseData.faces.Count];
            for (int i = 0; i < baseData.faces.Count; i++)
            {
                Face3D face = baseData.faces[i];
                uint startEdge = face.halfEdge;
                uint currEdge = startEdge;
                Vector3 center = Vector3.zero;
                int vertexCount = 0;

                do
                {
                    HalfEdge3D he = baseData.GetEdge(currEdge);
                    center += baseData.GetNode(he.origin).position;
                    vertexCount++;
                    currEdge = he.next;
                } while (currEdge != startEdge);

                rawFacePoints[i] = center / vertexCount;
                facePointNodes[i] = nextData.AddNode(rawFacePoints[i], 1); // Type 1 = Face Point
            }

            // --- STEP 2: EDGE POINTS ---
            // Average of the two original edge endpoints AND the face points of the two adjoining faces.
            for (int i = 0; i < baseData.halfEdges.Count; i++)
            {
                HalfEdge3D he = baseData.GetEdge((uint)i);

                // Skip processing if this is a ghost edge, or if we've already processed its twin
                if (he.isGhost == 1) continue;
                if (he.twin != uint.MaxValue && he.id > he.twin) continue;

                Vector3 v1 = baseData.GetNode(he.origin).position;
                Vector3 v2 = baseData.GetNode(he.target).position;

                Vector3 edgePointPos;

                // Interior Edge
                if (he.twin != uint.MaxValue && baseData.GetEdge(he.twin).isGhost == 0)
                {
                    Vector3 f1 = rawFacePoints[he.face];
                    Vector3 f2 = rawFacePoints[baseData.GetEdge(he.twin).face];
                    edgePointPos = (v1 + v2 + f1 + f2) / 4f;
                }
                else // Boundary Edge
                {
                    edgePointPos = (v1 + v2) / 2f;
                }

                uint newEdgeNodeId = nextData.AddNode(edgePointPos, 2); // Type 2 = Edge Point
                edgePointNodes[he.id] = newEdgeNodeId;
                if (he.twin != uint.MaxValue) edgePointNodes[he.twin] = newEdgeNodeId;
            }

            // --- STEP 3: VERTEX POINTS (Moving original vertices) ---
            for (int i = 0; i < baseData.allNodes.Count; i++)
            {
                Node3D node = baseData.allNodes[i];

                // Find all faces and edges touching this vertex by circulating around its half-edges
                uint startEdge = node.halfEdge;

                if (startEdge == uint.MaxValue) continue; // Unconnected node

                uint currEdge = startEdge;
                Vector3 avgFacePoints = Vector3.zero;
                Vector3 avgEdgeMidpoints = Vector3.zero;
                int valence = 0;
                bool isOnBoundary = false;

                do
                {
                    HalfEdge3D he = baseData.GetEdge(currEdge);

                    if (he.isGhost == 0)
                    {
                        avgFacePoints += rawFacePoints[he.face];
                    }

                    // Note: Catmull-Clark uses the midpoint of the ORIGINAL edge here, not the new Edge Point
                    Vector3 edgeMidpoint = (baseData.GetNode(he.origin).position + baseData.GetNode(he.target).position) / 2f;
                    avgEdgeMidpoints += edgeMidpoint;

                    valence++;

                    if (he.isBoundary == 1 || he.twin == uint.MaxValue || baseData.GetEdge(he.twin).isGhost == 1)
                    {
                        isOnBoundary = true;
                    }

                    // Move to the next half-edge around the vertex (Twin -> Next)
                    if (he.twin != uint.MaxValue)
                        currEdge = baseData.GetEdge(he.twin).next;
                    else
                        break; // Hit a hard boundary without ghosts

                } while (currEdge != startEdge);

                Vector3 newVertexPos;

                if (isOnBoundary)
                {
                    // Simplified boundary smoothing rule
                    newVertexPos = node.position;
                }
                else
                {
                    avgFacePoints /= valence;
                    avgEdgeMidpoints /= valence;

                    // Catmull-Clark Vertex Formula
                    float n = valence;
                    newVertexPos = (avgFacePoints + (2f * avgEdgeMidpoints) + ((n - 3f) * node.position)) / n;
                }

                vertexPointNodes[node.id] = nextData.AddNode(newVertexPos, 0); // Type 0 = Original Vertex
            }

            // --- STEP 4: WIRE UP NEW QUADS ---
            // For each face, build a quad for each of its original edges
            for (int i = 0; i < baseData.faces.Count; i++)
            {
                Face3D face = baseData.faces[i];
                uint startEdge = face.halfEdge;
                uint currEdge = startEdge;

                do
                {
                    HalfEdge3D he = baseData.GetEdge(currEdge);

                    uint vNew = vertexPointNodes[he.origin];             // 1. Adjusted original vertex
                    uint eNext = edgePointNodes[he.id];                  // 2. Edge point of current edge
                    uint fPoint = facePointNodes[face.id];               // 3. Central face point
                    uint ePrev = edgePointNodes[baseData.GetEdge(he.prev).id]; // 4. Edge point of previous edge

                    nextData.AddQuad(vNew, eNext, fPoint, ePrev);

                    currEdge = he.next;
                } while (currEdge != startEdge);
            }

            // Seal boundaries of the new mesh to create the necessary ghost edges
            nextData.SealOpenBoundaries();

            return nextData;
        }
    }

}