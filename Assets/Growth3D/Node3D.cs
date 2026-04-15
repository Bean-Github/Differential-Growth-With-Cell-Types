using System.Collections.Generic;
using UnityEngine;

namespace Growth3D
{
    [System.Serializable]
    public class Node3D
    {
        // ID
        public int id;
        static int nextID = 0;
        public int meshIndex;

        // PROPERTIES
        public Vector3 position;
        public Vector3 currVelocity;
        public float mass = 1.0f;
        public Vector3Int currentCell;
        public float Curvature { get; private set; }

        // REFERENCES
        public Edge3D halfEdge;
        public List<Node3D> neighbors => GetNeighbors();
        public List<Edge3D> edges => GetConnectedEdges();

        public Node3D(Vector3 pos, float mass = 1.0f)
        {
            position = pos;
            id = nextID++;
            this.mass = mass;
        }

        public void UpdatePosition()
        {
            position += currVelocity * Time.deltaTime;
        }

        public void ApplyForce(Vector3 force)
        {
            Vector3 acceleration = force / mass;
            currVelocity += acceleration * Time.deltaTime;
        }

        // Returns a clean list of all connected neighbors in perfect circular order
        public List<Node3D> GetNeighbors()
        {
            var neighbors = new List<Node3D>();

            if (halfEdge == null) return neighbors;

            Edge3D currentEdge = halfEdge;

            do
            {
                // currentEdge always starts at THIS node.
                // Therefore, currentEdge.twin always starts at the NEIGHBOR.
                neighbors.Add(currentEdge.twin.origin);

                // Rotate to the next outgoing edge
                currentEdge = currentEdge.twin.next;

            } while (currentEdge != halfEdge && currentEdge != null);

            return neighbors;
        }

        // Returns a list of all OUTGOING half-edges connected to this node
        public List<Edge3D> GetConnectedEdges()
        {
            var connectedEdges = new List<Edge3D>();

            if (halfEdge == null) return connectedEdges;

            Edge3D currentEdge = halfEdge;

            do
            {
                // Add the current outgoing edge to our list
                connectedEdges.Add(currentEdge);

                // Rotate to the next outgoing edge
                currentEdge = currentEdge.twin.next;

            } while (currentEdge != halfEdge && currentEdge != null);

            return connectedEdges;
        }

        public void CalculateCurvature()
        {
            var neighbors = GetNeighbors();

            // We need at least 3 neighbors to form a 3D surface
            if (neighbors.Count < 3)
            {
                Curvature = 0f;
                return;
            }

            float angleSum = 0f;

            for (int i = 0; i < neighbors.Count; i++)
            {
                // Get the current neighbor, and the NEXT neighbor in the circle
                // The modulo operator (%) ensures the last neighbor connects back to the first one (index 0)
                Vector3 posA = neighbors[i].position;
                Vector3 posB = neighbors[(i + 1) % neighbors.Count].position;

                angleSum += GetAngleBetweenNeighbors(posA, posB);
            }

            Curvature = (2f * Mathf.PI) - angleSum;
        }

        private float GetAngleBetweenNeighbors(Vector3 posA, Vector3 posB)
        {
            Vector3 v1 = (posA - this.position).normalized;
            Vector3 v2 = (posB - this.position).normalized;
            return Mathf.Acos(Mathf.Clamp(Vector3.Dot(v1, v2), -1f, 1f));
        }
    }
}


