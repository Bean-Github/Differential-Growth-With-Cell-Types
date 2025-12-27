using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using UnityEditor.Experimental.GraphView;

// TODO: implement in a compute shader
public class Grower2D : MonoBehaviour
{
    public float growthRate = 0.1f; // Units per second
    public List<Node2D> nodes = new List<Node2D>();

    [Tooltip("Strength of repulsion between nodes")]
    public float separationForce = 0.5f; 
    [Tooltip("Minimum distance before repulsion occurs")]
    public float separationDistance = 3.0f; 
    [Tooltip("Drag applied to node velocity")]
    public float nodeDrag = 0.1f; 
    [Tooltip("Distance threshold for adding new nodes")]
    public float nodeAddDistance = 1.3f;

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
    }

    // Update is called once per frame
    void Update()
    {
        ApplySeparationForces();
        ApplyDrag();
        UpdateInsertions();

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

    void ApplyDrag()
    {
        foreach (Node2D node in nodes)
        {
            node.ApplyForce(-node.currVelocity * nodeDrag);
        }
    }

    void UpdateInsertions()
    {
        Node2D[] currNodes = nodes.ToArray();

        foreach (Node2D node in currNodes)
        {
            Node2D[] neighbors = node.neighbors.Values.ToArray();

            foreach (var neighbor in neighbors)
            {
                float distance = (node.position - neighbor.position).magnitude;
                if (distance > nodeAddDistance)
                {
                    InsertNode(node, neighbor);
                }
            }
        }
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
                Debug.DrawLine(node.position, neighbor.position, Color.green);

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







