using UnityEngine;

public class CityTrainingManager : MonoBehaviour
{
    [Header("Настройки зоны города")]
    public float minX = -100f;
    public float maxX = 100f;
    public float minZ = -100f;
    public float maxZ = 100f;
    public float raycastHeight = 100f;

    public void RespawnAgent(DroneFlightController drone, GameObject package, Transform target)
    {
        // 1. Спавним дрона
        Vector3 dronePos = GetRandomValidPosition();
        drone.transform.position = dronePos;
        drone.transform.rotation = Quaternion.Euler(drone.transform.eulerAngles.x, Random.Range(0, 360), drone.transform.eulerAngles.z);
        
        // Сброс физики (на всякий случай)
        var rb = drone.GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        // 2. Спавним посылку недалеко от дрона (в радиусе 10-30 метров)
        Vector3 pkgPos = GetRandomValidPositionNear(dronePos, 10f, 30f);
        if (package != null)
        {
            package.transform.position = pkgPos;
            package.SetActive(true);
            var pkgRb = package.GetComponent<Rigidbody>();
            if (pkgRb != null)
            {
                pkgRb.linearVelocity = Vector3.zero;
                pkgRb.angularVelocity = Vector3.zero;
            }
        }

        // 3. Спавним цель далеко от дрона
        Vector3 targetPos = GetRandomValidPositionNear(dronePos, 30f, 80f);
        if (target != null)
        {
            target.position = targetPos;
        }
    }

    private Vector3 GetRandomValidPosition()
    {
        for (int i = 0; i < 30; i++) // Пытаемся 30 раз найти хорошее место
        {
            float rx = Random.Range(minX, maxX);
            float rz = Random.Range(minZ, maxZ);
            Vector3 rayStart = new Vector3(rx, raycastHeight, rz);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f))
            {
                // Ищем относительно плоскую поверхность (дорога, крыша, земля)
                if (hit.normal.y > 0.8f && !hit.collider.name.Contains("Drone") && !hit.collider.name.Contains("Package") && !hit.collider.name.Contains("Target"))
                {
                    return hit.point + Vector3.up * 2.0f; // Чуть выше земли
                }
            }
        }
        return new Vector3(0, 5, 0); // Фолбэк
    }

    private Vector3 GetRandomValidPositionNear(Vector3 center, float minRadius, float maxRadius)
    {
        for (int i = 0; i < 30; i++)
        {
            Vector2 randomCircle = Random.insideUnitCircle.normalized * Random.Range(minRadius, maxRadius);
            float rx = center.x + randomCircle.x;
            float rz = center.z + randomCircle.y;
            Vector3 rayStart = new Vector3(rx, raycastHeight, rz);

            if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 200f))
            {
                if (hit.normal.y > 0.8f && !hit.collider.name.Contains("Drone") && !hit.collider.name.Contains("Package"))
                {
                    return hit.point + Vector3.up * 1.5f;
                }
            }
        }
        return center + new Vector3(minRadius, 5, minRadius); // Фолбэк
    }
}
