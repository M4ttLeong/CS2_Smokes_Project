using UnityEngine;

public class Voxel
{
    public Vector3 position;
    public float sideLength;

    public Voxel(Vector3 position, float side_Length)
    {
        this.position = position;
        this.sideLength = side_Length;
    }

    public void DrawVoxel(Color color)
    {
        Gizmos.color = color;
        Gizmos.DrawWireCube(position, Vector3.one * sideLength);
    }

}
