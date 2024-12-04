using NUnit.Framework;
using TMPro;
using UnityEngine;
using System.Collections.Generic;
using System.Threading;
using UnityEngine.UIElements;


public class Voxelizer : MonoBehaviour
{
    public ComputeShader voxelShader;

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
            }
            
        }

        if(timer >= smokeDisipationTimer) 
        {
            int clearKernelHandle = voxelShader.FindKernel("ClearBuffer");
            voxelShader.Dispatch(clearKernelHandle,
                Mathf.CeilToInt(gridSizeX / 8.0f),
                Mathf.CeilToInt(gridSizeY / 8.0f),
                Mathf.CeilToInt(gridSizeZ / 1.0f));
            timer = 0.0f;
            smokeOnScreen = false;
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

    void OnDrawGizmos()
    {
        if (voxelList != null && voxelBuffer != null)
        {
            Voxel[] voxels = new Voxel[voxelList.Count];
            voxelBuffer.GetData(voxels);
            foreach(Voxel v in voxels)
            {
                v.DrawVoxel(Color.red, v.density);
            }
        }
    }
}
