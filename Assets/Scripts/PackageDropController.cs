using UnityEngine;
using UnityEngine.InputSystem;
using Action = System.Action;

public class PackageDropController : MonoBehaviour
{
    [Header("Настройки магнита")]
    [Tooltip("Радиус под которым дрон может захватить посылку (ВРЕМЕННО УВЕЛИЧЕН ДЛЯ ОБУЧЕНИЯ)")]
    public float pickupRadius = 15.0f;
    [Tooltip("Тег для посылок")]
    public string packageTag = "Package";

    [Header("Cooldown")]
    [Tooltip("Задержка между подбором/сбросом (секунды)")]
    public float interactCooldown = 0.5f;

    private GameObject currentPackage;
    private Rigidbody droneRb;
    private bool hasPackage = false;
    private float originalPackageMass = 0f;
    private float lastInteractTime = -10f;

    // Для тренировки ML-Agents
    private Vector3 initialPackagePosition;
    private Quaternion initialPackageRotation;
    private GameObject lastKnownPackage;

    // События для DroneFlightController
    public event Action OnPackagePickedUp;
    public event Action OnPackageDropped;

    private void Start()
    {
        // Возвращаем адекватный радиус
        pickupRadius = 3.0f; 
        
        droneRb = GetComponent<Rigidbody>();
        
        // Если посылка еще не назначена генератором арен, ищем её на сцене
        if (lastKnownPackage == null)
        {
            GameObject pkg = null;
            try
            {
                pkg = GameObject.FindGameObjectWithTag(packageTag);
            }
            catch (UnityException)
            {
                Debug.LogWarning($"Тег '{packageTag}' не определен в проекте. Ищем по имени 'Package'.");
            }
            
            if (pkg == null) pkg = GameObject.Find("Package");
            if (pkg != null)
            {
                SetPackageForTraining(pkg);
            }
        }
    }

    public void SetPackageForTraining(GameObject pkg)
    {
        lastKnownPackage = pkg;
        initialPackagePosition = pkg.transform.localPosition;
        initialPackageRotation = pkg.transform.localRotation;
    }

    private void Update()
    {
        // Убрали принудительное выравнивание в Quaternion.identity, чтобы не ломать 3D-модель посылки
    }

    public void TryInteractPackage()
    {
        // Cooldown — защита от спама
        if (Time.time - lastInteractTime < interactCooldown)
            return;

        lastInteractTime = Time.time;

        if (hasPackage)
        {
            DropPackage();
        }
        else
        {
            TryPickupPackage();
        }
    }

    private void TryPickupPackage()
    {
        Collider[] hitColliders = Physics.OverlapSphere(transform.position, pickupRadius);
        foreach (var hitCollider in hitColliders)
        {
            string objName = hitCollider.gameObject.name.ToLower();

            if (objName.Contains("package") || objName.Contains("box") || objName.Contains("container"))
            {
                currentPackage = hitCollider.gameObject;
                lastKnownPackage = currentPackage;
                hasPackage = true;

                Rigidbody pkgRb = currentPackage.GetComponent<Rigidbody>();
                if (pkgRb != null)
                {
                    originalPackageMass = pkgRb.mass;
                    droneRb.mass += originalPackageMass;
                    // Обнуляем скорость до включения isKinematic, чтобы не было варнингов в Unity 6+
                    pkgRb.linearVelocity = Vector3.zero;
                    pkgRb.angularVelocity = Vector3.zero;
                    pkgRb.isKinematic = true;
                }

                currentPackage.transform.SetParent(transform, false); // false = координаты теперь локальные от дрона
                currentPackage.transform.localPosition = new Vector3(0, -1.0f, 0); // Ровно под дроном
                currentPackage.transform.localRotation = initialPackageRotation; // Исходный поворот коробки
                
                Collider droneCollider = GetComponent<Collider>();
                Collider pkgCollider = currentPackage.GetComponent<Collider>();
                if (droneCollider != null && pkgCollider != null)
                    Physics.IgnoreCollision(droneCollider, pkgCollider, true);

                Debug.Log("Посылка примагничена Agent-ом!");

                // Событие подбора
                OnPackagePickedUp?.Invoke();
                break;
            }
        }
    }

    private void DropPackage()
    {
        if (currentPackage != null)
        {
            currentPackage.transform.SetParent(null);

            Rigidbody pkgRb = currentPackage.GetComponent<Rigidbody>();
            if (pkgRb != null)
            {
                // Возвращаем физику — отключаем isKinematic
                pkgRb.isKinematic = false;
                pkgRb.mass = originalPackageMass;
            }
            else
            {
                // Если Rigidbody каким-то образом отсутствует — добавляем
                pkgRb = currentPackage.AddComponent<Rigidbody>();
                pkgRb.mass = originalPackageMass;
            }

            droneRb.mass = Mathf.Max(1f, droneRb.mass - originalPackageMass);
            
            Collider droneCollider = GetComponent<Collider>();
            Collider pkgCollider = currentPackage.GetComponent<Collider>();
            if (droneCollider != null && pkgCollider != null)
                Physics.IgnoreCollision(droneCollider, pkgCollider, false);

            currentPackage = null;
            hasPackage = false;
            Debug.Log("Посылка сброшена Agent-ом!");

            // Событие сброса
            OnPackageDropped?.Invoke();
        }
    }

    /// <summary>
    /// Проверяет, находится ли сброшенная посылка в зоне доставки.
    /// Вызывать после DropPackage().
    /// </summary>
    public bool CheckDeliveryZone(Transform targetZone, float deliveryRadius)
    {
        if (lastKnownPackage == null || targetZone == null)
            return false;

        float distance = Vector3.Distance(
            new Vector3(lastKnownPackage.transform.position.x, 0, lastKnownPackage.transform.position.z),
            new Vector3(targetZone.position.x, 0, targetZone.position.z)
        );

        return distance < deliveryRadius;
    }

    public bool HasPackage() { return hasPackage; }
    public GameObject GetPackage() { return lastKnownPackage; }

    public void ResetPackageForTraining(Transform arenaParent)
    {
        // Сначала сбрасываем посылку из рук дрона
        if (hasPackage && currentPackage != null)
        {
            currentPackage.transform.SetParent(null);
            Rigidbody pkgRb = currentPackage.GetComponent<Rigidbody>();
            if (pkgRb != null)
            {
                pkgRb.isKinematic = false;
            }

            Collider droneCollider = GetComponent<Collider>();
            Collider pkgCollider = currentPackage.GetComponent<Collider>();
            if (droneCollider != null && pkgCollider != null)
                Physics.IgnoreCollision(droneCollider, pkgCollider, false);

            droneRb.mass = Mathf.Max(1f, droneRb.mass - originalPackageMass);
            currentPackage = null;
            hasPackage = false;
        }
        
        if (lastKnownPackage != null)
        {
            Rigidbody pkgRb = lastKnownPackage.GetComponent<Rigidbody>();
            if (pkgRb != null)
            {
                pkgRb.isKinematic = false;
                pkgRb.linearVelocity = Vector3.zero;
                pkgRb.angularVelocity = Vector3.zero;
            }
            lastKnownPackage.transform.SetParent(arenaParent);
            lastKnownPackage.transform.localPosition = initialPackagePosition + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-2f, 2f));
            lastKnownPackage.transform.localRotation = initialPackageRotation;
        }

        lastInteractTime = -10f; // Сбрасываем cooldown
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, pickupRadius);
    }
}
