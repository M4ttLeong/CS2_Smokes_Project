using UnityEngine;

public class NewMonoBehaviourScript : MonoBehaviour
{
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    float extentX;
    float extentY;
    float extentZ;
    Vector3 origin;
    Voxel testVoxel = new Voxel(new Vector3(0,0,0), 0.5f);
    void Start()
    {
        origin = transform.position;
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogWarning("No Renderer found on this GameObject.");
            return;
        }

        extentX = renderer.bounds.extents.x;
        if (gameObject.name == "Map_Floor") {
            extentY = 8.0f; 
        } else {
            extentY = renderer.bounds.extents.y;
        }
        extentZ = renderer.bounds.extents.z;
    }

    void OnDrawGizmos()
    {
        Debug.Log("OnDrawGizmos called");
        if (testVoxel != null)
        {
            Debug.Log("TestVoxel exists");
            // Draw the voxel as a red wireframe cube
            testVoxel.DrawVoxel(Color.red);
        }
    }

    // Update is called once per frame
    void Update()
    {
        //Need to find the corners of the AABB
        Vector3 maxXYZ = new Vector3(origin.x + extentX, origin.y + extentY, origin.z + extentZ);
        Vector3 minXYZ;

        if (gameObject.name == "Map_Floor")
        {
            minXYZ = new Vector3(origin.x - extentX, origin.y, origin.z - extentZ);
        }
        else
        {
            minXYZ = new Vector3(origin.x - extentX, origin.y - extentY, origin.z - extentZ);
        }

        Color line_Color = Color.red;
        Debug.DrawLine(minXYZ, maxXYZ, line_Color);
    }
}
