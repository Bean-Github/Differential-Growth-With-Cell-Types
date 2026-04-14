using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace Growth3D
{
    // TODO: implement in a compute shader
    public class Grower3D : MonoBehaviour
    {
        [Header("Initial Shape")]
        public float startRadius = 1.0f;
        public int initialNodeCount = 10;

        [Header("Repulsion")]
        [Tooltip("Strength of repulsion between nodes")]
        public float separationForce = 0.5f;
        [Tooltip("Maximum separation distance (also the spatial hash cell size)")]
        public float separationDistance = 3.0f;

        [Header("Cohesion")]
        [Tooltip("Attraction force towards neighbors")]
        public float attractionForce = 0.2f;

        [Header("Smoothing")]
        [Tooltip("Strength of Laplacian smoothing force")]
        public float laplacianSmoothing = 0.1f;

        [Header("Growth")]
        public float growthRate = 0.1f; // time between adding new nodes
        public float curvatureThreshold = 0.5f; // threshold for curvature-based growth
        public float edgeLengthThreshold = 1.5f; // threshold for edge length-based growth

        [Header("Other Forces")]
        [Tooltip("Drag applied to node velocity")]
        public float nodeDrag = 0.1f;

        [Header("Debug")]
        public bool debug_DrawGrid;
        public int debug_NumNodes;

        [Header("References")]
        public ShapeGenerator3D shapeGenerator;

        NodeHoard3D _nodeHoard;

        void Awake()
        {
            _nodeHoard = shapeGenerator.Initialize(separationDistance);
            shapeGenerator.CreateTestSphere(startRadius);
        }

        void Start()
        {
            StartCoroutine(Grow());
        }

        // Update is called once per frame
        void Update()
        {
            ApplyNaturalForces();
            ApplyPressureGradient();

            _nodeHoard.UpdateNodeData();

            RenderNodes();

            // debug
            debug_NumNodes = _nodeHoard.numNodes;
        }

        // updaters
        void ApplyNaturalForces()
        {
            foreach (Node3D node in _nodeHoard.allNodes)
            {
                // SEPARATION
                List<Node3D> nearbyNodes = _nodeHoard.GetNearbyNodesWithinDistance(node, separationDistance);
                foreach (Node3D other in nearbyNodes)
                {
                    Vector3 repulsionDir = (node.position - other.position).normalized;

                    float falloffFactor = GrowingHelpers3D.GetSpikyKernel3D(node.position, other.position, separationDistance);

                    node.ApplyForce(repulsionDir * separationForce * separationDistance * falloffFactor);
                }

                // ATTRACTION
                foreach ((int i, Node3D neighbor) in node.neighbors)
                {
                    Vector3 attractionDir = (neighbor.position - node.position).normalized;
                    node.ApplyForce(attractionDir * attractionForce);
                }

                // LAPLACIAN ATTRACTION (SMOOTHING)
                if (node.neighbors.Count > 0)
                {
                    Vector3 neighborCenter = Vector3.zero;

                    // calculate centroid of neighbors
                    foreach ((int i, Node3D neighbor) in node.neighbors)
                    {
                        neighborCenter += neighbor.position;
                    }
                    neighborCenter /= node.neighbors.Count;

                    // calculate the Laplacian vector (Vector from node to centroid)
                    Vector3 laplacianVector = neighborCenter - node.position;

                    node.ApplyForce(laplacianVector * laplacianSmoothing);
                }

                // DRAG
                node.ApplyForce(-node.currVelocity * nodeDrag);
            }
        }

        // use a simple radial pressure gradient
        void ApplyPressureGradient()
        {
            
        }

        IEnumerator Grow()
        {
            while (true)
            {
                yield return new WaitForSeconds(growthRate);

                TryUpdateInsertions();
            }
        }

        List<Edge3D> edgesToSplit = new List<Edge3D>();
        void TryUpdateInsertions()
        {
            edgesToSplit.Clear(); // clear previous frame's data

            foreach (Edge3D edge in _nodeHoard.allEdges)
            {
                if (edge.Curvature < curvatureThreshold)
                {
                    edgesToSplit.Add(edge);
                    continue;
                }

                if (edge.Length > edgeLengthThreshold)
                {
                    edgesToSplit.Add(edge);
                }
            }

            foreach (Edge3D edge in edgesToSplit)
            {
                _nodeHoard.InsertNode(edge);
            }
        }

        void RenderNodes()
        {
            // empty for now, just using debug OnGUI drawing. In the future, this is where you'd update your mesh or particle system with the new node positions.
        }


        private void OnDrawGizmos()
        {
            _nodeHoard?.DebugDrawNodes();
            if (debug_DrawGrid)
            {
                _nodeHoard?.DebugDrawGrid();
            }
        }
    }


}




