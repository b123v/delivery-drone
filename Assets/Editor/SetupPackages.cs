using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using System.IO;

public class SetupPackages 
{
    [MenuItem("Tools/Setup Drone Packages & Drop Zones")]
    public static void Setup() 
    {
        var drone = GameObject.Find("DroneSketchfab");
        if (drone != null) 
        {
            Transform boxChild = null;
            foreach (Transform child in drone.transform)
            {
                string lowerName = child.name.ToLower();
                if (lowerName.Contains("container") || (lowerName.Contains("box") && !lowerName.Contains("box026") && !lowerName.Contains("box027")))
                {
                    boxChild = child;
                    break;
                }
            }
            if (boxChild != null) Undo.DestroyObjectImmediate(boxChild.gameObject);
        }

        GameObject packageRoot = GameObject.Find("PackagesRoot");
        if (packageRoot != null) Undo.DestroyObjectImmediate(packageRoot);
        
        packageRoot = new GameObject("PackagesRoot");
        Undo.RegisterCreatedObjectUndo(packageRoot, "Create PackagesRoot");

        // 1. Создаем папку для материалов
        if (!AssetDatabase.IsValidFolder("Assets/Materials"))
        {
            AssetDatabase.CreateFolder("Assets", "Materials");
        }

        // 2. Создаем текстуру картона и сохраняем как файл (чтобы не пропадала)
        string texPath = "Assets/Materials/CardboardTex.png";
        Texture2D cardboardTex = new Texture2D(128, 128);
        for (int y = 0; y < 128; y++) {
            for (int x = 0; x < 128; x++) {
                float noise = Mathf.PerlinNoise(x * 0.1f, y * 0.1f) * 0.2f;
                cardboardTex.SetPixel(x, y, new Color(0.7f - noise, 0.5f - noise, 0.3f - noise));
            }
        }
        cardboardTex.Apply();
        File.WriteAllBytes(texPath, cardboardTex.EncodeToPNG());
        AssetDatabase.ImportAsset(texPath);
        Texture2D loadedTex = AssetDatabase.LoadAssetAtPath<Texture2D>(texPath);

        // 3. Создаем материалы с поддержкой URP/HDRP
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) shader = Shader.Find("Standard");

        Material boxMat = new Material(shader);
        if (boxMat.HasProperty("_BaseMap")) boxMat.SetTexture("_BaseMap", loadedTex); // URP
        if (boxMat.HasProperty("_MainTex")) boxMat.SetTexture("_MainTex", loadedTex); // Standard
        if (boxMat.HasProperty("_BaseColor")) boxMat.SetColor("_BaseColor", Color.white);
        
        AssetDatabase.CreateAsset(boxMat, "Assets/Materials/PackageMat.mat");

        Material crateMat = new Material(shader);
        Color crateColor = new Color(0.2f, 0.5f, 0.8f);
        if (crateMat.HasProperty("_BaseColor")) crateMat.SetColor("_BaseColor", crateColor);
        if (crateMat.HasProperty("_Color")) crateMat.SetColor("_Color", crateColor);
        
        AssetDatabase.CreateAsset(crateMat, "Assets/Materials/DropZoneMat.mat");

        // 4. Раскидываем 10 коробок
        Vector3 center = drone != null ? drone.transform.position : new Vector3(-320f, 6f, 65f);
        for (int i = 0; i < 10; i++)
        {
            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = "Package_" + i;
            box.tag = "Untagged";
            var rb = box.AddComponent<Rigidbody>();
            rb.mass = 2f;
            box.GetComponent<MeshRenderer>().sharedMaterial = boxMat;
            box.transform.localScale = new Vector3(0.8f, 0.8f, 0.8f);
            
            Vector2 randomCircle = Random.insideUnitCircle * 20f;
            box.transform.position = center + new Vector3(randomCircle.x, 3f, randomCircle.y);
            box.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
            box.transform.SetParent(packageRoot.transform);
        }

        // 5. Создаем 3 зоны доставки (Открытые контейнеры)
        for (int i = 0; i < 3; i++)
        {
            GameObject crateRoot = new GameObject("DropZone_" + i);
            Vector2 rPos = Random.insideUnitCircle * 25f;
            crateRoot.transform.position = center + new Vector3(rPos.x, 0.5f, rPos.y);
            
            // Дно
            GameObject bottom = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bottom.transform.SetParent(crateRoot.transform);
            bottom.transform.localPosition = Vector3.zero;
            bottom.transform.localScale = new Vector3(4f, 0.2f, 4f);
            bottom.GetComponent<MeshRenderer>().sharedMaterial = crateMat;

            // Стенки
            Vector3[] pos = { new Vector3(2f, 1f, 0), new Vector3(-2f, 1f, 0), new Vector3(0, 1f, 2f), new Vector3(0, 1f, -2f) };
            Vector3[] scale = { new Vector3(0.2f, 2f, 4.2f), new Vector3(0.2f, 2f, 4.2f), new Vector3(3.8f, 2f, 0.2f), new Vector3(3.8f, 2f, 0.2f) };

            for(int w = 0; w < 4; w++) {
                GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
                wall.transform.SetParent(crateRoot.transform);
                wall.transform.localPosition = pos[w];
                wall.transform.localScale = scale[w];
                wall.GetComponent<MeshRenderer>().sharedMaterial = crateMat;
            }

            crateRoot.transform.SetParent(packageRoot.transform);
        }

        AssetDatabase.SaveAssets();
        EditorSceneManager.MarkSceneDirty(EditorSceneManager.GetActiveScene());
        Debug.Log("Посылки и зоны созданы с поддержкой URP!");
    }
}
