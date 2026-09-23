// Card.cs
// Содержит информацию о вытягиваемых карточках
using System;

[Serializable]
public class Card
{
    public string text;
    public CardType type;
    
    // Параметры для разных типов карт
    public int targetTile = -1; // Для перемещения
    public int amount = 0;      // Для денег
    public bool isGetOutOfJail = false; // Для карты выхода из тюрьмы
}

public enum CardType
{
    CollectMoney,      // Получить деньги
    PayMoney,          // Заплатить
    MoveToTile,        // Переместиться
    GoToJail,          // Отправиться в тюрьму
    GetOutOfJailCard,  // Получить карту выхода
    StealFromOthers    // Украсть у всех
}