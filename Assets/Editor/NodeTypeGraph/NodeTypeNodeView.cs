using UnityEditor;
using UnityEditor.Experimental.GraphView;
using UnityEngine;
using UnityEngine.UIElements;
using UnityEditor.UIElements;

namespace Growth3D.Editor
{
    // Visual node for one entry in baseNodeTypes[]. All fields are bound directly to the
    // underlying SerializedProperty, so edits here are ordinary serialized-field edits
    // (full Undo/Redo support, no extra save step needed for the field values themselves).
    public class NodeTypeNodeView : Node
    {
        public int Index { get; private set; }
        public Port InputPort { get; private set; }   // other types that switch INTO this type, or that name this type as their child
        public Port OutputPort { get; private set; }  // the one type this type switches TO
        public Port ChildPort { get; private set; }   // the type this node's children default to

        readonly NodeTypeGraphLayout layoutAsset;
        readonly NodeTypeLayoutEntry layoutEntry;

        // Tracks the Undo group open for an in-progress drag so every SetPosition call during
        // a single mouse gesture collapses into one Undo step instead of one per frame.
        int moveUndoGroup = -1;

        public NodeTypeNodeView(int index, SerializedProperty elementProp, NodeTypeGraphLayout layoutAsset, NodeTypeLayoutEntry layoutEntry)
        {
            Index = index;
            this.layoutAsset = layoutAsset;
            this.layoutEntry = layoutEntry;

            title = (string.IsNullOrEmpty(layoutEntry.displayName) ? "Type " + index : layoutEntry.displayName) + " (" + index + ")";
            viewDataKey = "nodeType_" + index;

            style.left = layoutEntry.position.x;
            style.top = layoutEntry.position.y;

            BuildPorts();
            BuildFields(elementProp);
            RegisterMoveUndoHandlers();

            RefreshExpandedState();
            RefreshPorts();
        }

        void BuildPorts()
        {
            InputPort = InstantiatePort(Orientation.Horizontal, Direction.Input, Port.Capacity.Multi, typeof(bool));
            InputPort.portName = "In";
            inputContainer.Add(InputPort);

            // Capacity.Single because a NodeType can only ever switch to ONE targetType.
            OutputPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            OutputPort.portName = "Switches To";
            outputContainer.Add(OutputPort);

            // Capacity.Single because a NodeType can only ever have ONE childType.
            // Tinted so "Child Type" edges are visually distinct from "Switches To" edges.
            // Leaving this unconnected means the child type simply defaults to this node itself
            // (see NodeTypeGraphView.PopulateView / OnGraphViewChanged).
            ChildPort = InstantiatePort(Orientation.Horizontal, Direction.Output, Port.Capacity.Single, typeof(bool));
            ChildPort.portName = "Child Type";
            ChildPort.portColor = new Color(0.95f, 0.62f, 0.15f);
            outputContainer.Add(ChildPort);
        }

        void BuildFields(SerializedProperty elementProp)
        {
            // Cosmetic-only name, stored in the layout sidecar rather than NodeType itself.
            var nameField = new TextField("Name") { value = layoutEntry.displayName };
            nameField.RegisterValueChangedCallback(evt =>
            {
                Undo.RecordObject(layoutAsset, "Rename Node Type");
                layoutEntry.displayName = evt.newValue;
                title = string.IsNullOrEmpty(evt.newValue) ? "Type " + Index : evt.newValue;
                EditorUtility.SetDirty(layoutAsset);
            });
            mainContainer.Add(nameField);

            AddFloatField(elementProp, "mass");
            AddFloatField(elementProp, "drag");
            AddFloatField(elementProp, "growthRate");
            AddFloatField(elementProp, "turgorPressure");
            AddFloatField(elementProp, "laplacianSmoothing");
            AddFloatField(elementProp, "switchTime");
            AddFloatField(elementProp, "inheritanceWeight");

            AddFloatField(elementProp, "auxinGenerationRate");
            AddFloatField(elementProp, "auxinTransportRate");
            AddFloatField(elementProp, "auxinDiffusionRate");
            AddFloatField(elementProp, "auxinStealRate");
            AddFloatField(elementProp, "auxinThreshold");
            AddFloatField(elementProp, "auxinGrowthFactor");
            AddFloatField(elementProp, "auxinFluxCanalization");

            AddFloatField(elementProp, "flattenFactor");
            AddVector3Field(elementProp, "growthTensor");
            AddBoolField(elementProp, "useGravity");
            AddBoolField(elementProp, "resetAgeOnSwitch");
            AddFloatField(elementProp, "maxSpeed");
            AddColorField(elementProp, "color");

            // targetType and childType are intentionally NOT exposed as manual fields here —
            // they are driven entirely by the "Switches To" / "Child Type" output edges (see
            // NodeTypeGraphView). targetType uses uint.MaxValue as its "none" sentinel, matching
            // the UINT_MAX convention already used throughout the compute shaders. childType has
            // no "none" state: an unconnected Child Type port means the node's children default
            // to this same type, which NodeTypeGraphView enforces whenever the edge is removed,
            // never created, or found holding a stale/legacy value.
        }

        void AddFloatField(SerializedProperty elementProp, string propName)
        {
            var prop = elementProp.FindPropertyRelative(propName);
            var field = new FloatField(ObjectNames.NicifyVariableName(propName));
            field.BindProperty(prop);
            mainContainer.Add(field);
        }

        void AddVector3Field(SerializedProperty elementProp, string propName)
        {
            var prop = elementProp.FindPropertyRelative(propName);
            var field = new Vector3Field(ObjectNames.NicifyVariableName(propName));
            field.BindProperty(prop);
            mainContainer.Add(field);
        }

        void AddBoolField(SerializedProperty elementProp, string propName)
        {
            var prop = elementProp.FindPropertyRelative(propName);
            var field = new Toggle(ObjectNames.NicifyVariableName(propName));

            field.value = prop.uintValue > 0;

            // manually map UI changes back to the uint property
            field.RegisterValueChangedCallback(evt =>
            {
                prop.uintValue = evt.newValue ? 1u : 0u;

                // Because we aren't using auto-binding, we must explicitly apply the change
                prop.serializedObject.ApplyModifiedProperties();
            });

            mainContainer.Add(field);
        }

        void AddColorField(SerializedProperty elementProp, string propName)
        {
            var prop = elementProp.FindPropertyRelative(propName);
            var field = new ColorField(ObjectNames.NicifyVariableName(propName));
            field.BindProperty(prop);
            mainContainer.Add(field);
        }

        void RegisterMoveUndoHandlers()
        {
            RegisterCallback<MouseDownEvent>(_ =>
            {
                moveUndoGroup = Undo.GetCurrentGroup();
                Undo.SetCurrentGroupName("Move Node Type");
            }, TrickleDown.TrickleDown);

            RegisterCallback<MouseUpEvent>(_ =>
            {
                if (moveUndoGroup >= 0)
                {
                    Undo.CollapseUndoOperations(moveUndoGroup);
                    moveUndoGroup = -1;
                }
            }, TrickleDown.TrickleDown);
        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);

            // Record on every call (not just once at drag start) so the Undo system actually
            // has a fresh "before" snapshot each time it flushes mid-drag; RegisterMoveUndoHandlers
            // then collapses all of those per-frame records into a single step on mouse-up.
            Undo.RecordObject(layoutAsset, "Move Node Type");
            layoutEntry.position = new Vector2(newPos.x, newPos.y);
            EditorUtility.SetDirty(layoutAsset);
        }
    }
}
