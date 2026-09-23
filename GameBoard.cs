// GameBoard.cs
// Основная логика игрового поля: данные клеток, карты, покупка, информация о клетках
using UnityEngine;
using TMPro;
using UnityEngine.InputSystem;

public class GameBoard : MonoBehaviour
{
    [Header("Ссылки на клетки (0..39)")]
    public Transform[] tiles;

    [Header("Данные клеток")]
    public TileData[] tileData;

    [Header("Маркеры владельцев (0=синий, 1=красный, 2=жёлтый, 3=зелёный)")]
    public GameObject[] ownerMarkers;

    [Header("UI подсказки при клике")]
    public GameObject tileInfoPopup;
    public TMP_Text tileInfoText;

    // Карты
    public Card[] chanceCards;
    public Card[] communityChestCards;

    private System.Random random = new System.Random();
    private bool isTileInfoActive = false;

    private void Awake()
    {
        if (tiles == null || tiles.Length != 40)
        {
            Debug.LogError("GameBoard: Нужно ровно 40 клеток!");
            return;
        }

        InitializeTileData();
        InitializeCards();
        HideTileInfo();
    }

    // Инициализация данных всех 40 клеток поля
    // ???: вынести в ScriptableObject или JSON, чтобы не хардкодить
    void InitializeTileData()
    {
        tileData = new TileData[40];

        tileData[0]  = new TileData("Вперед", TileType.Start);
        tileData[1]  = new TileData("Житная ул.", TileType.Street) { price = 60, colorGroup = "Brown", houseCost = 50, rent = new int[] { 2, 10, 30, 90, 160, 250 } };
        tileData[2]  = new TileData("Обзественная казна", TileType.CommunityChest);
        tileData[3]  = new TileData("Нагатинская ул.", TileType.Street) { price = 60, colorGroup = "Brown", houseCost = 50, rent = new int[] { 4, 20, 60, 180, 320, 450 } };
        tileData[4]  = new TileData("Подоходный налог", TileType.Tax) { taxAmount = 200 };
        tileData[5]  = new TileData("Рижская железная дорога", TileType.Railway) { price = 200, rentBase = 25 };
        tileData[6]  = new TileData("Варшавчкое шоссе", TileType.Street) { price = 100, colorGroup = "Light Blue", houseCost = 50, rent = new int[] { 6, 30, 90, 270, 400, 550 } };
        tileData[7]  = new TileData("Шанс", TileType.Chance);
        tileData[8]  = new TileData("ул. Огарева", TileType.Street) { price = 100, colorGroup = "Light Blue", houseCost = 50, rent = new int[] { 6, 30, 90, 270, 400, 550 } };
        tileData[9]  = new TileData("Первая парковая ул.", TileType.Street) { price = 120, colorGroup = "Light Blue", houseCost = 50, rent = new int[] { 8, 40, 100, 300, 450, 600 } };
        tileData[10] = new TileData("Тюрьма / Просто посетитель", TileType.JailVisit);
        tileData[11] = new TileData("ул. Полянка", TileType.Street) { price = 140, colorGroup = "Pink", houseCost = 100, rent = new int[] { 10, 50, 150, 450, 625, 750 } };
        tileData[12] = new TileData("Электростанция", TileType.Utility) { price = 150 };
        tileData[13] = new TileData("ул. Сретенка", TileType.Street) { price = 140, colorGroup = "Pink", houseCost = 100, rent = new int[] { 10, 50, 150, 450, 625, 750 } };
        tileData[14] = new TileData("Ростовская наб.", TileType.Street) { price = 160, colorGroup = "Pink", houseCost = 100, rent = new int[] { 12, 60, 180, 500, 700, 900 } };
        tileData[15] = new TileData("Курская железная дорога", TileType.Railway) { price = 200, rentBase = 25 };
        tileData[16] = new TileData("Рязанский проспект", TileType.Street) { price = 180, colorGroup = "Orange", houseCost = 100, rent = new int[] { 14, 70, 200, 550, 750, 950 } };
        tileData[17] = new TileData("Общественная казна", TileType.CommunityChest);
        tileData[18] = new TileData("ул. Вавилова", TileType.Street) { price = 180, colorGroup = "Orange", houseCost = 100, rent = new int[] { 14, 70, 200, 550, 750, 950 } };
        tileData[19] = new TileData("Рублевское шоссе", TileType.Street) { price = 200, colorGroup = "Orange", houseCost = 100, rent = new int[] { 16, 80, 220, 600, 800, 1000 } };
        tileData[20] = new TileData("Бесплатная стоянка", TileType.FreeParking);
        tileData[21] = new TileData("ул. Тверская", TileType.Street) { price = 220, colorGroup = "Red", houseCost = 150, rent = new int[] { 18, 90, 250, 700, 875, 1050 } };
        tileData[22] = new TileData("Шанс", TileType.Chance);
        tileData[23] = new TileData("Пушкинская ул.", TileType.Street) { price = 220, colorGroup = "Red", houseCost = 150, rent = new int[] { 18, 90, 250, 700, 875, 1050 } };
        tileData[24] = new TileData("Площадь Маяковского", TileType.Street) { price = 240, colorGroup = "Red", houseCost = 150, rent = new int[] { 20, 100, 300, 750, 925, 1100 } };
        tileData[25] = new TileData("Казанская железная дорога", TileType.Railway) { price = 200, rentBase = 25 };
        tileData[26] = new TileData("ул. Грузинский Вал", TileType.Street) { price = 260, colorGroup = "Yellow", houseCost = 150, rent = new int[] { 22, 110, 330, 800, 975, 1150 } };
        tileData[27] = new TileData("ул. Чайковского", TileType.Street) { price = 260, colorGroup = "Yellow", houseCost = 150, rent = new int[] { 22, 110, 330, 800, 975, 1150 } };
        tileData[28] = new TileData("Водопровод", TileType.Utility) { price = 150 };
        tileData[29] = new TileData("Смоленская площадь", TileType.Street) { price = 280, colorGroup = "Yellow", houseCost = 150, rent = new int[] { 24, 120, 360, 850, 1025, 1200 } };
        tileData[30] = new TileData("Отправляйтесь в тюрьму!", TileType.GoToJail);
        tileData[31] = new TileData("ул. Щусева", TileType.Street) { price = 300, colorGroup = "Green", houseCost = 200, rent = new int[] { 26, 130, 390, 900, 1100, 1275 } };
        tileData[32] = new TileData("Гоголевский бульвар", TileType.Street) { price = 300, colorGroup = "Green", houseCost = 200, rent = new int[] { 26, 130, 390, 900, 1100, 1275 } };
        tileData[33] = new TileData("Общественная казна", TileType.CommunityChest);
        tileData[34] = new TileData("Кутузовский проспект", TileType.Street) { price = 320, colorGroup = "Green", houseCost = 200, rent = new int[] { 28, 150, 450, 1000, 1200, 1400 } };
        tileData[35] = new TileData("Ленинградская железная дорога", TileType.Railway) { price = 200, rentBase = 25 };
        tileData[36] = new TileData("Шанс", TileType.Chance);
        tileData[37] = new TileData("ул. Малая Бронная", TileType.Street) { price = 350, colorGroup = "Dark Blue", houseCost = 200, rent = new int[] { 35, 175, 500, 1100, 1300, 1500 } };
        tileData[38] = new TileData("Сверхналог", TileType.Tax) { taxAmount = 100 };
        tileData[39] = new TileData("ул. Арбат", TileType.Street) { price = 400, colorGroup = "Dark Blue", houseCost = 200, rent = new int[] { 50, 200, 600, 1400, 1700, 2000 } };
    }

