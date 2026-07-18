using Growth3DCompute;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Growth3D.Editor
{
    public class NodeTypeGraphWindow : EditorWindow
    {
        // Change this if your NodeType[] field on Grower3DGenotype has a different name.
        const string baseNodeTypesFieldName = "baseNodeTypes";

        [SerializeField] Grower3DGenotype genotypeAsset;

        NodeTypeGraphView graphView;
        NodeTypeGraphLayout layoutAsset;

        [MenuItem("Growth3D/Node Type Graph Editor")]
        public static void Open()
        {
            var window = GetWindow<NodeTypeGraphWindow>();
            window.titleContent = new GUIContent("Node Type Graph");
        }

        void OnEnable()
        {
            BuildToolbar();
        }

        void BuildToolbar()
        {
            rootVisualElement.Clear();

            var toolbar = new Toolbar();

            var genotypeField = new ObjectField("Genotype")
            {
                objectType = typeof(Grower3DGenotype),
                value = genotypeAsset
            };
            genotypeField.RegisterValueChangedCallback(evt =>
            {
                genotypeAsset = evt.newValue as Grower3DGenotype;
                RebuildGraph();
            });
            toolbar.Add(genotypeField);

            toolbar.Add(new ToolbarButton(() => graphView?.AddNewNodeType()) { text = "Add Node Type" });
            toolbar.Add(new ToolbarButton(SaveAll) { text = "Save" });

            rootVisualElement.Add(toolbar);

            if (genotypeAsset != null)
                RebuildGraph();
        }

        void RebuildGraph()
        {
            if (graphView != null)
            {
                rootVisualElement.Remove(graphView);
                graphView = null;
            }

            if (genotypeAsset == null)
                return;

            var so = new SerializedObject(genotypeAsset);
            if (so.FindProperty(baseNodeTypesFieldName) == null)
            {
                Debug.LogError($"'{genotypeAsset.name}' has no serialized field called " +
                                $"'{baseNodeTypesFieldName}'. Update baseNodeTypesFieldName " +
                                "in NodeTypeGraphWindow.cs to match your field name.");
                return;
            }

            layoutAsset = LoadOrCreateLayout(genotypeAsset);

            graphView = new NodeTypeGraphView(so, layoutAsset, baseNodeTypesFieldName)
            {
                style = { flexGrow = 1 }
            };
            rootVisualElement.Add(graphView);
        }

        NodeTypeGraphLayout LoadOrCreateLayout(Object genotype)
        {
            string path = AssetDatabase.GetAssetPath(genotype);

            foreach (var asset in AssetDatabase.LoadAllAssetsAtPath(path))
            {
                if (asset is NodeTypeGraphLayout existing)
                    return existing;
            }

            var layout = ScriptableObject.CreateInstance<NodeTypeGraphLayout>();
            layout.name = "NodeTypeGraphLayout";
            layout.hideFlags = HideFlags.HideInHierarchy; // keeps it tucked away in the Project window

            AssetDatabase.AddObjectToAsset(layout, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.ImportAsset(path);

            return layout;
        }

        void SaveAll()
        {
            if (genotypeAsset != null)
                EditorUtility.SetDirty(genotypeAsset);

            if (layoutAsset != null)
                EditorUtility.SetDirty(layoutAsset);

            AssetDatabase.SaveAssets();
        }
    }
}
