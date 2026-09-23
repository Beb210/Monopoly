// DiceUVFixer.cs
// Костыль, потому юнити не нравятся мои текстуры
using UnityEngine;

[RequireComponent(typeof(MeshFilter))]
public class DiceUVFixer : MonoBehaviour
{
    [Header("Настройка развёртки текстуры")]
    [Tooltip("Количество колонок в атласе (2 или 3)")]
    public int atlasColumns = 2;
    [Tooltip("Количество рядов в атласе (3 или 2)")]
    public int atlasRows = 3;

    [Header("Порядок граней в вашей текстуре")]
    [Tooltip("Какая грань где находится в атласе")]
    public int[] faceOrder = new int[] { 2, 3, 1, 6, 4, 5 }; 
    // 0=+X, 1=-X, 2=+Y, 3=-Y, 4=+Z, 5=-Z

    private void Start()
    {
        FixUVs();
    }

    // Тот самый костыль
    public void FixUVs()
    {
        MeshFilter meshFilter = GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.mesh == null)
        {
            Debug.LogError("DiceUVFixer: Mesh не найден!");
            return;
        }

        Mesh mesh = meshFilter.mesh;
        Vector2[] uv = new Vector2[mesh.uv.Length];
        
        float cellWidth = 1f / atlasColumns;
        float cellHeight = 1f / atlasRows;

        for (int face = 0; face < 6; face++)
        {
            int faceIndexInAtlas = faceOrder[face]; 
            int col = (faceIndexInAtlas - 1) % atlasColumns;
            int row = (faceIndexInAtlas - 1) / atlasColumns;
            
            float uMin = col * cellWidth;
            float uMax = (col + 1) * cellWidth;
            float vMin = 1f - (row + 1) * cellHeight;
            float vMax = 1f - row * cellHeight;

            int baseVert = face * 4;
            uv[baseVert + 0] = new Vector2(uMin, vMin);
            uv[baseVert + 1] = new Vector2(uMin, vMax);
            uv[baseVert + 2] = new Vector2(uMax, vMin);
            uv[baseVert + 3] = new Vector2(uMax, vMax);
        }
        
        mesh.uv = uv;
        Debug.Log("DiceUVFixer: UV координаты исправлены!");
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireCube(transform.position, Vector3.one);
    }
}