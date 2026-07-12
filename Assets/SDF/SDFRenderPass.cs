using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;

public class SDFRenderPassFeature : ScriptableRendererFeature
{
    class SDFPass : ScriptableRenderPass
    {
        const string m_PassName = "VolumetricFogPass";
        Material m_Material;
        SDFSettings m_Settings;

        public void Setup(Material mat, SDFSettings settings)
        {
            m_Material = mat;
            m_Settings = settings;
            requiresIntermediateTexture = true;
        }


        private void UpdateMaterialProperties(ContextContainer frameData)
        {
            if (m_Material == null || m_Settings == null) return;

            // 1. Fetch Camera Data
            var cameraData = frameData.Get<UniversalCameraData>();
            var camera = cameraData.camera;

            m_Material.SetFloat("NearPlane", camera.nearClipPlane);
            m_Material.SetFloat("FarPlane", camera.farClipPlane);

            Matrix4x4 proj = camera.projectionMatrix;
            Matrix4x4 view = camera.worldToCameraMatrix;
            Matrix4x4 viewProj = proj * view;

            m_Material.SetMatrix("_CameraInvProjection", proj.inverse);
            m_Material.SetMatrix("_CameraProjection", proj);

            m_Material.SetColor("_Color", m_Settings.color);
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            // Only render if material is valid
            if (m_Material == null) return;

            var resourceData = frameData.Get<UniversalResourceData>();

            // Ensure we aren't trying to write to the backbuffer directly if we need a texture read
            if (resourceData.isActiveTargetBackBuffer)
            {
                Debug.LogWarning($"Skipping {m_PassName}. Cannot run on BackBuffer; requires intermediate texture.");
                return;
            }

            // Update the material properties for this frame
            UpdateMaterialProperties(frameData);

            // 1. Get the source (Current Camera Color)
            var source = resourceData.activeColorTexture;

            // 2. Create the destination texture (New Texture)
            var destinationDesc = renderGraph.GetTextureDesc(source);
            destinationDesc.name = m_PassName;
            destinationDesc.clearBuffer = false;

            TextureHandle destination = renderGraph.CreateTexture(destinationDesc);

            // 3. Setup the Blit parameters
            // This helper struct handles creating the pass and setting up the source/dest
            RenderGraphUtils.BlitMaterialParameters para = new(source, destination, m_Material, 0);

            // 4. Add the pass to the graph
            renderGraph.AddBlitPass(para, passName: m_PassName);

            // 5. Swap the Camera Color
            // This tells URP: "The active camera color is now this new texture we just drew to."
            resourceData.cameraColor = destination;
        }
    }

    [System.Serializable]
    public class SDFSettings
    {
        public Color color;
    }


    public SDFSettings settings = new SDFSettings();
    public Shader shader;
    public RenderPassEvent injectionPoint = RenderPassEvent.BeforeRenderingTransparents;

    Material m_Material;
    SDFPass m_ScriptablePass;

    public override void Create()
    {
        // Create material if shader is present
        if (shader != null)
        {
            m_Material = CoreUtils.CreateEngineMaterial(shader);
        }

        m_ScriptablePass = new SDFPass();
        m_ScriptablePass.renderPassEvent = injectionPoint;
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // Don't run in scene view if you want to avoid visual clutter, or keep it if you want to see fog in editor
        if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
            return;

        if (m_Material == null && shader != null)
            m_Material = CoreUtils.CreateEngineMaterial(shader);

        if (m_Material == null)
        {
            Debug.LogWarning("SDF material is null.");
            return;
        }

        // Pass the settings to the Render Pass
        m_ScriptablePass.Setup(m_Material, settings);
        renderer.EnqueuePass(m_ScriptablePass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(m_Material);
    }
}
