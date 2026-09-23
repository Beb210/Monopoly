// DiceRoller.cs
// Бросаем кубики
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;

public class DiceRoller : MonoBehaviour
{
    [Header("Префабы кубиков")]
    public GameObject dicePrefab;

    [Header("Зона броска")]
    public Transform diceRollArea;

    [Header("UI для отображения")]
    public TMP_Text diceResultText;

    [Header("Настройки физики")]
    public float throwForce = 5f;
    public float throwTorque = 3f;
    public float settleThreshold = 0.1f;
    public float settleDuration = 0.3f;

    [Header("Настройки кубика")]
    public float diceSize = 0.5f;

    public event System.Action<int, int> OnDiceRolled;

    private GameObject[] diceObjects = new GameObject[2];
    private Rigidbody[] diceRigidbodies = new Rigidbody[2];
    private bool[] diceSettled = new bool[2];
    private float[] settleTimers = new float[2];

    public bool isRolling = false;

    void Start()
    {
        // CreateDice(); // !!! мб потом пригодится в других сценах (инициализация в 1 броске)
    }

    // Невидимые стены чтобы кубики не улетели за пределы доски
    void CreateDiceBoundaries()
    {
        float areaSize = 1f;
        float wallHeight = 10f;
        float wallThickness = 0.5f;

        string[] wallNames = { "Wall_Front", "Wall_Back", "Wall_Left", "Wall_Right" };
        Vector3[] positions = {
            new Vector3(0, wallHeight/2, -areaSize/2 - wallThickness/2),
            new Vector3(0, wallHeight/2, areaSize/2 + wallThickness/2),
            new Vector3(-areaSize/2 - wallThickness/2, wallHeight/2, 0),
            new Vector3(areaSize/2 + wallThickness/2, wallHeight/2, 0)
        };
        Quaternion[] rotations = {
            Quaternion.identity,
            Quaternion.identity,
            Quaternion.Euler(0, 90, 0),
            Quaternion.Euler(0, 90, 0)
        };
        Vector3[] scales = {
            new Vector3(areaSize + wallThickness*2, wallHeight, wallThickness),
            new Vector3(areaSize + wallThickness*2, wallHeight, wallThickness),
            new Vector3(wallThickness, wallHeight, areaSize),
            new Vector3(wallThickness, wallHeight, areaSize)
        };

        for (int i = 0; i < 4; i++)
        {
            GameObject wall = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wall.name = wallNames[i];
            wall.transform.position = diceRollArea.position + positions[i];
            wall.transform.rotation = rotations[i];
            wall.transform.localScale = scales[i];
            wall.transform.SetParent(diceRollArea);
        }
    }

    void CreateDice()
    {
        // !!! скейл у diceRollArea должен быть (1,1,1), иначе физика кубов ломается !!!
        if (diceRollArea != null)
        {
            diceRollArea.localScale = Vector3.one;
        }

        if (diceObjects[0] != null) return;

        for (int i = 0; i < 2; i++)
        {
            if (dicePrefab != null)
            {
                diceObjects[i] = Instantiate(dicePrefab, diceRollArea.position, Quaternion.identity);
            }
            else
            {
                diceObjects[i] = GameObject.CreatePrimitive(PrimitiveType.Cube);
                diceObjects[i].name = $"Dice_{i}";
            }

            // Сброс всех трансформов
            diceObjects[i].transform.position = diceRollArea.position + new Vector3(-0.5f + i * 1f, 3f, 0);
            diceObjects[i].transform.rotation = Quaternion.identity;
            diceObjects[i].transform.localScale = Vector3.one * diceSize;

            // Настройка Rigidbody
            Rigidbody rb = diceObjects[i].GetComponent<Rigidbody>();
            if (rb == null) rb = diceObjects[i].AddComponent<Rigidbody>();
            
            rb.mass = 1f;
            rb.linearDamping = 0.5f;
            rb.angularDamping = 0.5f;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.isKinematic = false;
            rb.useGravity = true;

            // Настройка коллайдера
            BoxCollider collider = diceObjects[i].GetComponent<BoxCollider>();
            if (collider == null) collider = diceObjects[i].AddComponent<BoxCollider>();
            collider.size = Vector3.one;

            // Заглушка для материала
            Renderer rend = diceObjects[i].GetComponent<Renderer>();
            if (rend != null) rend.material.color = Color.white;

            diceRigidbodies[i] = rb;
            
            // Родитель сломает скейлы :(
            diceObjects[i].transform.SetParent(null); 
            
            diceSettled[i] = false;
            settleTimers[i] = 0f;
        }
    }

    public void RollDice()
    {
        if (isRolling) return;
        StartCoroutine(RollDiceCoroutine());
    }

