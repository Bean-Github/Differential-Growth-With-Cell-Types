using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;
using static UnityEditor.Searcher.SearcherWindow.Alignment;

namespace Growth3DCompute
{ 
    public class Grower3DComputeRunner : MonoBehaviour
    {
        [Header("Parameters")]
            public int nodeCount = 1000;

        [Tooltip("Strength of repulsion between nodes")]
            public float separationForce = 0.5f;
            [Tooltip("Maximum separation distance (also the spatial hash cell size)")]
            public float separationDistance = 3.0f;

            public float attractionForce = 0.2f;
            public float laplacianSmoothing = 0.1f;

            [Tooltip("Drag applied to node velocity")]
            public float nodeDrag = 0.1f;


        [Header("Shader Setup")]
            public ComputeShader computeShader;
            public SpatialHashComputeRunner spatialHashRunner;
            public Collider spawnCollider;

        // BUFFERS
        ComputeBuffer nodeBuffer;
        ComputeBuffer neighborBuffer;

        ComputeBuffer spatialLookupBuffer;
        ComputeBuffer startIndicesBuffer;
        Node3D[] nodeData;

        // values
        protected int applyNaturalForcesKernel;
        protected int moveKernel;

        // rendering
        public BasicParticleBufferRenderer particleRenderer;


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

            // get the information back from the shader to render
            particleRenderer.RenderParticles(nodeBuffer, nodeCount);
        }

