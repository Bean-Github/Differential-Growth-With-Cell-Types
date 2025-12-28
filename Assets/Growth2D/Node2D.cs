using UnityEngine;
using System.Collections.Generic;

[System.Serializable]
public class Node2D
{
    public Vector2 position;
    public Vector2 currVelocity;
    public float mass = 1.0f;

    public Dictionary<int, Node2D> neighbors;

    static int nextID = 0;

    public int id;

    public Node2D(Vector2 pos, float mass = 1.0f)
    {
        position = pos;
        neighbors = new Dictionary<int, Node2D>();

        // assign unique ID
        id = nextID;
        nextID++;
        this.mass = mass;
    }

    // physics update
    // moves the node based on its current velocity
    public void UpdatePosition()
    {
        position += currVelocity * Time.deltaTime;
    }

    public void ApplyForce(Vector2 force)
    {
        // F = m * a  =>  a = F / m
        Vector2 acceleration = force / mass;
        currVelocity += acceleration * Time.deltaTime;
    }


    // structure management
    public void AddNeighbor(Node2D neighbor)
    {
        if (!neighbors.ContainsKey(neighbor.id))
        {
            neighbors.Add(neighbor.id, neighbor);
        }
    }

    public void RemoveNeighbor(Node2D neighbor)
    {
        if (neighbors.ContainsKey(neighbor.id))
        {
            neighbors.Remove(neighbor.id);
        }
    }

}
