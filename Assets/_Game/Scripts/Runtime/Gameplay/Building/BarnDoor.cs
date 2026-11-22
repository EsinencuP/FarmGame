using MyLittleFarm.Core;
using MyLittleFarm.Gameplay.Animals;
using UnityEngine;

namespace MyLittleFarm.Gameplay.Building
{
    /// <summary>Exterior barn door. Its swing stops when the approach is occupied.</summary>
    public sealed class BarnDoor : MonoBehaviour
    {
        private const float Width = 1.18f;
        private const float OpenAngle = 102f;
        private readonly Collider[] _contacts = new Collider[12];
        private Transform _hinge;
        private float _height;
        private float _angle;
        private bool _opening;

        internal static BarnDoor Create(Transform building, BuildingArtMetrics metrics)
        {
            var door = new GameObject("Service door").AddComponent<BarnDoor>();
            door.transform.SetParent(building, false);
            door.transform.localPosition = building.InverseTransformPoint(metrics.DoorBottom) + Vector3.back * 0.026f;
            door._height = metrics.DoorHeight;
            Panel(door.transform, "Doorway", new Vector3(0f, door._height * .5f, 0f),
                new Vector3(Width, door._height, .028f), new Color(.13f, .085f, .055f));
            door._hinge = new GameObject("Door hinge").transform;
            door._hinge.SetParent(door.transform, false);
            door._hinge.localPosition = new Vector3(-Width * .5f, 0f, -.035f);
            Panel(door._hinge, "Door leaf", new Vector3(Width * .5f, door._height * .5f, 0),
                new Vector3(Width, door._height, .055f), new Color(.50f, .29f, .15f), solid: true);
            var brace = new Color(.32f, .18f, .10f);
            for (var i = 1; i <= 2; i++)
                Panel(door._hinge, "Door brace", new Vector3(Width * .5f, door._height * (.17f + i * .22f), -.035f),
                    new Vector3(Width * .92f, .1f, .035f), brace);
            Panel(door._hinge, "Latch", new Vector3(Width * .86f, door._height * .51f, -.055f),
                new Vector3(.09f, .055f, .04f), new Color(.20f, .22f, .24f));
            return door;
        }

        private static void Panel(Transform parent, string name, Vector3 position, Vector3 size, Color color, bool solid = false)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cube);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = size;
            if (!solid) RuntimeMaterials.RemoveCollider(part);
            RuntimeMaterials.Paint(part.GetComponent<Renderer>(), color);
        }

        private bool ReachedFromFront(Vector3 player)
        {
            var relative = transform.InverseTransformPoint(player);
            return relative.z < .12f && new Vector2(relative.x, relative.z).sqrMagnitude <= 2.2f * 2.2f;
        }

        public string Prompt(Vector3 player) => !ReachedFromFront(player) ? "Подойдите к входу в коровник"
            : InputReader.ActionHint + (_opening ? " — закрыть дверь · вход в загон справа" : " — открыть дверь коровника");

        public bool TryToggle(Vector3 player)
        {
            if (!ReachedFromFront(player) || SwingObstructed())
            {
                GameEvents.RaiseStatusChanged(ReachedFromFront(player) ? "Отойдите на шаг, чтобы освободить дверь"
                    : "Подойдите к входу в коровник");
                return false;
            }
            _opening = !_opening;
            return true;
        }

        private bool SwingObstructed()
        {
            var center = transform.TransformPoint(new Vector3(-.12f, _height * .5f, -Width * .51f));
            var count = Physics.OverlapBoxNonAlloc(center, new Vector3(Width * .63f, _height * .5f, Width * .53f),
                _contacts, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            if (count == _contacts.Length) return true;
            for (var i = 0; i < count; i++)
                if (_contacts[i] is CharacterController || _contacts[i].GetComponentInParent<AnimalView>() != null) return true;
            return false;
        }

        private void Update()
        {
            var target = _opening ? OpenAngle : 0f;
            if (Mathf.Approximately(_angle, target) || Time.deltaTime <= 0f || SwingObstructed()) return;
            _angle = Mathf.MoveTowards(_angle, target, 230f * Time.deltaTime);
            _hinge.localRotation = Quaternion.Euler(0f, _angle, 0f);
        }
    }
}
