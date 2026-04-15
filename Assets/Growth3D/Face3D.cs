using System.Collections.Generic;
using UnityEngine;

namespace Growth3D
{
    public class Face3D
    {
        // Only needs to point to ONE of the half-edges that make up its border
        public Edge3D halfEdge;
    }

}
