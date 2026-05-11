using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody))]
public class DroneFlightController : MonoBehaviour
{
    private Rigidbody rb;
    private Vector3 baseRotation;
    private float currentYawAngle;

    [Header("Настройки полета")]
    [Tooltip("Сила подъема (вверх)")]
    public float maxThrust = 15f;
    [Tooltip("Скорость полета (вперед/назад/вбок)")]
    public float movementSpeed = 10f;
    [Tooltip("Скорость поворота (рысканье)")]
    public float rotationSpeed = 100f; // Увеличили, так как теперь задаем градусы в секунду напрямую
    [Tooltip("Максимальный угол наклона при полете (градусы)")]
    public float tiltAmount = 20f; 

    [Header("Пропеллеры (Визуал)")]
    [Tooltip("Перетащите сюда 4 цилиндра/винта из иерархии дрона")]
    public Transform[] propellers;
    [Tooltip("Базовая скорость вращения винтов")]
    public float propellerSpeed = 2000f;
    [Tooltip("Ось, вокруг которой будут крутиться винты (обычно Y или Z)")]
    public Vector3 propellerSpinAxis = new Vector3(0, 1, 0);

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
        // Полностью блокируем физическое вращение от столкновений! 
        // Мы будем крутить его математически точно, чтобы он никогда не закрутился волчком.
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        
        baseRotation = transform.localEulerAngles;
        currentYawAngle = transform.eulerAngles.y;
    }

    private void FixedUpdate()
    {
        HandleFlightPhysics();
    }

    private void Update()
    {
        HandlePropellerSpin();
    }

    private void HandlePropellerSpin()
    {
        if (propellers == null || propellers.Length == 0) return;

        // Если нажат пробел (взлет) - крутим винты в 2 раза быстрее для эффектности
        float thrustMultiplier = 1f;
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            thrustMultiplier = 2f;

        float rotationStep = propellerSpeed * thrustMultiplier * Time.deltaTime;

        foreach (Transform prop in propellers)
        {
            if (prop != null)
            {
                // Крутим винты относительно их собственной локальной оси
                prop.Rotate(propellerSpinAxis * rotationStep, Space.Self);
            }
        }
    }

    private void HandleFlightPhysics()
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return; 

        // 1. Управление высотой (Тяга)
        float thrustInput = 0f;
        if (keyboard.spaceKey.isPressed) thrustInput = 1f;
        else if (keyboard.leftShiftKey.isPressed) thrustInput = -1f;

        Vector3 upwardForce = Vector3.up * ((Mathf.Abs(Physics.gravity.y) * rb.mass) + (thrustInput * maxThrust));
        rb.AddForce(upwardForce);

        // 2. Вращение влево/вправо (Q / E) - теперь мы сами жестко задаем угол
        float yawInput = 0f;
        if (keyboard.eKey.isPressed) yawInput = 1f;
        else if (keyboard.qKey.isPressed) yawInput = -1f;

        currentYawAngle += yawInput * rotationSpeed * Time.fixedDeltaTime;

        // 3. Управление движением (WASD)
        float moveX = 0f;
        // Модель со Sketchfab смотрит "задом наперед", поэтому мы инвертируем вообще всё (WASD)
        if (keyboard.dKey.isPressed) moveX -= 1f;
        if (keyboard.aKey.isPressed) moveX += 1f;

        float moveZ = 0f;
        if (keyboard.wKey.isPressed) moveZ -= 1f; 
        if (keyboard.sKey.isPressed) moveZ += 1f;

        // Движение рассчитываем строго горизонтально, игнорируя наклон носа дрона
        Quaternion yawRotation = Quaternion.Euler(0, currentYawAngle, 0);
        Vector3 forwardDir = yawRotation * Vector3.forward;
        Vector3 rightDir = yawRotation * Vector3.right;

        Vector3 moveDirection = (forwardDir * moveZ + rightDir * moveX) * movementSpeed;
        rb.AddForce(moveDirection);

        // 4. Визуальный наклон при полете
        ApplyTilt(moveX, moveZ);
    }

    private void ApplyTilt(float moveX, float moveZ)
    {
        float targetPitch = moveZ * tiltAmount;
        float targetRoll = -moveX * tiltAmount;
        Quaternion localTilt = Quaternion.Euler(targetPitch, 0, targetRoll);

        Quaternion baseRot = Quaternion.Euler(baseRotation.x, 0, baseRotation.z);
        Quaternion yawRot = Quaternion.Euler(0, currentYawAngle, 0);

        // Математически идеальное вращение без петель и спина
        Quaternion targetRotation = yawRot * baseRot * localTilt;

        // Применяем вращение прямо на физическое тело
        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 5f));
    }
}
