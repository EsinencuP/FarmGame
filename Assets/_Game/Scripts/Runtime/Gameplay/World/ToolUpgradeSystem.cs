using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Economy;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Постоянная мотыга: одна клетка, полоса 1×3, квадрат 3×3.</summary>
    [DefaultExecutionOrder(-85)]
    public sealed class ToolUpgradeSystem : MonoBehaviour
    {
        [SerializeField, Min(0)] private int firstUpgradePrice = 15;
        [SerializeField, Min(0)] private int secondUpgradePrice = 35;
        [SerializeField, Min(0.5f)] private float baseReach = 2.2f;

        // Уровень 0 является базовым, уровни 1 и 2 покупаются последовательно.
        private WalletSystem _wallet;
        public int Level { get; private set; }
        public bool HasBaseHoe {get;private set;}
        public string AreaLabel=>Level==0?"1 клетка":Level==1?"1×3":"3×3";
        public float Reach => baseReach;
        public int NextPrice => Level == 0 ? firstUpgradePrice : Level == 1 ? secondUpgradePrice : 0;

        /// <summary>Новая ферма получает постоянную мотыгу; рынок продаёт следующие уровни.</summary>
        public void Configure(InputReader_AI input, WalletSystem wallet)
        {
            _wallet = wallet;
            Level = 0;
            HasBaseHoe=true;
        }

        /// <summary>Покупает только следующий уровень и сообщает о нехватке средств.</summary>
        public bool TryUpgrade()
        {
            if (Level >= 2) return false;
            var price = NextPrice;
            if (!_wallet.TrySpend(price))
            {
                GameEvents.RaiseStatusChanged("Недостаточно монет для улучшения");
                return false;
            }
            Level++;
            MyLittleFarm.Audio.FarmAudio.PlayAtPlayer(MyLittleFarm.Audio.SoundId.ToolUpgrade);
            GameEvents.RaiseInventoryChanged();
            GameEvents.RaiseStatusChanged($"Мотыга улучшена: {AreaLabel}");
            return true;
        }

        /// <summary>Восстанавливает проверенный уровень из сохранения.</summary>
        public void Restore(int level,bool hasBaseHoe=true)
        {
            Level = Mathf.Clamp(level, 0, 2);
            HasBaseHoe=hasBaseHoe;
            GameEvents.RaiseInventoryChanged();
        }
    }
}
