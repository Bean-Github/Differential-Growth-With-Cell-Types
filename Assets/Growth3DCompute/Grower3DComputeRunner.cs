using Growth3D;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace Growth3DCompute
{ 
    public class Grower3DComputeRunner : MonoBehaviour
    {
        [Header("Parameters")]
            public int maxNodes = 100000;
            private int hashTableSize;
            private int maxFaces;
            private int maxHalfEdges;

            public int nodeCount;
            public int faceCount;
            public int halfEdgeCount;

            [Range(0.0f, 20.0f)]
            public float simulationSpeed = 1.0f;

            public Grower3DGenotype genotype;

        [Header("Global Physics Settings")]
        [Tooltip("Strength of repulsion between nodes")]
            public float separationForce = 0.5f;
            [Tooltip("Maximum separation distance (also the spatial hash cell size)")]
            public float separationDistance = 3.0f;
            public float gravity = 9.8f;


        [Header("Edge Settings")]
        // edge stuff
            public float baseRestLength = 2.0f;
            public float springStiffness = 3.0f;
            public float springDamping = 0.5f; // 1.0f = critical damping, 0.0f = no damping

            public float lateralDamping = 1.0f;

            public float splitDistanceThreshold = 4.0f;
            
            [Tooltip("Drag applied to node velocity")]


        [Header("Shader Setup")]
            public ComputeShader computeShader;
            public ComputeShader splitterShader;
            public SpatialHashComputeRunner spatialHashRunner;
            public SpatialHashComputeRunner spatialHashEdgeRunner;
            public SpatialHashComputeRunner spatialHashFaceRunner;

            public bool enableSplitting = true;
            public bool enableFlipping = true;
            public bool selfCollisions = true;
            public bool enableDebugLogs = false;

            public int defaultShapeType = 0; // 0 = plane, 1 = sphere

            public float debug_totalAuxin = 0.0f; // for debugging, shows the total auxin level in the system

        // BUFFERS
        public ComputeBuffer nodeBuffer;
        public ComputeBuffer halfEdgeBuffer;
        public ComputeBuffer faceBuffer;
        public ComputeBuffer nodeTypesBuffer;

        ComputeBuffer edgeWeightBuffer;

        // particle hash data
        ComputeBuffer spatialLookupBuffer;
        ComputeBuffer startIndicesBuffer;

        // edge hash data
        ComputeBuffer spatialEdgeLookupBuffer;
        ComputeBuffer startEdgeIndicesBuffer;

        // face hash data
        ComputeBuffer spatialFaceLookupBuffer;
        ComputeBuffer startFaceIndicesBuffer;

        ComputeBuffer counterBuffer; // buffer to store counts of nodes, half-edges, faces, pending splits
        ComputeBuffer nodeLocksBuffer; // buffer to store locks for nodes during splitting and flipping

        ComputeBuffer totalAuxinBuffer; // buffer to store total auxin levels for normalization. note: this is an int buffer, but it is really a float scaled by like 10000x
        ComputeBuffer auxinAccumulatorBuffer; // buffer to indicate how to change the auxin levels

        // this is used to accumulate interlocked velocity data from the edge-edge collision phase
        ComputeBuffer velocityAccumulatorBuffer;

        int[] counterArray;

        // KERNELS ---
        protected int unlockEdgesKernel;
        protected int markEdgesKernel;
        protected int evaluateSplitsKernel;
        protected int unlockNodesKernel;
        protected int findIndependentSetKernel;

        protected int markFlippableEdgesKernel;
        protected int evaluateFlipsKernel;

        // movement kernels
        protected int updateEdgesKernel;
        protected int resolveFaceCollisionsKernel;
        protected int resolveEdgeCollisionsKernel;
        protected int applyAccumulatedVelocitiesKernel;
        protected int calculateCurvaturesKernel;

        protected int updateAuxinLevelsKernel;
        protected int applyAccumulatedAuxinKernel;

        protected int applyNaturalForcesKernel;
        protected int moveKernel;
        // ---

        void Start()
        {
            hashTableSize = Mathf.NextPowerOfTwo(maxNodes);

            maxFaces = maxNodes * 2;
            maxHalfEdges = maxNodes * 6;

            CreateBuffers();

            SetKernelsAndBuffers();

            SetShaderParams();
        }


        float physicsAccumulator = 0.0f;
        private void Update()
        {
            Time.timeScale = simulationSpeed;

            print("curr num nodes: " + nodeCount);
            print("curr num half-edges: " + halfEdgeCount);
            print("curr num faces: " + faceCount);

            physicsAccumulator += Time.unscaledDeltaTime * simulationSpeed;

            // If the game lags and deltaTime is 0.048, this will safely run the simulation 3 times 
            // with small, stable steps to catch up, completely preventing spring explosions.
            while (physicsAccumulator >= Time.fixedDeltaTime)
            {
                RunComputeShader();

                physicsAccumulator -= Time.fixedDeltaTime;
            }

            #region Debug
            int[] totalAuxinData = new int[1];
            totalAuxinBuffer.GetData(totalAuxinData);
            debug_totalAuxin = ((float) totalAuxinData[0]) / 10000.0f;

            //if (enableDebugLogs)
            //{
            //    //// extract the data back to CPU and log it for debugging
            //    //Node3D[] debugNodeData = new Node3D[nodeCount];
            //    //nodeBuffer.GetData(debugNodeData, 0, 0, nodeCount);
            //    //for (int i = 0; i < nodeCount; i++)
            //    //{
            //    //    Debug.Log($"Node {i}: " +
            //    //        $"Position={debugNodeData[i].position}, " +
            //    //        $"Velocity={debugNodeData[i].velocity}, " +
            //    //        $"Mass={debugNodeData[i].mass}" +
            //    //        $"DebugInt={debugNodeData[i].debug_int}");
            //    //}

            //    //// debug: print all the sorted cell keys and their corresponding particle indices\
            //    //Entry[] debugKeys = new Entry[hashTableSize];
            //    //spatialLookupBuffer.GetData(debugKeys);
            //    //for (int i = 0; i < hashTableSize; i++)
            //    //{
            //    //    Debug.Log($"Cell {i}: Key={debugKeys[i].cellKey}");
            //    //    Debug.Log($"    Particle Index={debugKeys[i].particleIndex}, Hash={debugKeys[i].hash}");
            //    //}

            //    HalfEdge3D[] debugHalfEdgeData = new HalfEdge3D[halfEdgeCount];
            //    halfEdgeBuffer.GetData(debugHalfEdgeData, 0, 0, halfEdgeCount);
            //    for (int i = 0; i < halfEdgeCount; i++)
            //    {
            //        Debug.Log($"HalfEdge {i}: " +
            //            $"Origin={debugHalfEdgeData[i].origin}, " +
            //            $"Target={debugHalfEdgeData[i].target}, " +
            //            $"Next={debugHalfEdgeData[i].next}, " +
            //            $"Prev={debugHalfEdgeData[i].prev}, " +
            //            $"Twin={debugHalfEdgeData[i].twin}, " +
            //            $"Face={debugHalfEdgeData[i].face}" +
            //            $"CanSplit={debugHalfEdgeData[i].canSplit}" +
            //            $"IsBoundary={debugHalfEdgeData[i].isBoundary}" +
            //            $"IsGhost={debugHalfEdgeData[i].isGhost}" +
            //            $"CurrRestLength={debugHalfEdgeData[i].currRestLength}" +
            //            $"BaseRestLength={debugHalfEdgeData[i].baseRestLength}" +
            //            $"SplitDistanceThreshold={debugHalfEdgeData[i].splitDistanceThreshold}"
            //        );
            //    }
            //}


            ////SetTopologyBuffers(nodeHoardCompute);
            //if (Input.GetKeyDown(KeyCode.Space))
            //{
            //    //// pick a random edge and split it
            //    // Node3D[] debugNodeData = new Node3D[nodeCount];
            //    //nodeBuffer.GetData(debugNodeData, 0, 0, nodeCount);
            //    //for (int i = 0; i < Mathf.Min(nodeCount, 10); i++)
            //    //{
            //    //    Debug.Log($"Node {i}: " +
            //    //        $"Position={debugNodeData[i].position}, " +
            //    //        $"Velocity={debugNodeData[i].velocity}, " +
            //    //        $"Mass={debugNodeData[i].mass}" +
            //    //        $"DebugInt={debugNodeData[i].debug_int}");
            //    //}

            //    //HalfEdge3D[] debugHalfEdgeData = new HalfEdge3D[halfEdgeCount];
            //    //halfEdgeBuffer.GetData(debugHalfEdgeData, 0, 0, halfEdgeCount);

            //    //Face3D[] debugFaceData = new Face3D[faceCount];
            //    //faceBuffer.GetData(debugFaceData, 0, 0, faceCount);

            //    //int randomEdgeIndex = Random.Range(0, halfEdgeCount);
            //    //NodeHoardCompute nodeHoardCompute = new NodeHoardCompute(debugNodeData, debugHalfEdgeData, debugFaceData);

            //    //nodeHoardCompute.SplitTriangle(ref debugHalfEdgeData[randomEdgeIndex]);

            //    //// send back to GPU
            //    //nodeBuffer.SetData(nodeHoardCompute.allNodes);
            //    //halfEdgeBuffer.SetData(nodeHoardCompute.halfEdges);
            //    //faceBuffer.SetData(nodeHoardCompute.faces);
            //}

            #endregion
        }

        #region Buffer Creation and Setup
        protected void CreateBuffers()
        {
            counterArray = new int[4];
            counterBuffer = new ComputeBuffer(4, sizeof(int), ComputeBufferType.Raw);
            
            nodeLocksBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));
            uint[] emptyLocks = new uint[hashTableSize];
            nodeLocksBuffer.SetData(emptyLocks);

            // init particles
            CreateTopologyBuffers();

            // init edge weights
            float[] edgeWeights = new float[maxHalfEdges];
            edgeWeightBuffer = new ComputeBuffer(maxHalfEdges, sizeof(float));

            // init spatial hash buffers
            spatialLookupBuffer = new ComputeBuffer(hashTableSize, sizeof(uint) * 3);
            startIndicesBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));

            int maxEdgeLookupEntries = Mathf.NextPowerOfTwo(maxHalfEdges * 4);
            spatialEdgeLookupBuffer = new ComputeBuffer(maxEdgeLookupEntries, sizeof(uint) * 3);
            startEdgeIndicesBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));

            int maxFaceLookupEntries = Mathf.NextPowerOfTwo(maxFaces * 9);
            spatialFaceLookupBuffer = new ComputeBuffer(maxFaceLookupEntries, sizeof(uint) * 3);
            startFaceIndicesBuffer = new ComputeBuffer(hashTableSize, sizeof(uint));

            // set velocity accumulator buffer
            velocityAccumulatorBuffer = new ComputeBuffer(maxNodes * 3, sizeof(int));
            int[] zeros = new int[maxNodes * 3];
            velocityAccumulatorBuffer.SetData(zeros);

            // auxin
            totalAuxinBuffer = new ComputeBuffer(1, sizeof(int));
            totalAuxinBuffer.SetData(new int[] { 0 });
            auxinAccumulatorBuffer = new ComputeBuffer(maxNodes, sizeof(int));
            zeros = new int[maxNodes];
            auxinAccumulatorBuffer.SetData(zeros);
        }

        // sets up the shape / seed of the grower
        protected void CreateTopologyBuffers()
        {
            NodeHoardGenerator generator = new NodeHoardGenerator();

            generator.nodeHoard.InitGlobalValues(baseRestLength, springStiffness, splitDistanceThreshold);

            switch (defaultShapeType)
            {
                case 0:
                    generator.LoadMesh(genotype.mesh);
                    break;
                case 1:
                    generator.CreateTestSphere(5.0f, 1);
                    break;
                case 2:
                    generator.CreateTestHexagon(5.0f);
                    break;
                case 3:
                    generator.CreateTestPlane(10.0f, 10.0f, 0, 0);
                    break;
                default:
                    generator.LoadMesh(genotype.mesh);
                    break;
            }

            nodeBuffer = new ComputeBuffer(hashTableSize, Marshal.SizeOf(typeof(Node3D)));
            halfEdgeBuffer = new ComputeBuffer(maxHalfEdges, Marshal.SizeOf(typeof(HalfEdge3D)));
            faceBuffer = new ComputeBuffer(maxFaces, Marshal.SizeOf(typeof(Face3D)));

            // SETUP GENOTYPE
            nodeTypesBuffer = new ComputeBuffer(genotype.baseNodeTypes.Length, Marshal.SizeOf(typeof(NodeType)));
            nodeTypesBuffer.SetData(genotype.baseNodeTypes);


            // assign the starting cell types to the compute runner
            void AssignStartCellTypes(NodeHoardCompute nodeHoard)
            {
                for (int i = 0; i < nodeHoard.allNodes.Count; i++)
                {
                    Node3D node = nodeHoard.allNodes[i];

                    // change type based on condition
                    //if (i == 0) node.baseType = 1;

                    // set the type of the node based on its baseType
                    node.type = genotype.baseNodeTypes[node.baseType];

                    nodeHoard.allNodes[i] = node;
                }
            }

            AssignStartCellTypes(generator.nodeHoard);

            SetTopologyBuffers(generator.nodeHoard);
        }

        // given a nodeHoard, this sets the data
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

            findIndependentSetKernel = splitterShader.FindKernel("FindIndependentSet");

            markFlippableEdgesKernel = splitterShader.FindKernel("MarkFlippableEdges");
            evaluateFlipsKernel = splitterShader.FindKernel("EvaluateFlips");

            updateEdgesKernel = computeShader.FindKernel("UpdateEdges");
            resolveFaceCollisionsKernel = computeShader.FindKernel("ResolveFaceCollisions");
            resolveEdgeCollisionsKernel = computeShader.FindKernel("ResolveEdgeCollisions");
            applyAccumulatedVelocitiesKernel = computeShader.FindKernel("ApplyAccumulatedVelocities");
            calculateCurvaturesKernel = computeShader.FindKernel("CalculateCurvatures");
            updateAuxinLevelsKernel = computeShader.FindKernel("UpdateAuxinLevels");
            applyAccumulatedAuxinKernel = computeShader.FindKernel("ApplyAccumulatedAuxin");
            applyNaturalForcesKernel = computeShader.FindKernel("ApplyNaturalForces");
            moveKernel = computeShader.FindKernel("MoveParticles");

            // Compute Shader
            ComputeHelper.SetBufferToKernels("GlobalCounters", counterBuffer, computeShader,
                updateEdgesKernel, resolveFaceCollisionsKernel, resolveEdgeCollisionsKernel, 
                applyAccumulatedVelocitiesKernel, calculateCurvaturesKernel, 
                updateAuxinLevelsKernel, applyAccumulatedAuxinKernel,
                applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("GlobalCounters", counterBuffer, splitterShader,
                unlockEdgesKernel, findIndependentSetKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("NodeLocks", nodeLocksBuffer, splitterShader,
                markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("Nodes", nodeBuffer, computeShader,
                updateEdgesKernel, resolveFaceCollisionsKernel, resolveEdgeCollisionsKernel, 
                applyAccumulatedVelocitiesKernel, calculateCurvaturesKernel, updateAuxinLevelsKernel, applyAccumulatedAuxinKernel,
                applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("Nodes", nodeBuffer, splitterShader, 
                unlockEdgesKernel, findIndependentSetKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("HalfEdges", halfEdgeBuffer, computeShader,
                updateEdgesKernel, resolveFaceCollisionsKernel, resolveEdgeCollisionsKernel, calculateCurvaturesKernel, updateAuxinLevelsKernel, applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("HalfEdges", halfEdgeBuffer, splitterShader,
                unlockEdgesKernel, findIndependentSetKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("EdgeWeights", edgeWeightBuffer, splitterShader,
                unlockEdgesKernel, findIndependentSetKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            ComputeHelper.SetBufferToKernels("Faces", faceBuffer, computeShader,
                updateEdgesKernel, resolveFaceCollisionsKernel, calculateCurvaturesKernel, applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("Faces", faceBuffer, splitterShader, 
                unlockEdgesKernel, resolveFaceCollisionsKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);

            // spatial hash buffers
            ComputeHelper.SetBufferToKernels("SpatialLookup", spatialLookupBuffer, computeShader, 
                applyNaturalForcesKernel);
            ComputeHelper.SetBufferToKernels("StartIndices", startIndicesBuffer, computeShader,
                applyNaturalForcesKernel);

            ComputeHelper.SetBufferToKernels("EdgeSpatialLookup", spatialEdgeLookupBuffer, computeShader,
                updateEdgesKernel, resolveEdgeCollisionsKernel);
            ComputeHelper.SetBufferToKernels("EdgeStartIndices", startEdgeIndicesBuffer, computeShader,
                updateEdgesKernel, resolveEdgeCollisionsKernel);

            ComputeHelper.SetBufferToKernels("FaceSpatialLookup", spatialFaceLookupBuffer, computeShader,
                applyNaturalForcesKernel, resolveFaceCollisionsKernel);
            ComputeHelper.SetBufferToKernels("FaceStartIndices", startFaceIndicesBuffer, computeShader,
                applyNaturalForcesKernel, resolveFaceCollisionsKernel);

            // auxin buffers
            ComputeHelper.SetBufferToKernels("TotalAuxin", totalAuxinBuffer, computeShader,
                updateAuxinLevelsKernel, applyAccumulatedAuxinKernel, applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("AccumulatedAuxinInt", auxinAccumulatorBuffer, computeShader,
                updateAuxinLevelsKernel, applyAccumulatedAuxinKernel);

            // other buffers
            ComputeHelper.SetBufferToKernels("NodeTypes", nodeTypesBuffer, computeShader,
                updateEdgesKernel, calculateCurvaturesKernel, updateAuxinLevelsKernel, applyNaturalForcesKernel, moveKernel);
            ComputeHelper.SetBufferToKernels("NodeTypes", nodeTypesBuffer, splitterShader,
                unlockEdgesKernel, findIndependentSetKernel, markEdgesKernel, evaluateSplitsKernel, unlockNodesKernel, markFlippableEdgesKernel, evaluateFlipsKernel);
        
            ComputeHelper.SetBufferToKernels("AccumulatedVelocityInt", velocityAccumulatorBuffer, computeShader,
                resolveEdgeCollisionsKernel, applyAccumulatedVelocitiesKernel);
        }
        #endregion

        void SetShaderParams()
        {
            computeShader.SetInt("maxNodes", maxNodes);
            splitterShader.SetInt("maxNodes", maxNodes);
            computeShader.SetInt("hashTableSize", hashTableSize);
        }

        void SetShaderParamsRealtime()
        {
            computeShader.SetFloat("deltaTime", Time.fixedDeltaTime);

            computeShader.SetFloat("splitDistanceThreshold", splitDistanceThreshold);
            splitterShader.SetFloat("splitDistanceThreshold", splitDistanceThreshold);

            computeShader.SetFloat("separationForce", separationForce);
            computeShader.SetFloat("separationDistance", separationDistance);
            computeShader.SetFloat("springStiffness", springStiffness);
            computeShader.SetFloat("springDamping", springDamping);
            computeShader.SetFloat("lateralDamping", lateralDamping);

            computeShader.SetFloat("baseRestLength", baseRestLength);
            splitterShader.SetFloat("baseRestLength", baseRestLength);

            computeShader.SetFloat("gravity", gravity);

            spatialHashRunner.SetValues(this);
            spatialHashEdgeRunner.SetValues(this);
            spatialHashFaceRunner.SetValues(this);

            computeShader.SetInt("paddedNodeCount", Mathf.NextPowerOfTwo(nodeCount));
            computeShader.SetInt("paddedFaceLookupCount", Mathf.NextPowerOfTwo(faceCount * 9));
            computeShader.SetInt("paddedEdgeLookupCount", Mathf.NextPowerOfTwo(halfEdgeCount * 4));

            nodeTypesBuffer.SetData(genotype.baseNodeTypes);
        }

        // updates the current counts of nodes, half-edges, and faces from the GPU
        void UpdateCounters()
        {
            counterBuffer.GetData(counterArray);

            nodeCount = counterArray[0];
            halfEdgeCount = counterArray[1];
            faceCount = counterArray[2];
        }

        /// <summary>
        /// What happens:
        /// first, split edges, then flip edges
        /// then unlock nodes, update spatial hash, update edges, calculate curvatures, apply forces, and move nodes
        /// </summary>

        void RunComputeShader()
        {
            SetShaderParamsRealtime();

            void ResetPendingCounterBuffer()
            {
                counterArray[3] = 0; // Reset the pending splits
                counterBuffer.SetData(counterArray);
            }

            int maxIterations = 20;

            void SplitEdges()
            {
                bool pending = false;

                int initialThreadGroupsNodes = Mathf.CeilToInt(nodeCount / 64.0f);
                int initialThreadGroupsEdges = Mathf.CeilToInt(halfEdgeCount / 64.0f);

                // Start with a clean slate
                splitterShader.Dispatch(unlockNodesKernel, initialThreadGroupsNodes, 1, 1);
                splitterShader.Dispatch(unlockEdgesKernel, initialThreadGroupsEdges, 1, 1); // Unlock all edges before starting the splitting iterations

                int pendingSplits = 1;
                int iterations = 0;

                while (pendingSplits > 0 && iterations < maxIterations)
                {
                    ResetPendingCounterBuffer();

                    splitterShader.Dispatch(markEdgesKernel, initialThreadGroupsEdges, 1, 1);
                    splitterShader.Dispatch(findIndependentSetKernel, initialThreadGroupsEdges, 1, 1);
                    splitterShader.Dispatch(evaluateSplitsKernel, initialThreadGroupsEdges, 1, 1);

                    UpdateCounters();
                    pendingSplits = counterArray[3];

                    iterations++;
                }

                print("Iterations used for SPLIT: " + iterations);

                if (pendingSplits > 0)
                {
                    pending = true;
                    Debug.LogWarning($"FLIPPING: Reached max iterations ({maxIterations}) with {pendingSplits} pending flips remaining. Consider increasing maxIterations or adjusting flip criteria.");
                }
            }

            void FlipEdges()
            {
                bool pending = false;

                UpdateCounters();

                int iterations = 0;
                int pendingFlips = 1;

                int initialThreadGroupsEdges = Mathf.CeilToInt(halfEdgeCount / 64.0f);
                int initialThreadGroupsNodes = Mathf.CeilToInt(nodeCount / 64.0f);

                // Start with a clean slate
                splitterShader.Dispatch(unlockNodesKernel, initialThreadGroupsNodes, 1, 1);
                splitterShader.Dispatch(unlockEdgesKernel, initialThreadGroupsEdges, 1, 1); // Unlock all edges before starting the splitting iterations

                while (pendingFlips > 0 && iterations < maxIterations)
                {
                    ResetPendingCounterBuffer();

                    splitterShader.Dispatch(markFlippableEdgesKernel, initialThreadGroupsEdges, 1, 1);
                    splitterShader.Dispatch(findIndependentSetKernel, initialThreadGroupsEdges, 1, 1);
                    splitterShader.Dispatch(evaluateFlipsKernel, initialThreadGroupsEdges, 1, 1);

                    // set pendingFlips, and unlock the nodes and repeat
                    UpdateCounters();
                    pendingFlips = counterArray[3]; 

                    splitterShader.Dispatch(unlockNodesKernel, initialThreadGroupsNodes, 1, 1);

                    iterations++;
                }

                print("Iterations used for FLIP: " + iterations);

                if (pendingFlips > 0)
                {
                    pending = true;
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
                int threadGroupsNodes = Mathf.CeilToInt(nodeCount / 64.0f);
                splitterShader.Dispatch(unlockNodesKernel, threadGroupsNodes, 1, 1);
            }

            if (enableFlipping)
            {
                FlipEdges();
            }

            // --- Physics Phase ---
            int finalThreadGroupsNodes = Mathf.CeilToInt(nodeCount / 64.0f);
            int finalThreadGroupsEdges = Mathf.CeilToInt(halfEdgeCount / 64.0f);

            splitterShader.Dispatch(unlockNodesKernel, finalThreadGroupsNodes, 1, 1);

            spatialHashRunner.UpdateSpatialLookup(
                ref nodeBuffer, 
                ref spatialLookupBuffer, 
                ref startIndicesBuffer, 
                nodeCount
            );

            spatialHashEdgeRunner.UpdateSpatialLookup(
                ref halfEdgeBuffer,
                ref spatialEdgeLookupBuffer,
                ref startEdgeIndicesBuffer,
                halfEdgeCount
            );

            spatialHashFaceRunner.UpdateSpatialLookup(
                ref faceBuffer, 
                ref spatialFaceLookupBuffer, 
                ref startFaceIndicesBuffer, 
                faceCount
            );

            computeShader.Dispatch(updateEdgesKernel, finalThreadGroupsEdges, 1, 1);
            if (selfCollisions)
            {
                computeShader.Dispatch(resolveFaceCollisionsKernel, finalThreadGroupsNodes, 1, 1);
                computeShader.Dispatch(resolveEdgeCollisionsKernel, finalThreadGroupsEdges, 1, 1);
                computeShader.Dispatch(applyAccumulatedVelocitiesKernel, finalThreadGroupsNodes, 1, 1);
            }
            computeShader.Dispatch(calculateCurvaturesKernel, finalThreadGroupsNodes, 1, 1);
            computeShader.Dispatch(updateAuxinLevelsKernel, finalThreadGroupsNodes, 1, 1);
            computeShader.Dispatch(applyAccumulatedAuxinKernel, finalThreadGroupsNodes, 1, 1);
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
                edgeWeightBuffer,
                faceBuffer,

                spatialLookupBuffer, 
                startIndicesBuffer, 
                spatialEdgeLookupBuffer,
                startEdgeIndicesBuffer,
                spatialFaceLookupBuffer,
                startFaceIndicesBuffer,
                velocityAccumulatorBuffer,

                counterBuffer, 
                nodeLocksBuffer,
                nodeTypesBuffer,

                totalAuxinBuffer,
                auxinAccumulatorBuffer
            );
        }
    }

}

