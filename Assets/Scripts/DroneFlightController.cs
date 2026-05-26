using UnityEngine;
using UnityEngine.InputSystem;
using Unity.MLAgents;
using Unity.MLAgents.Actuators;
using Unity.MLAgents.Sensors;

[RequireComponent(typeof(Rigidbody))]
public class DroneFlightController : Agent
{
    private Rigidbody rb;
    private Quaternion baseRotNoYaw;
    private Vector3 episodeStartPos;
    public float currentYawAngle;

    [Header("Настройки полета")]
    public float maxThrust = 200f;
    public float movementSpeed = 300f;
    public float rotationSpeed = 800f; 
    public float tiltAmount = 60f; 

    [Header("Стабилизация")]
    [Tooltip("Линейное торможение (drag) — чем выше, тем быстрее дрон останавливается")]
    public float linearDrag = 3.0f; // больше сопротивления, чтобы дрон мог затормозить над коробкой
    [Tooltip("Максимальная скорость дрона")]
    public float maxSpeed = 400f;
    [Tooltip("Сила стабилизации высоты при нулевом thrust")]
    public float hoverStabilization = 5f;

    [Header("Пропеллеры (Визуал)")]
    public Transform[] propellers;
    public float propellerSpeed = 2000f;
    public Vector3 propellerSpinAxis = new Vector3(0, 1, 0);

    [Header("ML-Agents Объекты")]
    public Transform targetTransform;
    public PackageDropController packageController;
    private Vector3 initialPosition;
    
    [Header("Доставка")]
    [Tooltip("Радиус зоны доставки")]
    public float deliveryRadius = 3f;

    // Внутреннее состояние наград
    private float previousDistanceToPackage;
    private float previousDistanceToTarget;
    private bool packagePickedUpThisEpisode;
    private bool waitingForDeliveryCheck;
    private float hoverTargetHeight;
    private float initialYaw;

    // Защита от мгновенной гибели при спавне
    private float episodeStartTime;
    private const float spawnGracePeriod = 1.0f; // Секунды неуязвимости после спавна

    private void Awake()
    {
        // Получаем Rigidbody как можно раньше — до первого FixedUpdate
        rb = GetComponent<Rigidbody>();
        
        // ЗАПРЕЩАЕМ дрону переворачиваться вверх ногами при столкновениях
        rb.constraints = RigidbodyConstraints.FreezeRotationX | RigidbodyConstraints.FreezeRotationZ;
    }

    public override void Initialize()
    {
        MaxStep = 3000; // Ограничиваем максимальное время жизни дрона (защита от бесконечного зависания)

        if (rb == null) rb = GetComponent<Rigidbody>();
        rb.constraints = RigidbodyConstraints.FreezeRotation;
        rb.linearDamping = linearDrag;

        if (GetComponent<Unity.MLAgents.DecisionRequester>() == null)
        {
            var dr = gameObject.AddComponent<Unity.MLAgents.DecisionRequester>();
            dr.DecisionPeriod = 5;
            dr.TakeActionsBetweenDecisions = true;
        }

        // Автоматически исправляем настройки ML-Agents, чтобы не возникало IndexOutOfRangeException
        var bp = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
        if (bp != null)
        {
            bp.BrainParameters.VectorObservationSize = 19;
            bp.BrainParameters.ActionSpec = new Unity.MLAgents.Actuators.ActionSpec(4, new[] { 2 });
        }
        
        initialPosition = transform.localPosition;
        rotationSpeed = 150f;
        tiltAmount = 30f;

        // Сохраняем начальный наклон модели (особенно важно для импортированных из блендера моделей)
        currentYawAngle = transform.eulerAngles.y;
        initialYaw = currentYawAngle;
        episodeStartPos = transform.position;

        baseRotNoYaw = Quaternion.Euler(transform.eulerAngles.x, 0, transform.eulerAngles.z);
        
        if (packageController == null)
            packageController = GetComponent<PackageDropController>();

        // Подписываемся на события посылки
        if (packageController != null)
        {
            packageController.OnPackagePickedUp += HandlePackagePickedUp;
            packageController.OnPackageDropped += HandlePackageDropped;
        }
    }

    private void OnDestroy()
    {
        // Отписываемся от событий при уничтожении
        if (packageController != null)
        {
            packageController.OnPackagePickedUp -= HandlePackagePickedUp;
            packageController.OnPackageDropped -= HandlePackageDropped;
        }
    }

