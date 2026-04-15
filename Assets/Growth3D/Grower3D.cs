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
            public NodeHoard3DMeshRenderer nodeHoardMeshRenderer;

        NodeHoard3D _nodeHoard;

        void Awake()
        {
            _nodeHoard = shapeGenerator.Initialize(separationDistance);

            shapeGenerator.CreateTestSphere(startRadius);
            
            nodeHoardMeshRenderer.Initialize(_nodeHoard);
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

        #region Physics
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
                foreach (Node3D neighbor in node.neighbors)
                {
                    Vector3 attractionDir = (neighbor.position - node.position).normalized;
                    node.ApplyForce(attractionDir * attractionForce);
                }

                // LAPLACIAN ATTRACTION (SMOOTHING)
                if (node.neighbors.Count > 0)
                {
                    Vector3 neighborCenter = Vector3.zero;

                    // calculate centroid of neighbors
                    foreach (Node3D neighbor in node.neighbors)
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


        #endregion

        #region Growth
        IEnumerator Grow()
        {
            while (true)
            {
                yield return new WaitForSeconds(growthRate);

                TryUpdateInsertions();
            }
        }
        void TryUpdateInsertions()
        {
            // for now, just choose one edge
            int index = Random.Range(0, _nodeHoard.halfEdges.Count);
            Edge3D edgeToSplit = _nodeHoard.halfEdges[index];

            print("SPLIT EDGES!");

            _nodeHoard.SplitTriangle(edgeToSplit);
            nodeHoardMeshRenderer.GenerateNodeHoardMesh();
        }
        #endregion

        #region Rendering
        void RenderNodes()
        {
            nodeHoardMeshRenderer.UpdateMeshPositions();
        }

        private void OnDrawGizmos()
        {
            _nodeHoard?.DebugDrawNodes();
            if (debug_DrawGrid)
            {
                _nodeHoard?.DebugDrawGrid();
            }
        }
        #endregion
    }


}




