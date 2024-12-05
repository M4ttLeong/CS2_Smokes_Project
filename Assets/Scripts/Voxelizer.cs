using NUnit.Framework;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.UIElements;
using Unity.VisualScripting;


public class Voxelizer : MonoBehaviour
{
    public ComputeShader voxelShader;
    public GameObject smokePrefab;
    private List<GameObject> instantiatedSmokeVoxels = new List<GameObject>();

    public float growthSpeed = 10.0f;
    public Vector3 smokeSourcePos;
    private ComputeBuffer voxelBuffer;

    //OccupancyBuffer is where we bake in what voxels in the map are occupied and which aren't
    private ComputeBuffer occupancyBuffer;
    private int[] occupancy;

    private Vector3 originPos;
    private float extentX;
    //Designer can set the extentY them selves, height of the map
    public float extentY = 8.0f;
    private float extentZ;
    //Configurable but default is 1/8 the size of a unity squares
    public float voxelSize = 0.5f;
    private List<Voxel> voxelList;

    private int gridSizeX, gridSizeY, gridSizeZ;
    private float timer = 0.0f;
    public float smokeDisipationTimer = 2.0f;
    public bool canCreateMultipleSmokes = false;
    private bool smokeOnScreen = false;

    // This script needs to create a 1D buffer of voxels for my map
    // I suppose I'll just attach this to the floor of the map


    //For debugging gunshot
    Vector3 start = new Vector3(10,0,0);
    Vector3 end = new Vector3 (-10,0,0);
    
    void Start()
    {
        voxelList = new List<Voxel>();
        originPos = transform.position;
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("No Renderer found on this GameObject.");
            return;
        }

        extentX = renderer.bounds.extents.x;
        extentZ = renderer.bounds.extents.z;
        //I guess all of this can go in the start really, no updates needed

        //Get the corners of the AABB
        //To note, imagine the origin is at the bottom plane of the AABB

        Vector3 maxXYZ = new Vector3(originPos.x + extentX, originPos.y + extentY, originPos.z + extentZ);
        Vector3 minXYZ = new Vector3(originPos.x - extentX, originPos.y, originPos.z - extentZ);

        Debug.Log("maxXYZ x: " + maxXYZ.x + " y: " + maxXYZ.y + " z: " + maxXYZ.z);
        Debug.Log("minXYZ x: " + minXYZ.x + " y: " + minXYZ.y + " z: " + minXYZ.z);

        gridSizeX = Mathf.CeilToInt((maxXYZ.x - minXYZ.x) / voxelSize);
        gridSizeY = Mathf.CeilToInt((maxXYZ.y - minXYZ.y) / voxelSize);
        gridSizeZ = Mathf.CeilToInt((maxXYZ.z - minXYZ.z) / voxelSize);

        Debug.Log("Grid dimensions x: " + gridSizeX + " y: " + gridSizeY + " z: " + gridSizeZ);
        //Now we need to fill in the bounding box with voxels
        FillAABBWithVoxels(maxXYZ, minXYZ, gridSizeX * gridSizeY * gridSizeZ);

        InitializeComputeBuffer();

        InitializeOccupancyBuffer(gridSizeX * gridSizeY * gridSizeZ);

