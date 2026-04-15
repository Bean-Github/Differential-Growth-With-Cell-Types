using UnityEngine;

public class Grower3DComputeRunner : MonoBehaviour
{
    [Header("Shader Setup")]
    public ComputeShader computeShader;
    public int textureResolution = 256;

    private RenderTexture renderTexture;

    void Start()
    {
        // set up the RenderTexture
        renderTexture = new RenderTexture(textureResolution, textureResolution, 24);
        renderTexture.enableRandomWrite = true;
        renderTexture.Create();

        // link the texture to this object material
        Renderer renderer = GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.mainTexture = renderTexture;
        }
        else
        {
            Debug.LogWarning(":( no renderer found, add this to a quad or something with a renderer");
        }
    }

    private void Update()
    {
        // execute the shader
        RunComputeShader();
    }

    void RunComputeShader()
    {
        // find the specific function we want to run inside the compute file
        int kernelIndex = computeShader.FindKernel("CSMain");

        // setup shader variables
        SetupShaderParams(kernelIndex);

        // calculate how many thread groups we need (see slides)
        int threadGroupsX = Mathf.CeilToInt(textureResolution / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(textureResolution / 8.0f);

        // DISPATCH
        computeShader.Dispatch(kernelIndex, threadGroupsX, threadGroupsY, 1);
    }

    void SetupShaderParams(int kernelIndex)
    {
        // link our RenderTexture to the "Result" variable inside the Compute Shader
        computeShader.SetTexture(kernelIndex, "Result", renderTexture);
    }


    // this function is run when the object ComputeRunner is on is destroyed ex: when game closes
    void OnDestroy()
    {
        // release RenderTextures when you are done to prevent memory leaks (see slides)
        if (renderTexture != null)
        {
            renderTexture.Release();
        }
    }
}