    void InitializeCards()
    {
        chanceCards = new Card[]
        {
            new Card { text = "Пройдите на «Вперёд»!", type = CardType.MoveToTile, targetTile = 0 },
            new Card { text = "Отправляйтесь в тюрьму!", type = CardType.GoToJail },
            new Card { text = "Получите карту «Освобождение из тюрьмы»", type = CardType.GetOutOfJailCard, isGetOutOfJail = true },
            new Card { text = "Банк платит вам дивиденды $50", type = CardType.CollectMoney, amount = 50 },
            new Card { text = "Вы проиграли конкурс. Заплатите $15", type = CardType.PayMoney, amount = 15 }
        };

        communityChestCards = new Card[]
        {
            new Card { text = "Вы выиграли конкурс красоты! Получите $10", type = CardType.CollectMoney, amount = 10 },
            new Card { text = "Уплата страховки $50", type = CardType.PayMoney, amount = 50 },
            new Card { text = "Получите карту «Освобождение из тюрьмы»", type = CardType.GetOutOfJailCard, isGetOutOfJail = true },
            new Card { text = "Отправляйтесь на поле «Вперёд»!", type = CardType.MoveToTile, targetTile = 0 },
            new Card { text = "Ошибка в банке в вашу пользу. Получите $200", type = CardType.CollectMoney, amount = 200 }
        };
    }

