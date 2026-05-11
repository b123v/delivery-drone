using UnityEngine;
using UnityEditor;

public class MakeDaytimeEditor : Editor
{
    [MenuItem("Tools/Make Daytime")]
    public static void ConvertToDay()
    {
        // 1. Настраиваем солнце (Directional Light)
        Light[] allLights = FindObjectsOfType<Light>();
        bool sunFound = false;

        foreach (Light light in allLights)
        {
            // Отключаем уличные фонари (Point и Spot)
            if (light.type == LightType.Point || light.type == LightType.Spot)
            {
                light.enabled = false;
            }
            // Настраиваем основной свет (Солнце)
            else if (light.type == LightType.Directional)
            {
                light.intensity = 1.5f; // Яркость солнца
                light.color = new Color(1f, 0.95f, 0.9f); // Слегка теплый свет
                light.transform.rotation = Quaternion.Euler(50, -30, 0); // Угол солнца в небе
                light.shadows = LightShadows.Soft;
                sunFound = true;
            }
        }

        // Если солнца не было, создаем его
        if (!sunFound)
        {
            GameObject sunObj = new GameObject("Sun (Directional Light)");
            Light sun = sunObj.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.5f;
            sun.color = new Color(1f, 0.95f, 0.9f);
            sun.transform.rotation = Quaternion.Euler(50, -30, 0);
            sun.shadows = LightShadows.Soft;
        }

        // 2. Отключаем свечение (Emission) окон
        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
        foreach (MeshRenderer renderer in renderers)
        {
            foreach (Material mat in renderer.sharedMaterials)
            {
                if (mat != null && mat.HasProperty("_EmissionColor"))
                {
                    mat.SetColor("_EmissionColor", Color.black);
                    mat.DisableKeyword("_EMISSION");
                }
            }
        }

        // 3. Базовое осветление теней
        RenderSettings.ambientLight = new Color(0.6f, 0.6f, 0.6f);

        Debug.Log("Город успешно переведен в дневной режим!");
    }
}
