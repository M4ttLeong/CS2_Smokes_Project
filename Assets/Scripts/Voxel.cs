using UnityEngine;

public struct Voxel
{
    public Vector3 position; //Sent to GPU
    public float density; //Sent to GPU
    public float sideLength; //Kept on the CPU

    public Voxel(Vector3 position, float density, float side_Length)
    {
        this.position = position;
        this.density = density;
        this.sideLength = side_Length;
    }

    public void DrawVoxel(Color color, float density = 0.0f)
    {
        Gizmos.color = new Color(color.r, color.g, color.b, density);
        Gizmos.DrawWireCube(position, Vector3.one * sideLength);
    }

}
