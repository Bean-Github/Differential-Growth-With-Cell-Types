using UnityEngine;

namespace Growth3D
{ 
    public class Edge3D
    {
        // references
        public Node3D nodeA;
        public Node3D nodeB;
        public int id;

        public float Curvature
        {
            get => (nodeA.Curvature + nodeB.Curvature) / 2.0f;
            private set { }
        }
        public float Length
        {
            get => (nodeB.position - nodeA.position).magnitude;
            private set { }
        }

        public Edge3D(Node3D a, Node3D b, int id)
        {
            nodeA = a;
            nodeB = b;
            this.id = id;
        }

        public float GetLength()
        {
            return (nodeB.position - nodeA.position).magnitude;
        }

        public override bool Equals(object obj)
        {
            if (obj == null || GetType() != obj.GetType())
            {
                return false;
            }

            // TODO: write your implementation of Equals() here
            return this.id == ((Edge3D)obj).id;
        }
        public override int GetHashCode()
        {
            return id.GetHashCode();
        }
    }

}