        protected void CreateBuffers()
        {
            // init particles
            CreateNodeBuffer();

            spatialLookupBuffer = new ComputeBuffer(nodeCount, sizeof(uint) * 4);
            startIndicesBuffer = new ComputeBuffer(nodeCount, sizeof(uint));
        }
        protected void CreateNodeBuffer()
        {
            // Helper method that safely splits an edge, ensures it snaps outward to a perfect sphere, 
            // and caches the index to avoid creating duplicate vertices on shared edges.
            int GetMidpointIndex(Dictionary<long, int> cache, List<Vector3> vertices, int p1, int p2)
            {
                long first = Mathf.Min(p1, p2);
                long second = Mathf.Max(p1, p2);
                long key = (first << 32) | second;

                if (cache.TryGetValue(key, out int existingIndex))
                {
                    return existingIndex;
                }

                Vector3 middle = (vertices[p1] + vertices[p2]) / 2.0f;
                vertices.Add(middle.normalized); // Snaps the point outward to the unit sphere radius

                int newIndex = vertices.Count - 1;
                cache.Add(key, newIndex);
                return newIndex;
            }

            int subdivisions = 2; // Adjust this for more/less detail (0 = icosahedron, 1 = 80 faces, 2 = 320 faces, etc.)
            float startRadius = 5.0f;

            // --- STEP 1: DEFINE BASE ICOSAHEDRON (12 Vertices, 20 Triangles) ---
            float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
            List<Vector3> vertices = new List<Vector3>()
                {
                    new Vector3(-1,  t,  0).normalized, new Vector3( 1,  t,  0).normalized,
                    new Vector3(-1, -t,  0).normalized, new Vector3( 1, -t,  0).normalized,
                    new Vector3( 0, -1,  t).normalized, new Vector3( 0,  1,  t).normalized,
                    new Vector3( 0, -1, -t).normalized, new Vector3( 0,  1, -t).normalized,
                    new Vector3( t,  0, -1).normalized, new Vector3( t,  0,  1).normalized,
                    new Vector3(-t,  0, -1).normalized, new Vector3(-t,  0,  1).normalized
                };

            List<int> triangles = new List<int>()
                {
                    0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
                    1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,    7, 1, 8,
                    3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
                    4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
                };

            // --- STEP 2: SUBDIVIDE TRIANGLES ---
            // Loops through your subdivision count to split each face into 4 smaller ones
            for (int i = 0; i < subdivisions; i++)
            {
                List<int> subdividedTriangles = new List<int>();
                System.Collections.Generic.Dictionary<long, int> midpointCache = new System.Collections.Generic.Dictionary<long, int>();

                for (int j = 0; j < triangles.Count; j += 3)
                {
                    int a = triangles[j];
                    int b = triangles[j + 1];
                    int c = triangles[j + 2];

                    // Get or create unique midpoints for each edge
                    int ab = GetMidpointIndex(midpointCache, vertices, a, b);
                    int bc = GetMidpointIndex(midpointCache, vertices, b, c);
                    int ca = GetMidpointIndex(midpointCache, vertices, c, a);

                    // Create 4 new triangles out of the 1 original triangle
                    subdividedTriangles.AddRange(new int[] { a, ab, ca });
                    subdividedTriangles.AddRange(new int[] { b, bc, ab });
                    subdividedTriangles.AddRange(new int[] { c, ca, bc });
                    subdividedTriangles.AddRange(new int[] { ab, bc, ca });
                }
                triangles = subdividedTriangles;
            }
            // --- STEP 3: EXTRACT STRUCTURAL NEIGHBORS FROM TRIANGLES ---
            int meshVertexCount = vertices.Count;

            // OVERRIDE: Force nodeCount to the nearest power of 2 (e.g., 642 becomes 1024)
            nodeCount = Mathf.NextPowerOfTwo(meshVertexCount);

            System.Collections.Generic.HashSet<int>[] neighborsMap = new System.Collections.Generic.HashSet<int>[meshVertexCount];
            for (int i = 0; i < meshVertexCount; i++) neighborsMap[i] = new System.Collections.Generic.HashSet<int>();

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
            nodeBuffer = new ComputeBuffer(nodeCount, Marshal.SizeOf(typeof(Node3D)));
            nodeData = new Node3D[nodeCount];
            List<int> flatNeighbors = new List<int>();

            // 1. Fill the start of the array with your connected Icosahedron mesh
            for (int i = 0; i < meshVertexCount; i++)
            {
                int startIndex = flatNeighbors.Count;
                int count = neighborsMap[i].Count;

                foreach (int neighborIndex in neighborsMap[i])
                {
                    flatNeighbors.Add(neighborIndex);
                }

                nodeData[i] = new Node3D
                {
                    position = vertices[i] * 5.0f, // startRadius
                    curvature = 0.0f,
                    velocity = Vector3.zero,
                    mass = 1.0f,
                    neighborStartIndex = startIndex,
                    neighborCount = count
                };
            }

            // 2. Fill the remainder of the array with free-floating particles to hit the Power of 2
            for (int i = meshVertexCount; i < nodeCount; i++)
            {
                nodeData[i] = new Node3D
                {
                    // Spawn them 100,000 units away. 
                    // We add a little random spread so they don't all stack in the exact same mathematical position.
                    position = new Vector3(100000f, 100000f, 100000f) + (Random.insideUnitSphere * 100f),
                    curvature = 0.0f,
                    velocity = Vector3.zero,
                    mass = 1.0f,
                    neighborStartIndex = 0,
                    neighborCount = 0
                };
            }

            nodeBuffer.SetData(nodeData);

            neighborBuffer = new ComputeBuffer(flatNeighbors.Count, sizeof(int));
            neighborBuffer.SetData(flatNeighbors.ToArray());
        }
        //protected void CreateNodeBuffer()
        //{
        //    // Helper method that safely splits an edge, ensures it snaps outward to a perfect sphere, 
        //    // and caches the index to avoid creating duplicate vertices on shared edges.
        //    int GetMidpointIndex(Dictionary<long, int> cache, List<Vector3> vertices, int p1, int p2)
        //    {
        //        long first = Mathf.Min(p1, p2);
        //        long second = Mathf.Max(p1, p2);
        //        long key = (first << 32) | second;

        //        if (cache.TryGetValue(key, out int existingIndex))
        //        {
        //            return existingIndex;
        //        }

        //        Vector3 middle = (vertices[p1] + vertices[p2]) / 2.0f;
        //        vertices.Add(middle.normalized); // Snaps the point outward to the unit sphere radius

        //        int newIndex = vertices.Count - 1;
        //        cache.Add(key, newIndex);
        //        return newIndex;
        //    }

        //    int subdivisions = 2; // Adjust this for more/less detail (0 = icosahedron, 1 = 80 faces, 2 = 320 faces, etc.)
        //    float startRadius = 5.0f;

