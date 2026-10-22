using System;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    public sealed class RareSeedCache : MonoBehaviour
    {
        [SerializeField] private string cacheId;
        [SerializeField] private string locationName;
        [SerializeField] private GameObject seedsVisual;
        private FarmProgression _progress;
        private Transform _player;
        private float _nextRefresh;
        public string Id=>cacheId;
        public string LocationName=>locationName;
        public void Configure(FarmProgression progress,Transform player){_progress=progress;_player=player;Refresh();}
        public bool WithinReach(Vector3 position){var delta=position-transform.position;delta.y=0;return delta.sqrMagnitude<=2.4f*2.4f;}
        public bool Collect()=>_progress!=null&&_progress.CollectCache(cacheId);
        public string Prompt
        {
            get
            {
                if(_player==null||!WithinReach(_player.position))return locationName+" — подойдите ближе";
                var remaining=_progress==null?0:_progress.Cache(cacheId).nextAvailableUnixMs-DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                return remaining>0?locationName+" — новые семена через "+Mathf.CeilToInt(remaining/60000f)+" мин"
                    :"E / ЛКМ — найти 2 редких семени кукурузы";
            }
        }
        public void Refresh()
        {
            if(seedsVisual!=null)seedsVisual.SetActive(_progress==null
                ||_progress.Cache(cacheId).nextAvailableUnixMs<=DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
        }
        private void Update(){if(Time.unscaledTime<_nextRefresh)return;_nextRefresh=Time.unscaledTime+1;Refresh();}
    }
}
