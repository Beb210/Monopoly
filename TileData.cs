// TileData.cs
// Данные одной клетки поля: название, тип, цена, аренда, владелец, дома
using System;

[Serializable]
public class TileData
{
    public string name;
    public TileType type;

    // Для улиц
    public int price = 0;
    public string colorGroup = "";
    public int houseCost = 0;
    public int[] rent = new int[6]; // [0] база, [1-4] — с домами, [5] — отель
    public int owner = -1;
    public int houses = 0;

    // Для налогов
    public int taxAmount = 0;

    // Для ЖД и коммуналок
    public int rentBase = 0;

    public TileData(string name, TileType type)
    {
        this.name = name;
        this.type = type;
    }

    // Расчёт текущей аренды с учётом количества домов и монополии
    public int GetRent(GameBoard board, int diceRoll = 0)
    {
        if (type == TileType.Street)
        {
            if (houses == 0)
            {
                int baseRent = rent[0];
                // х2 если монополия
                if (IsMonopoly(board))
                    return baseRent * 2;
                return baseRent;
            }
            else if (houses <= 4)
            {
                return rent[houses];
            }
            else
            {
                return rent[5]; // отель
            }
        }
        else if (type == TileType.Railway)
        {
            int owned = board.CountOwnedTilesByPlayer(owner, TileType.Railway);
            return rentBase * (1 << (owned - 1)); // 25, 50, 100, 200
        }
        else if (type == TileType.Utility)
        {
            if (diceRoll == 0) return 0;
            int owned = board.CountOwnedTilesByPlayer(owner, TileType.Utility);
            return diceRoll * (owned == 1 ? 4 : 10);
        }
        else if (type == TileType.Tax)
        {
            return taxAmount;
        }

        return 0;
    }

    private bool IsMonopoly(GameBoard board)
    {
        return owner != -1 && board.IsColorGroupOwned(owner, colorGroup);
    }
}