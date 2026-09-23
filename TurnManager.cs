// TurnManager.cs
// Менеджер ходов: обработка бросков, покупок, тюрем, строительства, ботов.
// Если кто-то это читает, простите что вам приходится это видеть.
// В актуальном состоянии код представляет гигакостыль, который надо разбить.
// Однако, он рабочий и подходит для десонстрации мвп версии игры.
// ???: разнести по паттерну State (NormalTurnState, JailTurnState, BuildState итд)
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using TMPro;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class TurnManager : MonoBehaviour
{
    public DiceRoller diceRoller;

    private bool isWaitingForDice = false;
    private bool isOnExtraTurnFromDouble = false;
    private bool isTurnInProgress = false;

    // Режим постройки домов
    public Button buildHouseButton;
    private bool hasRolledDiceThisTurn = false;
    private bool isBuildHouseMode = false;
    private int targetTileForBuilding = -1;

    // Флаги для обработки дублей
    private bool hasRolledDoublesThisTurn = false;
    private bool shouldGiveExtraTurn = false;
    private bool isEndingTurn = false;

    public GameObject buildHousePopup;
    public TMP_Text buildHouseText;
    public Button buildHouseConfirm;
    public Button buildHouseCancel;

    // Режим покупки у других игроков
    public Button buyFromOthersButton;
    private bool isBuyFromOthersMode = false;
    private int targetTileForPurchase = -1;

    public GameObject buyFromOthersPopup;
    public TMP_Text buyFromOthersText;
    public Button buyFromOthersConfirm;
    public Button buyFromOthersCancel;

    // Режим покупки у банка
    public Button buyModeButton;
    public GameObject buyModePopup;
    private bool isBuyModeActive = false;

    // Состояние игроков
    public int currentPlayer = 0;
    public int maxPlayers = 4;

    // Сохранённое состояние кнопок (рудимент, но вдруг пригодится)
    private (bool dice, bool payJail, bool useCard, bool endTurn, bool buyFromOthers, bool buildHouse) savedButtonState;

    public PlayerPawn[] pawns;
    public int[] currentTileIndices;
    public int[] balances;
    public int[] jailCards;
    public bool[] isInJail;
    public int[] consecutiveDoubles;

    // Кнопки
    public Button diceButton;
    public Button payJailButton;
    public Button useCardButton;
    public Button endTurnButton;

    // Попап покупки у банка
    public GameObject buyPropertyPopup;
    public TMP_Text propertyInfoText;
    public Button buyConfirmButton;
    public Button buyCancelButton;

    public TMP_Text balanceDisplay;

    // Простое сообщение
    public GameObject simplePopupPanel;
    public TMP_Text simplePopupText;

    private GameBoard gameBoard;

    // Констранты полей
    private const int JailIndex = 10;
    private const int GoIndex = 0;

    private bool isSimplePopupActive = false;
    private int pendingPurchaseTile = -1;

    string GetPlayerName(int playerId)
    {
        return playerId == 0 ? "Игрока" : $"Бота {playerId + 1}";
    }

    // Обработка броска при попытке выйти из тюрьмы
    private void ProcessJailDiceResult(int d1, int d2, int playerId)
    {
        bool isDouble = d1 == d2;
        ShowSimpleMessage($"Игрок {playerId + 1} пытается выйти...\nВыпало: {d1} и {d2}", isFromBot: playerId != 0);

        if (isDouble)
        {
            // Успешный выход
            isInJail[playerId] = false;
            consecutiveDoubles[playerId] = 0;

            int diceRoll = d1 + d2;
            currentTileIndices[playerId] = gameBoard.NormalizeTileIndex(JailIndex + diceRoll);

            StartCoroutine(pawns[playerId].Move(diceRoll, JailIndex, playerId));
            ShowSimpleMessage($"Игрок {playerId + 1} выходит из тюрьмы!", isFromBot: playerId != 0);

            if (playerId == 0)
                SetGameplayButtons(false, false, false, true, true, true);
            else if (isTurnInProgress)
                StartCoroutine(DelayedEndTurn());
        }
        else
        {
            // Провал
            if (playerId == 0)
            {
                SetGameplayButtons(false, false, false, true, true, true);
                ShowSimpleMessage("Не удалось выйти.", isFromBot: playerId != 0);
            }
            else if (isTurnInProgress)
            {
                StartCoroutine(DelayedEndTurn());
            }
        }
    }

    // Полная стоимость клетки
    int CalculateTileTotalValue(TileData tile)
    {
        int value = tile.price;
        if (tile.type == TileType.Street && tile.houses > 0)
        {
            value += tile.houses * tile.houseCost;
        }
        return value;
    }

    // Режим строительства

    void ToggleBuildHouseMode()
    {
        SetGameplayButtons(false, false, false, false, false, false);

        if (!isBuildHouseMode)
        {
            isBuildHouseMode = true;
            ShowSimpleMessage("Выберите свою улицу для постройки дома");
            gameBoard.HideTileInfo();
            buildHousePopup?.SetActive(false);
            HideBuyPropertyPopup();
            HideBuyFromOthersPopup();
        }
        else
        {
            ExitBuildHouseMode();
        }
    }

    void ExitBuildHouseMode()
    {
        isBuildHouseMode = false;
        targetTileForBuilding = -1;
        buildHousePopup?.SetActive(false);
        RestoreGameplayButtons();
    }

    void OnBuildHouseConfirm()
    {
        if (targetTileForBuilding == -1) return;

        TileData tile = gameBoard.GetTileData(targetTileForBuilding);

        // Проверки
        if (tile.type != TileType.Street || tile.owner != currentPlayer || tile.houses >= 4)
        {
            ShowSimpleMessage("Невозможно построить дом!");
            ExitBuildHouseMode();
            return;
        }
        if (!gameBoard.IsColorGroupOwned(currentPlayer, tile.colorGroup))
        {
            ShowSimpleMessage("У вас нет монополии на этот цвет!");
            ExitBuildHouseMode();
            return;
        }
        if (balances[currentPlayer] < tile.houseCost)
        {
            ShowSimpleMessage("Недостаточно денег!");
            ExitBuildHouseMode();
            return;
        }

        // Строим
        balances[currentPlayer] -= tile.houseCost;
        tile.houses++;
        UpdateBalanceDisplay();
        SpawnHouseMarker(targetTileForBuilding, tile.houses);

        ShowSimpleMessage($"Вы построили дом на {tile.name}!\nДомов: {tile.houses}");
        ExitBuildHouseMode();
    }

    void OnBuildHouseCancel()
    {
        HideBuildHousePopup();
        ExitBuildHouseMode();

        // Восстановление UI с учётом дубля
        if (hasRolledDiceThisTurn)
        {
            SetGameplayButtons(
                showDice: shouldGiveExtraTurn,
                showPayJail: false,
                showUseCard: false,
                showEndTurn: !shouldGiveExtraTurn,
                showBuyFromOthers: true,
                showBuildHouse: true
            );
        }
        else
        {
            if (isInJail[currentPlayer])
            {
                bool hasJailCard = jailCards[currentPlayer] > 0;
                SetGameplayButtons(true, true, hasJailCard, false, true, true);
            }
            else
            {
                SetGameplayButtons(true, false, false, false, true, true);
            }
        }
    }

    void HideBuildHousePopup()
    {
        buildHousePopup?.SetActive(false);
        Time.timeScale = 1f;
    }

    // Спавн визуального маркера дома на клетке
    // ??? переделать на пул объектов
    private void SpawnHouseMarker(int tileIndex, int houseNumber)
    {
        Vector3 basePosition = gameBoard.GetTilePosition(tileIndex);

        // Поворот в зависимости от стороны
        int side = tileIndex / 10;
        float rotationAngle = side * 90f;

        // Базовое смещение домиков к центру доски
        Vector3 offset = Vector3.zero;
        switch (side)
        {
            case 0: offset = new Vector3(-0.3f, 0, +0.3f); break;
            case 1: offset = new Vector3(+0.3f, 0, +0.3f); break;
            case 2: offset = new Vector3(+0.3f, 0, -0.3f); break;
            case 3: offset = new Vector3(-0.3f, 0, -0.3f); break;
        }

        // Размещаем дома в ряд вдоль стороны
        float spacing = 0.15f;
        switch (side)
        {
            case 0: offset.x += (houseNumber - 1) * spacing; break;
            case 1: offset.z += (houseNumber - 1) * spacing; break;
            case 2: offset.x -= (houseNumber - 1) * spacing; break;
            case 3: offset.z -= (houseNumber - 1) * spacing; break;
        }

        Vector3 finalPosition = basePosition + offset;

        // Префаб
        GameObject housePrefab = Resources.Load<GameObject>("HousePrefab");
        if (housePrefab == null)
        {
            Debug.LogError("Префаб дома не найден! Создайте 'HousePrefab' в папке Resources.");
            return;
        }

        GameObject house = Instantiate(housePrefab, finalPosition, Quaternion.Euler(0, rotationAngle, 0));
        house.transform.SetParent(gameBoard.tiles[tileIndex]);
        house.tag = "HouseMarker";
        house.transform.localScale = Vector3.one * 0.1f;
    }

    // Режим покупки у других игроков

    void OnBuyFromOthersConfirm()
    {
        if (targetTileForPurchase == -1) return;

        TileData tile = gameBoard.GetTileData(targetTileForPurchase);
        int totalValue = CalculateTileTotalValue(tile);
        int purchasePrice = Mathf.CeilToInt(totalValue * 1.5f); // подлые 50 процентиков:)

        if (tile.owner == -1 || tile.owner == currentPlayer || balances[currentPlayer] < purchasePrice)
        {
            HideBuyFromOthersPopup();
            return;
        }

        // Списание/начисление денег
        balances[currentPlayer] -= purchasePrice;
        balances[tile.owner] += purchasePrice;

        // Передача собственности
        tile.owner = currentPlayer;
        tile.houses = 0; // все дома сгорают при перепродаже :(

        gameBoard.SetTileOwner(targetTileForPurchase, currentPlayer);
        UpdateBalanceDisplay();

        ShowSimpleMessage($"Ты купил:\n{tile.name}\nу {GetPlayerName(tile.owner)}!");
        HideBuyFromOthersPopup();

        if (hasRolledDiceThisTurn)
            SetGameplayButtons(false, false, false, true, true, true);
        else
            RestoreInitialButtons();
    }

    void OnBuyFromOthersCancel()
    {
        HideBuyFromOthersPopup();
        ExitBuyFromOthersMode();

        if (hasRolledDiceThisTurn)
            SetGameplayButtons(false, false, false, true, true, true);
        else
            RestoreInitialButtons();
    }

    // Восстановление начальных кнопок хода
    private void RestoreInitialButtons()
    {
        if (isInJail[currentPlayer])
        {
            bool hasJailCard = jailCards[currentPlayer] > 0;
            SetGameplayButtons(true, true, hasJailCard, false, true, true);
        }
        else
        {
            SetGameplayButtons(true, false, false, false, true, true);
        }
    }

    void HideBuyFromOthersPopup()
    {
        buyFromOthersPopup?.SetActive(false);
        Time.timeScale = 1f;
    }

    void ToggleBuyFromOthersMode()
    {
        SetGameplayButtons(false, false, false, false, false, false);

        if (!isBuyFromOthersMode)
        {
            isBuyFromOthersMode = true;
            ShowSimpleMessage("Выберите собственность другого игрока");
            HideBuyPropertyPopup();
            gameBoard.HideTileInfo();
            buyFromOthersPopup?.SetActive(false);
        }
        else
        {
            ExitBuyFromOthersMode();
            RestoreGameplayButtons();
        }
    }

    void RestoreGameplayButtons()
    {
        if (currentPlayer != 0) return; // только для игрока

        if (!hasRolledDiceThisTurn || shouldGiveExtraTurn)
        {
            if (isInJail[currentPlayer])
            {
                bool hasJailCard = jailCards[currentPlayer] > 0;
                SetGameplayButtons(true, true, hasJailCard, false, true, true);
            }
            else
            {
                SetGameplayButtons(true, false, false, false, true, true);
            }
        }
        else
        {
            if (isInJail[currentPlayer])
            {
                bool hasJailCard = jailCards[currentPlayer] > 0;
                SetGameplayButtons(false, true, hasJailCard, false, true, true);
            }
            else
            {
                SetGameplayButtons(false, false, false, true, true, true);
            }
        }
    }

    void ExitBuyFromOthersMode()
    {
        isBuyFromOthersMode = false;
        targetTileForPurchase = -1;
        buyFromOthersPopup?.SetActive(false);
        RestoreGameplayButtons();
    }

    // Инициализация... дальше бога нет

    void Start()
    {
        gameBoard = FindObjectOfType<GameBoard>();
        if (gameBoard == null) Debug.LogError("GameBoard не найден!");

        diceRoller = FindObjectOfType<DiceRoller>();
        if (diceRoller != null)
        {
            diceRoller.OnDiceRolled += OnDiceResultReceived;
        }

        if (pawns == null || pawns.Length != maxPlayers)
        {
            Debug.LogError($"Количество фишек ({pawns?.Length ?? 0}) != MaxPlayers ({maxPlayers})!");
            enabled = false;
            return;
        }

        currentTileIndices = new int[maxPlayers];
        balances = new int[maxPlayers];
        isInJail = new bool[maxPlayers];
        jailCards = new int[maxPlayers];
        consecutiveDoubles = new int[maxPlayers];

        for (int i = 0; i < maxPlayers; i++)
        {
            balances[i] = 2000; // стартовый капитал
            jailCards[i] = (i == 0) ? 1 : 0; // у игрока сразу одна карта выхода
            isInJail[i] = false;
            consecutiveDoubles[i] = 0;
            currentTileIndices[i] = 0;
        }

        // Подписка на кнопки
        if (diceButton != null) diceButton.onClick.AddListener(OnDiceButtonPressed);
        if (payJailButton != null) payJailButton.onClick.AddListener(OnPayJailButtonPressed);
        if (useCardButton != null) useCardButton.onClick.AddListener(OnUseCardButtonPressed);
        if (endTurnButton != null) endTurnButton.onClick.AddListener(OnEndTurnButtonPressed);
        if (buyConfirmButton != null) buyConfirmButton.onClick.AddListener(OnBuyConfirmPressed);
        if (buyCancelButton != null) buyCancelButton.onClick.AddListener(OnBuyCancelPressed);

        if (buyFromOthersButton != null) buyFromOthersButton.onClick.AddListener(ToggleBuyFromOthersMode);
        if (buyFromOthersConfirm != null) buyFromOthersConfirm.onClick.AddListener(OnBuyFromOthersConfirm);
        if (buyFromOthersCancel != null) buyFromOthersCancel.onClick.AddListener(OnBuyFromOthersCancel);

        if (buildHouseButton != null) buildHouseButton.onClick.AddListener(ToggleBuildHouseMode);
        if (buildHouseConfirm != null) buildHouseConfirm.onClick.AddListener(OnBuildHouseConfirm);
        if (buildHouseCancel != null) buildHouseCancel.onClick.AddListener(OnBuildHouseCancel);

        SetGameplayButtons(false, false, false, false, false, false);
        UpdateBalanceDisplay();
        StartTurn();
    }

    // Обработка кликов мыши

    void Update()
    {
        if (Mouse.current == null || Camera.main == null) return;

        if (Mouse.current.leftButton.wasPressedThisFrame)
        {
            Ray ray = Camera.main.ScreenPointToRay(Mouse.current.position.ReadValue());
            bool closedAny = false;

            // Закрытие окон при клике вне их
            if (buyPropertyPopup != null && buyPropertyPopup.activeSelf)
            {
                if (!IsClickInsideRectTransform(buyPropertyPopup))
                {
                    HideBuyPropertyPopup();
                    closedAny = true;
                }
                else
                {
                    return; // клик внутри — не обрабатываем дальше
                }
            }

            if (isSimplePopupActive)
            {
                HideSimplePopup();
                closedAny = true;
            }

            if (gameBoard != null && gameBoard.IsTileInfoActive())
            {
                gameBoard.HideTileInfo();
                closedAny = true;
            }

            if (buyFromOthersPopup != null && buyFromOthersPopup.activeSelf)
            {
                if (!IsClickInsideRectTransform(buyFromOthersPopup))
                {
                    HideBuyFromOthersPopup();
                    closedAny = true;
                }
                else
                {
                    return;
                }
            }

            if (buildHousePopup != null && buildHousePopup.activeSelf)
            {
                if (!IsClickInsideRectTransform(buildHousePopup))
                {
                    HideBuildHousePopup();
                    closedAny = true;
                }
                else
                {
                    return;
                }
            }

            if (closedAny) return;

            // Обработка клика по клетке поля
            if (Physics.Raycast(ray, out RaycastHit hit, 100f))
            {
                for (int i = 0; i < gameBoard.tiles.Length; i++)
                {
                    if (hit.transform == gameBoard.tiles[i])
                    {
                        TileData tile = gameBoard.tileData[i];

                        // Режим покупки чужой собственности
                        if (isBuyFromOthersMode)
                        {
                            if (tile.owner != -1 && tile.owner != currentPlayer &&
                                (tile.type == TileType.Street || tile.type == TileType.Railway || tile.type == TileType.Utility))
                            {
                                int totalValue = CalculateTileTotalValue(tile);
                                int purchasePrice = Mathf.CeilToInt(totalValue * 1.5f);

                                if (balances[currentPlayer] >= purchasePrice)
                                {
                                    targetTileForPurchase = i;
                                    buyFromOthersText.text = $"Купить у {GetPlayerName(tile.owner)}:\n{tile.name}\nСтоимость: ${purchasePrice}";
                                    SetGameplayButtons(false, false, false, false, false, false);
                                    buyFromOthersPopup.SetActive(true);
                                    Time.timeScale = 0f;
                                }
                                else
                                {
                                    ShowSimpleMessage("Недостаточно денег!");
                                    ExitBuyFromOthersMode();
                                }
                            }
                            else
                            {
                                ShowSimpleMessage("Это нельзя купить!");
                                ExitBuyFromOthersMode();
                            }
                            return;
                        }

                        // Режим постройки дома
                        if (isBuildHouseMode)
                        {
                            if (tile.type == TileType.Street && tile.owner == currentPlayer)
                            {
                                if (gameBoard.IsColorGroupOwned(currentPlayer, tile.colorGroup))
                                {
                                    if (tile.houses < 4)
                                    {
                                        targetTileForBuilding = i;
                                        buildHouseText.text = $"Построить дом на:\n{tile.name}\nСтоимость: ${tile.houseCost}";
                                        buildHousePopup.SetActive(true);
                                        Time.timeScale = 0f;
                                    }
                                    else
                                    {
                                        ShowSimpleMessage("Максимум домов уже построен!");
                                        ExitBuildHouseMode();
                                    }
                                }
                                else
                                {
                                    ShowSimpleMessage("У вас нет монополии на этот цвет!");
                                    ExitBuildHouseMode();
                                }
                            }
                            else
                            {
                                ShowSimpleMessage("Это не ваша улица или не улица!");
                                ExitBuildHouseMode();
                            }
                            return;
                        }

                        // Обычный просмотр информации
                        gameBoard.HideTileInfo();
                        gameBoard.ShowTileInfo(tile);
                        return;
                    }
                }
            }
        }
    }

    // Проверка, попал ли клик по попапу
    private bool IsClickInsideRectTransform(GameObject popup)
    {
        RectTransform rect = popup.GetComponent<RectTransform>();
        if (rect == null) return false;

        Vector2 local;
        return RectTransformUtility.ScreenPointToLocalPointInRectangle(
            rect,
            Mouse.current.position.ReadValue(),
            null,
            out local);
    }

    // Управление ходом

    void StartTurn()
    {
        if (isTurnInProgress) return; // защита от рекурсии
        isTurnInProgress = true;
        hasRolledDiceThisTurn = false;
        hasRolledDoublesThisTurn = false;
        shouldGiveExtraTurn = false;
        isOnExtraTurnFromDouble = false;

        string playerName = (currentPlayer == 0) ? "Твой" : $"Бот {currentPlayer + 1}";
        ShowSimpleMessage($"{playerName} ход начинается!", isFromBot: currentPlayer != 0);

        if (currentPlayer == 0)
        {
            // В автоходы пускают тока ботов
            if (isInJail[currentPlayer])
            {
                bool hasJailCard = jailCards[currentPlayer] > 0;
                SetGameplayButtons(true, true, hasJailCard, false, true, true);
            }
            else
            {
                SetGameplayButtons(true, false, false, false, true, true);
            }
        }
        else
        {
            StartCoroutine(BotTurn());
        }
    }

    public void SetGameplayButtons(bool showDice, bool showPayJail, bool showUseCard,
                                   bool showEndTurn, bool showBuyFromOthers = false,
                                   bool showBuildHouse = false)
    {
        if (diceButton != null) diceButton.gameObject.SetActive(showDice);
        if (payJailButton != null) payJailButton.gameObject.SetActive(showPayJail);
        if (useCardButton != null) useCardButton.gameObject.SetActive(showUseCard);
        if (endTurnButton != null) endTurnButton.gameObject.SetActive(showEndTurn);
        if (buyFromOthersButton != null) buyFromOthersButton.gameObject.SetActive(showBuyFromOthers);
        if (buildHouseButton != null) buildHouseButton.gameObject.SetActive(showBuildHouse);
    }

    public void OnDiceButtonPressed()
    {
        if (currentPlayer != 0 || isTurnInProgress == false) return;

        hasRolledDiceThisTurn = true;
        SetGameplayButtons(false, false, false, false, false, false);
        gameBoard.HideTileInfo();
        isWaitingForDice = true;

        diceRoller.RollDice();
    }

    private void OnDiceResultReceived(int d1, int d2)
    {
        if (!isWaitingForDice) return;
        isWaitingForDice = false;

        hasRolledDoublesThisTurn = (d1 == d2);

        if (isInJail[currentPlayer])
            ProcessJailDiceResult(d1, d2, currentPlayer);
        else
            StartCoroutine(DoDiceRollAndMove(currentPlayer, d1, d2));
    }

    public void OnPayJailButtonPressed()
    {
        if (currentPlayer != 0 || isTurnInProgress == false || !isInJail[currentPlayer] || balances[currentPlayer] < 50) return;
        PayToLeaveJail(currentPlayer);
    }

    public void OnUseCardButtonPressed()
    {
        if (currentPlayer != 0 || isTurnInProgress == false || !isInJail[currentPlayer] || jailCards[currentPlayer] <= 0) return;
        UseGetOutOfJailCard(currentPlayer);
    }

    public void OnEndTurnButtonPressed()
    {
        if (currentPlayer != 0 || isTurnInProgress == false) return;
        SetGameplayButtons(false, false, false, false, false, false);
        EndTurn();
    }

    private bool CanBuildHouses(int playerId)
    {
        foreach (var tile in gameBoard.tileData)
        {
            if (tile.type == TileType.Street && tile.owner == playerId && tile.houses < 4)
            {
                if (gameBoard.IsColorGroupOwned(playerId, tile.colorGroup) && balances[playerId] >= tile.houseCost)
                    return true;
            }
        }
        return false;
    }

    public void OnBuyConfirmPressed()
    {
        if (pendingPurchaseTile == -1) return;

        TileData tile = gameBoard.GetTileData(pendingPurchaseTile);
        if (tile.owner != -1 || balances[0] < tile.price)
        {
            HideBuyPropertyPopup();
            return;
        }

        balances[0] -= tile.price;
        tile.owner = 0;
        gameBoard.SetTileOwner(pendingPurchaseTile, 0);
        UpdateBalanceDisplay();
        HideBuyPropertyPopup();

        ShowSimpleMessage($"Ты купил:\n{tile.name}\nза ${tile.price}!");

        // Учитываем был ли дубль
        SetGameplayButtons(
            showDice: shouldGiveExtraTurn,
            showPayJail: false,
            showUseCard: false,
            showEndTurn: !shouldGiveExtraTurn,
            showBuyFromOthers: true,
            showBuildHouse: true
        );
    }

    public void OnBuyCancelPressed()
    {
        HideBuyPropertyPopup();
        SetGameplayButtons(false, false, false, true, true, true);
    }

    void ShowBuyPropertyPopup(TileData tile)
    {
        if (buyPropertyPopup == null || propertyInfoText == null) return;

        gameBoard.HideTileInfo();
        propertyInfoText.text = $"{tile.name}\nСтоимость: ${tile.price}";
        buyPropertyPopup.SetActive(true);
        Time.timeScale = 0f;
    }

    void HideBuyPropertyPopup()
    {
        if (buyPropertyPopup != null)
        {
            buyPropertyPopup.SetActive(false);
            pendingPurchaseTile = -1;
            Time.timeScale = 1f;
        }
    }

    // Проверки и банкротство

    bool CanAfford(int playerId, int amount) => balances[playerId] >= amount;

    void DeclareBankruptcy(int playerId)
    {
        if (playerId == 0)
            ShowSimpleMessage("Ты обанкротился!\nИгра окончена.");
        else
            ShowSimpleMessage($"Бот {playerId + 1} обанкротился!", isFromBot: true);

        // Возврат собственности банку
        for (int i = 0; i < gameBoard.tileData.Length; i++)
        {
            if (gameBoard.tileData[i].owner == playerId)
            {
                gameBoard.tileData[i].owner = -1;
                foreach (Transform child in gameBoard.tiles[i])
                {
                    if (child.CompareTag("OwnerMarker"))
                        Destroy(child.gameObject);
                }
            }
        }

        if (playerId == 0)
        {
            enabled = false;
            Time.timeScale = 0f;
        }
    }

    // Тюрьма

    void RollDiceToEscapeJail(int playerId)
    {
        int dice1 = Random.Range(1, 7);
        int dice2 = Random.Range(1, 7);
        bool isDouble = dice1 == dice2;

        ShowSimpleMessage($"Игрок {playerId + 1} пытается выйти...\nВыпало: {dice1} и {dice2}", isFromBot: playerId != 0);

        if (isDouble)
        {
            isInJail[playerId] = false;
            consecutiveDoubles[playerId] = 0;

            int diceRoll = dice1 + dice2;
            currentTileIndices[playerId] = gameBoard.NormalizeTileIndex(JailIndex + diceRoll);
            StartCoroutine(pawns[playerId].Move(diceRoll, JailIndex, playerId));

            ShowSimpleMessage($"Игрок {playerId + 1} выходит из тюрьмы!", isFromBot: playerId != 0);

            if (playerId == 0)
                SetGameplayButtons(false, false, false, true, true, true);
            else if (isTurnInProgress)
                StartCoroutine(DelayedEndTurn());
        }
        else
        {
            if (playerId == 0)
            {
                SetGameplayButtons(false, false, false, true, true, true);
                ShowSimpleMessage("Не удалось выйти.", isFromBot: playerId != 0);
            }
            else if (isTurnInProgress)
            {
                StartCoroutine(DelayedEndTurn());
            }
        }
    }

    void PayToLeaveJail(int playerId)
    {
        if (!CanAfford(playerId, 50))
        {
            DeclareBankruptcy(playerId);
            return;
        }

        balances[playerId] -= 50;
        isInJail[playerId] = false;
        consecutiveDoubles[playerId] = 0;
        UpdateBalanceDisplay();

        ShowSimpleMessage($"Игрок {playerId + 1} вышел за $50!", isFromBot: playerId != 0);

        if (playerId == 0)
            SetGameplayButtons(true, false, false, false, true, true);
        else if (isTurnInProgress)
            StartCoroutine(RollDiceAndMoveAfterJail(playerId));
    }

    void UseGetOutOfJailCard(int playerId)
    {
        jailCards[playerId]--;
        isInJail[playerId] = false;
        consecutiveDoubles[playerId] = 0;
        UpdateBalanceDisplay();

        ShowSimpleMessage($"Игрок {playerId + 1} использовал карту выхода из тюрьмы!", isFromBot: playerId != 0);

        if (playerId == 0)
            SetGameplayButtons(true, false, false, false, true, true);
        else if (isTurnInProgress)
            StartCoroutine(RollDiceAndMoveAfterJail(playerId));
    }

    IEnumerator RollDiceAndMoveAfterJail(int playerId)
    {
        yield return new WaitForSeconds(1f);
        RollDiceAndMove(playerId);
    }

    // Обычный ход

    void RollDiceAndMove(int playerId)
    {
        StartCoroutine(DoDiceRollAndMove(playerId));
    }

    IEnumerator DoDiceRollAndMove(int playerId, int d1 = 0, int d2 = 0)
    {
        // Если значения не переданы — генерируем случайно (для ботов)
        if (d1 == 0) d1 = Random.Range(1, 7);
        if (d2 == 0) d2 = Random.Range(1, 7);

        int diceRoll = d1 + d2;
        bool isDouble = d1 == d2;

        ShowSimpleMessage($"Игрок {playerId + 1} бросает кубики...\nВыпало: {d1} и {d2}", isFromBot: playerId != 0);
        yield return new WaitWhile(() => isSimplePopupActive);

        // Проверка на три дубля подряд
        if (isDouble)
        {
            consecutiveDoubles[playerId]++;
            if (consecutiveDoubles[playerId] >= 3)
            {
                consecutiveDoubles[playerId] = 0;
                isInJail[playerId] = true;
                currentTileIndices[playerId] = JailIndex;

                yield return StartCoroutine(pawns[playerId].MoveToJail(JailIndex, playerId));
                ShowSimpleMessage($"Игрок {playerId + 1} отправлен в тюрьму\nза 3 дубля подряд!", isFromBot: playerId != 0);
                yield return new WaitWhile(() => isSimplePopupActive);

                if (playerId == 0)
                    SetGameplayButtons(false, false, false, true, true, true);
                else
                    StartCoroutine(DelayedEndTurn());

                yield break;
            }
        }
        else
        {
            consecutiveDoubles[playerId] = 0;
        }

        // Перемещение
        int startTile = currentTileIndices[playerId];
        int newTile = gameBoard.NormalizeTileIndex(startTile + diceRoll);
        bool passedGo = startTile + diceRoll >= 40;
        currentTileIndices[playerId] = newTile;

        TileData landedTile = gameBoard.GetTileData(newTile);

        // Клетка гоу ту джейл
        if (landedTile.type == TileType.GoToJail)
        {
            currentTileIndices[playerId] = JailIndex;
            isInJail[playerId] = true;
            consecutiveDoubles[playerId] = 0;

            yield return StartCoroutine(pawns[playerId].MoveToJail(JailIndex, playerId));
            ShowSimpleMessage($"Игрок {playerId + 1} отправлен в тюрьму!", isFromBot: playerId != 0);

            if (playerId == 0)
                SetGameplayButtons(false, false, false, true, true, true);
            else
                StartCoroutine(DelayedEndTurn());

            yield break;
        }

        yield return StartCoroutine(AnimateAndProcessMove(playerId, diceRoll, d1, d2, startTile, newTile, passedGo, isDouble));
    }

    IEnumerator AnimateAndProcessMove(int playerId, int diceRoll, int dice1, int dice2,
                                      int startTile, int newTile, bool passedGo, bool isDouble)
    {
        // Анимация движения
        yield return StartCoroutine(pawns[playerId].Move(diceRoll, startTile, playerId));

        TileData landedTile = gameBoard.GetTileData(newTile);

        // Проход через поле Вперёд
        if (passedGo || newTile == GoIndex)
        {
            balances[playerId] += 200;
            UpdateBalanceDisplay();
            ShowSimpleMessage($"Игрок {playerId + 1} прошёл «Вперёд!»\nПолучает $200!", isFromBot: playerId != 0);
            yield return new WaitWhile(() => isSimplePopupActive);
        }

        // Налоги
        if (landedTile.type == TileType.Tax)
        {
            int taxAmount = landedTile.taxAmount;
            if (!CanAfford(playerId, taxAmount))
            {
                DeclareBankruptcy(playerId);
                yield break;
            }
            balances[playerId] -= taxAmount;
            UpdateBalanceDisplay();
            ShowSimpleMessage($"Игрок {playerId + 1} платит налог:\n${taxAmount}", isFromBot: playerId != 0);
            yield return new WaitWhile(() => isSimplePopupActive);
        }

        // Аренда
        if (landedTile.owner != -1 && landedTile.owner != playerId)
        {
            int rent = landedTile.GetRent(gameBoard, diceRoll);
            if (rent > 0)
            {
                if (!CanAfford(playerId, rent))
                {
                    DeclareBankruptcy(playerId);
                    yield break;
                }
                balances[playerId] -= rent;
                balances[landedTile.owner] += rent;
                UpdateBalanceDisplay();

                string ownerName = (landedTile.owner == 0) ? "Игроку" : $"Боту {landedTile.owner + 1}";
                ShowSimpleMessage($"Игрок {playerId + 1} платит\n${rent} {ownerName}\nза {landedTile.name}", isFromBot: true);
                yield return new WaitWhile(() => isSimplePopupActive);
            }
        }

        // Логика дублей
        bool allowExtraTurn = isDouble;
        bool wasCardDrawn = false;

        // Карты Шанс и Казна
        if (landedTile.type == TileType.Chance || landedTile.type == TileType.CommunityChest)
        {
            wasCardDrawn = true;
            string deckName = (landedTile.type == TileType.Chance) ? "Шанс" : "Общественная казна";

            ShowSimpleMessage($"Игрок {playerId + 1} попал на\n{deckName}!", isFromBot: playerId != 0);
            yield return new WaitWhile(() => isSimplePopupActive);

            Card card = (landedTile.type == TileType.Chance)
                ? gameBoard.DrawChanceCard()
                : gameBoard.DrawCommunityChestCard();

            ShowSimpleMessage($"Карта:\n{card.text}", isFromBot: playerId != 0);
            yield return new WaitWhile(() => isSimplePopupActive);

            // Если карта перемещает игрока — дубль аннулируется
            bool cardMovesPlayer = card.type == CardType.MoveToTile || card.type == CardType.GoToJail;
            if (cardMovesPlayer)
                allowExtraTurn = false;

            yield return StartCoroutine(ProcessCardAction(playerId, card));

            // Если карта отправила в тюрьму — завершаем ход
            if (isInJail[playerId])
            {
                if (playerId == 0)
                    SetGameplayButtons(false, false, false, true, true, true);
                else
                    StartCoroutine(DelayedEndTurn());
                yield break;
            }
        }

        // Сохраняем флаг доп. хода для использования в UI
        shouldGiveExtraTurn = allowExtraTurn;

        // 7. Покупка собственности (если клетка свободна)
        bool isBuyable = (landedTile.type == TileType.Street ||
                          landedTile.type == TileType.Railway ||
                          landedTile.type == TileType.Utility) &&
                         landedTile.owner == -1;

        if (playerId == 0 && isBuyable && !wasCardDrawn)
        {
            if (balances[playerId] >= landedTile.price)
            {
                pendingPurchaseTile = newTile;
                ShowBuyPropertyPopup(landedTile);
            }
            else
            {
                SetGameplayButtons(
                    showDice: shouldGiveExtraTurn,
                    showPayJail: false,
                    showUseCard: false,
                    showEndTurn: !shouldGiveExtraTurn,
                    showBuyFromOthers: true,
                    showBuildHouse: true
                );
                ShowSimpleMessage($"Недостаточно денег для покупки {landedTile.name}!", isFromBot: playerId != 0);
                yield return new WaitWhile(() => isSimplePopupActive);
            }
        }
        else if (playerId == 0 && !wasCardDrawn)
        {
            SetGameplayButtons(
                showDice: shouldGiveExtraTurn,
                showPayJail: false,
                showUseCard: false,
                showEndTurn: !shouldGiveExtraTurn,
                showBuyFromOthers: true,
                showBuildHouse: true
            );
        }

        // Завершение хода (бот)
        if (playerId != 0)
        {
            if (isBuyable && !wasCardDrawn && balances[playerId] >= landedTile.price)
            {
                balances[playerId] -= landedTile.price;
                landedTile.owner = playerId;
                gameBoard.SetTileOwner(newTile, playerId);
                UpdateBalanceDisplay();

                ShowSimpleMessage($"Бот {playerId + 1} купил:\n{landedTile.name}", isFromBot: true);
                yield return new WaitWhile(() => isSimplePopupActive);
            }

            if (allowExtraTurn)
            {
                yield return new WaitForSeconds(1f);
                if (isTurnInProgress)
                    RollDiceAndMove(playerId);
            }
            else if (isTurnInProgress)
            {
                StartCoroutine(DelayedEndTurn());
            }
        }
        else
        {
            // Завершение хода (игрок)
            if (wasCardDrawn)
            {
                // После карты ход передаётся дальше
                SetGameplayButtons(false, false, false, true, true, true);
            }
            else
            {
                if (shouldGiveExtraTurn)
                    SetGameplayButtons(true, false, false, false, true, true);
                else
                    SetGameplayButtons(false, false, false, true, true, true);
            }
        }
    }

    IEnumerator ProcessCardAction(int playerId, Card card)
    {
        switch (card.type)
        {
            case CardType.CollectMoney:
                balances[playerId] += card.amount;
                UpdateBalanceDisplay();
                ShowSimpleMessage($"Игрок {playerId + 1} получает ${card.amount}!", isFromBot: playerId != 0);
                break;

            case CardType.PayMoney:
                if (!CanAfford(playerId, card.amount))
                {
                    DeclareBankruptcy(playerId);
                    yield break;
                }
                balances[playerId] -= card.amount;
                UpdateBalanceDisplay();
                ShowSimpleMessage($"Игрок {playerId + 1} платит ${card.amount}!", isFromBot: playerId != 0);
                break;

            case CardType.MoveToTile:
                {
                    int startTile = currentTileIndices[playerId];
                    int targetTile = card.targetTile;
                    int steps = targetTile - startTile;
                    if (steps <= 0) steps += 40;

                    currentTileIndices[playerId] = targetTile;
                    yield return StartCoroutine(pawns[playerId].Move(steps, startTile, playerId));

                    if (startTile > targetTile)
                    {
                        balances[playerId] += 200;
                        UpdateBalanceDisplay();
                        ShowSimpleMessage($"Игрок {playerId + 1} прошёл «Вперёд!»\nПолучает $200!", isFromBot: playerId != 0);
                        yield return new WaitWhile(() => isSimplePopupActive);
                    }

                    TileData landedTile = gameBoard.GetTileData(targetTile);
                    if (landedTile.type == TileType.GoToJail)
                    {
                        currentTileIndices[playerId] = JailIndex;
                        isInJail[playerId] = true;
                        consecutiveDoubles[playerId] = 0;
                        yield return StartCoroutine(pawns[playerId].MoveToJail(JailIndex, playerId));
                        ShowSimpleMessage($"Игрок {playerId + 1} отправлен в тюрьму!", isFromBot: playerId != 0);
                    }
                    else
                    {
                        yield return StartCoroutine(ProcessTileAfterMove(playerId, targetTile, steps));
                    }
                }
                break;

            case CardType.GoToJail:
                currentTileIndices[playerId] = JailIndex;
                isInJail[playerId] = true;
                consecutiveDoubles[playerId] = 0;
                yield return StartCoroutine(pawns[playerId].MoveToJail(JailIndex, playerId));
                ShowSimpleMessage($"Игрок {playerId + 1} отправлен в тюрьму!", isFromBot: playerId != 0);
                yield return new WaitWhile(() => isSimplePopupActive);

                if (playerId == 0)
                    SetGameplayButtons(false, false, false, true, true, true);
                else if (isTurnInProgress)
                    StartCoroutine(DelayedEndTurn());
                break;

            case CardType.GetOutOfJailCard:
                jailCards[playerId]++;
                UpdateBalanceDisplay();
                ShowSimpleMessage($"Игрок {playerId + 1} получил карту\n«Освобождение из тюрьмы»!", isFromBot: playerId != 0);
                break;

            case CardType.StealFromOthers:
                int totalStolen = 0;
                for (int i = 0; i < maxPlayers; i++)
                {
                    if (i != playerId && balances[i] > 0)
                    {
                        int stolen = Mathf.Min(card.amount, balances[i]);
                        if (!CanAfford(i, stolen)) continue;

                        balances[i] -= stolen;
                        totalStolen += stolen;
                    }
                }
                balances[playerId] += totalStolen;
                UpdateBalanceDisplay();
                ShowSimpleMessage($"Игрок {playerId + 1} украл ${totalStolen} у других!", isFromBot: playerId != 0);
                break;
        }
    }

    IEnumerator ProcessTileAfterMove(int playerId, int tileIndex, int diceRoll)
    {
        TileData tile = gameBoard.GetTileData(tileIndex);

        if (tile.owner != -1 && tile.owner != playerId)
        {
            int rent = tile.GetRent(gameBoard, diceRoll);
            if (rent > 0)
            {
                if (!CanAfford(playerId, rent))
                {
                    DeclareBankruptcy(playerId);
                    yield break;
                }
                balances[playerId] -= rent;
                balances[tile.owner] += rent;
                UpdateBalanceDisplay();

                string ownerName = (tile.owner == 0) ? "Игроку" : $"Боту {tile.owner + 1}";
                ShowSimpleMessage($"Игрок {playerId + 1} платит\n${rent} {ownerName}\nза {tile.name}", isFromBot: playerId != 0);
                yield return new WaitWhile(() => isSimplePopupActive);
            }
        }

        bool isBuyable = (tile.type == TileType.Street ||
                          tile.type == TileType.Railway ||
                          tile.type == TileType.Utility) &&
                         tile.owner == -1;

        if (playerId == 0 && isBuyable)
        {
            if (balances[playerId] >= tile.price)
            {
                pendingPurchaseTile = tileIndex;
                ShowBuyPropertyPopup(tile);
            }
            else
            {
                SetGameplayButtons(false, false, false, true, true, true);
                ShowSimpleMessage($"Недостаточно денег для покупки\n{tile.name}!", isFromBot: playerId != 0);
                yield return new WaitWhile(() => isSimplePopupActive);
            }
        }
    }

    // Строительство (автоматическое, сейчас отключено)

    void CheckAndBuildHouses()
    {
        if (currentPlayer != 0) return;

        var groups = new System.Collections.Generic.HashSet<string>();
        foreach (var tile in gameBoard.tileData)
        {
            if (tile.type == TileType.Street && tile.owner == 0)
                groups.Add(tile.colorGroup);
        }

        foreach (string group in groups)
        {
            if (gameBoard.IsColorGroupOwned(0, group))
            {
                TileData cheapest = null;
                foreach (var tile in gameBoard.tileData)
                {
                    if (tile.type == TileType.Street && tile.colorGroup == group && tile.houses < 5)
                    {
                        if (cheapest == null || tile.houseCost < cheapest.houseCost)
                            cheapest = tile;
                    }
                }

                if (cheapest != null && CanAfford(0, cheapest.houseCost))
                {
                    balances[0] -= cheapest.houseCost;
                    cheapest.houses++;
                    UpdateBalanceDisplay();
                    ShowSimpleMessage($"Ты построил дом на:\n{cheapest.name}\nДомов: {cheapest.houses}");
                    break; // один дом за ход
                }
            }
        }
    }

    // Боты

    IEnumerator BotTurn()
    {
        if (isTurnInProgress == false) yield break;

        // Небольшая задержка для естественности
        yield return new WaitForSeconds(1f);

        if (isInJail[currentPlayer])
        {
            if (jailCards[currentPlayer] > 0)
            {
                UseGetOutOfJailCard(currentPlayer);
                yield return new WaitForSeconds(1f);
                isWaitingForDice = true;
                diceRoller.RollDice();
                yield break;
            }
            else if (balances[currentPlayer] >= 50)
            {
                PayToLeaveJail(currentPlayer);
                yield return new WaitForSeconds(1f);
                isWaitingForDice = true;
                diceRoller.RollDice();
                yield break;
            }
            else
            {
                isWaitingForDice = true;
                diceRoller.RollDice();
                yield break;
            }
        }
        else
        {
            isWaitingForDice = true;
            diceRoller.RollDice();
            yield break;
        }
    }

    IEnumerator DelayedEndTurn()
    {
        if (isTurnInProgress == false) yield break;
        yield return new WaitForSeconds(2f);
        EndTurn();
    }

    void EndTurn()
    {
        if (isEndingTurn) return; // защита от двойного вызова
        isEndingTurn = true;

        // CheckAndBuildHouses(); // отключено — сейчас строительство ручное
        currentPlayer = (currentPlayer + 1) % maxPlayers;
        StartCoroutine(CompleteEndTurn());
    }

    IEnumerator CompleteEndTurn()
    {
        yield return null; // дать завершиться всем текущим корутинам
        isTurnInProgress = false;
        isEndingTurn = false;
        StartTurn();
    }

    // Попапы сообщений

    public void ShowSimpleMessage(string message, bool isFromBot = false)
    {
        if (simplePopupPanel != null && simplePopupText != null)
        {
            simplePopupText.text = message;
            simplePopupPanel.SetActive(true);
            isSimplePopupActive = true;
            Time.timeScale = 0f;

            // Автозакрытие только для сообщений от ботов
            if (isFromBot)
                StartCoroutine(AutoCloseBotMessage(2f));
        }
    }

    IEnumerator AutoCloseBotMessage(float delay)
    {
        yield return new WaitForSecondsRealtime(delay);
        if (isSimplePopupActive)
            HideSimplePopup();
    }

    public void HideSimplePopup()
    {
        if (simplePopupPanel != null)
        {
            simplePopupPanel.SetActive(false);
            isSimplePopupActive = false;
            Time.timeScale = 1f;
        }
    }

    // UI баланса

    public void UpdateBalanceDisplay()
    {
        if (balanceDisplay == null) return;

        string text = "";
        for (int i = 0; i < maxPlayers; i++)
        {
            string name = (i == 0) ? "Игрок" : $"Бот";
            text += $"{name} {i + 1}: ${balances[i]}\n";
        }
        balanceDisplay.text = text;
    }

    public void QuitGame()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}