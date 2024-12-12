using UnityEngine;
using UnityEngine.LightTransport;
using System.IO;
using Unity.Mathematics;
using UnityEngine.Rendering;

public class RayMarcher : MonoBehaviour
{
    public ComputeShader raymarchShader;
    public Voxelizer voxelizer;
    public Color smokeColor = Color.gray;
    public float stepSize = 0.1f;
    public int maxSteps = 128;
    public float densityFalloff = 0.5f;

    private Camera cam;
    private RenderTexture raymarchOutput;


    //THESE ARE JUST FOR LOGGING
    private float logTimer = 0.0f;
    public float logInterval = 3.0f; // Log every second

    //THESE ARE FOR LOGGING RAY POSITIONS ALSO DEBUGGING
    int totalSteps;
    ComputeBuffer rayPositionBuffer;


    public Material compositeMaterial;
    private RenderTexture depthTexture;
    private Material depthMaterial;
    private CommandBuffer depthCommandBuffer;

    private void OnEnable()
    {
        cam = GetComponent<Camera>();
        cam.depthTextureMode = DepthTextureMode.Depth;
    }
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        //This script should get attached to the camera
        cam = GetComponent<Camera>();
        raymarchOutput = new RenderTexture(Screen.width, Screen.height, 0, RenderTextureFormat.ARGBFloat);
        raymarchOutput.enableRandomWrite = true;
        raymarchOutput.Create();

        
        depthTexture = new RenderTexture(Screen.width, Screen.height, 24, RenderTextureFormat.RFloat);
        depthTexture.enableRandomWrite = true;
        depthTexture.Create();

        depthMaterial = new Material(Shader.Find("Hidden/CopyDepth"));

        // Set up the command buffer
        depthCommandBuffer = new CommandBuffer();
        depthCommandBuffer.name = "Copy Depth Texture";
        depthCommandBuffer.Blit(null, depthTexture, depthMaterial);

        cam.AddCommandBuffer(CameraEvent.AfterDepthTexture, depthCommandBuffer);

