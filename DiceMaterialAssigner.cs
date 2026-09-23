// DiceMaterialAssigner.cs
// чтобэ текстурка красиво легла на кубик
using UnityEngine;

public class DiceMaterialAssigner : MonoBehaviour
{
    [Header("Материалы для граней")]
    public Material face1; // Верх
    public Material face2; // Перед
    public Material face3; // Право
    public Material face4; // Лево
    public Material face5; // Зад
    public Material face6; // Низ

    void Start()
    {
        AssignMaterials();
    }

    void AssignMaterials()
    {
        Renderer renderer = GetComponent<Renderer>();
        if (renderer == null)
        {
            Debug.LogError("DiceMaterialAssigner: Renderer не найден!");
            return;
        }

        Material[] materials = new Material[6];
        
        // Порядок материалов для стандартного Unity Cube
        materials[0] = face3; // +X (право)
        materials[1] = face4; // -X (лево)
        materials[2] = face1; // +Y (верх)
        materials[3] = face6; // -Y (низ)
        materials[4] = face2; // +Z (перед)
        materials[5] = face5; // -Z (зад)
        
        renderer.materials = materials;
    }

    [ContextMenu("Apply Materials")]
    public void ApplyMaterialsFromEditor()
    {
        AssignMaterials();
    }
}