using System.Collections.Generic;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyLittleFarm.Gameplay.World
{
    public sealed class FarmAmbientLife : MonoBehaviour
    {
        private sealed class Flight
        {
            public Transform root;
            public Transform left;
            public Transform right;
            public Vector3 anchor;
            public float phase;
        }

        private readonly List<Flight> _butterflies = new List<Flight>();
        private readonly List<Flight> _birds = new List<Flight>();
        private GridSystem _grid;
        private Transform _player;
        private DayNightCycle _clock;
        private WeatherController_AI _weather;
        private ParticleSystem _flies;
        private Mesh _butterflyWing;
        private Mesh _birdWing;
        private Material _flyMaterial;

        public static void Create(Transform farm, GridSystem grid, Transform player, Vector2Int penMin, Vector2Int penMax)
        {
            var clock = farm.GetComponentInChildren<DayNightCycle>(true);
            var materials = farm.GetComponent<RuntimeMaterials>();
            if (clock == null || materials == null) return;
            var life = new GameObject("Farm ambient life").AddComponent<FarmAmbientLife>();
            life.transform.SetParent(farm, false);
            life._grid = grid;
            life._player = player;
            life._clock = clock;
            life._weather = farm.GetComponentInChildren<WeatherController_AI>(true);
            life._butterflyWing = Wing("Butterfly wing", new Vector3(.11f, 0f, .10f),
                new Vector3(.20f, 0f, .035f), new Vector3(.12f, 0f, -.08f));
            life._birdWing = Wing("Bird wing", new Vector3(.23f, 0f, .09f),
                new Vector3(.49f, 0f, -.06f), new Vector3(.19f, 0f, -.14f));

            var amber = materials.Get(new Color(.92f, .62f, .30f));
            var blue = materials.Get(new Color(.48f, .68f, .79f));
            // Travel along the meadow and orchard edges, one insect per route.
            var anchors = new[] { new Vector2(2.2f, -1.5f), new Vector2(7.1f, 7.4f), new Vector2(-11.4f, 10.0f) };
            for (var i = 0; i < anchors.Length; i++)
            {
                var p = anchors[i];
                var center = new Vector3(p.x, 0f, p.y);
                if (!grid.IsWalkable(grid.WorldToGrid(center))) continue;
                var insect = life.CreateFlight("Butterfly", center, i * 2.1f, life._butterflyWing, i == 1 ? blue : amber);
                insect.root.localScale = Vector3.one * .82f;
                life._butterflies.Add(insect);
            }

            if (grid.IsChunkLoaded(grid.GridToChunk(new Vector2Int(-17, 18))))
            {
                var white = materials.Get(new Color(.74f, .77f, .76f));
                for (var i = 0; i < 2; i++)
                    life._birds.Add(life.CreateFlight("Bird", new Vector3(-i * .62f, i * .21f, (i % 2 == 0 ? -1 : 1) * i * .52f),
                        i * .5f, life._birdWing, white));
            }
            life.CreateFlies((grid.GridToWorld(penMin) + grid.GridToWorld(penMax)) * .5f,
                (Vector2)(penMax - penMin) * grid.CellSize);
        }

        private Flight CreateFlight(string name, Vector3 anchor, float phase, Mesh wing, Material material)
        {
            var body = new GameObject(name).transform;
            body.SetParent(transform, false);
            body.gameObject.layer = 2;
            var left = AddWing(body, wing, material);
            var right = AddWing(body, wing, material);
            right.localScale = new Vector3(-1f, 1f, 1f);
            body.gameObject.SetActive(false);
            return new Flight { root = body, left = left, right = right, anchor = anchor, phase = phase };
        }

        private static Transform AddWing(Transform parent, Mesh mesh, Material material)
        {
            var go = new GameObject("Wing", typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.layer = 2;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return go.transform;
        }

        private static Mesh Wing(string name, Vector3 shoulder, Vector3 tip, Vector3 tail)
        {
            var mesh = new Mesh { name = name, hideFlags = HideFlags.DontSave };
            mesh.vertices = new[] { Vector3.zero, shoulder, tip, tail, Vector3.zero, shoulder, tip, tail };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3, 4, 6, 5, 4, 7, 6 };
            mesh.RecalculateNormals();
            return mesh;
        }

        private void CreateFlies(Vector3 ground, Vector2 penSize)
        {
            var go = new GameObject("Flies near the pen");
            go.transform.SetParent(transform, false);
            go.transform.position = ground + Vector3.up * .86f;
            go.layer = 2;
            _flies = go.AddComponent<ParticleSystem>();
            _flies.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = _flies.main;
            main.playOnAwake = false;
            main.maxParticles = 5;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.9f, 3.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(.08f, .2f);
            main.startSize = new ParticleSystem.MinMaxCurve(.022f, .035f);
            main.startColor = new Color(.25f, .19f, .12f, .7f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 0;
            var shape = _flies.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(Mathf.Max(1f, penSize.x * .8f), .55f, Mathf.Max(1f, penSize.y * .8f));
            var emission = _flies.emission;
            emission.rateOverTime = 1.25f;
            var noise = _flies.noise;
            noise.enabled = true;
            noise.strength = .28f;
            noise.frequency = 1.05f;
            var renderer = go.GetComponent<ParticleSystemRenderer>();
            _flyMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            renderer.sharedMaterial = _flyMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            go.SetActive(false);
        }

        private void Update()
        {
            var daytime = _clock.Hour >= 6.5f && _clock.Hour <= 18.4f;
            var dry = _weather == null || _weather.RainIntensity < .55f;
            var t = Time.time;
            foreach (var insect in _butterflies)
            {
                var active = daytime && dry && NearPlayer(insect.anchor, 19f);
                insect.root.gameObject.SetActive(active);
                if (!active) continue;
                var u = t * .62f + insect.phase;
                var position = insect.anchor + new Vector3(Mathf.Sin(u) * 1.9f, 0f, Mathf.Sin(u * .64f + insect.phase) * 1.25f);
                position.y = _grid.GetSurfaceHeight(position.x, position.z) + .85f + .26f * Mathf.Sin(u * 2.6f);
                var direction = new Vector3(Mathf.Cos(u) * 1.9f, .15f * Mathf.Cos(u * 2.6f), .8f * Mathf.Cos(u * .64f + insect.phase));
                Animate(insect, position, direction, 24f, 58f);
            }
            var fliesActive = daytime && dry && NearPlayer(_flies.transform.position, 13f);
            if (fliesActive != _flies.gameObject.activeSelf)
            {
                _flies.gameObject.SetActive(fliesActive);
                if (fliesActive) _flies.Play();
            }
            var sweep = Mathf.Repeat(t + 4f, 38f) / 13f;
            var flight = Vector3.Lerp(new Vector3(-30f, 6.2f, 12f), new Vector3(23f, 10f, 24f), sweep);
            var flightVisible = daytime && dry && sweep > .08f && sweep < .94f && NearPlayer(flight, 30f);
            foreach (var bird in _birds)
            {
                bird.root.gameObject.SetActive(flightVisible);
                if (flightVisible) Animate(bird, flight + bird.anchor, new Vector3(1f, .04f, .23f), 13f, 33f);
            }
        }

        private bool NearPlayer(Vector3 center, float distance)
        {
            var delta = _player.position - center;
            delta.y = 0f;
            return delta.sqrMagnitude < distance * distance;
        }

        private static void Animate(Flight flight, Vector3 position, Vector3 forward, float rate, float amount)
        {
            var rotation = forward.sqrMagnitude > .001f ? Quaternion.LookRotation(forward) : flight.root.rotation;
            flight.root.SetPositionAndRotation(position, rotation);
            var flap = Mathf.Sin(Time.time * rate + flight.phase) * amount;
            flight.left.localRotation = Quaternion.Euler(0f, 0f, flap);
            flight.right.localRotation = Quaternion.Euler(0f, 0f, -flap);
        }

        private void OnDestroy()
        {
            Destroy(_butterflyWing);
            Destroy(_birdWing);
            Destroy(_flyMaterial);
        }
    }
}
