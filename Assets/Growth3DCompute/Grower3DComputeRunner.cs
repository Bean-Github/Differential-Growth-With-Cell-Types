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

            public float springStiffness = 3.0f;
            public float restLength = 1.0f;
            public float laplacianSmoothing = 0.1f;
            public float gravity = 9.8f;

            [Tooltip("Drag applied to node velocity")]
            public float nodeDrag = 0.1f;

            public float splitDistanceThreshold = 5.0f;

        [Header("Shader Setup")]
            public ComputeShader computeShader;
            public ComputeShader splitterShader;
            public SpatialHashComputeRunner spatialHashRunner;
            public Collider spawnCollider;

            public bool enableSplitting = true;
            public bool enableDebugLogs = false;

            public int defaultShapeType = 0; // 0 = plane, 1 = sphere

        // rendering
        [Header("Rendering")]
            public BasicParticleBufferRenderer particleRenderer;
            public BasicEdgeBufferRenderer edgeRenderer;
            public MeshFilter nodeHoardMeshFilter;

        // BUFFERS
        ComputeBuffer nodeBuffer;
        ComputeBuffer halfEdgeBuffer;
        ComputeBuffer faceBuffer;

        ComputeBuffer spatialLookupBuffer;
        ComputeBuffer startIndicesBuffer;

        ComputeBuffer counterBuffer;
        ComputeBuffer nodeLocksBuffer;

        int[] counterArray;

        // kernels
        protected int unlockEdgesKernel;
        protected int markEdgesKernel;
        protected int evaluateSplitsKernel;
        protected int unlockNodesKernel;

        protected int markFlippableEdgesKernel;
        protected int evaluateFlipsKernel;

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
            print("curr num nodes: " + nodeCount);
            print("curr num half-edges: " + halfEdgeCount);
            print("curr num faces: " + faceCount);
            // execute the shader
            RunComputeShader();

            // get the information back from the shader to render
            particleRenderer.RenderParticles(nodeBuffer, nodeCount);
            edgeRenderer.RenderEdges(nodeBuffer, halfEdgeBuffer, nodeCount, halfEdgeCount);

            #region Debug

            if (enableDebugLogs)
            {
                //// extract the data back to CPU and log it for debugging
                //Node3D[] debugNodeData = new Node3D[nodeCount];
                //nodeBuffer.GetData(debugNodeData, 0, 0, nodeCount);
                //for (int i = 0; i < nodeCount; i++)
                //{
                //    Debug.Log($"Node {i}: " +
                //        $"Position={debugNodeData[i].position}, " +
                //        $"Velocity={debugNodeData[i].velocity}, " +
                //        $"Mass={debugNodeData[i].mass}" +
                //        $"DebugInt={debugNodeData[i].debug_int}");
                //}

                //// debug: print all the sorted cell keys and their corresponding particle indices\
                //Entry[] debugKeys = new Entry[hashTableSize];
                //spatialLookupBuffer.GetData(debugKeys);
                //for (int i = 0; i < hashTableSize; i++)
                //{
                //    Debug.Log($"Cell {i}: Key={debugKeys[i].cellKey}");
                //    Debug.Log($"    Particle Index={debugKeys[i].particleIndex}, Hash={debugKeys[i].hash}");
                //}

                HalfEdge3D[] debugHalfEdgeData = new HalfEdge3D[halfEdgeCount];
                halfEdgeBuffer.GetData(debugHalfEdgeData, 0, 0, halfEdgeCount);
                for (int i = 0; i < halfEdgeCount; i++)
                {
                    Debug.Log($"HalfEdge {i}: " +
                        $"Origin={debugHalfEdgeData[i].origin}, " +
                        $"Target={debugHalfEdgeData[i].target}, " +
                        $"Next={debugHalfEdgeData[i].next}, " +
                        $"Prev={debugHalfEdgeData[i].prev}, " +
                        $"Twin={debugHalfEdgeData[i].twin}, " +
                        $"Face={debugHalfEdgeData[i].face}" +
                        $"WantsToSplit={debugHalfEdgeData[i].wantsToSplit}" +
                        $"CanSplit={debugHalfEdgeData[i].canSplit}" +
                        $"IsBoundary={debugHalfEdgeData[i].isBoundary}" +
                        $"IsGhost={debugHalfEdgeData[i].isGhost}"
                    );
                }
            }


            //SetTopologyBuffers(nodeHoardCompute);
            if (Input.GetKeyDown(KeyCode.Space))
            {
                //// pick a random edge and split it
                // Node3D[] debugNodeData = new Node3D[nodeCount];
                //nodeBuffer.GetData(debugNodeData, 0, 0, nodeCount);
                //for (int i = 0; i < Mathf.Min(nodeCount, 10); i++)
                //{
                //    Debug.Log($"Node {i}: " +
                //        $"Position={debugNodeData[i].position}, " +
                //        $"Velocity={debugNodeData[i].velocity}, " +
                //        $"Mass={debugNodeData[i].mass}" +
                //        $"DebugInt={debugNodeData[i].debug_int}");
                //}

                //HalfEdge3D[] debugHalfEdgeData = new HalfEdge3D[halfEdgeCount];
                //halfEdgeBuffer.GetData(debugHalfEdgeData, 0, 0, halfEdgeCount);

                //Face3D[] debugFaceData = new Face3D[faceCount];
                //faceBuffer.GetData(debugFaceData, 0, 0, faceCount);

                //int randomEdgeIndex = Random.Range(0, halfEdgeCount);
                //NodeHoardCompute nodeHoardCompute = new NodeHoardCompute(debugNodeData, debugHalfEdgeData, debugFaceData);

                //nodeHoardCompute.SplitTriangle(ref debugHalfEdgeData[randomEdgeIndex]);

                //// send back to GPU
                //nodeBuffer.SetData(nodeHoardCompute.allNodes);
                //halfEdgeBuffer.SetData(nodeHoardCompute.halfEdges);
                //faceBuffer.SetData(nodeHoardCompute.faces);
            }

            if (Input.GetKey(KeyCode.M))
            {
                // convert to mesh
                // extract the data back to CPU and log it for debugging
                Node3D[] nodeData = new Node3D[nodeCount];
                nodeBuffer.GetData(nodeData, 0, 0, nodeCount);

                HalfEdge3D[] halfEdgeData = new HalfEdge3D[halfEdgeCount];
                halfEdgeBuffer.GetData(halfEdgeData, 0, 0, halfEdgeCount);

                Face3D[] faceData = new Face3D[faceCount];
                faceBuffer.GetData(faceData, 0, 0, faceCount);

                NodeHoardCompute nodeHoardCompute = new NodeHoardCompute(nodeData, halfEdgeData, faceData);

                Mesh newMesh = NodeHoardMeshGenerator.GenerateMesh(nodeHoardCompute);

                nodeHoardMeshFilter.mesh = newMesh;
            }
            #endregion
        }

        protected void CreateBuffers()
        {
            counterArray = new int[4];
            counterBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.Raw);
            
            nodeLocksBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));
            uint[] emptyLocks = new uint[hashTableSize];
            nodeLocksBuffer.SetData(emptyLocks);

            // init particles
            CreateTopologyBuffers();

            spatialLookupBuffer = new ComputeBuffer(hashTableSize, sizeof(uint) * 3);
            startIndicesBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));
        }

        protected void CreateTopologyBuffers()
        {
            NodeHoardGenerator generator = new NodeHoardGenerator();

            switch (defaultShapeType)
            {
                case 0:
                    generator.CreateTestPlane(10.0f, 10.0f, subdivisions, subdivisions);
                    break;
                case 1:
                    generator.CreateTestSphere(3.0f, subdivisions);
                    break;
                case 2:
                    generator.CreateTestHexagon(5.0f);
                    break;
                default:
                    generator.CreateTestPlane(10.0f, 10.0f, subdivisions, subdivisions);
                    break;
            }
                    //generator.CreateTestSphere(3.0f);

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
            counterArray[3] = 0; // pending splits
            counterBuffer.SetData(counterArray);
        }

        void SetKernelsAndBuffers()
        {
            unlockEdgesKernel = splitterShader.FindKernel("UnlockEdges");
            markEdgesKernel = splitterShader.FindKernel("MarkEdges");
            evaluateSplitsKernel = splitterShader.FindKernel("EvaluateSplits");
            unlockNodesKernel = splitterShader.FindKernel("UnlockNodes");

            markFlippableEdgesKernel = splitterShader.FindKernel("MarkFlippableEdges");
            evaluateFlipsKernel = splitterShader.FindKernel("EvaluateFlips");

            applyNaturalForcesKernel = computeShader.FindKernel("ApplyNaturalForces");
            moveKernel = computeShader.FindKernel("MoveParticles");

            // Compute Shader
            ComputeHelper.SetBufferToKernels("GlobalCounters", counterBuffer, computeShader,
                applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("GlobalCounters", counterBuffer, splitterShader,
                unlockEdgesKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("NodeLocks", nodeLocksBuffer, splitterShader,
                markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("Nodes", nodeBuffer, computeShader, 
                applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("Nodes", nodeBuffer, splitterShader, 
                unlockEdgesKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("HalfEdges", halfEdgeBuffer, computeShader,
                applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("HalfEdges", halfEdgeBuffer, splitterShader,
                unlockEdgesKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("Faces", faceBuffer, computeShader, 
                applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("Faces", faceBuffer, splitterShader, 
                unlockEdgesKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("SpatialLookup", spatialLookupBuffer, computeShader, 
                 applyNaturalForcesKernel);

            ComputeHelper.SetBufferToKernels("StartIndices", startIndicesBuffer, computeShader,
                applyNaturalForcesKernel);
        }

        void SetShaderParams()
        {
            computeShader.SetInt("maxNodes", maxNodes);
            splitterShader.SetInt("maxNodes", maxNodes);
            computeShader.SetInt("hashTableSize", hashTableSize);
        }

        void SetShaderParamsRealtime()
        {
            //computeShader.SetInt("nodeCount", nodeCount);
            computeShader.SetFloat("deltaTime", Time.deltaTime);

            computeShader.SetFloat("splitDistanceThreshold", splitDistanceThreshold);
            splitterShader.SetFloat("splitDistanceThreshold", splitDistanceThreshold);

            computeShader.SetFloat("separationForce", separationForce);
            computeShader.SetFloat("separationDistance", separationDistance);
            computeShader.SetFloat("springStiffness", springStiffness);
            computeShader.SetFloat("restLength", restLength);
            computeShader.SetFloat("laplacianSmoothing", laplacianSmoothing);
            computeShader.SetFloat("gravity", gravity);

            computeShader.SetFloat("nodeDrag", nodeDrag);

            computeShader.SetVector("boundsCenter", spawnCollider.bounds.center);
            computeShader.SetVector("boundsExtents", spawnCollider.bounds.extents);

            spatialHashRunner.SetValues(separationDistance);

            computeShader.SetInt("paddedNodeCount", Mathf.NextPowerOfTwo(nodeCount));
        }

        // gets the current counts of nodes, half-edges, and faces from the GPU and updates the local variables accordingly
        void UpdateCounters()
        {
            counterBuffer.GetData(counterArray);

            nodeCount = counterArray[0];
            halfEdgeCount = counterArray[1];
            faceCount = counterArray[2];
        }


        void RunComputeShader()
        {
            SetShaderParamsRealtime();

            void ResetPendingCounterBuffer()
            {
                counterArray[3] = 0; // Reset the pending splits
                counterBuffer.SetData(counterArray);
            }

            void SplitEdges()
            {
                int maxIterations = 40;
                int iterations = 0;
                int pendingSplits = 1;

                int threadGroupsEdges = Mathf.CeilToInt(halfEdgeCount / 8.0f);
                int initialThreadGroupsNodes = Mathf.CeilToInt(nodeCount / 8.0f);

                // Start with a clean slate
                splitterShader.Dispatch(unlockNodesKernel, initialThreadGroupsNodes, 1, 1);
                splitterShader.Dispatch(unlockEdgesKernel, threadGroupsEdges, 1, 1); // Unlock all edges before starting the splitting iterations

                while (pendingSplits > 0 && iterations < maxIterations)
                {
                    ResetPendingCounterBuffer();

                    for (int i = 0; i < 4; i++)
                    {
                        splitterShader.SetInt("sliceIndex", i);

                        splitterShader.Dispatch(markEdgesKernel, threadGroupsEdges, 1, 1);
                        splitterShader.Dispatch(evaluateSplitsKernel, threadGroupsEdges, 1, 1);

                        UpdateCounters(); // Read counts
                        pendingSplits = counterArray[3];

                        // Unlock the nodes for the *next* iteration
                        int threadGroupsNodes = Mathf.CeilToInt(nodeCount / 8.0f);
                        splitterShader.Dispatch(unlockNodesKernel, threadGroupsNodes, 1, 1);
                    }

                    iterations++;
                }

                if (pendingSplits > 0)
                {
                    Debug.LogWarning($"SPLITTING: Reached max iterations ({maxIterations}) with {pendingSplits} pending splits remaining. Consider increasing maxIterations or adjusting split criteria.");
                }
            }
            void FlipEdges()
            {
                UpdateCounters();

                int maxIterations = 40;
                int iterations = 0;
                int pendingFlips = 1;

                int initialThreadGroupsEdges = Mathf.CeilToInt(halfEdgeCount / 8.0f);
                int initialThreadGroupsNodes = Mathf.CeilToInt(nodeCount / 8.0f);

                // Start with a clean slate
                splitterShader.Dispatch(unlockNodesKernel, initialThreadGroupsNodes, 1, 1);
                splitterShader.Dispatch(unlockEdgesKernel, initialThreadGroupsEdges, 1, 1); // Unlock all edges before starting the splitting iterations

                while (pendingFlips > 0 && iterations < maxIterations)
                {
                    ResetPendingCounterBuffer();

                    for (int i = 0; i < 4; i++)
                    {
                        splitterShader.SetInt("sliceIndex", i);

                        splitterShader.Dispatch(markFlippableEdgesKernel, initialThreadGroupsEdges, 1, 1);
                        splitterShader.Dispatch(evaluateFlipsKernel, initialThreadGroupsEdges, 1, 1);

                        // set pendingFlips, and unlock the nodes and repeat
                        UpdateCounters();
                        pendingFlips = counterArray[3]; 

                        splitterShader.Dispatch(unlockNodesKernel, initialThreadGroupsNodes, 1, 1);
                    }

                    iterations++;
                }

                if (pendingFlips > 0)
                {
                    Debug.LogWarning($"FLIPPING: Reached max iterations ({maxIterations}) with {pendingFlips} pending flips remaining. Consider increasing maxIterations or adjusting flip criteria.");
                }
            }

            UpdateCounters();

            if (enableSplitting && nodeCount < maxNodes)
            {
                SplitEdges();
            }
            else
            {
                UpdateCounters();
                int threadGroupsNodes = Mathf.CeilToInt(nodeCount / 8.0f);
                splitterShader.Dispatch(unlockNodesKernel, threadGroupsNodes, 1, 1);
            }

            FlipEdges();

            // --- Physics Phase ---
            int finalThreadGroupsNodes = Mathf.CeilToInt(nodeCount / 8.0f);

            splitterShader.Dispatch(unlockNodesKernel, finalThreadGroupsNodes, 1, 1);

            spatialHashRunner.UpdateSpatialLookup(ref nodeBuffer, ref spatialLookupBuffer, ref startIndicesBuffer, nodeCount);

            computeShader.Dispatch(applyNaturalForcesKernel, finalThreadGroupsNodes, 1, 1);
            computeShader.Dispatch(moveKernel, finalThreadGroupsNodes, 1, 1);
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

        public int debug_int; // for debugging purposes only
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

        public int wantsToSplit; // flag set by the GPU to indicate that this edge should be split
        public int canSplit;

        public int wantsToFlip; // flag set by the GPU to indicate that this edge should be flipped
        public int canFlip;

        public int isBoundary;
        public int isGhost;
    };  

    public unsafe struct Face3D
    {   
        public uint halfEdge; // ID of one of the half-edges bounding this face

        // this index
        public uint id;
    };

    public struct Entry
    {
        public uint particleIndex;
        public uint hash;
        public uint cellKey;
    }

}

