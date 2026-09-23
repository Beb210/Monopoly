// FixCubeUV.cs
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class FixCubeUV : MonoBehaviour
{
    void Start()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        Mesh mesh = meshFilter.mesh;

        // Правильные UV для 6 материалов на стандартном кубе
        Vector2[] uv = new Vector2[24];

        // Грань 0: +X (право)
        uv[0] = new Vector2(1, 0); uv[1] = new Vector2(1, 1);
        uv[2] = new Vector2(0, 0); uv[3] = new Vector2(0, 1);
        // Грань 1: -X (лево)
        uv[4] = new Vector2(0, 0); uv[5] = new Vector2(0, 1);
        uv[6] = new Vector2(1, 0); uv[7] = new Vector2(1, 1);
        // Грань 2: +Y (верх)
        uv[8] = new Vector2(0, 1); uv[9] = new Vector2(0, 0);
        uv[10] = new Vector2(1, 1); uv[11] = new Vector2(1, 0);
        // Грань 3: -Y (низ)
        uv[12] = new Vector2(1, 0); uv[13] = new Vector2(1, 1);
        uv[14] = new Vector2(0, 0); uv[15] = new Vector2(0, 1);
        // Грань 4: +Z (перед)
        uv[16] = new Vector2(0, 0); uv[17] = new Vector2(0, 1);
        uv[18] = new Vector2(1, 0); uv[19] = new Vector2(1, 1);
        // Грань 5: -Z (зад)
        uv[20] = new Vector2(1, 0); uv[21] = new Vector2(1, 1);
        uv[22] = new Vector2(0, 0); uv[23] = new Vector2(0, 1);

        mesh.uv = uv;
    }
}