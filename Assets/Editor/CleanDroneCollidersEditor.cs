using UnityEngine;
using UnityEditor;

public class CleanDroneCollidersEditor : Editor
{
    [MenuItem("Tools/Clean Drone Mesh Colliders")]
    public static void CleanColliders()
    {
        // Ищем дрона по имени
        GameObject drone = GameObject.Find("DroneSketchfab");
        if (drone == null)
        {
            Debug.LogError("Дрон с именем DroneSketchfab не найден на сцене!");
            return;
        }

        // Ищем ВСЕ MeshCollider во всех вложенных объектах любой глубины
        MeshCollider[] colliders = drone.GetComponentsInChildren<MeshCollider>(true);
        int count = 0;

        foreach (MeshCollider col in colliders)
        {
            DestroyImmediate(col);
            count++;
        }

        Debug.Log($"Успешно удалено {count} лишних MeshCollider из внутренностей дрона!");
    }
}
