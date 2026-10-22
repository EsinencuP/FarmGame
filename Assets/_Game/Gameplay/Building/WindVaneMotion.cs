using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    public sealed class WindVaneMotion : MonoBehaviour
    {
        [SerializeField] private Transform arrow;
        private void Update(){if(arrow!=null)arrow.localRotation=Quaternion.Euler(0,25f+Mathf.Sin(Time.unscaledTime*.15f)*35f,0);}
    }
}
