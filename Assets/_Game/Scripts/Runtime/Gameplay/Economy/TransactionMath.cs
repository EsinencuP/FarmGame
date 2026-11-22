namespace MyLittleFarm.Gameplay.Economy
{
    /// <summary>Проверяет арифметику покупок и продаж без зависимостей от Unity.</summary>
    public static class TransactionMath
    {
        /// <summary>Вычисляет стоимость партии до изменения денег или инвентаря.</summary>
        public static bool TryPurchase(int unitPrice, int amount, int currentStock, int balance, out int cost)
        {
            cost = 0;
            if (unitPrice < 0 || amount <= 0 || currentStock < 0 || balance < 0
                || currentStock > int.MaxValue - amount) return false;
            var product = (long)unitPrice * amount;
            if (product > balance) return false;
            cost = (int)product;
            return true;
        }

        /// <summary>Добавляет стоимость строки продажи, не переполняя кошелёк или long.</summary>
        public static bool TryAddSale(int unitPrice, int amount, int balance, long runningTotal,
            out long nextTotal)
        {
            nextTotal = runningTotal;
            if (unitPrice < 0 || amount < 0 || balance < 0 || runningTotal < 0
                || runningTotal > int.MaxValue - balance) return false;
            var remaining = int.MaxValue - balance - runningTotal;
            if (unitPrice > 0 && amount > remaining / unitPrice) return false;
            nextTotal += (long)unitPrice * amount;
            return true;
        }
    }
}
