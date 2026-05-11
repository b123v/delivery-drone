using UnityEngine;
using UnityEngine.InputSystem;

public class PackageDropController : MonoBehaviour
{
    [Header("Настройки сброса")]
    [Tooltip("Объект посылки (коробки), который нужно сбросить")]
    public GameObject packageObject;
    
    [Tooltip("Масса коробки после сброса")]
    public float packageMass = 0.5f;

    private bool isDropped = false;

    private void Update()
    {
        var keyboard = Keyboard.current;
        // Сброс по кнопке F
        if (keyboard != null && keyboard.fKey.wasPressedThisFrame && !isDropped)
        {
            DropPackage();
        }
    }

    private void DropPackage()
    {
        if (packageObject == null) 
        {
            Debug.LogWarning("Объект посылки не назначен в скрипте!");
            return;
        }

        isDropped = true;

        // 1. Отсоединяем коробку от дрона
        packageObject.transform.SetParent(null);

        // 2. Добавляем физическое тело (Rigidbody)
        Rigidbody packageRb = packageObject.GetComponent<Rigidbody>();
        if (packageRb == null)
        {
            packageRb = packageObject.AddComponent<Rigidbody>();
        }

        // Настраиваем массу
        packageRb.mass = packageMass;

        // 3. ОТКЛЮЧАЕМ СТОЛКНОВЕНИЯ между дроном и посылкой!
        // Иначе в момент сброса коллайдеры могут пересечься, и дрон отлетит в сторону.
        Collider[] droneColliders = GetComponentsInChildren<Collider>();
        Collider[] packageColliders = packageObject.GetComponentsInChildren<Collider>();

        foreach (Collider dCol in droneColliders)
        {
            foreach (Collider pCol in packageColliders)
            {
                Physics.IgnoreCollision(dCol, pCol);
            }
        }

        Debug.Log("📦 Посылка успешно сброшена!");
    }
}