        smokeSourcePos = transform.position + new Vector3(0, 1, 0);

    }

    // Update is called once per frame
    void Update()
    {
        /*
        int kernelHandle = voxelShader.FindKernel("CSMain");
        voxelShader.SetVector("smokeSourcePos", smokeSourcePos);

        voxelShader.Dispatch(kernelHandle,
            Mathf.CeilToInt(gridSizeX / 8.0f),
            Mathf.CeilToInt(gridSizeY / 8.0f),
            Mathf.CeilToInt(gridSizeZ / 1.0f));
        */

        //drawSmokeVoxels();
        
        //timer to control when we clear the smoke
        timer += Time.deltaTime;

        if (Input.GetMouseButtonDown(0))
        {
            if(!smokeOnScreen || canCreateMultipleSmokes)
            {
                Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                RaycastHit hit;

                if (Physics.Raycast(ray, out hit, 50))
                {
                    smokeSourcePos = hit.point;
                    int kernelHandle = voxelShader.FindKernel("CSMain");
                    voxelShader.SetVector("smokeSourcePos", smokeSourcePos);

                    voxelShader.Dispatch(kernelHandle,
                        Mathf.CeilToInt(gridSizeX / 8.0f),
                        Mathf.CeilToInt(gridSizeY / 8.0f),
                        Mathf.CeilToInt(gridSizeZ / 1.0f));
                    smokeOnScreen = true;
                }

                drawSmokeVoxels();
            }
            
        }

        if (Input.GetMouseButtonDown(1))
        {
            if (smokeOnScreen)
            {
                //Simulate gun shot to break up the volume. 
                FireRayAndClearVoxels();
            }
        }

        Debug.Log("drawing line");
        Debug.DrawLine(start, end, Color.red);

        if (timer >= smokeDisipationTimer) 
        {
            int clearKernelHandle = voxelShader.FindKernel("ClearBuffer");
            voxelShader.Dispatch(clearKernelHandle,
                Mathf.CeilToInt(gridSizeX / 8.0f),
                Mathf.CeilToInt(gridSizeY / 8.0f),
                Mathf.CeilToInt(gridSizeZ / 1.0f));
            timer = 0.0f;
            smokeOnScreen = false;

            removeSmokeVoxels();
        }

    }

    void FillAABBWithVoxels(Vector3 maxXYZ, Vector3 minXYZ, int gridSize)
    {
        int i = 0;
        occupancy = new int[gridSize];
        for (float x = minXYZ.x + voxelSize/2; x < maxXYZ.x; x += voxelSize)
        {
            for (float y = minXYZ.y + voxelSize/2; y < maxXYZ.y; y += voxelSize)
            {
                for (float z = minXYZ.z + voxelSize/2; z < maxXYZ.z; z += voxelSize)
                {
                    //Make a voxel
                    float density = 0.0f;
                    Voxel v = new Voxel(new Vector3(x, y, z), density, voxelSize);
                    voxelList.Add(v);

                    //fill the occupancy list here
                    if(isOccupied(new Vector3 (x, y, z)))
                    {
                        occupancy[i] = 1;
                    } else
                    {
                        occupancy[i] = 0;
                    }

                    i++;
                }
            }
        }
    }

    void InitializeComputeBuffer()
    {
        voxelBuffer = new ComputeBuffer(voxelList.Count, sizeof(float) * 5); //3 floats for pos, 1 for density, 1 for sidelength
        voxelBuffer.SetData(voxelList.ToArray());

        int kernelHandle = voxelShader.FindKernel("CSMain");
        voxelShader.SetBuffer(kernelHandle, "voxelBuffer", voxelBuffer);

        int clearKernelHandle = voxelShader.FindKernel("ClearBuffer");
        voxelShader.SetBuffer(clearKernelHandle, "voxelBuffer", voxelBuffer);

        voxelShader.SetInt("groupSizeX", gridSizeX);
        voxelShader.SetInt("groupSizeY", gridSizeY);
        voxelShader.SetInt("groupSizeZ", gridSizeZ);

        voxelShader.SetFloat("growthSpeed", growthSpeed);
    }

    void InitializeOccupancyBuffer(int gridSize)
    {
        //This buffer should never change really because the map occupancy doesn't vary, not dynamic
        //I think this is baking

        //At this point we should have the occupancy list of size grid
        //Load that into the separate compute buffer for occupancy
        occupancyBuffer = new ComputeBuffer(gridSize, sizeof(int)); //This buffer just stores 0s and 1s
        occupancyBuffer.SetData(occupancy);

        //kernel still needs to be specified here, each kernel needs to know which buffer resources
        //it can access

        //Don't need to let the occupancyBuffer see the ClearBuffer kernel for instance since
        //it's not relevant
        int kernelHandle = voxelShader.FindKernel("CSMain");
        voxelShader.SetBuffer(kernelHandle, "occupancyBuffer", occupancyBuffer);

    }

    bool isOccupied(Vector3 pos, float radius = 0.1f)
    {
        //Method to check if there is anything in a small radius from pos that thas a collider
        Collider[] colliders = Physics.OverlapSphere(pos, radius);
        return colliders.Length > 0;
    }

    void drawSmokeVoxels()
    {
        if (voxelList != null && voxelBuffer != null)
        {
            Voxel[] voxels = new Voxel[voxelList.Count];
            voxelBuffer.GetData(voxels);
            foreach (Voxel v in voxels)
            {
                //instantiate a new voxelPrefab, give it a random color
                if (v.density > 0.001f)
                {
                    GameObject smokeVoxel = Instantiate(smokePrefab, v.position, Quaternion.identity);
                    smokeVoxel.transform.localScale = Vector3.one * v.sideLength;
                    Renderer renderer = smokeVoxel.GetComponent<Renderer>();
                    if(renderer != null)
                    {
                        renderer.material.color = new Color(Random.value, Random.value, Random.value);
                    }

                    instantiatedSmokeVoxels.Add(smokeVoxel);
                }
            }
        }
    }

    void removeSmokeVoxels()
    {
        foreach(GameObject smokeVoxel in instantiatedSmokeVoxels)
        {
            Destroy(smokeVoxel);
        }
        instantiatedSmokeVoxels.Clear();
    }

    void FireRayAndClearVoxels()
    {
        Debug.Log("Got here");
        //CPU side computation of 1 ray
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);

        Vector3 maxXYZ = new Vector3(originPos.x + extentX, originPos.y + extentY, originPos.z + extentZ);
        Vector3 minXYZ = new Vector3(originPos.x - extentX, originPos.y, originPos.z - extentZ);

        float tMin, tMax;
        if (!RayBoxIntersection(ray.origin, ray.direction, minXYZ, maxXYZ, out tMin, out tMax))
        {
            // No intersection with the voxel grid bounding box
            Debug.Log("Didn't hit the box");
            return;
        } else
        {
            Debug.Log("Hit the AABB");
        }

        start = ray.origin + ray.direction * tMin;
        end = ray.origin + ray.direction * tMax;

        

        Voxel[] voxels = new Voxel[voxelBuffer.count];
        voxelBuffer.GetData(voxels);

        ClearVoxelsAlongRay(start, end, voxels);
        voxelBuffer.SetData(voxels);
    }

    //AI generated function
    void ClearVoxelsAlongRay(Vector3 start, Vector3 end, Voxel[] voxels)
    {
        // Convert start/end world positions to voxel coordinates
        // Grid origin is at minXYZ + voxelSize/2 offset per voxel
        Vector3 gridMin = new Vector3(originPos.x - extentX, originPos.y, originPos.z - extentZ);

        Vector3 startShifted = start - gridMin;
        Vector3 endShifted = end - gridMin;

        int startX = Mathf.FloorToInt(startShifted.x / voxelSize);
        int startY = Mathf.FloorToInt(startShifted.y / voxelSize);
        int startZ = Mathf.FloorToInt(startShifted.z / voxelSize);

        // Check bounds
        if (!IsInsideGrid(startX, startY, startZ))
            return;

        // Calculate the ray direction in voxel coordinates
        Vector3 rayDir = (end - start).normalized;

        // Direction steps
        int stepX = (rayDir.x > 0) ? 1 : -1;
        int stepY = (rayDir.y > 0) ? 1 : -1;
        int stepZ = (rayDir.z > 0) ? 1 : -1;

        // Next boundary planes
        float tMaxX = VoxelBoundaryIntersection(start.x, rayDir.x, gridMin.x, startX, stepX);
        float tMaxY = VoxelBoundaryIntersection(start.y, rayDir.y, gridMin.y, startY, stepY);
        float tMaxZ = VoxelBoundaryIntersection(start.z, rayDir.z, gridMin.z, startZ, stepZ);

        // tDelta values
        float tDeltaX = (voxelSize / Mathf.Abs(rayDir.x));
        float tDeltaY = (voxelSize / Mathf.Abs(rayDir.y));
        float tDeltaZ = (voxelSize / Mathf.Abs(rayDir.z));

        int x = startX;
        int y = startY;
        int z = startZ;

        // Traverse until we go out of bounds or hit the end
        while (IsInsideGrid(x, y, z))
        {
            // Clear density of current voxel
            int index = x * (gridSizeY * gridSizeZ) + y * gridSizeZ + z;
            voxels[index].density = 0.0f;

            // Move to next voxel
            if (tMaxX < tMaxY)
            {
                if (tMaxX < tMaxZ)
                {
                    x += stepX;
                    tMaxX += tDeltaX;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }
            else
            {
                if (tMaxY < tMaxZ)
                {
                    y += stepY;
                    tMaxY += tDeltaY;
                }
                else
                {
                    z += stepZ;
                    tMaxZ += tDeltaZ;
                }
            }

            // Stop if we've passed the end point (optional optimization)
            // Could check if distance along ray > (end - start).magnitude
        }
    }

    bool IsInsideGrid(int x, int y, int z)
    {
        return (x >= 0 && x < gridSizeX &&
                y >= 0 && y < gridSizeY &&
                z >= 0 && z < gridSizeZ);
    }

    float VoxelBoundaryIntersection(float startCoord, float dir, float gridOrigin, int voxelIndex, int step)
    {
        float boundary = gridOrigin + (voxelIndex + (step > 0 ? 1 : 0)) * voxelSize;
        return (dir != 0) ? (boundary - startCoord) / dir : float.MaxValue;
    }

    //Entry exit code AI Generated
    bool RayBoxIntersection(Vector3 rayOrigin, Vector3 rayDir, Vector3 boxMin, Vector3 boxMax, out float tMin, out float tMax)
    {
        tMin = float.MinValue;
        tMax = float.MaxValue;

        for (int i = 0; i < 3; i++)
        {
            float invD = 1.0f / rayDir[i];
            float t0 = (boxMin[i] - rayOrigin[i]) * invD;
            float t1 = (boxMax[i] - rayOrigin[i]) * invD;

            if (invD < 0.0f)
            {
                float temp = t0;
                t0 = t1;
                t1 = temp;
            }

            tMin = Mathf.Max(tMin, t0);
            tMax = Mathf.Min(tMax, t1);

            if (tMax < tMin)
                return false;
        }
        return true;
    }


    public ComputeBuffer GetVoxelBuffer()
    {
        return this.voxelBuffer;
    }

    public Vector3 GetGridSize()
    {
        return new Vector3(this.gridSizeX, this.gridSizeY, this.gridSizeZ);
    }

    public float GetVoxelDensityAtPosition(Vector3 position)
    {
        // Calculate the minimum corner of the voxel grid in world space
        Vector3 minXYZ = new Vector3(-gridSizeX / 2.0f * voxelSize, 0, -gridSizeZ / 2.0f * voxelSize);

        // Shift the input position into the voxel grid's coordinate system
        Vector3 shiftedPos = position - minXYZ;

        // Convert world-space position to voxel indices
        int x = Mathf.FloorToInt(shiftedPos.x / voxelSize);
        int y = Mathf.FloorToInt(shiftedPos.y / voxelSize);
        int z = Mathf.FloorToInt(shiftedPos.z / voxelSize);

        // Bounds check
        if (x < 0 || y < 0 || z < 0 || x >= gridSizeX || y >= gridSizeY || z >= gridSizeZ)
        {
            return 0.0f; // Out of bounds
        }

        // Calculate the 1D index of the voxel in the buffer
        int index = x * (gridSizeY * gridSizeZ) + y * gridSizeZ + z;

        // Create a managed array to hold voxel buffer data
        float[] voxelData = new float[voxelBuffer.count * 5];

        // Copy data from GPU buffer to CPU
        voxelBuffer.GetData(voxelData);

        // Return the density of the voxel (4th float in each 5-float block)
        return voxelData[index * 5 + 3];
    }
    void OnDrawGizmos()
    {
        if (voxelList != null && voxelBuffer != null)
        {
            Voxel[] voxels = new Voxel[voxelList.Count];
            voxelBuffer.GetData(voxels);
            foreach(Voxel v in voxels)
            {
                //Set v.density to 1.0 if you want to see the whole AABB
                v.DrawVoxel(Color.red, v.density);
            }
        }
    }
}
