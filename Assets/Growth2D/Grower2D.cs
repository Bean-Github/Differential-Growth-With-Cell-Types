using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

// TODO: implement in a compute shader
public class Grower2D : MonoBehaviour
{
    public float startRadius = 1.0f;
    public int initialNodeCount = 10;

    public float growthRate = 0.1f; // time between adding new nodes

    public float baseProbability = 0.01f;

    public SpatialHash nodeHash;

    public float curvatureThreshold = 0.5f; // threshold for curvature-based growth

    [Tooltip("Strength of repulsion between nodes")]
    public float separationForce = 0.5f; 
    [Tooltip("Minimum distance before repulsion occurs")]
    public float separationDistance = 3.0f; 
    [Tooltip("Drag applied to node velocity")]
    public float nodeDrag = 0.1f; 
    [Tooltip("Distance threshold for adding new nodes")]
    public float nodeAddDistance = 1.3f;

    [Tooltip("Attraction force towards neighbors")]
    public float attractionForce = 0.2f;

    public int currNumNodes = 0;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        nodeHash = new SpatialHash(separationDistance);

        // start out with a circle of nodes
        for (int i = 0; i < initialNodeCount; i++)
        {
            float angle = (2 * Mathf.PI / initialNodeCount) * i;
            Vector2 pos = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * startRadius;
            Node2D newNode = CreateNode(pos);
            // connect to previous node
            if (i > 0)
            {
                ConnectNodes(newNode, nodeHash.GetNode(i-1));
            }
        }

        // connect last node to first to close the loop
        ConnectNodes(nodeHash.GetNode(0), nodeHash.GetNode(initialNodeCount - 1));

        StartCoroutine(Grow());
    }

    // Update is called once per frame
    void Update()
    {
        ApplyForces();

        UpdateAllPositions(Time.deltaTime);

        RenderNodes();
    }

    public void InsertNode(Node2D newNode)
    {
        nodeHash.AddNode(newNode);
    }

    void ApplyForces()
    {
        foreach (Node2D node in nodeHash.nodes)
        {
            // SEPARATION
            Node2D[] nearbyNodes = nodeHash.GetAllWithinDistance(node, separationDistance);

            foreach (Node2D other in nearbyNodes)
            {
                Vector2 repulsionDir = (node.position - other.position).normalized;

                float falloffFactor = GrowingHelpers2D.GetSmoothFalloff2D(node.position, other.position, separationDistance);

                node.ApplyForce(repulsionDir * separationForce * separationDistance * falloffFactor);

            }

            // ATTRACTION
            foreach (var neighbor in node.neighbors.Values)
            {
                Vector2 attractionDir = (neighbor.position - node.position).normalized;
                node.ApplyForce(attractionDir * attractionForce);
            }

            // DRAG
            node.ApplyForce(-node.currVelocity * nodeDrag);
        }
    }

    IEnumerator Grow()
    {
        while (true)
        {
            TryUpdateInsertions();

            yield return new WaitForSeconds(growthRate);
        }
    }

    // TODO: associate a probability for each edge and then go through ALL edges each growth step, and query that probability

    // picks a random edge and inserts a new node between the two nodes
    bool TryUpdateInsertions()
    {
        foreach (Node2D node in nodeHash.nodes)
        {
            foreach (var neighbor in node.neighbors.Values)
            {
                float edgeLength = Vector2.Distance(node.position, neighbor.position);
                if (edgeLength > nodeAddDistance)
                {
                    float curvature = GrowingHelpers2D.GetCurvature2D(node, neighbor);
                    float curvatureFactor = Mathf.Clamp01(curvature / curvatureThreshold);
                    float insertionProbability = baseProbability * (1.0f + curvatureFactor);
                    if (Random.value < insertionProbability)
                    {
                        InsertNode(node, neighbor);
                        return true; // only insert one node per growth step
                    }
                }
            }
        }

        return false;
    }

    void UpdateAllPositions(float deltaTime)
    {
        foreach (Node2D node in nodeHash.nodes)
        {
            node.UpdatePosition();
        }
    }

    void InsertNode(Node2D nodeA, Node2D nodeB)
    {
        if (nodeA.neighbors.ContainsKey(nodeB.id) == false || nodeB.neighbors.ContainsKey(nodeA.id) == false)
        {
            Debug.LogWarning("Attempted to insert node between non-neighboring nodes");
            return;
        }

        Vector2 midPoint = (nodeA.position + nodeB.position) / 2.0f;
        Node2D newNode = CreateNode(midPoint);

        newNode.currVelocity = (nodeA.currVelocity + nodeB.currVelocity) / 2.0f;

        // connect the new node to its neighbors
        ConnectNodes(newNode, nodeA);
        ConnectNodes(newNode, nodeB);

        nodeA.RemoveNeighbor(nodeB);
        nodeB.RemoveNeighbor(nodeA);

        Debug.DrawLine(nodeA.position, nodeB.position, Color.red, 1.0f);
    }

    Node2D CreateNode(Vector2 position)
    {
        Node2D newNode = new Node2D(position);
        nodeHash.AddNode(newNode);

        currNumNodes++;

        return newNode;
    }

    void ConnectNodes(Node2D nodeA, Node2D nodeB)
    {
        nodeA.AddNeighbor(nodeB);
        nodeB.AddNeighbor(nodeA);
    }

    void RenderNodes()
    {
        // debug draw lines between nodes and draw a cross at each node position
        foreach (Node2D node in nodeHash.nodes)
        {
            foreach (var neighbor in node.neighbors.Values)
            {
                float intensity = GrowingHelpers2D.GetCurvature2D(node, neighbor);
                Color col = new Color(0.0f, intensity, 0.0f);
                Debug.DrawLine(node.position, neighbor.position, col);

                Debug.DrawLine(node.position + Vector2.up * 0.1f, node.position + Vector2.down * 0.1f, Color.white);
                Debug.DrawLine(node.position + Vector2.left * 0.1f, node.position + Vector2.right * 0.1f, Color.white);
            }
        }
    }

}







