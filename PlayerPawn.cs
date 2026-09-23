// PlayerPawn.cs
// Фишка игрока и все ее анимации
using UnityEngine;
using System.Collections;

public class PlayerPawn : MonoBehaviour
{
    [Header("Настройки прыжка")]
    public float height = 0.5f;
    public float duration = 0.2f;

    private GameBoard gameBoard;
    private bool isJumping = false;
    private int playerId = -1;

    public AudioClip stepSound;
    private AudioSource audioSource;

    // Фишки каждого игрока расставлены по углам клеток, чтобы они не пересекались
    private static readonly Vector3[] offsets = new Vector3[]
    {
        new Vector3(+1f, 0, +1f), // Игрок 0 — верхний правый
        new Vector3(+1f, 0, -1f), // Игрок 1 — нижний правый
        new Vector3(-1f, 0, -1f), // Игрок 2 — нижний левый
        new Vector3(-1f, 0, +1f)  // Игрок 3 — верхний левый
    };

    // Тюрьма (узники)
    private static readonly Vector3[] jailOffsets = new Vector3[]
    {
        new Vector3(+0.8f, 0, +0.8f),  // Игрок 0
        new Vector3(+0.8f, 0, -0.8f),  // Игрок 1
        new Vector3(-0.8f, 0, -0.8f),  // Игрок 2
        new Vector3(-0.8f, 0, +0.8f)   // Игрок 3
    };

    // Тюрьма (посетители)
    private static readonly Vector3[] visitOffsets = new Vector3[]
    {
        new Vector3(+1.2f, 0, +1.2f),
        new Vector3(+1.2f, 0, -1.2f),
        new Vector3(-1.2f, 0, -1.2f),
        new Vector3(-1.2f, 0, +1.2f)
    };

    private void Start()
    {
        gameBoard = FindObjectOfType<GameBoard>();
        if (gameBoard == null)
            Debug.LogError("GameBoard не найден!");

        audioSource = GetComponent<AudioSource>();
        if (audioSource == null)
            Debug.LogError("PlayerPawn: AudioSource не найден!");
    }

    /// Перемещает фишку пошагово с учётом айди игрока для смещения.
    public IEnumerator Move(int steps, int currentTileIndex, int playerId)
    {
        if (isJumping)
        {
            Debug.LogWarning("Pawn is already moving!");
            yield break;
        }

        this.playerId = playerId;
        isJumping = true;

        int currentTile = currentTileIndex;
        for (int i = 0; i < steps; i++)
        {
            currentTile = gameBoard.NormalizeTileIndex(currentTile + 1);
            Vector3 basePosition = gameBoard.GetTilePosition(currentTile);
            Vector3 targetPosition = ApplyOffset(basePosition, currentTile);

            yield return StartCoroutine(PerformJump(targetPosition, currentTile));
        }

        isJumping = false;
    }

    private Vector3 ApplyOffset(Vector3 basePosition, int tileIndex)
    {
        if (playerId < 0 || playerId >= offsets.Length)
            return basePosition;

        // Клетка 10 — тюрьма. Для обычного прохода (посетитель) используем visitOffsets.
        // Если игрок арестован — вызывается отдельный метод MoveToJail.
        if (tileIndex == 10)
            return basePosition + visitOffsets[playerId];

        return basePosition + offsets[playerId];
    }

    /// Мгновенно перемещает фишку в тюрьму с тюремным смещением.
    public IEnumerator MoveToJail(int jailTileIndex, int playerId)
    {
        this.playerId = playerId;
        isJumping = true;

        Vector3 basePosition = FindObjectOfType<GameBoard>().GetTilePosition(jailTileIndex);
        Vector3 targetPosition = basePosition + jailOffsets[playerId];
        Quaternion targetRotation = GetRotationForTile(jailTileIndex);

        // Телепорт в тюрьму
        float teleportDuration = 0.3f;
        Vector3 start = transform.position;
        float time = 0;

        while (time < teleportDuration)
        {
            transform.position = Vector3.Lerp(start, targetPosition, time / teleportDuration);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, time / teleportDuration);
            time += Time.deltaTime;
            yield return null;
        }

        transform.position = targetPosition;
        transform.rotation = targetRotation;
        isJumping = false;
    }

    IEnumerator PerformJump(Vector3 targetPosition, int targetTileIndex)
    {
        Vector3 start = transform.position;
        Vector3 end = targetPosition;
        Quaternion targetRotation = GetRotationForTile(targetTileIndex);

        float time = 0f;

        // Параболический прыжок через синус (Матан с 1 курса и правда чучут пригодился)
        while (time < duration)
        {
            float t = time / duration;
            Vector3 horizontal = Vector3.Lerp(start, end, t);
            float vertical = Mathf.Sin(t * Mathf.PI) * height;
            transform.position = horizontal + Vector3.up * vertical;
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, t);
            time += Time.deltaTime;
            yield return null;
        }

        // Фиксируем финальную позицию и поворот
        transform.position = end;
        transform.rotation = targetRotation;

        // Звук шага
        if (audioSource != null && stepSound != null)
        {
            audioSource.PlayOneShot(stepSound);
        }
    }

    // Поворот фишки в зависимости от стороны поля
    private Quaternion GetRotationForTile(int tileIndex)
    {
        tileIndex = gameBoard.NormalizeTileIndex(tileIndex);

        if (tileIndex >= 0 && tileIndex < 10)
            return Quaternion.Euler(0, 0, 0);      // нижняя - вправо
        else if (tileIndex >= 10 && tileIndex < 20)
            return Quaternion.Euler(0, 90, 0);     // правая - вглубь
        else if (tileIndex >= 20 && tileIndex < 30)
            return Quaternion.Euler(0, 180, 0);    // верхняя - влево
        else
            return Quaternion.Euler(0, 270, 0);    // левая - к камере
    }
}