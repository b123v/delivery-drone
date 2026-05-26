#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;
using System.Collections.Generic;

public class TrainingArenaGenerator : EditorWindow
{
    [MenuItem("Drone AI/1. Generate Sky Arenas")]
    public static void GenerateArenas()
    {
        AddTagIfNotExist("Package");
        AddTagIfNotExist("Target");
        AddTagIfNotExist("Obstacle");

        // 1. Ищем оригинального дрона и посылку (включая отключенные)
        GameObject originalDrone = GameObject.FindFirstObjectByType<DroneFlightController>(FindObjectsInactive.Include)?.gameObject;
        if (originalDrone == null)
        {
            Debug.LogError("Не найден дрон (DroneFlightController) на сцене!");
            return;
        }

        GameObject originalPackage = GameObject.FindFirstObjectByType<PackageDropController>(FindObjectsInactive.Include)?.GetPackage();
        if (originalPackage == null) originalPackage = GameObject.Find("Package");

        // 2. Создаем корень Арен высоко в небе
        GameObject arenasRoot = new GameObject("TrainingArenas_ML");
        arenasRoot.transform.position = new Vector3(0, 1000, 0); // Высоко, чтобы не мешал город

        // 3. Создаем базовую Арену
        GameObject baseArena = new GameObject("Arena_Base");
        baseArena.transform.SetParent(arenasRoot.transform);
        baseArena.transform.localPosition = Vector3.zero;

        // Пол Арены
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Cube);
        floor.name = "Floor";
        floor.transform.SetParent(baseArena.transform);
        floor.transform.localPosition = new Vector3(0, -0.5f, 0);
        floor.transform.localScale = new Vector3(40, 1, 40);

        // Стены (невидимые)
        CreateWall(baseArena.transform, "Wall_N", new Vector3(0, 10, 20), new Vector3(40, 20, 1));
        CreateWall(baseArena.transform, "Wall_S", new Vector3(0, 10, -20), new Vector3(40, 20, 1));
        CreateWall(baseArena.transform, "Wall_E", new Vector3(20, 10, 0), new Vector3(1, 20, 40));
        CreateWall(baseArena.transform, "Wall_W", new Vector3(-20, 10, 0), new Vector3(1, 20, 40));
        CreateWall(baseArena.transform, "Ceiling", new Vector3(0, 20, 0), new Vector3(40, 1, 40));

        // Цель (куда нести посылку)
        GameObject target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        target.name = "TargetZone";
        target.transform.SetParent(baseArena.transform);
        target.transform.localPosition = new Vector3(10, 0.1f, 10);
        target.transform.localScale = new Vector3(4, 0.1f, 4);
        target.GetComponent<Collider>().isTrigger = true; // Триггер, чтобы не биться об него
        target.tag = "Target";

        // 4. Клонируем Посылку
        GameObject clonedPackage = null;
        if (originalPackage != null)
        {
            clonedPackage = Instantiate(originalPackage, baseArena.transform);
            clonedPackage.name = "Package_Clone";
            clonedPackage.transform.localPosition = new Vector3(5, 1, 5);
            clonedPackage.SetActive(true); // ВАЖНО: включаем, если оригинал был выключен
        }
        else 
        {
            // Если оригинальная посылка не найдена — создадим запасную
            clonedPackage = GameObject.CreatePrimitive(PrimitiveType.Cube);
            clonedPackage.name = "Package_Clone";
            clonedPackage.transform.SetParent(baseArena.transform);
            clonedPackage.transform.localPosition = new Vector3(5, 1, 5);
            clonedPackage.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            clonedPackage.AddComponent<Rigidbody>();
        }

        // 5. Клонируем Дрона
        GameObject clonedDrone = Instantiate(originalDrone, baseArena.transform);
        clonedDrone.name = "DroneAgent_Clone";
        clonedDrone.transform.localPosition = new Vector3(0, 2, 0);
        clonedDrone.SetActive(true); // ВАЖНО: включаем, если оригинал был выключен
        
        // Отключаем лишние камеры и AudioListener у клонов
        Camera cam = clonedDrone.GetComponentInChildren<Camera>();
        if (cam != null) DestroyImmediate(cam.gameObject);
        AudioListener al = clonedDrone.GetComponentInChildren<AudioListener>();
        if (al != null) DestroyImmediate(al);

        // Настраиваем Дрона для ML-Agents
        DroneFlightController dfc = clonedDrone.GetComponent<DroneFlightController>();
        dfc.targetTransform = target.transform;
        
        // Жестко привязываем клонированную коробку к этому дрону!
        if (dfc.packageController != null && clonedPackage != null)
        {
            dfc.packageController.SetPackageForTraining(clonedPackage);
        }
        
        // Добавляем DecisionRequester
        if (clonedDrone.GetComponent<DecisionRequester>() == null)
        {
            var dr = clonedDrone.AddComponent<DecisionRequester>();
            dr.DecisionPeriod = 5;
            dr.TakeActionsBetweenDecisions = true;
        }

