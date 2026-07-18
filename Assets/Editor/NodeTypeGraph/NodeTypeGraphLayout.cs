using System;
using System.Collections.Generic;
using UnityEngine;

namespace Growth3D.Editor
{
    // Purely cosmetic per-node data for the graph editor (position on canvas, display name).
    // Indexed 1:1 with the genotype's baseNodeTypes array. This is saved as a hidden
    // sub-asset of the genotype file itself, so it travels with it automatically and
    // never needs to be assigned manually.
    [Serializable]
    public class NodeTypeLayoutEntry
    {
        public string displayName = "New Type";
        public Vector2 position = Vector2.zero;
    }

    public class NodeTypeGraphLayout : ScriptableObject
    {
        public List<NodeTypeLayoutEntry> entries = new List<NodeTypeLayoutEntry>();

        // Keeps 'entries' the same length as baseNodeTypes, padding new entries
        // (laid out left-to-right so new nodes don't stack on top of each other)
        // or trimming from the end as needed.
        public void EnsureSize(int size)
        {
            while (entries.Count < size)
            {
                entries.Add(new NodeTypeLayoutEntry
                {
                    displayName = "Type " + entries.Count,
                    position = new Vector2(entries.Count * 280, 0)
                });
            }
            while (entries.Count > size)
            {
                entries.RemoveAt(entries.Count - 1);
            }
        }

        public void RemoveAt(int index)
        {
            if (index >= 0 && index < entries.Count)
                entries.RemoveAt(index);
        }
    }
}