        //    // --- STEP 1: DEFINE BASE ICOSAHEDRON (12 Vertices, 20 Triangles) ---
        //    float t = (1.0f + Mathf.Sqrt(5.0f)) / 2.0f;
        //    List<Vector3> vertices = new List<Vector3>()
        //    {
        //        new Vector3(-1,  t,  0).normalized, new Vector3( 1,  t,  0).normalized,
        //        new Vector3(-1, -t,  0).normalized, new Vector3( 1, -t,  0).normalized,
        //        new Vector3( 0, -1,  t).normalized, new Vector3( 0,  1,  t).normalized,
        //        new Vector3( 0, -1, -t).normalized, new Vector3( 0,  1, -t).normalized,
        //        new Vector3( t,  0, -1).normalized, new Vector3( t,  0,  1).normalized,
        //        new Vector3(-t,  0, -1).normalized, new Vector3(-t,  0,  1).normalized
        //    };

        //    List<int> triangles = new List<int>()
        //    {
        //        0, 11, 5,   0, 5, 1,    0, 1, 7,    0, 7, 10,   0, 10, 11,
        //        1, 5, 9,    5, 11, 4,   11, 10, 2,  10, 7, 6,    7, 1, 8,
        //        3, 9, 4,    3, 4, 2,    3, 2, 6,    3, 6, 8,    3, 8, 9,
        //        4, 9, 5,    2, 4, 11,   6, 2, 10,   8, 6, 7,    9, 8, 1
        //    };

        //    // --- STEP 2: SUBDIVIDE TRIANGLES ---
        //    // Loops through your subdivision count to split each face into 4 smaller ones
        //    for (int i = 0; i < subdivisions; i++)
        //    {
        //        List<int> subdividedTriangles = new List<int>();
        //        System.Collections.Generic.Dictionary<long, int> midpointCache = new System.Collections.Generic.Dictionary<long, int>();

        //        for (int j = 0; j < triangles.Count; j += 3)
        //        {
        //            int a = triangles[j];
        //            int b = triangles[j + 1];
        //            int c = triangles[j + 2];

        //            // Get or create unique midpoints for each edge
        //            int ab = GetMidpointIndex(midpointCache, vertices, a, b);
        //            int bc = GetMidpointIndex(midpointCache, vertices, b, c);
        //            int ca = GetMidpointIndex(midpointCache, vertices, c, a);

        //            // Create 4 new triangles out of the 1 original triangle
        //            subdividedTriangles.AddRange(new int[] { a, ab, ca });
        //            subdividedTriangles.AddRange(new int[] { b, bc, ab });
        //            subdividedTriangles.AddRange(new int[] { c, ca, bc });
        //            subdividedTriangles.AddRange(new int[] { ab, bc, ca });
        //        }
        //        triangles = subdividedTriangles;
        //    }

        //    // Automatically override your script's node count based on geometry generation
        //    nodeCount = vertices.Count;
        //    paddedNodeCount = Mathf.NextPowerOfTwo(nodeCount);

        //    // --- STEP 3: EXTRACT STRUCTURAL NEIGHBORS FROM TRIANGLES ---
        //    // Using HashSets ensures that sharing multiple faces won't create duplicate neighbor pointers
        //    System.Collections.Generic.HashSet<int>[] neighborsMap = new System.Collections.Generic.HashSet<int>[nodeCount];
        //    for (int i = 0; i < nodeCount; i++) neighborsMap[i] = new System.Collections.Generic.HashSet<int>();

        //    for (int i = 0; i < triangles.Count; i += 3)
        //    {
        //        int a = triangles[i];
        //        int b = triangles[i + 1];
        //        int c = triangles[i + 2];

        //        neighborsMap[a].Add(b); neighborsMap[a].Add(c);
        //        neighborsMap[b].Add(a); neighborsMap[b].Add(c);
        //        neighborsMap[c].Add(a); neighborsMap[c].Add(b);
        //    }

        //    // --- STEP 4: FLATTEN DATA FOR GPU PACKING ---
        //    nodeBuffer = new ComputeBuffer(paddedNodeCount, Marshal.SizeOf(typeof(Node3D)));
        //    nodeData = new Node3D[paddedNodeCount];
        //    List<int> flatNeighbors = new List<int>();

