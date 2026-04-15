using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

namespace Growth2D
{
    // TODO: implement in a compute shader
    public class Grower2D : MonoBehaviour
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
        public bool debugDrawGrid;

        // storage
        NodeHoard2D nodeHoard;

        void Awake()
        {
            nodeHoard = new NodeHoard2D(separationDistance);
        }

        // Start is called once before the first execution of Update after the MonoBehaviour is created
        void Start()
        {
            void CreateInitialShape()
            {
                // start out with a circle of nodes
                for (int i = 0; i < initialNodeCount; i++)
                {
                    float angle = (2 * Mathf.PI / initialNodeCount) * i;
                    Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * startRadius;
                    Node2D newNode = nodeHoard.AddNode(pos);
                    // connect to previous node
                    if (i > 0)
                    {
                        nodeHoard.AddEdge(newNode, nodeHoard.GetNode(i - 1));
                    }
                }

                // connect last node to first to close the loop
                nodeHoard.AddEdge(nodeHoard.GetNode(0), nodeHoard.GetNode(initialNodeCount - 1));
            }

            CreateInitialShape();
            StartCoroutine(Grow());
        }

        public int debug_NumNodes;

        // Update is called once per frame
        void Update()
        {
            ApplyNaturalForces();
            ApplyPressureGradient();

            nodeHoard.UpdateNodeData();

            RenderNodes();

            // debug
            debug_NumNodes = nodeHoard.numNodes;
        }

        // updaters
        void ApplyNaturalForces()
        {
            foreach (Node2D node in nodeHoard.allNodes)
            {
                // SEPARATION
                List<Node2D> nearbyNodes = nodeHoard.GetNearbyNodesWithinDistance(node, separationDistance);
                foreach (Node2D other in nearbyNodes)
                {
                    Vector2 repulsionDir = (node.position - other.position).normalized;

                    float falloffFactor = GrowingHelpers2D.GetSpikyKernel2D(node.position, other.position, separationDistance);

                    node.ApplyForce(repulsionDir * separationForce * separationDistance * falloffFactor);
                }

                // ATTRACTION
                foreach ((int i, Node2D neighbor) in node.neighbors)
                {
                    Vector2 attractionDir = (neighbor.position - node.position).normalized;
                    node.ApplyForce(attractionDir * attractionForce);
                }

                // LAPLACIAN ATTRACTION (SMOOTHING)
                if (node.neighbors.Count > 0)
                {
                    Vector2 neighborCenter = Vector2.zero;

                    // calculate centroid of neighbors
                    foreach ((int i, Node2D neighbor) in node.neighbors)
                    {
                        neighborCenter += neighbor.position;
                    }
                    neighborCenter /= node.neighbors.Count;

                    // calculate the Laplacian vector (Vector from node to centroid)
                    Vector2 laplacianVector = neighborCenter - node.position;

                    node.ApplyForce(laplacianVector * laplacianSmoothing);
                }

                // DRAG
                node.ApplyForce(-node.currVelocity * nodeDrag);
            }
        }


        void ApplyPressureGradient()
        {
            //float scale = 5.0f;
            //float speed = 2.0f;

            //// Calculate a moving target point along a figure-8 path
            //Vector2 targetPos = new Vector2(
            //    Mathf.Cos(Time.time * speed) * scale,
            //    Mathf.Sin(Time.time * speed * 2.0f) * (scale / 2.0f)
            //);

            //foreach (Node2D node in nodeHoard.allNodes)
            //{
            //    // Pull nodes toward the moving target
            //    Vector2 direction = targetPos - node.position;
            //    node.ApplyForce(direction.normalized * 3.0f);
            //}
        }

        IEnumerator Grow()
        {
            while (true)
            {
                TryUpdateInsertions();

                yield return new WaitForSeconds(growthRate);
            }
        }

        List<Edge2D> edgesToSplit = new List<Edge2D>();
        void TryUpdateInsertions()
        {
            edgesToSplit.Clear(); // clear previous frame's data

            foreach (Edge2D edge in nodeHoard.allEdges)
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

            foreach (Edge2D edge in edgesToSplit)
            {
                nodeHoard.InsertNode(edge);
            }
        }

        void RenderNodes()
        {
            // empty for now, just using debug OnGUI drawing. In the future, this is where you'd update your mesh or particle system with the new node positions.
        }


        private void OnDrawGizmos()
        {
            nodeHoard?.DebugDrawNodes();
            if (debugDrawGrid)
            {
                nodeHoard?.DebugDrawGrid();
            }
        }
    }


}




