using UnityEngine;
using UnityEditor;

public class AddCityCollidersEditor : Editor
{
    [MenuItem("Tools/Add Colliders to City")]
    public static void AddColliders()
    {
        // Ищем все видимые 3D-модели (MeshRenderer) на текущей сцене
        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
        int addedCount = 0;

        foreach (MeshRenderer renderer in renderers)
        {
            // Проверяем, есть ли уже какой-нибудь коллайдер на этом объекте
            Collider existingCollider = renderer.GetComponent<Collider>();
            
            if (existingCollider == null)
            {
                // Если коллайдера нет, добавляем MeshCollider (он точно повторяет форму модели)
                renderer.gameObject.AddComponent<MeshCollider>();
                addedCount++;
            }
        }

        Debug.Log($"Успешно добавлено коллайдеров: {addedCount}. Теперь город осязаем!");
    }
}