        cam.depthTextureMode = DepthTextureMode.Depth;
    }

    void WriteVoxelsToFile(ComputeBuffer voxelBuffer)
    {
        //START COMMENT OUT
        //This needs to be removed, performance hit looking at the buffer on CPU Side but I need to see
        //whats in the buffer
        // Ensure you know the size of the buffer
        logTimer += Time.deltaTime;

        if (logTimer >= logInterval)
        {
            string filePath = Path.Combine(Application.persistentDataPath, "VoxelDataLog.txt");
            int totalVoxels = voxelBuffer.count;

            // Create a managed array to hold the data
            float[] voxelData = new float[voxelBuffer.count * 5];

            voxelBuffer.GetData(voxelData);

            // Iterate over the data and print it
            using (StreamWriter writer = new StreamWriter(filePath))
            {
                writer.WriteLine("Voxel Buffer Data:");
                for (int i = 0; i < totalVoxels; i++)
                {
                    // Calculate the index of the first float for the current voxel
                    int baseIndex = i * 5;

                    // Extract data for the current voxel
                    Vector3 position = new Vector3(voxelData[baseIndex], voxelData[baseIndex + 1], voxelData[baseIndex + 2]);
                    float density = voxelData[baseIndex + 3];
                    float sideLength = voxelData[baseIndex + 4];

                    // Print the voxel data
                    //Debug.Log($"Voxel {i}: Position={position}, Density={density}, SideLength={sideLength}");
                    writer.WriteLine($"Voxel {i}: Position={position}, Density={density}, SideLength={sideLength}");
                }
            }
            Debug.Log($"File written to: {filePath}");
            logTimer = 0.0f;
        }
        //END OF COMMENT OUT
    }

    void WriteRayPositionsToFile(ComputeBuffer rayPositionBuffer)
    {
        string filePath = Path.Combine(Application.persistentDataPath, "RayPosLog.txt");
        // Create a buffer for ray positions
        int totalSteps = maxSteps * Screen.width * Screen.height;

        // After Dispatch, read the buffer
        float3[] rayPositions = new float3[totalSteps];
        rayPositionBuffer.GetData(rayPositions);

        // Write to a file
        using (StreamWriter writer = new StreamWriter(filePath))
        {
            for (int i = 0; i < totalSteps; i++)
            {
                writer.WriteLine($"Ray Position {i}: {rayPositions[i]}");
            }
        }
    }
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (voxelizer == null || raymarchShader == null)
        {
            Graphics.Blit(source, destination); // Pass through if no data
            return;
        }

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;

        raymarchShader.SetMatrix("_CameraInvViewProjection", viewProjMatrix.inverse);
        raymarchShader.SetVector("_CameraWorldPos", cam.transform.position);
        raymarchShader.SetFloat("_StepSize", stepSize);
        
        raymarchShader.SetInt("_MaxSteps", maxSteps);

        // Pass voxel data and settings
        int kernelHandle = raymarchShader.FindKernel("CSMain");
        ComputeBuffer voxelBuffer = voxelizer.GetVoxelBuffer();

        /*int totalSteps = maxSteps * Screen.width * Screen.height;
        ComputeBuffer rayPositionBuffer = new ComputeBuffer(totalSteps, sizeof(float) * 3);
        raymarchShader.SetBuffer(kernelHandle, "_RayPositionBuffer", rayPositionBuffer);

        WriteRayPositionsToFile(rayPositionBuffer);*/

        //WriteVoxelsToFile(voxelBuffer);
        //So above logging proves voxelBuffer is up to date
        //So voxelBuffer IS getting the density information when I create smoke

        Vector3 gridSize = voxelizer.GetGridSize();
        raymarchShader.SetBuffer(kernelHandle, "_VoxelBuffer", voxelBuffer);
        raymarchShader.SetInt("_GridSizeX", (int)gridSize.x);
        raymarchShader.SetInt("_GridSizeY", (int)gridSize.y);
        raymarchShader.SetInt("_GridSizeZ", (int)gridSize.z);
        raymarchShader.SetFloat("_VoxelSize", voxelizer.voxelSize);

        // Smoke properties
        raymarchShader.SetVector("_SmokeColor", smokeColor);
        raymarchShader.SetFloat("_DensityFalloff", densityFalloff);

        raymarchShader.SetTexture(kernelHandle, "_RaymarchOutput", raymarchOutput);

        //Depth stuff
        raymarchShader.SetTexture(kernelHandle, "_SceneDepthTexture", depthTexture);
        raymarchShader.SetMatrix("_CameraViewMatrix", cam.worldToCameraMatrix);
        raymarchShader.SetMatrix("_CameraProjectionMatrix", cam.projectionMatrix);

        Matrix4x4 invProjMatrix = projMatrix.inverse;
        raymarchShader.SetMatrix("_CameraInvProjectionMatrix", invProjMatrix);
        Vector4 projectionParams = new Vector4(
            cam.nearClipPlane,
            cam.farClipPlane,
            cam.farClipPlane / (cam.farClipPlane - cam.nearClipPlane),
            (-cam.farClipPlane * cam.nearClipPlane) / (cam.farClipPlane - cam.nearClipPlane)
        );
        raymarchShader.SetVector("_ProjectionParams", projectionParams);




        raymarchShader.SetInt("_OutputWidth", raymarchOutput.width);
        raymarchShader.SetInt("_OutputHeight", raymarchOutput.height);

        int threadGroupsX = Mathf.CeilToInt(raymarchOutput.width / 8.0f);
        int threadGroupsY = Mathf.CeilToInt(raymarchOutput.height / 8.0f);
        raymarchShader.Dispatch(kernelHandle, threadGroupsX, threadGroupsY, 1);

        //Graphics.Blit(raymarchOutput, destination);
        //New code
        //This is currently not depth aware
        compositeMaterial.SetTexture("_MainTex", source);
        compositeMaterial.SetTexture("_SmokeTex", raymarchOutput);
        compositeMaterial.SetTexture("_SmokeDepthTex", depthTexture);
        

        // Blit using the composite material
        Graphics.Blit(source, destination, compositeMaterial);
    }


    /*

    //Drawing gizmos to simulate the rays that should be projected in the ray marcher shader
    void OnDrawGizmos()
    {
        if (cam == null || voxelizer == null) return;

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;

        // Inverse View-Projection Matrix
        Matrix4x4 invViewProjMatrix = viewProjMatrix.inverse;

        Gizmos.color = Color.red;

        int sampleStep = 50; // Adjust for performance: fewer rays for visualization
        float stepSize = this.stepSize; // Use the same step size as your raymarcher
        int maxSteps = this.maxSteps; // Maximum number of steps per ray

        for (int y = 0; y < Screen.height; y += sampleStep)
        {
            for (int x = 0; x < Screen.width; x += sampleStep)
            {
                // Generate UV coordinates
                Vector2 uv = new Vector2(x / (float)Screen.width, y / (float)Screen.height);

                // Transform UV to clip space
                Vector4 clipPos = new Vector4(uv.x * 2 - 1, uv.y * 2 - 1, 0, 1);

                // Transform to world space
                Vector4 transformedClipPos = invViewProjMatrix * clipPos;
                Vector3 worldPos = new Vector3(transformedClipPos.x, transformedClipPos.y, transformedClipPos.z) / transformedClipPos.w;

                // Calculate the ray direction from the camera to the world-space position
                Vector3 rayDir = (worldPos - cam.transform.position).normalized;
                rayDir = rayDir.normalized;

                Vector3 rayPos = cam.transform.position; // Camera world position
                Gizmos.color = Color.Lerp(Color.red, Color.blue, uv.y); // Color based on screen space

                for (int step = 0; step < maxSteps; step++)
                {
                    Vector3 nextPos = rayPos + rayDir * stepSize;

                    // Draw a line segment between ray positions
                    Gizmos.DrawLine(rayPos, nextPos);

                    // Stop if the ray exits the voxel grid
                    Vector3 gridMin = new Vector3(-10, 0, -10); // Grid bounds (adjust if needed)
                    Vector3 gridMax = new Vector3(10, 8, 10);

                    

                    rayPos = nextPos;
                }
            }
        }
    }*/
    /*
    void OnDrawGizmos()
    {
        if (cam == null || voxelizer == null) return;

        Matrix4x4 projMatrix = GL.GetGPUProjectionMatrix(cam.projectionMatrix, false);
        Matrix4x4 viewProjMatrix = projMatrix * cam.worldToCameraMatrix;

        Gizmos.color = Color.red;

        int sampleStep = 50; // Adjust to visualize fewer/more rays for performance
        for (int y = 0; y < Screen.height; y += sampleStep)
        {
            for (int x = 0; x < Screen.width; x += sampleStep)
            {
                // Get the ray for this pixel
                Ray ray = cam.ScreenPointToRay(new Vector3(x, y, 0));
                Vector3 rayPos = ray.origin;
                Vector3 rayDir = ray.direction.normalized;

                for (int step = 0; step < maxSteps; step++)
                {
                    Vector3 nextPos = rayPos + rayDir * stepSize;

                    float density = voxelizer.GetVoxelDensityAtPosition(nextPos);

                    if (density > 0.0f)
                    {
                        // Voxel has density, color the ray segment purple
                        Gizmos.color = Color.black;
                        Gizmos.DrawLine(rayPos, nextPos);
                    }
                    else
                    {
                        // Default ray color
                        Gizmos.color = Color.Lerp(Color.red, Color.blue, (float)y / Screen.height);
                    }

                    // Draw a line segment between ray positions
                    //Gizmos.DrawLine(rayPos, nextPos);

                    // Stop if the ray exits the voxel grid
                    /*Vector3 gridSize = voxelizer.GetGridSize();
                    if (nextPos.x < -10 || nextPos.y < -10 || nextPos.z < -10 ||
                        nextPos.x >= (gridSize.x * voxelizer.voxelSize)/2 ||
                        nextPos.y >= (gridSize.y * voxelizer.voxelSize)/2 ||
                        nextPos.z >= (gridSize.z * voxelizer.voxelSize)/2)
                    {
                        break;
                    }

                    rayPos = nextPos;
                }
            }
        }
    }*/
}