        // Настраиваем BehaviorParameters
        var bp = clonedDrone.GetComponent<BehaviorParameters>();
        if (bp == null) bp = clonedDrone.AddComponent<BehaviorParameters>();
        
        bp.BehaviorName = "DroneDelivery";
        bp.BehaviorType = BehaviorType.Default; // ПРИНУДИТЕЛЬНО ВКЛЮЧАЕМ РЕЖИМ ОБУЧЕНИЯ
        bp.BrainParameters.VectorObservationSize = 19; 
        bp.BrainParameters.NumStackedVectorObservations = 1;
        
        // 4 Continuous Actions (moveX, moveZ, yaw, thrust)
        // 1 Discrete Action branch (drop/pickup) with 2 choices (0 or 1)
        bp.BrainParameters.ActionSpec = new ActionSpec(4, new int[] { 2 });

        // Добавляем радар (Lidar)
        var raySensor = clonedDrone.GetComponent<RayPerceptionSensorComponent3D>();
        if (raySensor == null) raySensor = clonedDrone.AddComponent<RayPerceptionSensorComponent3D>();
        
        raySensor.SensorName = "Lidar3D";
        raySensor.DetectableTags = new List<string> { "Obstacle", "Package", "Target" };
        raySensor.RaysPerDirection = 7; // 7 влево, 7 вправо + 1 по центру = 15 лучей по горизонтали
        raySensor.MaxRayDegrees = 180f; // Круговой обзор (360 градусов)
        raySensor.SphereCastRadius = 0.5f;
        raySensor.RayLength = 30f;
        raySensor.ObservationStacks = 1;

        // Отключаем оригиналы
        originalDrone.SetActive(false);
        if (originalPackage != null) originalPackage.SetActive(false);

        // 6. Размножаем Арену (8x8 = 64 штуки)
        int gridSize = 8;
        
        // Добавляем препятствия на базовую арену
        GenerateObstaclesForArena(baseArena.transform);

        for (int x = 0; x < gridSize; x++)
        {
            for (int z = 0; z < gridSize; z++)
            {
                if (x == 0 && z == 0) continue; // baseArena уже стоит на 0,0

                GameObject arenaClone = Instantiate(baseArena, arenasRoot.transform);
                arenaClone.transform.localPosition = new Vector3(x * 50, 0, z * 50);
                arenaClone.name = $"Arena_{x}_{z}";
                
                // Перегенерируем препятствия, чтобы каждая арена была уникальной
                GenerateObstaclesForArena(arenaClone.transform);
            }
        }

        Debug.Log("✅ Успешно сгенерировано 16 Скай-Арен на высоте 1000м! Нажмите Play для проверки.");
    }

    private static void CreateWall(Transform parent, string name, Vector3 pos, Vector3 scale)
    {
        GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
        wall.name = name;
        wall.transform.SetParent(parent);
        wall.transform.localPosition = pos;
        wall.transform.localScale = scale;
        
        // Убираем MeshRenderer, чтобы стена была невидимой, но оставляем BoxCollider
        DestroyImmediate(wall.GetComponent<MeshRenderer>());
    }

    private static void GenerateObstaclesForArena(Transform arenaTransform)
    {
        // Сначала удаляем старые препятствия, если это клон базовой арены
        foreach (Transform child in arenaTransform)
        {
            if (child.name.StartsWith("Obstacle_"))
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // Спавним 4 случайных столба-препятствия
        for (int i = 0; i < 4; i++)
        {
            GameObject obstacle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            obstacle.name = $"Obstacle_{i}";
            obstacle.tag = "Obstacle"; // Важно для радара
            
            // Случайная позиция, но не слишком близко к центру (0,0) и к краям
            float rx = Random.Range(-15f, 15f);
            float rz = Random.Range(-15f, 15f);
            
            // Защита: не спавним прямо над центром (зоной спавна дрона)
            if (Mathf.Abs(rx) < 4f && Mathf.Abs(rz) < 4f) rx += 5f;

            obstacle.transform.SetParent(arenaTransform);
            obstacle.transform.localPosition = new Vector3(rx, 5f, rz);
            obstacle.transform.localScale = new Vector3(Random.Range(2f, 4f), 10f, Random.Range(2f, 4f));
            
            // Красим в красный или темный цвет, чтобы было видно
            var renderer = obstacle.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                renderer.sharedMaterial.color = new Color(0.3f, 0.3f, 0.3f);
            }
        }
    }

    private static void AddTagIfNotExist(string tag)
    {
        SerializedObject tagManager = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        SerializedProperty tagsProp = tagManager.FindProperty("tags");

        for (int i = 0; i < tagsProp.arraySize; i++)
        {
            SerializedProperty t = tagsProp.GetArrayElementAtIndex(i);
            if (t.stringValue.Equals(tag)) { return; }
        }

        tagsProp.InsertArrayElementAtIndex(0);
        SerializedProperty newTag = tagsProp.GetArrayElementAtIndex(0);
        newTag.stringValue = tag;
        tagManager.ApplyModifiedProperties();
    }
}
#endif
