using UnityEngine;
using UnityEditor;
using System.Linq;

public class CitySetup : EditorWindow
{
    [MenuItem("ML-Agents/Setup City For Drone")]
    public static void SetupCity()
    {
        // 1. Помечаем все здания и деревья как Obstacle
        int taggedCount = 0;
        MeshRenderer[] renderers = FindObjectsOfType<MeshRenderer>();
        foreach (var rend in renderers)
        {
            GameObject go = rend.gameObject;
            
            // Исключаем дрона, арены и посылки, проверяя самого объекта и ВСЕХ ЕГО РОДИТЕЛЕЙ!
            if (go.GetComponentInParent<DroneFlightController>() != null) continue;
            if (go.transform.root.name.Contains("Arena")) continue;
            if (go.transform.root.name.Contains("Package")) continue;
            if (go.transform.root.name.Contains("Target")) continue;
            if (go.CompareTag("Package") || go.CompareTag("Target")) continue;

            if (go.CompareTag("Untagged") || go.CompareTag("Obstacle"))
            {
                go.tag = "Obstacle";
                // Убеждаемся что у препятствия есть коллайдер
                if (go.GetComponent<Collider>() == null)
                {
                    go.AddComponent<MeshCollider>();
                }
                taggedCount++;
            }
        }
        Debug.Log($"[CitySetup] Помечено как Obstacle: {taggedCount} объектов.");

        // 1.5. Удаляем "невидимые стены" (коллайдеры без MeshRenderer), чтобы они не мешали радару
        int removedInvisibleWalls = 0;
        Collider[] allColliders = FindObjectsOfType<Collider>();
        foreach (Collider col in allColliders)
        {
            GameObject go = col.gameObject;
            
            // Защита от удаления важных коллайдеров
            if (go.GetComponentInParent<DroneFlightController>() != null) continue;
            if (go.transform.root.name.Contains("Arena")) continue;
            if (go.transform.root.name.Contains("Package") || go.CompareTag("Package")) continue;
            if (go.transform.root.name.Contains("Target") || go.CompareTag("Target")) continue;
            if (col.isTrigger) continue; // Триггеры нам не мешают
            if (col is TerrainCollider) continue; // Землю не трогаем

            // Если у объекта нет MeshRenderer (или он выключен), значит он невидим! Сносим его коллайдер.
            MeshRenderer mr = go.GetComponent<MeshRenderer>();
            if (mr == null || !mr.enabled)
            {
                DestroyImmediate(col);
                removedInvisibleWalls++;
            }
        }
        Debug.Log($"[CitySetup] Удалено {removedInvisibleWalls} невидимых стен/коллайдеров.");

        // 2. Удаляем тренировочные арены
        GameObject arenasFolder = GameObject.Find("TrainingArenas_ML");
        if (arenasFolder != null)
        {
            DestroyImmediate(arenasFolder);
            Debug.Log("[CitySetup] Тренировочные арены удалены.");
        }

        // 3. Ищем кастомную Посылку пользователя по тегу, исключая клонов из предыдущих запусков
        GameObject package = null;
        var allPackages = GameObject.FindGameObjectsWithTag("Package");
        foreach (var p in allPackages)
        {
            // Надежная проверка: если посылка находится внутри папки клонов (даже если её подобрал клон)
            if (p.transform.root.name.Contains("CityTrainingClones")) continue;
            // Также на всякий случай пропускаем, если она почему-то осталась в удаленной папке Арен
            if (p.transform.root.name.Contains("TrainingArenas_ML")) continue;
            
            package = p;
            Debug.Log($"[CitySetup] Найдена оригинальная кастомная посылка пользователя: {package.name}");
            break;
        }

        if (package == null)
        {
            package = GameObject.Find("Package_City");
        }
        
        if (package == null)
        {
            package = GameObject.CreatePrimitive(PrimitiveType.Cube);
            package.name = "Package_City";
            package.transform.position = new Vector3(10, 0.5f, 10);
            package.transform.localScale = new Vector3(0.5f, 0.5f, 0.5f);
            package.AddComponent<Rigidbody>();
            package.tag = "Package";
            Debug.Log("[CitySetup] Создана стандартная посылка-кубик.");
        }

        GameObject target = GameObject.Find("TargetZone_City");
        if (target == null)
        {
            target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "TargetZone_City";
            target.transform.position = new Vector3(-20, 0.1f, 30);
            target.transform.localScale = new Vector3(4, 0.1f, 4);
            Collider col = target.GetComponent<Collider>();
            if (col != null) col.isTrigger = true;
            target.tag = "Target";
        }

        // 4. Настраиваем дрона
        var allDrones = FindObjectsOfType<DroneFlightController>();
        DroneFlightController dfc = null;
        foreach (var d in allDrones)
        {
            if (d.transform.root.name.Contains("CityTrainingClones")) continue;
            dfc = d;
            break;
        }
        
        if (dfc != null)
        {
            GameObject drone = dfc.gameObject;
            
            // ЧИНИМ ОШИБКИ КОЛЛАЙДЕРОВ: удаляем кривые меш-коллайдеры с деталей дрона
            var badColliders = drone.GetComponentsInChildren<MeshCollider>();
            foreach (var bc in badColliders) {
                DestroyImmediate(bc);
            }
            
            // Убираем старый DeliveryManager, если он был
            var oldManager = drone.GetComponent<CityDeliveryManager>();
            if (oldManager != null) DestroyImmediate(oldManager);
            
            // Добавляем менеджер переобучения в городе
            var trainManager = drone.GetComponent<CityTrainingManager>();
            if (trainManager == null) trainManager = drone.AddComponent<CityTrainingManager>();

            // Автоматически добавляем Радар, если его нет (чтобы восстановить зрение после отката)
            var sensorComp = drone.GetComponent<Unity.MLAgents.Sensors.RayPerceptionSensorComponent3D>();
            if (sensorComp == null)
            {
                sensorComp = drone.AddComponent<Unity.MLAgents.Sensors.RayPerceptionSensorComponent3D>();
                sensorComp.SensorName = "RayPerceptionSensor";
                sensorComp.RaysPerDirection = 7;
                sensorComp.MaxRayDegrees = 180f;
                sensorComp.RayLength = 30f;
                sensorComp.DetectableTags = new System.Collections.Generic.List<string> { "Obstacle", "Package", "Target" };
                Debug.Log("[CitySetup] Радар автоматически добавлен и настроен (7 лучей, 3 тега = 75 сигналов)!");
            }

            // Пытаемся найти компонент BehaviorParameters
            var bp = drone.GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            if (bp != null)
            {
                bp.BehaviorName = "DroneDelivery"; 
                // ВАЖНО: Возвращаем режим в Default для тренировки!
                bp.BehaviorType = Unity.MLAgents.Policies.BehaviorType.Default;
                bp.Model = null; // Убираем старую модель, чтобы он мог учиться
                Debug.Log("[CitySetup] Дрон переведен в режим Обучения (Default).");
            }
            
            // Клонируем дрона 15 раз для параллельного обучения
            GameObject clonesFolder = GameObject.Find("CityTrainingClones");
            if (clonesFolder != null) DestroyImmediate(clonesFolder);
            clonesFolder = new GameObject("CityTrainingClones");
            
            for (int i = 0; i < 15; i++)
            {
                GameObject clonedDrone = Instantiate(drone, clonesFolder.transform);
                clonedDrone.name = "DroneAgent_Clone_" + i;
                
                // Удаляем AudioListener у клонов, чтобы не спамить консоль
                var audioListener = clonedDrone.GetComponent<AudioListener>();
                if (audioListener != null) DestroyImmediate(audioListener);
                
                GameObject clonedPackage = Instantiate(package, clonesFolder.transform);
                clonedPackage.name = "Package_Clone_" + i;
                
                GameObject clonedTarget = Instantiate(target, clonesFolder.transform);
                clonedTarget.name = "Target_Clone_" + i;
                
                var clonedDfc = clonedDrone.GetComponent<DroneFlightController>();
                clonedDfc.targetTransform = clonedTarget.transform;
                var clonedPdc = clonedDrone.GetComponent<PackageDropController>();
                clonedPdc.SetPackageForTraining(clonedPackage);
                
                // Спавним каждого клона
                trainManager.RespawnAgent(clonedDfc, clonedPackage, clonedTarget.transform);
            }
            
            // Спавним и оригинального дрона тоже
            trainManager.RespawnAgent(dfc, package, target.transform);
        }
        else
        {
            Debug.LogWarning("[CitySetup] Не найден оригинальный DroneFlightController на сцене!");
        }

        Debug.Log("[CitySetup] Город готов к массовой тренировке!");
    }
}
