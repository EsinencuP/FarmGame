using System;
using System.Collections.Generic;
using System.Text;

namespace MyLittleFarm.Gameplay.Building
{
    public static class FarmDecorationCatalog
    {
        public const int MaximumSignLength=48;
        public static IReadOnlyList<BuildingDefinition> Items { get; }=Array.AsReadOnly(new[]{
            D("fence_picket","Штакетник",1,1,2,1.1f),D("fence_rails","Жердяной забор",1,1,2,.95f),
            D("fence_cross","Крестовой забор",1,1,3,1.05f),D("fence_corner","Угол забора",1,1,3,1.1f),
            D("gate_rustic","Садовая калитка",2,1,5,1.15f),
            D("lantern_tall","Высокий фонарь",1,1,7,2.2f),D("lantern_low","Садовый фонарь",1,1,5,1.3f),
            D("lantern_double","Двойной фонарь",2,1,10,2.2f),
            D("flower_round","Круглая клумба",1,1,4,.55f),D("flower_raised","Высокая клумба",1,1,5,.75f),
            D("flower_barrel","Цветочная бочка",1,1,5,.95f),D("flower_long","Длинная клумба",2,1,7,.6f),
            D("bench_wood","Садовая скамейка",2,1,6,1f),D("bench_stone","Каменная скамья",2,1,8,.75f),
            D("rock_small","Малый камень",1,1,1,.5f),D("rock_large","Валун",2,2,3,1.05f),
            D("rock_group","Каменная группа",2,1,4,.75f),
            D("well_stone","Каменный колодец",2,2,12,1.2f),D("well_roof","Колодец с крышей",2,2,16,2.4f),
            D("weather_vane","Флюгер",1,1,6,2.4f),D("flag_green","Зелёный флаг",1,1,5,2.3f),
            D("flag_blue","Синий вымпел",1,1,5,2.3f),
            D("sign_post","Указатель фермы",1,1,4,1.5f),D("sign_wide","Большая вывеска",2,1,6,1.6f),
            D("barrel_wood","Деревянная бочка",1,1,3,.85f),D("crate_stack","Стопка ящиков",1,1,3,1.1f),
            D("hay_bale","Тюк сена",1,1,2,.65f),D("wheelbarrow","Садовая тачка",2,1,6,.9f),
            D("watering_can","Лейка",1,1,2,.45f),D("birdhouse","Скворечник",1,1,5,2f)
        });
        private static BuildingDefinition D(string id,string name,int w,int d,int price,float h)
            =>new BuildingDefinition(id,name,w,d,price,h);
        public static bool SupportsText(string id)=>id=="sign_post"||id=="sign_wide";
        public static string HouseLevelName(int level)=>level==0?"Старая хибара":level==1?"Обновлённый домик":"Усадьба";
        public static int HouseUpgradePrice(int level)=>level==0?25:level==1?60:0;
        public static int HouseUpgradeInvestment(int level)=>level==0?0:level==1?25:85;
        public static string CleanText(string text)
        {
            var result=new StringBuilder();
            foreach(var c in text??string.Empty)
            {
                if(char.IsControl(c)||char.IsSurrogate(c))continue;
                if(result.Length>=MaximumSignLength)break;
                result.Append(c);
            }
            return result.ToString().Trim();
        }
        public static bool ValidCustomization(BuildingRuntimeState state)=>state.appearanceLevel>=0
            &&state.appearanceLevel<=(state.definitionId=="house"?2:0)
            &&(SupportsText(state.definitionId)
                ?string.IsNullOrEmpty(state.customText)||state.customText==CleanText(state.customText)
                :string.IsNullOrEmpty(state.customText));
    }
}
