// Dice.cs
// Скрипт, определяющий поведение кубиков (и сами кубики тоже определяет)
using UnityEngine;
using System.Threading.Tasks;

public class Dice : MonoBehaviour
{
    [Header("Настройки определения результата")]
    [Tooltip("Порог скорости для считания кубика остановившимся")]
    public float settleThreshold = 0.05f;
    [Tooltip("Время стабильности перед определением результата (сек)")]
    public float settleTime = 0.5f;

    [Header("Ссылки на грани (опционально для отладки)")]
    public Transform[] faceMarkers; 

    [Header("Отладка")]
    public bool debugMode = false;

    // Компоненты
    private Rigidbody rb;
    private Renderer diceRenderer;

    // Состояние
    private bool isSettled = false;
    private float settleTimer = 0f;
    private int result = 0;

    // Нормали граней в локальном пространстве
    // Стандартная развёртка Unity Cube
    // 0: +X (право), 1: -X (лево), 2: +Y (верх), 3: -Y (низ), 4: +Z (перед), 5: -Z (зад)
    // Если кто-то это читает, простите мне этот позор, очень тяжело гранями
    private Vector3[] faceNormals = new Vector3[]
    {
        Vector3.right,   // 0: +X
        Vector3.left,    // 1: -X
        Vector3.up,      // 2: +Y
        Vector3.down,    // 3: -Y
        Vector3.forward, // 4: +Z
        Vector3.back     // 5: -Z
    };

    private int[] faceValues = new int[]
    {
        3, // +X
        4, // -X
        1, // +Y
        6, // -Y
        2, // +Z
        5  // -Z
    };

    public int Result => result;
    public bool IsReady => isSettled && result > 0;
    public bool IsRolling => !isSettled;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        diceRenderer = GetComponent<Renderer>();
        
        if (rb == null)
        {
            Debug.LogError("Dice: Rigidbody не найден! Добавьте Rigidbody на кубик.");
            enabled = false;
            return;
        }

        // Базовые настройки физики
        rb.mass = 1f;
        rb.linearDamping = 0.5f;
        rb.angularDamping = 0.5f;
        rb.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private void Start()
    {
        ResetDice();
    }

    // Бросает кубик с заданной силой и вращением
    public void Throw(Vector3 force, Vector3 torque)
    {
        if (rb == null) return;

        // Сброс состояния
        isSettled = false;
        result = 0;
        settleTimer = 0f;

        // Сброс скоростей
        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        // Применяем силу и вращение
        rb.AddForce(force, ForceMode.Impulse);
        rb.AddTorque(torque, ForceMode.Impulse);

        if (debugMode)
            Debug.Log($"Dice: Бросок! Force={force}, Torque={torque}");
    }

    // Полный сброс кубика в исходное состояние
    public void ResetDice()
    {
        isSettled = false;
        result = 0;
        settleTimer = 0f;

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }
        
        transform.position = Vector3.zero;
        transform.rotation = Quaternion.identity;
    }

    // тест! Принудительно устанавливает результат
    public void ForceResult(int value)
    {
        result = Mathf.Clamp(value, 1, 6);
        isSettled = true;
        if (debugMode)
            Debug.Log($"Dice: Принудительный результат = {result}");
    }

    private void Update()
    {
        if (isSettled || rb == null) return;

        // проверка на остановку куба
        bool isSlow = rb.linearVelocity.magnitude < settleThreshold &&
                      rb.angularVelocity.magnitude < settleThreshold;

        if (isSlow)
        {
            settleTimer += Time.deltaTime;
            if (settleTimer >= settleTime)
            {
                DetermineResult();
                isSettled = true;
                if (debugMode)
                    Debug.Log($"Dice: Результат определён = {result}");
            }
        }
        else
        {
            settleTimer = 0f;
        }
    }

    // Определяет, какая грань смотрит вверх
    private void DetermineResult()
    {
        Vector3 up = transform.up;
        float maxDot = -1f;
        int bestFaceIndex = 0;

        for (int i = 0; i < faceNormals.Length; i++)
        {
            Vector3 worldNormal = transform.TransformDirection(faceNormals[i]);
            float dot = Vector3.Dot(up, worldNormal);
            
            if (dot > maxDot)
            {
                maxDot = dot;
                bestFaceIndex = i;
            }
        }

        result = faceValues[bestFaceIndex];

        if (debugMode)
        {
            Debug.Log($"Dice: Грань {bestFaceIndex} смотрит вверх (dot={maxDot:F2})");
            Debug.Log($"Dice: Значение грани = {result}");
        }
    }

    // Асинхронное ожидание результата броска
    public async Task<int> WaitForResultAsync()
    {
        while (!IsReady)
        {
            await Task.Yield();
        }
        return result;
    }

    #region Debug
    private void OnDrawGizmos()
    {
        if (!debugMode) return;

        Color[] colors = new Color[] { Color.red, Color.red, Color.green, Color.green, Color.blue, Color.blue };
        
        for (int i = 0; i < faceNormals.Length; i++)
        {
            Gizmos.color = colors[i];
            Vector3 worldNormal = transform.TransformDirection(faceNormals[i]);
            Gizmos.DrawRay(transform.position, worldNormal * 0.5f);
        }

        if (isSettled)
        {
            Gizmos.color = Color.yellow;
            Gizmos.DrawSphere(transform.position + Vector3.up * 0.7f, 0.1f);
        }
    }

    private void OnGUI()
    {
        if (!debugMode) return;
        
        GUILayout.BeginArea(new Rect(10, 10, 300, 200));
        GUILayout.Label($"Dice Result: {result}");
        GUILayout.Label($"Is Ready: {isSettled}");
        GUILayout.Label($"Velocity: {rb?.linearVelocity.magnitude:F2}");
        GUILayout.Label($"Angular Velocity: {rb?.angularVelocity.magnitude:F2}");
        GUILayout.Label($"Settle Timer: {settleTimer:F2}");
        GUILayout.EndArea();
    }
    #endregion

    // Настройка под текстуру
    
    public void ConfigureForAtlas2x3()
    {
        faceValues = new int[] { 3, 4, 1, 6, 2, 5 };
        Debug.Log("Dice: Настроено для атласа 2×3");
    }

    public void ConfigureForAtlas3x2()
    {
        faceValues = new int[] { 3, 4, 1, 6, 2, 5 };
        Debug.Log("Dice: Настроено для атласа 3×2");
    }

    public void CalibrateFace(int faceIndex, int value)
    {
        if (faceIndex >= 0 && faceIndex < faceValues.Length)
        {
            faceValues[faceIndex] = value;
            Debug.Log($"Dice: Грань {faceIndex} теперь = {value}");
        }
    }
}