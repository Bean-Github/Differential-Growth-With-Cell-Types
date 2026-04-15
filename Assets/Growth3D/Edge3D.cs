using UnityEngine;

namespace Growth3D
{
    public class Edge3D
    {
        // references
        public Node3D origin;    // The vertex this edge starts from
        public Node3D target => next.origin;

        public Edge3D next;      // The next half-edge in the face loop
        public Edge3D prev;      // (Optional but highly recommended) The previous half-edge
        public Face3D face;      // The face this half-edge belongs to
        public Edge3D twin;      // The half-edge going the opposite direction

        public int id;

        public float Curvature
        {
            get => (origin.Curvature + next.origin.Curvature) / 2.0f;
            private set { }
        }
        public float Length
        {
            get => (origin.position - next.origin.position).magnitude;
            private set { }
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
