using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Growth3D.Editor
{
    public class NodeTypeGraphView : GraphView
    {
        const string ClipboardPrefix = "NODETYPE_CLIPBOARD:";

        readonly SerializedObject genotypeSerializedObject;
        readonly NodeTypeGraphLayout layoutAsset;
        readonly string baseNodeTypesFieldName;

        readonly List<NodeTypeNodeView> nodeViews = new List<NodeTypeNodeView>();

        public NodeTypeGraphView(SerializedObject genotypeSerializedObject, NodeTypeGraphLayout layoutAsset, string baseNodeTypesFieldName)
        {
            this.genotypeSerializedObject = genotypeSerializedObject;
            this.layoutAsset = layoutAsset;
            this.baseNodeTypesFieldName = baseNodeTypesFieldName;

            SetupZoom(ContentZoomer.DefaultMinScale, ContentZoomer.DefaultMaxScale);

            this.AddManipulator(new ContentDragger());
            this.AddManipulator(new SelectionDragger());
            this.AddManipulator(new RectangleSelector());
            this.AddManipulator(new FreehandSelector());

            var grid = new GridBackground();
            Insert(0, grid);
            grid.StretchToParentSize();

            var miniMap = new MiniMap { anchored = false };
            miniMap.SetPosition(new Rect(15, 15, 200, 120));
            Add(miniMap);

            // Binding the whole canvas means every field's BindProperty() call inside
            // NodeTypeNodeView resolves against this SerializedObject and auto-applies
            // edits (with full Undo support) as the user types.
            this.Bind(genotypeSerializedObject);

            graphViewChanged = OnGraphViewChanged;
            serializeGraphElements = OnSerializeGraphElements;
            canPasteSerializedData = OnCanPasteSerializedData;
            unserializeAndPaste = OnUnserializeAndPaste;

            // The layout asset's positions/names and the genotype's structural edits (add/delete)
            // are recorded through Undo.RecordObject rather than SerializedProperty, so nothing
            // rebuilds the visual graph automatically when the user hits Ctrl+Z / Ctrl+Y. Without
            // this, undo/redo would silently change the underlying data while the on-screen graph
            // kept showing the old (now wrong) nodes and edges.
            RegisterCallback<AttachToPanelEvent>(_ => Undo.undoRedoPerformed += OnUndoRedoPerformed);
            RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedoPerformed);

            PopulateView();
        }

        void OnUndoRedoPerformed()
        {
            PopulateView();
        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Add Node Type", _ => AddNewNodeType());
            base.BuildContextualMenu(evt);
        }

        // Output -> Input between two different nodes, EXCEPT the Child Type port, which is
        // also allowed to connect back to its own node's Input port: a self-loop is how you'd
        // explicitly show "children stay this type", though it's also just the default when
        // nothing is connected at all (see PopulateView).
        public override List<Port> GetCompatiblePorts(Port startPort, NodeAdapter nodeAdapter)
        {
            bool startsFromChildPort = startPort.node is NodeTypeNodeView startNode && startPort == startNode.ChildPort;

            return ports.ToList().Where(p =>
                p.direction != startPort.direction &&
                (p.node != startPort.node || startsFromChildPort)
            ).ToList();
        }

        public void AddNewNodeType()
        {
            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName("Add Node Type");

            var arrayProp = genotypeSerializedObject.FindProperty(baseNodeTypesFieldName);
            int newIndex = arrayProp.arraySize;
            arrayProp.InsertArrayElementAtIndex(newIndex);

            var elem = arrayProp.GetArrayElementAtIndex(newIndex);

            // Unity duplicates the last element's values on insert by default; give a brand-new
            // node type sane defaults instead.
            elem.FindPropertyRelative("mass").floatValue = 1f;
            elem.FindPropertyRelative("drag").floatValue = 10f;
            elem.FindPropertyRelative("growthRate").floatValue = 0f;
            elem.FindPropertyRelative("turgorPressure").floatValue = 0f;
            elem.FindPropertyRelative("laplacianSmoothing").floatValue = 1f;
            elem.FindPropertyRelative("switchTime").floatValue = -1f;
            elem.FindPropertyRelative("inheritanceWeight").floatValue = 0f;
            elem.FindPropertyRelative("auxinGenerationRate").floatValue = 0f;
            elem.FindPropertyRelative("auxinTransportRate").floatValue = 0f;
            elem.FindPropertyRelative("auxinDiffusionRate").floatValue = 0.1f;
            elem.FindPropertyRelative("auxinStealRate").floatValue = 0f;
            elem.FindPropertyRelative("auxinThreshold").floatValue = 0f;
            elem.FindPropertyRelative("auxinGrowthFactor").floatValue = 0f;
            elem.FindPropertyRelative("auxinFluxCanalization").floatValue = 0f;
            elem.FindPropertyRelative("growthTensor").vector3Value = new Vector3(1f, 1f, 1f);
            elem.FindPropertyRelative("useGravity").uintValue = 1; // true
            elem.FindPropertyRelative("resetAgeOnSwitch").uintValue = 0; // false
            elem.FindPropertyRelative("targetType").uintValue = (uint)newIndex; // no "switches to" yet
            elem.FindPropertyRelative("childType").uintValue = (uint)newIndex; // children default to itself
            elem.FindPropertyRelative("color").colorValue = Color.white;
            elem.FindPropertyRelative("flattenFactor").floatValue = 0f;
            elem.FindPropertyRelative("maxSpeed").floatValue = 20f;

            genotypeSerializedObject.ApplyModifiedProperties();

            Undo.RecordObject(layoutAsset, "Add Node Type");
            layoutAsset.EnsureSize(newIndex + 1);

            layoutAsset.entries[newIndex].displayName = "Type " + newIndex;
            EditorUtility.SetDirty(layoutAsset);

            Undo.CollapseUndoOperations(undoGroup);

            PopulateView();
        }

        // Node deletion needs special handling because targetType/childType are array-index
        // references: removing an entry has to shift every higher index down by one, and fix up
        // any reference that pointed at the deleted entry. This is safer to do as a full rebuild
        // than to try to patch the live GraphView in place.
        public override EventPropagation DeleteSelection()
        {
            var nodesToDelete = selection.OfType<NodeTypeNodeView>()
                                          .Select(n => n.Index)
                                          .OrderByDescending(i => i)
                                          .ToList();

            if (nodesToDelete.Count == 0)
            {
                return base.DeleteSelection();
            }

            bool confirmed = EditorUtility.DisplayDialog(
                "Delete Node Type(s)",
                $"Delete {nodesToDelete.Count} node type(s)? Any node whose 'Switches To' pointed " +
                "at a deleted type will be reset to 'none'. Any 'Child Type' that pointed at a " +
                "deleted type will default back to that node itself. Indices above the deleted " +
                "entries will shift down.",
                "Delete", "Cancel");

            if (!confirmed)
                return EventPropagation.Stop;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName($"Delete {nodesToDelete.Count} Node Type(s)");
            Undo.RecordObject(layoutAsset, "Delete Node Type(s)");

            var arrayProp = genotypeSerializedObject.FindProperty(baseNodeTypesFieldName);

            foreach (int index in nodesToDelete)
            {
                RemoveIndexAndReindex(arrayProp, index);
                layoutAsset.RemoveAt(index);
            }

            genotypeSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(layoutAsset);

            Undo.CollapseUndoOperations(undoGroup);

            PopulateView();

            return EventPropagation.Continue;
        }

        void RemoveIndexAndReindex(SerializedProperty arrayProp, int removedIndex)
        {
            int count = arrayProp.arraySize;
            for (int i = 0; i < count; i++)
            {
                if (i == removedIndex)
                    continue;

                // Where node i itself will end up after this entry is deleted and everything
                // above it shifts down - needed because a self-defaulting childType has to keep
                // pointing at "itself", not at whatever index it used to be.
                int selfFinalIndex = i < removedIndex ? i : i - 1;

                var elem = arrayProp.GetArrayElementAtIndex(i);
                FixTargetTypeReference(elem.FindPropertyRelative("targetType"), removedIndex, selfFinalIndex);
                FixChildTypeReference(elem.FindPropertyRelative("childType"), removedIndex, selfFinalIndex);
            }

            arrayProp.DeleteArrayElementAtIndex(removedIndex);
        }

        void FixTargetTypeReference(SerializedProperty refProp, int removedIndex, int selfFinalIndex)
        {
            uint value = refProp.uintValue;
            if (value == uint.MaxValue)
                return;

            if (value == removedIndex)
                refProp.uintValue = (uint)selfFinalIndex; // pointed at the deleted node -> now "none"
            else if (value > removedIndex)
                refProp.uintValue = value - 1; // shift down to fill the gap
        }

        void FixChildTypeReference(SerializedProperty refProp, int removedIndex, int selfFinalIndex)
        {
            uint value = refProp.uintValue;

            // uint.MaxValue is also treated as "pointed at the deleted node" here so any leftover
            // legacy sentinel gets opportunistically cleaned up to the new self-default convention.
            if (value == uint.MaxValue || value == removedIndex)
                refProp.uintValue = (uint)selfFinalIndex;
            else if (value > removedIndex)
                refProp.uintValue = value - 1; // shift down to fill the gap
        }

        GraphViewChange OnGraphViewChanged(GraphViewChange change)
        {
            if (change.elementsToRemove != null)
            {
                foreach (var element in change.elementsToRemove)
                {
                    if (element is Edge edge && edge.output != null && edge.output.node is NodeTypeNodeView sourceNode)
                    {
                        if (edge.output == sourceNode.OutputPort)
                            SetFieldValue(sourceNode.Index, "targetType", (uint)sourceNode.Index);
                        else if (edge.output == sourceNode.ChildPort)
                            SetFieldValue(sourceNode.Index, "childType", (uint)sourceNode.Index); // back to "defaults to itself"
                    }
                }
            }

            if (change.edgesToCreate != null)
            {
                foreach (var edge in change.edgesToCreate)
                {
                    if (edge.output.node is NodeTypeNodeView sourceNode && edge.input.node is NodeTypeNodeView targetNode)
                    {
                        if (edge.output == sourceNode.OutputPort)
                            SetFieldValue(sourceNode.Index, "targetType", (uint)targetNode.Index);
                        else if (edge.output == sourceNode.ChildPort)
                            SetFieldValue(sourceNode.Index, "childType", (uint)targetNode.Index);
                    }
                }
            }

            genotypeSerializedObject.ApplyModifiedProperties();
            return change;
        }

        void SetFieldValue(int nodeIndex, string fieldName, uint value)
        {
            var arrayProp = genotypeSerializedObject.FindProperty(baseNodeTypesFieldName);
            var elem = arrayProp.GetArrayElementAtIndex(nodeIndex);
            elem.FindPropertyRelative(fieldName).uintValue = value;
        }

        void PopulateView()
        {
            graphElements.ToList().ForEach(RemoveElement);
            nodeViews.Clear();

            genotypeSerializedObject.Update();
            var arrayProp = genotypeSerializedObject.FindProperty(baseNodeTypesFieldName);
            int count = arrayProp.arraySize;
            layoutAsset.EnsureSize(count);

            for (int i = 0; i < count; i++)
            {
                var elemProp = arrayProp.GetArrayElementAtIndex(i).Copy();
                var nodeView = new NodeTypeNodeView(i, elemProp, layoutAsset, layoutAsset.entries[i]);
                AddElement(nodeView);
                nodeViews.Add(nodeView);
            }

            bool normalizedAnyChildType = false;

            for (int i = 0; i < count; i++)
            {
                var elemProp = arrayProp.GetArrayElementAtIndex(i);

                uint target = elemProp.FindPropertyRelative("targetType").uintValue;
                if (target != uint.MaxValue && target < count)
                {
                    var edge = nodeViews[i].OutputPort.ConnectTo(nodeViews[(int)target].InputPort);
                    AddElement(edge);
                }

                var childProp = elemProp.FindPropertyRelative("childType");
                uint child = childProp.uintValue;
                if (child == uint.MaxValue || child >= count)
                {
                    // Legacy data (old MaxValue sentinel) or an out-of-range reference:
                    // normalize to "defaults to itself".
                    childProp.uintValue = (uint)i;
                    normalizedAnyChildType = true;
                    child = (uint)i;
                }

                if (child != i) // only an explicit override away from the (self) default gets a drawn edge
                {
                    var childEdge = nodeViews[i].ChildPort.ConnectTo(nodeViews[(int)child].InputPort);
                    AddElement(childEdge);
                }
            }

            if (normalizedAnyChildType)
                genotypeSerializedObject.ApplyModifiedProperties();
        }

        // --- Copy / paste (also powers Ctrl+D duplicate) ---------------------------------

        [Serializable]
        class CopiedNode
        {
            public string displayName;
            public Vector2 position;
            public float mass, drag, growthRate, turgorPressure, laplacianSmoothing, switchTime,
                         inheritanceWeight, auxinGenerationRate, auxinTransportRate, auxinDiffusionRate,
                         auxinStealRate, auxinThreshold, auxinGrowthFactor, auxinFluxCanalization, flattenFactor, maxSpeed;
            public Vector3 growthTensor;
            public bool useGravity;
            public bool resetAgeOnSwitch;
            public Color color;

            // Index into the copied set itself (not the original genotype), or -1 for "none" /
            // "not part of this copy". Remapped to real indices once pasted nodes exist.
            public int targetType = -1;
            public int childType = -1;
        }

        [Serializable]
        class ClipboardPayload
        {
            public List<CopiedNode> nodes = new List<CopiedNode>();
        }

        string OnSerializeGraphElements(IEnumerable<GraphElement> elements)
        {
            var selectedNodes = elements.OfType<NodeTypeNodeView>().OrderBy(n => n.Index).ToList();
            if (selectedNodes.Count == 0)
                return string.Empty;

            var copiedIndices = new HashSet<int>(selectedNodes.Select(n => n.Index));
            var localIndex = new Dictionary<int, int>();
            for (int i = 0; i < selectedNodes.Count; i++)
                localIndex[selectedNodes[i].Index] = i;

            var arrayProp = genotypeSerializedObject.FindProperty(baseNodeTypesFieldName);
            var payload = new ClipboardPayload();

            foreach (var nodeView in selectedNodes)
            {
                var elem = arrayProp.GetArrayElementAtIndex(nodeView.Index);
                var entry = layoutAsset.entries[nodeView.Index];

                uint target = elem.FindPropertyRelative("targetType").uintValue;
                uint child = elem.FindPropertyRelative("childType").uintValue;

                var copy = new CopiedNode
                {
                    displayName = entry.displayName,
                    position = entry.position,
                    mass = elem.FindPropertyRelative("mass").floatValue,
                    drag = elem.FindPropertyRelative("drag").floatValue,
                    growthRate = elem.FindPropertyRelative("growthRate").floatValue,
                    turgorPressure = elem.FindPropertyRelative("turgorPressure").floatValue,
                    laplacianSmoothing = elem.FindPropertyRelative("laplacianSmoothing").floatValue,
                    switchTime = elem.FindPropertyRelative("switchTime").floatValue,
                    inheritanceWeight = elem.FindPropertyRelative("inheritanceWeight").floatValue,
                    auxinGenerationRate = elem.FindPropertyRelative("auxinGenerationRate").floatValue,
                    auxinTransportRate = elem.FindPropertyRelative("auxinTransportRate").floatValue,
                    auxinDiffusionRate = elem.FindPropertyRelative("auxinDiffusionRate").floatValue,
                    auxinStealRate = elem.FindPropertyRelative("auxinStealRate").floatValue,
                    auxinThreshold = elem.FindPropertyRelative("auxinThreshold").floatValue,
                    auxinGrowthFactor = elem.FindPropertyRelative("auxinGrowthFactor").floatValue,
                    growthTensor = elem.FindPropertyRelative("growthTensor").vector3Value,
                    auxinFluxCanalization = elem.FindPropertyRelative("auxinFluxCanalization").floatValue,
                    useGravity = elem.FindPropertyRelative("useGravity").uintValue != 0,
                    resetAgeOnSwitch = elem.FindPropertyRelative("resetAgeOnSwitch").uintValue != 0,
                    flattenFactor = elem.FindPropertyRelative("flattenFactor").floatValue,
                    maxSpeed = elem.FindPropertyRelative("maxSpeed").floatValue,
                    color = elem.FindPropertyRelative("color").colorValue,
                    targetType = (target != (uint)nodeView.Index && copiedIndices.Contains((int)target)) ? localIndex[(int)target] : -1,
                    childType = (child != (uint)nodeView.Index && copiedIndices.Contains((int)child)) ? localIndex[(int)child] : -1,
                };

                payload.nodes.Add(copy);
            }

            return ClipboardPrefix + JsonUtility.ToJson(payload);
        }

        bool OnCanPasteSerializedData(string data)
        {
            return !string.IsNullOrEmpty(data) && data.StartsWith(ClipboardPrefix);
        }

        void OnUnserializeAndPaste(string operationName, string data)
        {
            if (!OnCanPasteSerializedData(data))
                return;

            ClipboardPayload payload;
            try
            {
                payload = JsonUtility.FromJson<ClipboardPayload>(data.Substring(ClipboardPrefix.Length));
            }
            catch
            {
                return;
            }

            if (payload?.nodes == null || payload.nodes.Count == 0)
                return;

            int undoGroup = Undo.GetCurrentGroup();
            Undo.SetCurrentGroupName(operationName);
            Undo.RecordObject(layoutAsset, operationName);

            var arrayProp = genotypeSerializedObject.FindProperty(baseNodeTypesFieldName);
            int baseIndex = arrayProp.arraySize;

            for (int i = 0; i < payload.nodes.Count; i++)
            {
                int newIndex = arrayProp.arraySize;
                arrayProp.InsertArrayElementAtIndex(newIndex);
                var elem = arrayProp.GetArrayElementAtIndex(newIndex);
                var copy = payload.nodes[i];

                elem.FindPropertyRelative("mass").floatValue = copy.mass;
                elem.FindPropertyRelative("drag").floatValue = copy.drag;
                elem.FindPropertyRelative("growthRate").floatValue = copy.growthRate;
                elem.FindPropertyRelative("turgorPressure").floatValue = copy.turgorPressure;
                elem.FindPropertyRelative("laplacianSmoothing").floatValue = copy.laplacianSmoothing;
                elem.FindPropertyRelative("switchTime").floatValue = copy.switchTime;
                elem.FindPropertyRelative("inheritanceWeight").floatValue = copy.inheritanceWeight;
                elem.FindPropertyRelative("auxinGenerationRate").floatValue = copy.auxinGenerationRate;
                elem.FindPropertyRelative("auxinTransportRate").floatValue = copy.auxinTransportRate;
                elem.FindPropertyRelative("auxinDiffusionRate").floatValue = copy.auxinDiffusionRate;
                elem.FindPropertyRelative("auxinStealRate").floatValue = copy.auxinStealRate;
                elem.FindPropertyRelative("auxinThreshold").floatValue = copy.auxinThreshold;
                elem.FindPropertyRelative("auxinGrowthFactor").floatValue = copy.auxinGrowthFactor;
                elem.FindPropertyRelative("growthTensor").vector3Value = copy.growthTensor;
                elem.FindPropertyRelative("auxinFluxCanalization").floatValue = copy.auxinFluxCanalization;
                elem.FindPropertyRelative("useGravity").uintValue = copy.useGravity ? 1u : 0u;
                elem.FindPropertyRelative("resetAgeOnSwitch").uintValue = copy.resetAgeOnSwitch ? 1u : 0u;
                elem.FindPropertyRelative("color").colorValue = copy.color;
                elem.FindPropertyRelative("targetType").uintValue = (uint)newIndex; // resolved below once all pasted nodes exist
                elem.FindPropertyRelative("childType").uintValue = (uint)newIndex;  // defaults to itself until resolved below
                elem.FindPropertyRelative("flattenFactor").floatValue = copy.flattenFactor;
                elem.FindPropertyRelative("maxSpeed").floatValue = 20f; // default to 0, user can edit later
            }

            genotypeSerializedObject.ApplyModifiedProperties();

            layoutAsset.EnsureSize(arrayProp.arraySize);

            for (int i = 0; i < payload.nodes.Count; i++)
            {
                var copy = payload.nodes[i];
                var entry = layoutAsset.entries[baseIndex + i];
                entry.displayName = string.IsNullOrEmpty(copy.displayName) ? "Type " + (baseIndex + i) : copy.displayName;
                entry.position = copy.position + new Vector2(40, 40); // offset so pastes don't sit exactly on top of the originals
            }

            // Now that every pasted node exists, remap any internal target/child references
            // (connections between two nodes that were copied together).
            for (int i = 0; i < payload.nodes.Count; i++)
            {
                var copy = payload.nodes[i];
                var elem = arrayProp.GetArrayElementAtIndex(baseIndex + i);

                if (copy.targetType >= 0)
                    elem.FindPropertyRelative("targetType").uintValue = (uint)(baseIndex + copy.targetType);

                if (copy.childType >= 0)
                    elem.FindPropertyRelative("childType").uintValue = (uint)(baseIndex + copy.childType);
            }

            genotypeSerializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(layoutAsset);

            Undo.CollapseUndoOperations(undoGroup);

            PopulateView();

            // Select the newly pasted nodes so the user can immediately drag them into place.
            ClearSelection();
            for (int i = 0; i < payload.nodes.Count; i++)
                AddToSelection(nodeViews[baseIndex + i]);
        }
    }
}
