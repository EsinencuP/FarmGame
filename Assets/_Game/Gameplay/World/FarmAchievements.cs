using System;

namespace MyLittleFarm.Gameplay.World
{
    public sealed class FarmAchievement
    {
        public readonly string Id,Name,Description;
        public readonly Func<FarmProgression,bool> IsEarned;
        public FarmAchievement(string id,string name,string description,Func<FarmProgression,bool> earned)
        {Id=id;Name=name;Description=description;IsEarned=earned;}
    }
    public static class FarmAchievements
    {
        public static readonly FarmAchievement[] All =
        {
            new FarmAchievement("first_harvest","Первый урожай","Соберите первую грядку",p=>p.Data.harvests>=1),
            new FarmAchievement("coins_100","100 монет","Накопите 100 монет в кошельке",p=>p.Data.highestBalance>=100),
            new FarmAchievement("plant_10","Первые грядки","Посадите 10 растений",p=>p.Data.plantings>=10),
            new FarmAchievement("plant_100","Зелёные руки","Посадите 100 растений",p=>p.Data.plantings>=100),
            new FarmAchievement("all_crops","На любой вкус","Посадите все 6 культур",p=>p.Data.crops.Count>=6),
            new FarmAchievement("harvest_100","Полные корзины","Соберите 100 единиц урожая",p=>p.Data.harvestedUnits>=100),
            new FarmAchievement("revenue_1000","Успешная ферма","Выручите 1000 монет от продаж",p=>p.Data.revenue>=1000),
            new FarmAchievement("house","Дом, милый дом","Постройте дом",p=>p.HasBuilding("house")),
            new FarmAchievement("storage","Всё по местам","Постройте склад-сундук",p=>p.HasBuilding("storage")),
            new FarmAchievement("all_sectors","Открыть все сектора","Купите последний закрытый сектор",p=>p.AllSectorsOpen),
            new FarmAchievement("orchard","Начало сада","Посадите яблоню",p=>p.TreeCount>0),
            new FarmAchievement("apples_10","Яблочный день","Соберите 10 яблок",p=>p.Data.apples>=10),
            new FarmAchievement("chickens","Первый житель загона","Купите курицу",p=>p.AnimalCount>0),
            new FarmAchievement("eggs_10","Яичная ферма","Соберите 10 яиц",p=>p.Data.eggs>=10),
            new FarmAchievement("shepherd","Нанять пастуха","Наймите помощника у рынка за 80 монет",p=>p.Data.shepherdHired),
            new FarmAchievement("sun_bonus","В нужный час","Соберите урожай с бонусом времени суток",p=>p.Data.bonusHarvests>0),
            new FarmAchievement("rare_seed","Редкая находка","Найдите редкие семена кукурузы",p=>p.Data.rareSeeds>0),
            new FarmAchievement("explorer","Исследователь","Откройте все три тайника семян",p=>p.DiscoveredCaches==3),
            new FarmAchievement("decorator","Ферма с характером","Разместите 5 разных видов декора",p=>p.Data.decorations.Count>=5),
            new FarmAchievement("manor","Полноценная усадьба","Улучшите дом до третьего вида",p=>p.HasManor)
        };
    }
}
