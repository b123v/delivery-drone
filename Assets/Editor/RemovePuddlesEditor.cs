using UnityEngine;
using UnityEditor;

public class RemovePuddlesEditor : Editor
{
    [MenuItem("Tools/Remove Puddles (Make Roads Dry)")]
    public static void RemoveWater()
    {
        // Ищем все материалы в проекте
        string[] guids = AssetDatabase.FindAssets("t:Material");
        int count = 0;

        foreach (string guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Ограничиваемся только материалами нашего города
            if (path.Contains("Demo City By Versatile Studio"))
            {
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                bool modified = false;

                // Удаляем текстуры из слотов Specular и Metallic, которые делают лужи
                if (mat.HasProperty("_SpecGlossMap") && mat.GetTexture("_SpecGlossMap") != null)
                {
                    mat.SetTexture("_SpecGlossMap", null);
                    modified = true;
                }
                if (mat.HasProperty("_MetallicGlossMap") && mat.GetTexture("_MetallicGlossMap") != null)
                {
                    mat.SetTexture("_MetallicGlossMap", null);
                    modified = true;
                }

                // Снижаем общую гладкость
                if (mat.HasProperty("_Smoothness"))
                {
                    mat.SetFloat("_Smoothness", 0.1f);
                    modified = true;
                }
                if (mat.HasProperty("_Glossiness")) // Для старых шейдеров
                {
                    mat.SetFloat("_Glossiness", 0.1f);
                    modified = true;
                }

                if (modified)
                {
                    EditorUtility.SetDirty(mat);
                    count++;
                }
            }
        }

        AssetDatabase.SaveAssets();
        Debug.Log($"Успешно высушено материалов: {count}!");
    }
}
