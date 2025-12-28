using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using System.Collections;

// TODO: implement in a compute shader
public class Grower2D : MonoBehaviour
{
    public float growthRate = 0.1f; // time between adding new nodes
    public List<Node2D> nodes = new List<Node2D>();

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

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        // start out with a square of nodes
        nodes.Add(new Node2D(new Vector2(-1, -1)));
        nodes.Add(new Node2D(new Vector2(1, -1)));
        nodes.Add(new Node2D(new Vector2(1, 1)));
        nodes.Add(new Node2D(new Vector2(-1, 1)));

        // connect the nodes
        ConnectNodes(nodes[0], nodes[1]);
        ConnectNodes(nodes[1], nodes[2]);
        ConnectNodes(nodes[2], nodes[3]);
        ConnectNodes(nodes[3], nodes[0]);

        StartCoroutine(Grow());
    }

    // Update is called once per frame
    void Update()
    {
        ApplySeparationForces();
        ApplyAttractionForces();
        ApplyDrag();

        UpdateAllPositions(Time.deltaTime);

        RenderNodes();
    }

    public void InsertNode(Node2D newNode)
    {
        nodes.Add(newNode);
    }

    void ApplySeparationForces()
    {
        foreach (Node2D node in nodes)
        {
            Node2D[] nearbyNodes = GetAllWithinDistance(node, separationDistance);
            foreach (Node2D other in nearbyNodes)
            {
                Vector2 repulsionDir = (node.position - other.position).normalized;

                float falloffFactor = GrowingHelpers2D.GetSmoothFalloff2D(node.position, other.position, separationDistance);

                node.ApplyForce(repulsionDir * separationForce * separationDistance * falloffFactor);

            }
        }
    }

    void ApplyAttractionForces()
    {
        foreach (Node2D node in nodes)
        {
            foreach (var neighbor in node.neighbors.Values)
            {
                Vector2 attractionDir = (neighbor.position - node.position).normalized;
                node.ApplyForce(attractionDir * attractionForce);
            }
        }
    }

    void ApplyDrag()
    {
        foreach (Node2D node in nodes)
        {
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

    // picks a random edge and inserts a new node between the two nodes
    bool TryUpdateInsertions()
    {
        Node2D[] currNodes = nodes.ToArray();

        int randomIndex = Random.Range(0, currNodes.Length);

        // pick a random neighbor to insert between
        Node2D nodeA = currNodes[randomIndex];
        if (nodeA.neighbors.Count == 0)
        {
            return false;
        }
        Node2D nodeB = nodeA.neighbors.Values.ElementAt(Random.Range(0, nodeA.neighbors.Count));

        // calculate curvature at node A
        float curvature = GrowingHelpers2D.GetCurvature2D(nodeA, nodeB);
        Debug.DrawLine(nodeA.position, nodeB.position, Color.red, 1.0f);
        print(curvature);

        if (Mathf.Abs(curvature) < curvatureThreshold)
        {
            return false;
        }

        InsertNode(nodeA, nodeB);


        return true;
    }

    void UpdateAllPositions(float deltaTime)
    {
        foreach (Node2D node in nodes)
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

        // connect the new node to its neighbors
        ConnectNodes(newNode, nodeA);
        ConnectNodes(newNode, nodeB);

        nodeA.RemoveNeighbor(nodeB);
        nodeB.RemoveNeighbor(nodeA);
    }

    Node2D CreateNode(Vector2 position)
    {
        Node2D newNode = new Node2D(position);
        nodes.Add(newNode);

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
        foreach (Node2D node in nodes)
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

    // TODO: optimize this with spatial partitioning
    // find all nodes within a certain distance of a position
    Node2D[] GetAllWithinDistance(Node2D centerNode, float distance)
    {
        List<Node2D> results = new List<Node2D>();
        float sqrDistance = distance * distance;
        foreach (Node2D other in nodes)
        {
            if ((other.position - centerNode.position).sqrMagnitude <= sqrDistance)
            {
                results.Add(other);
            }
        }

        return results.ToArray();
    }
}