        //    for (int i = 0; i < nodeCount; i++)
        //    {
        //        int startIndex = flatNeighbors.Count;
        //        int count = neighborsMap[i].Count;

        //        foreach (int neighborIndex in neighborsMap[i])
        //        {
        //            flatNeighbors.Add(neighborIndex);
        //        }

        //        nodeData[i] = new Node3D
        //        {
        //            position = vertices[i] * startRadius, // Multiply by radius to size it up
        //            curvature = 0.0f,
        //            velocity = Vector3.zero,
        //            mass = 1.0f,

        //            neighborStartIndex = startIndex,
        //            neighborCount = count
        //        };
        //    }

        //    for (int i = nodeCount; i < paddedNodeCount; i++)
        //    {
        //        nodeData[i] = new Node3D
        //        {
        //            position = Vector3.zero,
        //            curvature = 0.0f,
        //            velocity = Vector3.zero,
        //            mass = 1.0f,
        //            neighborStartIndex = 0,
        //            neighborCount = 0
        //        };
        //    }

        //    nodeBuffer.SetData(nodeData);

        //    neighborBuffer = new ComputeBuffer(flatNeighbors.Count, sizeof(int));
        //    neighborBuffer.SetData(flatNeighbors.ToArray());
        //}

        void SetKernelsAndBuffers()
        {
            // kernels
            applyNaturalForcesKernel = computeShader.FindKernel("ApplyNaturalForces");
            moveKernel = computeShader.FindKernel("MoveParticles");

            // buffers
            computeShader.SetBuffer(applyNaturalForcesKernel, "Nodes", nodeBuffer);
            computeShader.SetBuffer(moveKernel, "Nodes", nodeBuffer);

            computeShader.SetBuffer(applyNaturalForcesKernel, "NeighborIndices", neighborBuffer);
            computeShader.SetBuffer(moveKernel, "NeighborIndices", neighborBuffer);

            computeShader.SetBuffer(applyNaturalForcesKernel, "SpatialLookup", spatialLookupBuffer);
            computeShader.SetBuffer(applyNaturalForcesKernel, "StartIndices", startIndicesBuffer);
        }

        void SetShaderParams()
        {
            computeShader.SetInt("nodeCount", nodeCount);

            int powerOfTwoNodeCount = Mathf.NextPowerOfTwo(nodeCount);
            computeShader.SetInt("paddedNodeCount", powerOfTwoNodeCount);
        }

        void SetShaderParamsRealtime()
        {
            computeShader.SetFloat("deltaTime", Time.deltaTime);

            computeShader.SetFloat("separationForce", separationForce);
            computeShader.SetFloat("separationDistance", separationDistance);
            computeShader.SetFloat("attractionForce", attractionForce);
            computeShader.SetFloat("laplacianSmoothing", laplacianSmoothing);

            computeShader.SetFloat("nodeDrag", nodeDrag);

            computeShader.SetVector("boundsCenter", spawnCollider.bounds.center);
            computeShader.SetVector("boundsExtents", spawnCollider.bounds.extents);

            spatialHashRunner.SetValues(separationDistance, spawnCollider.bounds);
        }

        void RunComputeShader()
        {
            SetShaderParamsRealtime();
            
            // calculate how many thread groups we need (see slides)
            int threadGroupsX = Mathf.CeilToInt(nodeCount / 8.0f);

            // DISPATCH
            spatialHashRunner.UpdateSpatialLookup(ref nodeBuffer, ref spatialLookupBuffer, ref startIndicesBuffer); // dispatch spatial hash first to update the lookup tables

            computeShader.Dispatch(applyNaturalForcesKernel, threadGroupsX, 1, 1);
            computeShader.Dispatch(moveKernel, threadGroupsX, 1, 1);
        }


        // this function is run when the object ComputeRunner is on is destroyed ex: when game closes
        void OnDestroy()
        {
            ComputeHelper.Release(nodeBuffer, neighborBuffer, spatialLookupBuffer, startIndicesBuffer);
        }
    }


    // ALL STRUCTS
    struct Node3D
    {
        public Vector3 position;
        public float curvature;

        public Vector3 velocity;
        public float mass;

        public int neighborStartIndex;
        public int neighborCount;
    }


}




