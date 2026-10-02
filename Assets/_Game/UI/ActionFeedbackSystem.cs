using System.Collections.Generic;
using MyLittleFarm.Core;
using UnityEngine;

namespace MyLittleFarm.UI
{
    /// <summary>Показывает временные цветовые вспышки действия без готовых VFX-ассетов.</summary>
    public sealed class ActionFeedbackSystem : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] private float duration = 0.55f;
        [SerializeField, Min(0f)] private float riseHeight = 0.7f;
        [SerializeField] private Color plantColor = new Color(0.30f, 0.90f, 0.25f);
        [SerializeField] private Color harvestColor = new Color(1f, 0.82f, 0.28f);
        [SerializeField] private Color saleColor = new Color(1f, 0.92f, 0.12f);
        [SerializeField] private Color collectColor = new Color(0.96f, 0.96f, 0.86f);
        [SerializeField, Range(1, 64)] private int maxVisibleMarkers = 24;

        // Активные маркеры анимируются, а скрытые сферы повторно используются при следующем действии.
        private readonly List<Marker> _markers = new List<Marker>(24);
        private readonly Stack<GameObject> _pool = new Stack<GameObject>(24);

        private struct Marker
        {
            public GameObject view;
            public Vector3 origin;
            public float startedAt;
        }

        private void OnEnable() => GameEvents.ActionFeedback += Show;
        private void OnDisable() => GameEvents.ActionFeedback -= Show;

        private void Show(string action, Vector3 worldPosition)
        {
            // Цвет различает посадку, сбор и продажу; сфера заменяется будущей VFX-моделью.
            if (!Application.isPlaying || _markers.Count >= maxVisibleMarkers) return;
            var color = action == "plant" ? plantColor : action == "harvest" ? harvestColor
                : action == "collect" ? collectColor : saleColor;
            var view = _pool.Count > 0 ? _pool.Pop() : CreateMarkerView();
            view.SetActive(true);
            view.transform.position = worldPosition + Vector3.up * 0.3f;
            view.transform.localScale = Vector3.one * 0.24f;
            RuntimeMaterials.Paint(view.GetComponent<Renderer>(), color);
            _markers.Add(new Marker { view = view, origin = view.transform.position, startedAt = Time.unscaledTime });
        }

        /// <summary>Создаёт одну сферу без коллайдера, которую затем хранит пул эффектов.</summary>
        private GameObject CreateMarkerView()
        {
            var view = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            view.name = "Action Feedback";
            view.transform.SetParent(transform, true);
            RuntimeMaterials.RemoveCollider(view);
            return view;
        }

        private void Update()
        {
            // Несколько одновременных вспышек обновляются одним компонентом и удаляются по истечении времени.
            for (var index = _markers.Count - 1; index >= 0; index--)
            {
                var marker = _markers[index];
                var progress = (Time.unscaledTime - marker.startedAt) / duration;
                if (progress >= 1f)
                {
                    marker.view.SetActive(false);
                    _pool.Push(marker.view);
                    _markers.RemoveAt(index);
                    continue;
                }
                marker.view.transform.position = marker.origin + Vector3.up * (riseHeight * progress);
                marker.view.transform.localScale = Vector3.one * (0.24f * (1f - progress));
            }
        }
    }
}
