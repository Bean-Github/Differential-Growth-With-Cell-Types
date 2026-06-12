using Growth3D;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Growth3DCompute
{ 
    public class Grower3DComputeRunner : MonoBehaviour
    {
        public int subdivisions = 2;

        [Header("Parameters")]
            public int maxNodes = 100000;
            private int hashTableSize;
            private int maxFaces;
            private int maxHalfEdges;

            private int nodeCount;
            private int faceCount;
            private int halfEdgeCount;

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
            //public ComputeShader splitterShader;
            public SpatialHashComputeRunner spatialHashRunner;
            public Collider spawnCollider;

        // rendering
        [Header("Rendering")]
            public BasicParticleBufferRenderer particleRenderer;
            public BasicEdgeBufferRenderer edgeRenderer;

        // BUFFERS
        ComputeBuffer nodeBuffer;
        ComputeBuffer halfEdgeBuffer;
        ComputeBuffer faceBuffer;

        ComputeBuffer spatialLookupBuffer;
        ComputeBuffer startIndicesBuffer;

        ComputeBuffer counterBuffer;
        ComputeBuffer nodeLocksBuffer;

        int[] counterArray;

        //Node3D[] nodeData;
        //HalfEdge3D[] halfEdgeData;
        //Face3D[] faceData;

        // kernels
        protected int markEdgesKernel;
        protected int evaluateSplitsKernel;
        protected int unlockNodesKernel;
        protected int applyNaturalForcesKernel;
        protected int moveKernel;

        void Start()
        {
            hashTableSize = Mathf.NextPowerOfTwo(maxNodes);

            maxFaces = maxNodes * 2;
            maxHalfEdges = maxNodes * 6;

            CreateBuffers();

            SetKernelsAndBuffers();

            SetShaderParams();
        }

        private void Update()
        {
            // read back the atomic counter from the GPU
            counterBuffer.GetData(counterArray);

            // update our C# with the new total
            nodeCount = counterArray[0];
            halfEdgeCount = counterArray[1];
            faceCount = counterArray[2];

            print("curr num nodes: " + nodeCount);

            // execute the shader
            RunComputeShader();

            // get the information back from the shader to render
            particleRenderer.RenderParticles(nodeBuffer, nodeCount);
            edgeRenderer.RenderEdges(nodeBuffer, halfEdgeBuffer, nodeCount, halfEdgeCount);


            if (Input.GetKeyDown(KeyCode.Space))
            {
                // extract the data back to CPU and log it for debugging
                Node3D[] debugNodeData = new Node3D[nodeCount];
                nodeBuffer.GetData(debugNodeData, 0, 0, nodeCount);
                for (int i = 0; i < Mathf.Min(nodeCount, 10); i++)
                {
                    Debug.Log($"Node {i}: " +
                        $"Position={debugNodeData[i].position}, " +
                        $"Velocity={debugNodeData[i].velocity}, " +
                        $"Mass={debugNodeData[i].mass}");
                }

                HalfEdge3D[] debugHalfEdgeData = new HalfEdge3D[halfEdgeCount];
                halfEdgeBuffer.GetData(debugHalfEdgeData, 0, 0, halfEdgeCount);

                Face3D[] debugFaceData = new Face3D[faceCount];
                faceBuffer.GetData(debugFaceData, 0, 0, faceCount);

                //// pick a random edge and split it
                //int randomEdgeIndex = Random.Range(0, halfEdgeCount);
                //NodeHoardCompute nodeHoardCompute = new NodeHoardCompute(debugNodeData, debugHalfEdgeData, debugFaceData);

                //nodeHoardCompute.SplitTriangle(ref debugHalfEdgeData[randomEdgeIndex]);

                //// send back to GPU
                //nodeBuffer.SetData(nodeHoardCompute.allNodes);
                //halfEdgeBuffer.SetData(nodeHoardCompute.halfEdges);
                //faceBuffer.SetData(nodeHoardCompute.faces);

                //SetTopologyBuffers(nodeHoardCompute);
            }

        }

        protected void CreateBuffers()
        {
            counterArray = new int[3];
            counterBuffer = new ComputeBuffer(3, sizeof(int), ComputeBufferType.Raw);
            
            nodeLocksBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));
            uint[] emptyLocks = new uint[hashTableSize];
            nodeLocksBuffer.SetData(emptyLocks);

            // init particles
            CreateTopologyBuffers();

            spatialLookupBuffer = new ComputeBuffer(hashTableSize, sizeof(uint) * 4);
            startIndicesBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));
        }

        protected void CreateTopologyBuffers()
        {
            NodeHoardGenerator generator = new NodeHoardGenerator();

            generator.CreateTestSphere(3.0f);

            nodeBuffer = new ComputeBuffer(hashTableSize, Marshal.SizeOf(typeof(Node3D)));
            halfEdgeBuffer = new ComputeBuffer(maxHalfEdges, Marshal.SizeOf(typeof(HalfEdge3D)));
            faceBuffer = new ComputeBuffer(maxFaces, Marshal.SizeOf(typeof(Face3D)));

            SetTopologyBuffers(generator.nodeHoard);
        }

        protected unsafe void SetTopologyBuffers(NodeHoardCompute nodeHoard)
        {
            nodeBuffer.SetData(nodeHoard.allNodes);
            halfEdgeBuffer.SetData(nodeHoard.halfEdges);
            faceBuffer.SetData(nodeHoard.faces);

            nodeCount = nodeHoard.allNodes.Count;
            halfEdgeCount = nodeHoard.halfEdges.Count;
            faceCount = nodeHoard.faces.Count;

            counterArray[0] = nodeCount;
            counterArray[1] = halfEdgeCount;
            counterArray[2] = faceCount;
            counterBuffer.SetData(counterArray);
        }

        void SetKernelsAndBuffers()
        {
            markEdgesKernel = computeShader.FindKernel("MarkEdges");
            evaluateSplitsKernel = computeShader.FindKernel("EvaluateSplits");
            unlockNodesKernel = computeShader.FindKernel("UnlockNodes");
            applyNaturalForcesKernel = computeShader.FindKernel("ApplyNaturalForces");
            moveKernel = computeShader.FindKernel("MoveParticles");

            // Compute Shader
            ComputeHelper.SetBufferToKernels("GlobalCounters", counterBuffer, computeShader, 
                markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, applyNaturalForcesKernel, moveKernel);

            ComputeHelper.SetBufferToKernels("NodeLocks", nodeLocksBuffer, computeShader,
                markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel);

            ComputeHelper.SetBufferToKernels("Nodes", nodeBuffer, computeShader, 
                markEdgesKernel, evaluateSplitsKernel, applyNaturalForcesKernel, moveKernel);

            ComputeHelper.SetBufferToKernels("HalfEdges", halfEdgeBuffer, computeShader, 
                markEdgesKernel, evaluateSplitsKernel, applyNaturalForcesKernel, moveKernel);

            ComputeHelper.SetBufferToKernels("Faces", faceBuffer, computeShader, 
                markEdgesKernel, evaluateSplitsKernel, applyNaturalForcesKernel, moveKernel);

            ComputeHelper.SetBufferToKernels("SpatialLookup", spatialLookupBuffer, computeShader, 
                 applyNaturalForcesKernel);

            ComputeHelper.SetBufferToKernels("StartIndices", startIndicesBuffer, computeShader,
                applyNaturalForcesKernel);

        }

        void SetShaderParams()
        {
            computeShader.SetInt("maxNodes", maxNodes);
            computeShader.SetInt("hashTableSize", hashTableSize);
        }

        void SetShaderParamsRealtime()
        {
            //computeShader.SetInt("nodeCount", nodeCount);
            computeShader.SetFloat("deltaTime", Time.deltaTime);

            computeShader.SetFloat("splitDistanceThreshold", splitDistanceThreshold);
            computeShader.SetFloat("splitDistanceThreshold", splitDistanceThreshold);

            computeShader.SetFloat("separationForce", separationForce);
            computeShader.SetFloat("separationDistance", separationDistance);
            computeShader.SetFloat("attractionForce", attractionForce);
            computeShader.SetFloat("laplacianSmoothing", laplacianSmoothing);

            computeShader.SetFloat("nodeDrag", nodeDrag);

            computeShader.SetVector("boundsCenter", spawnCollider.bounds.center);
            computeShader.SetVector("boundsExtents", spawnCollider.bounds.extents);

            computeShader.SetInt("frameCount", Time.frameCount);

            spatialHashRunner.SetValues(separationDistance);

            computeShader.SetInt("paddedNodeCount", Mathf.NextPowerOfTwo(nodeCount));
        }

        void RunComputeShader()
        {
            SetShaderParamsRealtime();
            
            // calculate how many thread groups we need
            int threadGroupsNodes = Mathf.CeilToInt(nodeCount / 8.0f);
            int threadGroupsEdges = Mathf.CeilToInt(halfEdgeCount / 8.0f);

            // DISPATCH
            //computeShader.Dispatch(markEdgesKernel, threadGroupsEdges, 1, 1);
            //computeShader.Dispatch(evaluateSplitsKernel, threadGroupsEdges, 1, 1);
            //computeShader.Dispatch(unlockNodesKernel, threadGroupsNodes, 1, 1);

            spatialHashRunner.UpdateSpatialLookup(ref nodeBuffer, ref spatialLookupBuffer, ref startIndicesBuffer, nodeCount); // dispatch spatial hash first to update the lookup tables

            computeShader.Dispatch(applyNaturalForcesKernel, threadGroupsNodes, 1, 1);
            computeShader.Dispatch(moveKernel, threadGroupsNodes, 1, 1);

        }


        // this function is run when the object ComputeRunner is on is destroyed ex: when game closes
        void OnDestroy()
        {
            // release all buffers to prevent memory leaks
            ComputeHelper.Release(
                nodeBuffer, 
                halfEdgeBuffer, 
                faceBuffer, 
                spatialLookupBuffer, 
                startIndicesBuffer, 
                counterBuffer, 
                nodeLocksBuffer
            );
        }
    }

    // ALL STRUCTS
    public unsafe struct Node3D
    {
        public Vector3 position;
        public Vector3 velocity;

        public float curvature;
        public float mass;

        public uint halfEdge; // ID of one of the half-edges originating from this vertex

        // this index
        public uint id;
    }

    public unsafe struct HalfEdge3D
    {
        // node refs
        public uint origin;
        public uint target;

        // edge refs
        public uint next;
        public uint prev;
        public uint twin;

        // the face this half-edge belongs to
        public uint face;

        // this index
        public uint id;

        public uint wantsToSplit; // flag set by the GPU to indicate that this edge should be split
    };  

    public unsafe struct Face3D
    {   
        public uint halfEdge; // ID of one of the half-edges bounding this face

        // this index
        public uint id;
    };

}

