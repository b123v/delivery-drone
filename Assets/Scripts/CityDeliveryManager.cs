using UnityEngine;
using System.Collections.Generic;

public class CityDeliveryManager : MonoBehaviour
{
    public DroneFlightController drone;
    public Transform deliveryBase;

    private List<GameObject> allPackages = new List<GameObject>();
    private PackageDropController dropController;

    void Start()
    {
        if (drone == null)
            drone = FindFirstObjectByType<DroneFlightController>();

        if (drone != null)
        {
            dropController = drone.GetComponent<PackageDropController>();
            if (dropController != null)
            {
                dropController.OnPackagePickedUp += HandlePackagePickedUp;
                dropController.OnPackageDropped += HandlePackageDropped;
            }
        }

        // Ищем все объекты с тегом Package на старте
        FindAllPackages();
        AssignNearestPackage();
    }

    public void FindAllPackages()
    {
        allPackages.Clear();
        GameObject[] pkgs = GameObject.FindGameObjectsWithTag("Package");
        foreach (var p in pkgs)
        {
            // Проверяем, не прикреплена ли уже эта посылка к кому-то
            if (p.transform.parent == null || !p.transform.parent.name.Contains("Drone"))
            {
                allPackages.Add(p);
            }
        }
        Debug.Log($"[CityDeliveryManager] Найдено {allPackages.Count} посылок в городе!");
    }

    public void AssignNearestPackage()
    {
        if (drone == null || dropController == null || allPackages.Count == 0) return;

        GameObject nearest = null;
        float minDist = float.MaxValue;

        foreach (var p in allPackages)
        {
            if (p != null)
            {
                float d = Vector3.Distance(drone.transform.position, p.transform.position);
                if (d < minDist)
                {
                    minDist = d;
                    nearest = p;
                }
            }
        }

        if (nearest != null)
        {
            dropController.SetPackageForTraining(nearest);
            // Пока мы летим за посылкой, цель для компаса - сама посылка
            drone.targetTransform = nearest.transform;
            Debug.Log($"[CityDeliveryManager] Дрон отправлен за ближайшей посылкой: {nearest.name}");
        }
    }

    private void HandlePackagePickedUp()
    {
        // Как только подобрал — цель меняется на зеленую базу
        if (drone != null && deliveryBase != null)
        {
            drone.targetTransform = deliveryBase;
            Debug.Log("[CityDeliveryManager] Посылка взята! Дрон летит на базу.");
        }
    }

    private void HandlePackageDropped()
    {
        // После доставки убираем посылку из списка и ищем новую
        if (dropController != null && dropController.GetPackage() != null)
        {
            allPackages.Remove(dropController.GetPackage());
            // Уничтожаем доставленную посылку через 2 секунды
            Destroy(dropController.GetPackage(), 2f);
        }

        // Выбираем следующую
        Invoke(nameof(AssignNearestPackage), 0.5f);
    }
}
