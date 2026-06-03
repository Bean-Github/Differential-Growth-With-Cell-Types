using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

namespace Growth3DCompute
{ 
    public class Grower3DComputeRunner : MonoBehaviour
    {
        public int subdivisions = 2;

        [Header("Parameters")]
            public int maxNodes = 100000;
            private int nodeCount;       // Keep this, but make it private!
            int paddedNodeCount; // the actual size of the buffer, which is the next power of 2 from nodeCount

        [Tooltip("Strength of repulsion between nodes")]
            public float separationForce = 0.5f;
            [Tooltip("Maximum separation distance (also the spatial hash cell size)")]
            public float separationDistance = 3.0f;

            public float attractionForce = 0.2f;
            public float laplacianSmoothing = 0.1f;

            [Tooltip("Drag applied to node velocity")]
            public float nodeDrag = 0.1f;


        public float splitDistanceThreshold = 5.0f;

        [Header("Shader Setup")]
            public ComputeShader computeShader;
            public SpatialHashComputeRunner spatialHashRunner;
            public Collider spawnCollider;

        // rendering
        [Header("Rendering")]
            public BasicParticleBufferRenderer particleRenderer;
            public BasicEdgeBufferRenderer edgeRenderer;

        // BUFFERS
        ComputeBuffer nodeBuffer;
        //ComputeBuffer neighborBuffer;

        ComputeBuffer spatialLookupBuffer;
        ComputeBuffer startIndicesBuffer;

        ComputeBuffer counterBuffer;
        int[] counterArray;

        Node3D[] nodeData;

        // values
        protected int evaluateSplitsKernel;
        protected int applyNaturalForcesKernel;
        protected int moveKernel;



        void Start()
        {
            CreateBuffers();

            SetKernelsAndBuffers();

            SetShaderParams();
        }

        private void Update()
        {
            // execute the shader
            RunComputeShader();

            // read back the atomic counter from the GPU
            counterBuffer.GetData(counterArray);

            // update our C# with the new total
            nodeCount = Mathf.Min(counterArray[0], maxNodes);

            print("curr num nodes: " + nodeCount);

            // get the information back from the shader to render
            particleRenderer.RenderParticles(nodeBuffer, nodeCount);
            edgeRenderer.RenderEdges(nodeBuffer, nodeCount);
        }

        protected void CreateBuffers()
        {
            counterArray = new int[1];
            counterBuffer = new ComputeBuffer(1, sizeof(int), ComputeBufferType.Raw);

            // init particles
            CreateNodeBuffer();

            spatialLookupBuffer = new ComputeBuffer(maxNodes, sizeof(uint) * 4);
            startIndicesBuffer = new ComputeBuffer(maxNodes, sizeof(uint));
        }

        protected unsafe void CreateNodeBuffer()
        {
            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();
            ShapeGenerator.CreateIcosphere(out vertices, out triangles, subdivisions: subdivisions);

            // SETUP NODES
            int meshVertexCount = vertices.Count;
            nodeCount = meshVertexCount;
            paddedNodeCount = Mathf.NextPowerOfTwo(nodeCount);

            HashSet<int>[] neighborsMap = new HashSet<int>[meshVertexCount];
            for (int i = 0; i < meshVertexCount; i++) neighborsMap[i] = new HashSet<int>();

            for (int i = 0; i < triangles.Count; i += 3)
            {
                int a = triangles[i];
                int b = triangles[i + 1];
                int c = triangles[i + 2];

                neighborsMap[a].Add(b); neighborsMap[a].Add(c);
                neighborsMap[b].Add(a); neighborsMap[b].Add(c);
                neighborsMap[c].Add(a); neighborsMap[c].Add(b);
            }

            // --- STEP 4: FLATTEN DATA FOR GPU PACKING ---
            // Size the buffer perfectly to the power-of-two nodeCount
            nodeBuffer = new ComputeBuffer(maxNodes, Marshal.SizeOf(typeof(Node3D)));
            nodeData = new Node3D[maxNodes];

            // 1. Fill the start of the array with your connected Icosahedron mesh
            for (int i = 0; i < meshVertexCount; i++)
            {
                int count = neighborsMap[i].Count;

                // 1. Create the node
                Node3D node = new Node3D
                {
                    position = vertices[i] * 5.0f,
                    curvature = 0.0f,
                    velocity = Vector3.zero,
                    mass = 1.0f,
                    neighborCount = count
                };

                // 2. Safely populate the fixed array up to the max limit of 8
                int nIndex = 0;
                foreach (int neighborIndex in neighborsMap[i])
                {
                    if (nIndex < 8)
                    {
                        node.neighbors[nIndex] = neighborIndex;
                        nIndex++;
                    }
                }

                nodeData[i] = node;
            }

            nodeBuffer.SetData(nodeData);

            counterArray[0] = nodeCount;
            counterBuffer.SetData(counterArray);
        }

