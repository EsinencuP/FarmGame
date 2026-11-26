using System;
using MyLittleFarm.Core;
using MyLittleFarm.Core.Grid;
using MyLittleFarm.Gameplay.Economy;
using MyLittleFarm.UI;
using UnityEngine;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>A single shore interaction whose catch cooldown follows the farm slot.</summary>
    public sealed class FishPond : MonoBehaviour
    {
        public const string FishId = "fish";
        public const int SellPrice = 10;
        internal const long MaxCatchTime = 253402300799999L;
        private const long CatchCooldownMs = 25_000;
        private const float Reach = 1.8f;
        private const float BiteDuration = 4f;
        private enum Phase { Idle, Waiting, Fighting, Reeling }
        private InventorySystem_AI _inventory;
        private InputReader_AI _input;
        private Transform _player;
        private IsometricCameraController _camera;
        private Transform _hudParent;
        private Font _hudFont;
        private FishingHUD _hud;
        private Transform _bobber;
        private FishingRodView _rod;
        private FishingChallenge _challenge;
        private Vector3 _waterPoint;
        private readonly Transform[] _fish = new Transform[3];
        private readonly System.Random _random = new();
        private LineRenderer _ripples;
        private Material _rippleMaterial;
        private float _biteAt;
        private bool _biteAnnounced;
        private Phase _phase;
        public long NextCatchAtUnixMs { get; private set; }
        internal bool IsFishing => _phase != Phase.Idle;
        private bool BobberInWater => _phase is Phase.Waiting or Phase.Fighting;

        internal static FishPond Create(Transform parent, GridSystem grid, InventorySystem_AI inventory,
            InputReader_AI input, PlayerController player, IsometricCameraController camera, Transform hudParent, Font hudFont)
        {
            var shore = new Vector2Int(-13, 8);
            var water = new Vector2Int(-15, 8);
            var bank = grid.GetTerrainCell(shore);
            if (bank.type is CellType.Water or CellType.Rock or CellType.OutOfBounds
                || !bank.Allows(CellPermissions.Walk, true) || grid.GetTerrainCell(water).type != CellType.Water) return null;
            var station = new GameObject("Fishing at West Pond");
            station.transform.SetParent(parent, false);
            station.transform.position = grid.GridToWorld(shore);
            var pond = station.AddComponent<FishPond>();
            pond._inventory = inventory;
            pond._input = input;
            pond._player = player.transform;
            pond._camera = camera;
            pond._hudParent = hudParent;
            pond._hudFont = hudFont;
            pond._waterPoint = grid.GridToWorld(water);
            pond._waterPoint.y = -0.205f;
            var target = station.AddComponent<InteractionTarget>();
            target.Configure(InteractionTargetKind.Fishing);
            target.SetPickBounds(new Bounds(Vector3.up * .5f, new Vector3(.85f, 1f, .65f)));
            var sign = Part(station.transform, "Fishing marker", PrimitiveType.Cube, new Color(.29f, .45f, .38f));
            sign.localPosition = Vector3.up * .72f;
            sign.localScale = new Vector3(.59f, .33f, .10f);
            var post = Part(station.transform, "Fishing marker post", PrimitiveType.Cube, new Color(.43f, .27f, .15f));
            post.localPosition = Vector3.up * .38f;
            post.localScale = new Vector3(.10f, .76f, .1f);
            var marker = new GameObject("Fish symbol", typeof(SpriteRenderer));
            marker.transform.SetParent(station.transform, false);
            marker.transform.localPosition = new Vector3(0f, .73f, -.055f);
            var sprite = marker.GetComponent<SpriteRenderer>();
            sprite.sprite = FarmUITheme.Sprite("Icons/crop_fish");
            if (sprite.sprite != null) marker.transform.localScale = Vector3.one * (.23f / sprite.sprite.bounds.size.y);
            pond._bobber = Part(station.transform, "Bobber", PrimitiveType.Sphere, new Color(.95f, .35f, .22f));
            pond._bobber.localScale = Vector3.one * .14f;
            pond._bobber.gameObject.SetActive(false);
            var fishPrefab = Resources.Load<GameObject>("FarmContent/FishVisual");
            pond._rod = station.AddComponent<FishingRodView>();
            pond._rod.Configure(player, pond._bobber, fishPrefab);
            pond.CreateRipples();
            if (fishPrefab != null)
            {
                for (var i = 0; i < pond._fish.Length; i++)
                    pond._fish[i] = VisualPrefab.Create(fishPrefab, station.transform, new Vector3(.3f, .23f, .42f)).transform;
            }
            return pond;
        }

        private static Transform Part(Transform parent, string name, PrimitiveType type, Color tint)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            part.transform.SetParent(parent, false);
            part.layer = 2;
            RuntimeMaterials.RemoveCollider(part);
            RuntimeMaterials.Paint(part.GetComponent<Renderer>(), tint);
            return part.transform;
        }

        private void CreateRipples()
        {
            var ring = new GameObject("Surface ripples") { layer = 2 };
            ring.transform.SetParent(transform, false);
            _ripples = ring.AddComponent<LineRenderer>();
            _rippleMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            _ripples.sharedMaterial = _rippleMaterial;
            _ripples.loop = true;
            _ripples.positionCount = 20;
            _ripples.widthMultiplier = .025f;
            _ripples.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        public bool InReach
        {
            get
            {
                var delta = _player.position - transform.position;
                delta.y = 0f;
                return isActiveAndEnabled && delta.sqrMagnitude <= Reach * Reach;
            }
        }

        public string Prompt(long now)
        {
            if (!InReach) return "Подойдите к знаку рыбалки на берегу";
            if (now < NextCatchAtUnixMs)
                return "Рыба вернётся через " + Mathf.CeilToInt((NextCatchAtUnixMs - now) / 1000f) + " с";
            return _phase switch
            {
                Phase.Waiting => Time.unscaledTime >= _biteAt
                    ? FarmText.Get("fishing.hook", InputReader_AI.ActionHint) : FarmText.Get("fishing.wait"),
                Phase.Fighting => FarmText.Get("fishing.controls"),
                Phase.Reeling => "",
                _ => InputReader_AI.ActionHint + " — забросить удочку"
            };
        }

        public bool Interact(long now)
        {
            if (now < 0L || now > MaxCatchTime || MustStopFishing()) return false;
            if (_phase is Phase.Fighting or Phase.Reeling) return false;
            if (_phase == Phase.Waiting && Time.unscaledTime > _biteAt + BiteDuration)
            {
                EscapeFish();
                return false;
            }
            if (now < NextCatchAtUnixMs || (_phase == Phase.Waiting && Time.unscaledTime < _biteAt))
            {
                GameEvents.RaiseStatusChanged(Prompt(now));
                return false;
            }
            if (_phase == Phase.Idle)
            {
                StartCast();
                return true;
            }
            if (_inventory.GetAmount(FishId) == int.MaxValue)
            {
                CancelCast();
                GameEvents.RaiseStatusChanged("Нет места в стопке рыбы");
                return false;
            }
            var roll = _random.NextDouble();
            var size = roll < .52 ? FishingSize.Small : roll < .85 ? FishingSize.Medium : FishingSize.Large;
            _challenge = new FishingChallenge(size, _random);
            _phase = Phase.Fighting;
            _hud.ShowChallenge(_challenge);
            return true;
        }

        private void StartCast()
        {
            _hud ??= new FishingHUD(_hudParent, _hudFont);
            _phase = Phase.Waiting;
            _biteAt = Time.unscaledTime + 2.8f;
            _biteAnnounced = false;
            _input.FishingActive = true;
            _camera.BeginFishing(_waterPoint);
            _rod.Cast(_waterPoint);
            _hud.WaitForBite(false);
            MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.FishingCast, _waterPoint);
        }

        private void LandFish(long now)
        {
            if (_inventory.GetAmount(FishId) == int.MaxValue)
            {
                CancelCast();
                GameEvents.RaiseStatusChanged("Нет места в стопке рыбы");
                return;
            }
            NextCatchAtUnixMs = Math.Min(MaxCatchTime, now + CatchCooldownMs);
            var size = _challenge.Size;
            _phase = Phase.Reeling;
            _hud.Close();
            _rod.Reel(size switch { FishingSize.Small => .8f, FishingSize.Medium => 1f, _ => 1.35f });
            _inventory.Add(FishId, 1);
            MyLittleFarm.Audio.FarmAudio.Play(MyLittleFarm.Audio.SoundId.FishingCatch, _waterPoint);
            GameEvents.RaiseActionFeedback("collect", transform.position, FishId, 1);
            GameEvents.RaiseStatusChanged(FarmText.Get(size switch
            {
                FishingSize.Small => "fishing.caughtSmall",
                FishingSize.Medium => "fishing.caughtMedium",
                _ => "fishing.caughtLarge"
            }));
        }

        public void Restore(long nextCatch)
        {
            NextCatchAtUnixMs = nextCatch;
            CancelCast(true);
        }

        private void CancelCast(bool immediate = false)
        {
            if (!IsFishing) return;
            _phase = Phase.Idle;
            _challenge = null;
            if (_input != null) _input.FishingActive = false;
            if (_camera != null) _camera.EndFishing(immediate);
            _hud?.Close();
            _rod?.Hide();
            if (_bobber != null) _bobber.gameObject.SetActive(false);
        }

        private void EscapeFish()
        {
            CancelCast();
            GameEvents.RaiseStatusChanged(FarmText.Get("fishing.escape"));
        }

        private bool MustStopFishing() => !InReach || _input.ModalInputActive || _input.BuildModeActive
            || _input.FishingCancelPressed || _input.Movement.sqrMagnitude > .01f || _input.JumpPressed;

        private void UpdateFishing()
        {
            if (!IsFishing) return;
            if (MustStopFishing())
            {
                CancelCast();
                return;
            }
            if (_phase == Phase.Waiting && Time.unscaledTime >= _biteAt)
            {
                if (Time.unscaledTime > _biteAt + BiteDuration) EscapeFish();
                else if (!_biteAnnounced)
                {
                    _biteAnnounced = true;
                    _hud.WaitForBite(true);
                }
            }
            else if (_phase == Phase.Fighting)
            {
                var result = _challenge.Advance(Time.unscaledDeltaTime, _input.FishingAxis);
                if (result == FishingResult.Caught) LandFish(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                else if (result == FishingResult.Escaped) EscapeFish();
                else _hud.UpdateChallenge(_challenge);
            }
            else if (_phase == Phase.Reeling && !_rod.Active) CancelCast();
        }

        private void Update()
        {
            UpdateFishing();
            var t = Time.time;
            if (BobberInWater)
            {
                var bob = Time.unscaledTime >= _biteAt ? 0.075f : 0.022f;
                _bobber.position = _waterPoint + Vector3.up * (Mathf.Sin(t * (bob > .03f ? 12f : 4f)) * bob);
            }
            var ripplePhase = Mathf.Repeat(t * (BobberInWater ? 1.9f : 0.6f), 1f);
            _ripples.startColor = _ripples.endColor = new Color(.74f, .95f, .94f, (1f - ripplePhase) * .7f);
            var ringRadius = .12f + ripplePhase * .51f;
            for (var i = 0; i < _ripples.positionCount; i++)
            {
                var angle = i * (Mathf.PI * 2f / _ripples.positionCount);
                _ripples.SetPosition(i, _waterPoint + new Vector3(Mathf.Cos(angle) * ringRadius, 0f, Mathf.Sin(angle) * ringRadius));
            }
            for (var i = 0; i < _fish.Length; i++)
            {
                var fish = _fish[i];
                if (fish == null) continue;
                var angle = t * (0.55f + i * 0.12f) + i * 2f;
                var radius = 0.8f + i * .26f;
                fish.position = _waterPoint + new Vector3(-1f + Mathf.Cos(angle) * radius,
                    -0.05f + .025f * Mathf.Sin(angle * 2f), Mathf.Sin(angle) * radius);
                fish.rotation = Quaternion.LookRotation(new Vector3(-Mathf.Sin(angle), 0f, Mathf.Cos(angle)));
            }
        }

        private void OnApplicationFocus(bool focused)
        {
            if (!focused) CancelCast(true);
        }

        private void OnDisable() => CancelCast(true);

        private void OnDestroy()
        {
            _hud?.Destroy();
            if (_rippleMaterial != null) Destroy(_rippleMaterial);
        }
    }
}
