using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>Marks art authored in metres; calibrated door endpoints are inspectable in the prefab.</summary>
    public sealed class BuildingArtMetrics : MonoBehaviour
    {
        public const float PlayerHeight = 1.8f;
        public const float DoorClearance = PlayerHeight * 1.1f;
        [SerializeField] private Vector3 doorBottom;
        [SerializeField] private Vector3 doorTop;
        public float DoorHeight => Vector3.Distance(transform.TransformPoint(doorBottom), transform.TransformPoint(doorTop));
        public Vector3 DoorBottom => transform.TransformPoint(doorBottom);
        public Vector3 DoorTop => transform.TransformPoint(doorTop);
        public void ConfigureDoor(Vector3 bottom, Vector3 top) { doorBottom = bottom; doorTop = top; }
    }
}
