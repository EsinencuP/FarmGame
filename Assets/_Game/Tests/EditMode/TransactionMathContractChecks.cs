using System;
using MyLittleFarm.Gameplay.Economy;

namespace MyLittleFarm.Tests.EditMode
{
    /// <summary>Проверяет настоящие расчёты покупок и продаж без Unity Editor.</summary>
    public static class TransactionMathContractChecks
    {
        /// <summary>Запускает граничные и повторяемые сценарии экономики.</summary>
        public static int Run()
        {
            // Счётчик показывает, сколько условий было проверено фактически.
            var assertions = 0;
            Action<bool, string> check = (condition, message) =>
            {
                assertions++;
                if (!condition) throw new InvalidOperationException(message);
            };

            check(TransactionMath.TryPurchase(3, 4, 8, 12, out var cost) && cost == 12,
                "Purchase accepts exact balance");
            check(!TransactionMath.TryPurchase(3, 4, 8, 11, out cost) && cost == 0,
                "Purchase rejects insufficient balance");
            check(TransactionMath.TryPurchase(0, 1, 0, 0, out cost) && cost == 0,
                "Free catalog item remains possible");
            check(!TransactionMath.TryPurchase(1, 2, int.MaxValue - 1, 10, out cost),
                "Purchase rejects stock overflow");
            check(!TransactionMath.TryPurchase(int.MaxValue, int.MaxValue, 0, int.MaxValue, out cost),
                "Purchase rejects large product");
            check(!TransactionMath.TryPurchase(-1, 1, 0, 10, out cost), "Negative price rejected");
            check(!TransactionMath.TryPurchase(1, 0, 0, 10, out cost), "Zero quantity rejected");
            check(!TransactionMath.TryPurchase(1, 1, -1, 10, out cost), "Negative stock rejected");
            check(!TransactionMath.TryPurchase(1, 1, 0, -1, out cost), "Negative balance rejected");

            check(TransactionMath.TryAddSale(5, 2, 10, 0, out var total) && total == 10,
                "First sale line added");
            check(TransactionMath.TryAddSale(7, 3, 10, total, out var next) && next == 31,
                "Second sale line added");
            check(TransactionMath.TryAddSale(1, 1, int.MaxValue - 1, 0, out next) && next == 1,
                "Sale accepts exact wallet limit");
            check(!TransactionMath.TryAddSale(1, 2, int.MaxValue - 1, 0, out next) && next == 0,
                "Sale rejects wallet overflow");
            check(!TransactionMath.TryAddSale(int.MaxValue, int.MaxValue, 0, 0, out next),
                "Sale rejects large product");
            check(!TransactionMath.TryAddSale(1, 1, 0, int.MaxValue, out next),
                "Sale rejects running total overflow");
            check(!TransactionMath.TryAddSale(-1, 1, 0, 0, out next), "Negative sale price rejected");
            check(!TransactionMath.TryAddSale(1, -1, 0, 0, out next), "Negative sale quantity rejected");
            check(TransactionMath.TryAddSale(0, 0, 0, 0, out next) && next == 0,
                "Empty sale line remains harmless");

            // Фиксированная последовательность проверяет тысячи сочетаний цены, количества и денег.
            var random = new Random(1729);
            for (var index = 0; index < 1000; index++)
            {
                var price = random.Next(0, 100000);
                var amount = random.Next(1, 100000);
                var stock = random.Next(0, int.MaxValue);
                var balance = random.Next(0, int.MaxValue);
                var expectedCost = (long)price * amount;
                var expectedPurchase = stock <= int.MaxValue - amount && expectedCost <= balance;
                var actualPurchase = TransactionMath.TryPurchase(price, amount, stock, balance, out cost);
                check(actualPurchase == expectedPurchase, "Purchase agrees with wide arithmetic");
                if (actualPurchase) check(cost == expectedCost, "Purchase cost agrees with wide arithmetic");

                var running = random.Next(0, int.MaxValue - balance);
                var expectedTotal = running + expectedCost;
                var expectedSale = expectedTotal <= int.MaxValue - balance;
                var actualSale = TransactionMath.TryAddSale(price, amount, balance, running, out next);
                check(actualSale == expectedSale, "Sale agrees with wide arithmetic");
                if (actualSale) check(next == expectedTotal, "Sale total agrees with wide arithmetic");
            }
            return assertions;
        }
    }
}