    // Показывает всплывающее окно с информацией о клетке
    public void ShowTileInfo(TileData tile)
    {
        if (tileInfoPopup == null || tileInfoText == null)
        {
            Debug.LogError("ShowTileInfo получил null!");
            return;
        }

        Debug.Log($"Показ информации для: {tile.name}, тип: {tile.type}, цена: {tile.price}");

        string info = $"<b>{tile.name}</b>\n\n"; // Название + 2 пустые строки

        switch (tile.type)
        {
            case TileType.Street:
                info += $"Стоимость: ${tile.price}\n";
                if (tile.owner == -1)
                {
                    info += "Свободна";
                }
                else
                {
                    string ownerName = (tile.owner == 0) ? "Игрок" : $"Бот {tile.owner + 1}";
                    info += $"Владелец: {ownerName}\n";
                    info += $"Аренда: ${tile.rent[0]}\n";

                    if (tile.houses > 0)
                    {
                        if (tile.houses == 5)
                            info += $"Отель: ${tile.rent[5]}\n";
                        else
                            info += $"Домов: {tile.houses}\n";

                        info += $"Аренда с домами: ${tile.rent[tile.houses]}\n";
                    }
                    info += $"Строить дом: ${tile.houseCost}";
                }
                break;

            case TileType.Railway:
                info += $"Стоимость: ${tile.price}\n";
                if (tile.owner == -1)
                {
                    info += "Свободна";
                }
                else
                {
                    string ownerName = (tile.owner == 0) ? "Игрок" : $"Бот {tile.owner + 1}";
                    info += $"Владелец: {ownerName}\n";
                    int owned = CountOwnedTilesByPlayer(tile.owner, TileType.Railway);
                    // Аренда ЖД удваивается за каждую дополнительную станцию: 25, 50, 100, 200
                    info += $"Аренда: ${tile.rentBase * (1 << (owned - 1))}\n";
                    info += $"Всего станций у владельца: {owned}";
                }
                break;

            case TileType.Utility:
                info += $"Стоимость: ${tile.price}\n";
                if (tile.owner == -1)
                {
                    info += "Свободна";
                }
                else
                {
                    string ownerName = (tile.owner == 0) ? "Игрок" : $"Бот {tile.owner + 1}";
                    info += $"Владелец: {ownerName}\n";
                    int owned = CountOwnedTilesByPlayer(tile.owner, TileType.Utility);
                    // Если одна — ×4, если обе — ×10 от суммы кубиков
                    info += $"Аренда: кубик × {(owned == 1 ? 4 : 10)}";
                }
                break;

            case TileType.Tax:
                info += $"Налог: ${tile.taxAmount}";
                break;
            case TileType.GoToJail:
                info += "Отправляйтесь в тюрьму!";
                break;
            case TileType.Start:
                info += "Получите $200 за проход!";
                break;
            case TileType.FreeParking:
                info += "Бесплатная стоянка";
                break;
            case TileType.JailVisit:
                info += "Просто в гости (тюрьма)";
                break;
            case TileType.Chance:
                info += "Вытяните карту «Шанс»";
                break;
            case TileType.CommunityChest:
                info += "Вытяните карту «Общественная казна»";
                break;
            default:
                info += "Специальная клетка";
                break;
        }

        Debug.Log($"Установленный текст: {info}");
        tileInfoText.text = info;
        tileInfoPopup.SetActive(true);
        isTileInfoActive = true;
    }

    public void HideTileInfo()
    {
        tileInfoPopup?.SetActive(false);
        isTileInfoActive = false;
    }

    public bool IsTileInfoActive() => isTileInfoActive;

    // Вспомогательные методы

    public Vector3 GetTilePosition(int tileIndex) => tiles[NormalizeTileIndex(tileIndex)].position;
    public TileData GetTileData(int tileIndex) => tileData[NormalizeTileIndex(tileIndex)];

    // Нормализация индекса с учётом перехода через 0
    public int NormalizeTileIndex(int index) => ((index % 40) + 40) % 40;

    public bool IsColorGroupOwned(int playerId, string colorGroup)
    {
        if (string.IsNullOrEmpty(colorGroup)) return false;

        foreach (var tile in tileData)
        {
            if (tile.type == TileType.Street && tile.colorGroup == colorGroup && tile.owner != playerId)
                return false;
        }
        return true;
    }

    public int CountOwnedTilesByPlayer(int playerId, TileType type)
    {
        int count = 0;
        foreach (var tile in tileData)
        {
            if (tile.type == type && tile.owner == playerId)
                count++;
        }
        return count;
    }

    // Устанавливает владельца клетки и спавн маркера
    public void SetTileOwner(int tileIndex, int playerId)
    {
        tileIndex = NormalizeTileIndex(tileIndex);
        if (tileIndex < 0 || tileIndex >= tileData.Length) return;

        tileData[tileIndex].owner = playerId;

        // Чистим владельца (если был)
        foreach (Transform child in tiles[tileIndex])
        {
            if (child.CompareTag("OwnerMarker")) Destroy(child.gameObject);
        }

        bool isBuyable = (tileData[tileIndex].type == TileType.Street ||
                          tileData[tileIndex].type == TileType.Railway ||
                          tileData[tileIndex].type == TileType.Utility);

        if (playerId >= 0 && isBuyable && playerId < ownerMarkers.Length && ownerMarkers[playerId] != null)
        {
            GameObject marker = Instantiate(ownerMarkers[playerId], tiles[tileIndex]);
            marker.tag = "OwnerMarker";
            marker.transform.localPosition = new Vector3(0f, 1f, 0f); 
            marker.transform.localRotation = Quaternion.identity;
            marker.transform.localScale = Vector3.one * 0.3f;
        }
    }

    public Card DrawChanceCard() => chanceCards?.Length > 0 ? chanceCards[random.Next(chanceCards.Length)] : new Card();
    public Card DrawCommunityChestCard() => communityChestCards?.Length > 0 ? communityChestCards[random.Next(communityChestCards.Length)] : new Card();
}