        void SetKernelsAndBuffers()
        {
            // kernels
            evaluateSplitsKernel = computeShader.FindKernel("EvaluateSplits");
            applyNaturalForcesKernel = computeShader.FindKernel("ApplyNaturalForces");
            moveKernel = computeShader.FindKernel("MoveParticles");

            // buffers
            computeShader.SetBuffer(evaluateSplitsKernel, "Nodes", nodeBuffer);
            computeShader.SetBuffer(evaluateSplitsKernel, "NodeCounter", counterBuffer);

            computeShader.SetBuffer(applyNaturalForcesKernel, "Nodes", nodeBuffer);
            computeShader.SetBuffer(moveKernel, "Nodes", nodeBuffer);

            computeShader.SetBuffer(applyNaturalForcesKernel, "SpatialLookup", spatialLookupBuffer);
            computeShader.SetBuffer(applyNaturalForcesKernel, "StartIndices", startIndicesBuffer);
        }

        void SetShaderParams()
        {
            computeShader.SetInt("maxNodes", maxNodes);
            computeShader.SetInt("paddedNodeCount", paddedNodeCount);
        }

        void SetShaderParamsRealtime()
        {
            computeShader.SetInt("nodeCount", nodeCount);
            computeShader.SetFloat("deltaTime", Time.deltaTime);

            computeShader.SetFloat("splitDistanceThreshold", splitDistanceThreshold);

            computeShader.SetFloat("separationForce", separationForce);
            computeShader.SetFloat("separationDistance", separationDistance);
            computeShader.SetFloat("attractionForce", attractionForce);
            computeShader.SetFloat("laplacianSmoothing", laplacianSmoothing);

            computeShader.SetFloat("nodeDrag", nodeDrag);

            computeShader.SetVector("boundsCenter", spawnCollider.bounds.center);
            computeShader.SetVector("boundsExtents", spawnCollider.bounds.extents);

            spatialHashRunner.SetValues(separationDistance);
        }

        void RunComputeShader()
        {
            SetShaderParamsRealtime();
            
            // calculate how many thread groups we need
            int threadGroupsX = Mathf.CeilToInt(nodeCount / 8.0f);    

            // DISPATCH
            computeShader.Dispatch(evaluateSplitsKernel, threadGroupsX, 1, 1);

            spatialHashRunner.UpdateSpatialLookup(ref nodeBuffer, ref spatialLookupBuffer, ref startIndicesBuffer, nodeCount); // dispatch spatial hash first to update the lookup tables

            computeShader.Dispatch(applyNaturalForcesKernel, threadGroupsX, 1, 1);
            computeShader.Dispatch(moveKernel, threadGroupsX, 1, 1);
        }


        // this function is run when the object ComputeRunner is on is destroyed ex: when game closes
        void OnDestroy()
        {
            ComputeHelper.Release(nodeBuffer, /*neighborBuffer,*/ spatialLookupBuffer, startIndicesBuffer, counterBuffer);
        }
    }

}

// ALL STRUCTS
public unsafe struct Node3D
{
    public Vector3 position;
    public Vector3 velocity;

    public float curvature;
    public float mass;

    public int neighborCount;

    public fixed int neighbors[8];
}