    private void HandlePackagePickedUp()
    {
        if (!packagePickedUpThisEpisode)
        {
            AddReward(0.5f); // Награда за первый подбор посылки
            packagePickedUpThisEpisode = true;
        }
        waitingForDeliveryCheck = false;
    }

    private void HandlePackageDropped()
    {
        waitingForDeliveryCheck = true;
    }

    public override void OnEpisodeBegin()
    {
        // Сброс физики
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Если есть менеджер города — просим его переспавнить нас
        var cityManager = FindFirstObjectByType<CityTrainingManager>();
        if (cityManager != null)
        {
            GameObject pkg = packageController != null ? packageController.GetPackage() : null;
            cityManager.RespawnAgent(this, pkg, targetTransform);
        }
        else if (transform.parent != null && transform.parent.name.Contains("Arena"))
        {
            transform.localPosition = new Vector3(
                Random.Range(-5f, 5f),
                Random.Range(3f, 7f),
                Random.Range(-5f, 5f)
            );
            currentYawAngle = Random.Range(0f, 360f);
            transform.localRotation = Quaternion.Euler(0, currentYawAngle, 0) * baseRotNoYaw;
        }

        // Запоминаем точку старта для локальных координат нейросети
        episodeStartPos = transform.position;
        
        // Сбрасываем состояние наград
        packagePickedUpThisEpisode = false;
        waitingForDeliveryCheck = false;
        hoverTargetHeight = transform.localPosition.y;
        episodeStartTime = Time.time;

        // Сбрасываем коробку
        if (packageController != null)
        {
            packageController.ResetPackageForTraining(transform.parent);
        }

        // Рандомизируем цель
        if (targetTransform != null)
        {
            targetTransform.localPosition = new Vector3(Random.Range(-15f, 15f), 0.5f, Random.Range(-15f, 15f));
            previousDistanceToTarget = Vector3.Distance(transform.localPosition, targetTransform.localPosition);
        }

        // Запоминаем начальное расстояние до посылки
        if (packageController != null && packageController.GetPackage() != null)
        {
            previousDistanceToPackage = Vector3.Distance(transform.localPosition, packageController.GetPackage().transform.localPosition);
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // 1. Позиция относительно точки старта эпизода (заменяет localPosition)
        Vector3 localPos = transform.position - episodeStartPos;
        sensor.AddObservation(localPos);
        sensor.AddObservation(rb.linearVelocity);
        sensor.AddObservation(transform.up);       
        sensor.AddObservation(transform.forward);   

        // Позиция цели
        if (targetTransform != null)
        {
            Vector3 targetLocal = targetTransform.position - episodeStartPos;
            sensor.AddObservation(targetLocal);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
        }

        // Позиция посылки
        if (packageController != null && packageController.GetPackage() != null)
        {
            Vector3 pkgLocal = packageController.GetPackage().transform.position - episodeStartPos;
            sensor.AddObservation(pkgLocal);
            sensor.AddObservation(packageController.HasPackage() ? 1f : 0f);
        }
        else
        {
            sensor.AddObservation(Vector3.zero);
            sensor.AddObservation(0f);
        }
    }

    public override void OnActionReceived(ActionBuffers actions)
    {
        float moveX = actions.ContinuousActions[0];
        float moveZ = actions.ContinuousActions[1];
        float yawInput = actions.ContinuousActions[2];
        float thrustInput = actions.ContinuousActions[3];
        int dropInput = actions.DiscreteActions[0];

        // ================= ФИЗИКА ПОЛЕТА =================
        // (Базовая антигравитация применяется в FixedUpdate постоянно)

        // Дополнительная тяга от ввода игрока/агента
        float thrustForce = thrustInput * maxThrust;
        
        // Стабилизация высоты: если thrust ≈ 0, мягко удерживаем текущую высоту
        if (Mathf.Abs(thrustInput) < 0.1f)
        {
            float heightError = hoverTargetHeight - transform.localPosition.y;
            thrustForce += heightError * hoverStabilization;
        }
        else
        {
            // Обновляем целевую высоту при активном вводе
            hoverTargetHeight = transform.localPosition.y;
        }

        rb.AddForce(Vector3.up * thrustForce);

        // Вращение по yaw
        currentYawAngle += yawInput * rotationSpeed * Time.fixedDeltaTime;

        Quaternion yawRotation = Quaternion.Euler(0, currentYawAngle, 0);
        Vector3 forwardDir = yawRotation * Vector3.forward;
        Vector3 rightDir = yawRotation * Vector3.right;

        // Горизонтальное движение
        Vector3 moveDirection = (forwardDir * moveZ + rightDir * moveX) * movementSpeed;
        rb.AddForce(moveDirection);

        // Ограничение максимальной скорости
        if (rb.linearVelocity.magnitude > maxSpeed)
        {
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
        }

        // Визуальный наклон: по тангажу (moveZ) и по крену (зависит от ПОВОРОТА, а не от полета боком!)
        ApplyTilt(yawInput, moveZ);

        // Кнопка F (подбор/сброс)
        if (dropInput == 1 && packageController != null)
        {
            packageController.TryInteractPackage();
        }

        // ================= ПРОВЕРКА ДОСТАВКИ =================
        if (waitingForDeliveryCheck && targetTransform != null && packageController != null)
        {
            if (packageController.CheckDeliveryZone(targetTransform, deliveryRadius))
            {
                AddReward(1.0f); // Jackpot — успешная доставка!
                Debug.Log("🎉 Посылка доставлена!");
                EndEpisode();
                return;
            }
            else
            {
                AddReward(-0.2f); // Штраф: сбросил мимо цели
                Debug.Log("❌ Посылка сброшена мимо цели!");
            }
            waitingForDeliveryCheck = false;
        }

        // ================= ФАЗОВАЯ СИСТЕМА НАГРАД =================
        bool currentlyHasPackage = packageController != null && packageController.HasPackage();

        if (currentlyHasPackage && targetTransform != null)
        {
            // Фаза 2: Несём посылку → летим к цели
            float currentDistance = Vector3.Distance(transform.position, targetTransform.position);
            
            // АВТО-СБРОС: если мы над целью (в пределах радиуса доставки), сбрасываем посылку
            if (currentDistance <= deliveryRadius)
            {
                packageController.TryInteractPackage();
            }

            if (currentDistance < previousDistanceToTarget)
            {
                // Вычисляем, куда смотрит дрон
                Vector3 dirToTarget = (targetTransform.position - transform.position).normalized;
                float faceDot = Vector3.Dot(transform.forward, dirToTarget);
                
                // Награждаем за движение + даем бонус, если он СМОТРИТ на цель
                AddReward(0.01f + (faceDot * 0.005f)); 
            }
            else
            {
                AddReward(-0.01f);  // Удаляемся от цели
            }
                
            previousDistanceToTarget = currentDistance;
        }
        else if (!currentlyHasPackage && !packagePickedUpThisEpisode && packageController != null && packageController.GetPackage() != null)
        {
            // Фаза 1: Нет посылки → летим к посылке
            float currentDistToPkg = Vector3.Distance(transform.position, packageController.GetPackage().transform.position);
            
            // АВТО-ПОДБОР: уменьшаем чит-радиус до 3 метров, чтобы он реально подлетал к ней
            if (currentDistToPkg <= 3.0f)
            {
                packageController.TryInteractPackage();
            }
            
            if (currentDistToPkg < previousDistanceToPackage)
            {
                Vector3 dirToPkg = (packageController.GetPackage().transform.position - transform.position).normalized;
                float faceDot = Vector3.Dot(transform.forward, dirToPkg);
                
                AddReward(0.005f + (faceDot * 0.002f));
            }
            else
            {
                AddReward(-0.005f);  // Удаляемся от посылки
            }
                
            previousDistanceToPackage = currentDistToPkg;
        }

        // ЭСТЕТИЧЕСКИЙ ШТРАФ: штрафуем за полет боком или задом наперед
        if (rb.linearVelocity.magnitude > 2.0f)
        {
            float moveFaceDot = Vector3.Dot(transform.forward, rb.linearVelocity.normalized);
            // Если летит прямо (Dot = 1), штрафа нет. Если боком (Dot = 0) или задом (Dot = -1) - штрафуем.
            if (moveFaceDot < 0.8f) 
            {
                AddReward(-0.001f); 
            }
        }

        // Штраф за слишком сильный наклон (чтобы не кувыркался)
        float uprightDot = Vector3.Dot(transform.up, Vector3.up);
        if (uprightDot < 0.5f) // Больше 60 градусов крен
        {
            AddReward(-0.1f);
        }

        // Штраф за время (чтобы торопился)
        AddReward(-0.0005f);
    }

    public override void Heuristic(in ActionBuffers actionsOut)
    {
        var keyboard = Keyboard.current;
        if (keyboard == null) return;

        var continuousActions = actionsOut.ContinuousActions;
        var discreteActions = actionsOut.DiscreteActions;

        // Инвертированный Thrust (вверх / вниз)
        continuousActions[3] = 0f;
        if (keyboard.spaceKey.isPressed) continuousActions[3] = 1f; // Space снова вверх
        else if (keyboard.leftShiftKey.isPressed) continuousActions[3] = -1f; // Shift снова вниз

        // Нормальный Yaw (поворот Q / E)
        continuousActions[2] = 0f;
        if (keyboard.eKey.isPressed) continuousActions[2] = 1f; // E вправо
        else if (keyboard.qKey.isPressed) continuousActions[2] = -1f; // Q влево

        // Инвертированный Strafe (влево-вправо: A / D)
        continuousActions[0] = 0f;
        if (keyboard.dKey.isPressed) continuousActions[0] = -1f; // D влево
        if (keyboard.aKey.isPressed) continuousActions[0] = 1f;  // A вправо

        // Инвертированный Forward/Back (W / S)
        continuousActions[1] = 0f;
        if (keyboard.wKey.isPressed) continuousActions[1] = -1f; // Теперь W назад
        if (keyboard.sKey.isPressed) continuousActions[1] = 1f;  // Теперь S вперед

        // Подбор/сброс посылки (F) остается без изменений
        discreteActions[0] = keyboard.fKey.isPressed ? 1 : 0;
    }

    private void ApplyTilt(float moveX, float moveZ)
    {
        float targetPitch = moveZ * tiltAmount;
        float targetRoll = -moveX * tiltAmount;
        
        Quaternion yawRot = Quaternion.Euler(0, currentYawAngle, 0);
        Quaternion localTilt = Quaternion.Euler(targetPitch, 0, targetRoll);
        Quaternion targetRotation = yawRot * localTilt * baseRotNoYaw;

        rb.MoveRotation(Quaternion.Slerp(rb.rotation, targetRotation, Time.fixedDeltaTime * 10f));
    }

    private void OnCollisionEnter(Collision collision)
    {
        // Grace period — игнорируем коллизии первую секунду после спавна
        if (Time.time - episodeStartTime < spawnGracePeriod)
            return;

        // Безопасная проверка
        string objName = collision.gameObject.name.ToLower();
        bool isPackage = objName.Contains("package") || objName.Contains("box") || objName.Contains("container");
        bool isTarget = objName.Contains("target");

        // Авария! (если ударились не о посылку и не о цель)
        if (!isPackage && !isTarget)
        {
            AddReward(-1.0f);
            
            // В ручном режиме просто отскакиваем от стен, не убиваем дрона
            var bp = GetComponent<Unity.MLAgents.Policies.BehaviorParameters>();
            bool isManual = bp != null && bp.BehaviorType == Unity.MLAgents.Policies.BehaviorType.HeuristicOnly;
            
            if (!isManual)
            {
                EndEpisode();
            }
        }
    }

    /// <summary>
    /// Постоянная компенсация гравитации — работает даже между решениями агента.
    /// Без этого дрон падает в паузах между DecisionRequester тиками.
    /// </summary>
    private void FixedUpdate()
    {
        if (rb == null) return;
        // Базовая антигравитация — всегда держим дрон в воздухе
        float gravityCompensation = Mathf.Abs(Physics.gravity.y) * rb.mass;
        rb.AddForce(Vector3.up * gravityCompensation);
    }

    private void Update()
    {
        if (propellers == null || propellers.Length == 0) return;
        float thrustMultiplier = 1f;
        if (Keyboard.current != null && Keyboard.current.spaceKey.isPressed)
            thrustMultiplier = 2f;
        float rotationStep = propellerSpeed * thrustMultiplier * Time.deltaTime;
        foreach (Transform prop in propellers)
        {
            if (prop != null) prop.Rotate(propellerSpinAxis * rotationStep, Space.Self);
        }
    }
}