    IEnumerator RollDiceCoroutine()
    {
        isRolling = true;
        
        if (diceObjects[0] == null) CreateDice();

        for (int i = 0; i < 2; i++)
        {
            diceSettled[i] = false;
            settleTimers[i] = 0f;

            // Рандом перед броском
            diceObjects[i].transform.position = diceRollArea.position + new Vector3(-0.5f + i * 1f, 5f, 0);
            diceObjects[i].transform.rotation = Quaternion.Euler(
                Random.Range(0f, 360f),
                Random.Range(0f, 360f),
                Random.Range(0f, 360f)
            );

            // Сброс физики
            diceRigidbodies[i].linearVelocity = Vector3.zero;
            diceRigidbodies[i].angularVelocity = Vector3.zero;
            diceRigidbodies[i].isKinematic = false;

            // Бросок
            Vector3 throwDirection = new Vector3(
                Random.Range(-1f, 1f),
                Random.Range(-1f, 0f),
                Random.Range(-1f, 1f)
            ).normalized;
            
            diceRigidbodies[i].AddForce(throwDirection * throwForce, ForceMode.Impulse);
            diceRigidbodies[i].AddTorque(
                Random.Range(-1f, 1f) * throwTorque,
                Random.Range(-1f, 1f) * throwTorque,
                Random.Range(-1f, 1f) * throwTorque,
                ForceMode.Impulse
            );
        }

        yield return StartCoroutine(WaitForDiceToSettle());

        int[] results = new int[2];
        for (int i = 0; i < 2; i)
        {
            results[i] = GetTopFaceValue(diceObjects[i].transform.rotation);
        }

        DisplayResults(results);
        OnDiceRolled?.Invoke(results[0], results[1]);
        
        isRolling = false;
    }

    IEnumerator WaitForDiceToSettle()
    {
        bool allSettled = false;
        while (!allSettled)
        {
            allSettled = true;
            for (int i = 0; i < 2; i++)
            {
                if (diceRigidbodies[i] == null) continue;
                
                float velocityMagnitude = diceRigidbodies[i].linearVelocity.magnitude;
                float angularVelocityMagnitude = diceRigidbodies[i].angularVelocity.magnitude;

                if (velocityMagnitude < settleThreshold && angularVelocityMagnitude < settleThreshold)
                {
                    settleTimers[i] += Time.deltaTime;
                    if (settleTimers[i] < settleDuration) allSettled = false;
                }
                else
                {
                    settleTimers[i] = 0f;
                    allSettled = false;
                }
            }
            yield return null;
        }
        // Небольшая пауза для игрока
        yield return new WaitForSeconds(0.3f);
    }

    // Определение выпавшей грани
    int GetTopFaceValue(Quaternion rotation)
    {
        float maxDot = -1f;
        int faceValue = 1;

        // Трансформируем КАЖДУЮ локальную грань в мировое пространство
        // и сравниваем с МИРОВЫМ Vector3.up
        CheckFace(rotation * Vector3.up,    Vector3.up, 1, ref maxDot, ref faceValue);
        CheckFace(rotation * Vector3.down,  Vector3.up, 6, ref maxDot, ref faceValue);
        CheckFace(rotation * Vector3.forward, Vector3.up, 2, ref maxDot, ref faceValue);
        CheckFace(rotation * Vector3.back,    Vector3.up, 5, ref maxDot, ref faceValue);
        CheckFace(rotation * Vector3.right,   Vector3.up, 3, ref maxDot, ref faceValue);
        CheckFace(rotation * Vector3.left,    Vector3.up, 4, ref maxDot, ref faceValue);
        
        return faceValue;
    }

    void CheckFace(Vector3 upDirection, Vector3 faceNormal, int value, ref float maxDot, ref int faceValue)
    {
        float dot = Vector3.Dot(upDirection, faceNormal);
        if (dot > maxDot)
        {
            maxDot = dot;
            faceValue = value;
        }
    }

    void DisplayResults(int[] results)
    {
        if (diceResultText != null)
        {
            diceResultText.text = $"Кубик 1: {results[0]}\nКубик 2: {results[1]}\nСумма: {results[0] + results[1]}";
        }
        Debug.Log($"Результат: {results[0]} + {results[1]} = {results[0] + results[1]}");
    }

    public void ResetDice()
    {
        for (int i = 0; i < 2; i++)
        {
            if (diceObjects[i] != null)
            {
                diceObjects[i].transform.position = diceRollArea.position + new Vector3(-0.5f + i * 1f, 0.5f, 0);
                diceObjects[i].transform.rotation = Quaternion.identity;
                diceObjects[i].transform.localScale = Vector3.one * diceSize;
                
                if (diceRigidbodies[i] != null)
                {
                    diceRigidbodies[i].linearVelocity = Vector3.zero;
                    diceRigidbodies[i].angularVelocity = Vector3.zero;
                    diceRigidbodies[i].isKinematic = true;
                }
            }
        }
        
        if (diceResultText != null) diceResultText.text = "Нажмите для броска";
        isRolling = false;
    }

    void OnDestroy()
    {
        for (int i = 0; i < 2; i++)
        {
            if (diceObjects[i] != null) Destroy(diceObjects[i]);
        }
    }
}