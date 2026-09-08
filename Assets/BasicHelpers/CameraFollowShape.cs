using UnityEngine;

[RequireComponent(typeof(Camera))]
public class CameraFollowShape : MonoBehaviour
{
    [Header("Target Tracking")]
    [Tooltip("The Renderer of the growing mesh.")]
    public Renderer targetRenderer;

    [Header("Zoom Settings")]
    [Tooltip("How much extra space to leave around the edges of the mesh.")]
    public float padding = 1.2f;
    [Tooltip("The closest the camera is allowed to zoom in.")]
    public float minDistance = 5f;

    private Camera cam;

    void Start()
    {
        cam = GetComponent<Camera>();
    }

    // We use LateUpdate for cameras to ensure the mesh has finished 
    // growing/moving in standard Update() before we calculate the camera position.
    void LateUpdate()
    {
        if (targetRenderer == null) return;

        // 1. Get the current bounds of the mesh
        Bounds bounds = targetRenderer.bounds;
        Vector3 boundsCenter = bounds.center;

        // 2. Get the radius of the bounds to ensure we encapsulate the whole shape
        float boundRadius = bounds.extents.magnitude;

        // 3. Calculate how far back we need to be
        if (cam.orthographic)
        {
            // For 2D / Orthographic cameras, we just change the orthographic size
            cam.orthographicSize = Mathf.Max(boundRadius * padding, minDistance);
            transform.position = boundsCenter - (transform.forward * 10f);
        }
        else
        {
            // For 3D / Perspective cameras, we calculate distance using the Field of View
            float fovInRadians = cam.fieldOfView * 0.5f * Mathf.Deg2Rad;
            float requiredDistance = (boundRadius * padding) / Mathf.Tan(fovInRadians);

            // Clamp the distance so it never gets closer than your zoomed-in starting point
            float finalDistance = Mathf.Max(requiredDistance, minDistance);

            // 4. Move the camera backward from the center of the mesh
            transform.position = boundsCenter - (transform.forward * finalDistance);
        }
    }
}