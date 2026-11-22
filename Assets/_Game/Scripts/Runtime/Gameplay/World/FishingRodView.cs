using MyLittleFarm.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace MyLittleFarm.Gameplay.World
{
    /// <summary>Procedural upper-body cast and reel animation with a world-space line.</summary>
    [DefaultExecutionOrder(170)]
    internal sealed class FishingRodView : MonoBehaviour
    {
        private const float CastRelease = .23f;
        private const float CastLanding = .64f;
        private const float ReelDuration = .46f;
        private PlayerController _player;
        private Transform _upperArm;
        private Transform _forearm;
        private Transform _hand;
        private Transform _bobber;
        private Transform _rod;
        private Transform _tip;
        private Transform _caughtFish;
        private Vector3 _caughtFishScale;
        private LineRenderer _line;
        private Material _lineMaterial;
        private Vector3 _water;
        private Vector3 _reelStart;
        private float _phaseStarted;
        private bool _reeling;

        public bool Active => _rod != null && _rod.gameObject.activeSelf;

        public void Configure(PlayerController player, Transform bobber, GameObject fishPrefab)
        {
            _player = player;
            _bobber = bobber;
            var animator = player.GetComponentInChildren<Animator>(true);
            if (animator != null && animator.isHuman)
            {
                _upperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                _forearm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                _hand = animator.GetBoneTransform(HumanBodyBones.RightHand);
            }
            _rod = new GameObject("Fishing rod grip").transform;
            _rod.SetParent(transform, false);
            CreateRodPart("Cork grip", new Vector3(0f, 0f, -.18f), new Vector3(0f, 0f, .09f), .047f, new Color(.63f, .40f, .22f));
            CreateRodPart("Bamboo rod", new Vector3(0f, 0f, .07f), new Vector3(0f, .03f, .72f), .025f, new Color(.59f, .37f, .16f));
            CreateRodPart("Bamboo tip", new Vector3(0f, .03f, .71f), new Vector3(0f, .12f, 1.12f), .016f, new Color(.83f, .62f, .29f));
            CreateRodPart("Reel", new Vector3(-.048f, -.055f, .08f), new Vector3(.05f, -.055f, .08f), .11f, new Color(.23f, .29f, .30f));
            _tip = new GameObject("Rod tip").transform;
            _tip.SetParent(_rod, false);
            _tip.localPosition = new Vector3(0f, .12f, 1.12f);
            _line = new GameObject("Fishing line") { layer = 2 }.AddComponent<LineRenderer>();
            _line.transform.SetParent(transform, false);
            _lineMaterial = new Material(Shader.Find("Sprites/Default")) { hideFlags = HideFlags.DontSave };
            _line.sharedMaterial = _lineMaterial;
            _line.startColor = _line.endColor = new Color(.86f, .91f, .79f, .8f);
            _line.positionCount = 12;
            _line.widthMultiplier = .012f;
            _line.shadowCastingMode = ShadowCastingMode.Off;
            _line.receiveShadows = false;
            if (fishPrefab != null)
            {
                _caughtFish = VisualPrefab.Create(fishPrefab, transform, new Vector3(.3f, .23f, .42f)).transform;
                _caughtFish.name = "Fish on the line";
                _caughtFishScale = _caughtFish.localScale;
            }
            Hide();
        }

        private void CreateRodPart(string name, Vector3 start, Vector3 end, float thickness, Color color)
        {
            var part = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            part.name = name;
            part.layer = 2;
            part.transform.SetParent(_rod, false);
            part.transform.localPosition = (start + end) * .5f;
            part.transform.localRotation = Quaternion.FromToRotation(Vector3.up, end - start);
            part.transform.localScale = new Vector3(thickness, Vector3.Distance(start, end) * .5f, thickness);
            RuntimeMaterials.RemoveCollider(part);
            RuntimeMaterials.Paint(part.GetComponent<Renderer>(), color);
        }

        public void Cast(Vector3 water)
        {
            _water = water;
            _reeling = false;
            _phaseStarted = Time.unscaledTime;
            _player.HoldingFishingRod = true;
            _rod.gameObject.SetActive(true);
            _bobber.gameObject.SetActive(false);
            _line.enabled = false;
        }

        public void Reel(float fishScale)
        {
            if (!Active) return;
            if (_caughtFish != null) _caughtFish.localScale = _caughtFishScale * fishScale;
            _reelStart = _bobber.position;
            _reeling = true;
            _phaseStarted = Time.unscaledTime;
        }

        public void Hide()
        {
            if (_player != null) _player.HoldingFishingRod = false;
            if (_rod != null) _rod.gameObject.SetActive(false);
            if (_bobber != null) _bobber.gameObject.SetActive(false);
            if (_line != null) _line.enabled = false;
            if (_caughtFish != null) _caughtFish.gameObject.SetActive(false);
        }

        private void LateUpdate()
        {
            if (!Active) return;
            var elapsed = Time.unscaledTime - _phaseStarted;
            if (_reeling && elapsed >= ReelDuration) { Hide(); return; }
            var player = _player.transform;
            var horizontal = Vector3.ProjectOnPlane(_water - player.position, Vector3.up);
            var forward = horizontal.sqrMagnitude > .001f ? horizontal.normalized : player.forward;
            player.rotation = Quaternion.Slerp(player.rotation, Quaternion.LookRotation(forward), 1f - Mathf.Exp(-16f * Time.unscaledDeltaTime));
            var right = player.right;
            var up = Vector3.up;
            var shoulder = _upperArm != null ? _upperArm.position : player.position + up * .55f + right * .21f;
            var waiting = shoulder + forward * .31f - up * .24f + right * .10f;
            var drawback = shoulder - forward * .14f + up * .18f + right * .22f;
            var release = shoulder + forward * .51f - up * .08f + right * .06f;
            var reel = _reeling ? Mathf.Clamp01(elapsed / ReelDuration) : 0f;
            var pull = _reeling ? Mathf.Sin(reel * Mathf.PI) : 0f;
            Vector3 grip;
            float lift;
            if (_reeling)
            {
                grip = waiting + up * (.28f * pull) - forward * (.22f * pull);
                lift = .44f + 1.1f * pull;
            }
            else if (elapsed < CastRelease)
            {
                var weight = Mathf.SmoothStep(0f, 1f, elapsed / CastRelease);
                grip = Vector3.Lerp(waiting, drawback, weight);
                lift = .4f + weight * 2.2f;
            }
            else if (elapsed < CastLanding)
            {
                var flight = Mathf.Clamp01((elapsed - CastRelease) / (CastLanding - CastRelease));
                grip = Vector3.Lerp(release, waiting, Mathf.SmoothStep(0f, 1f, flight));
                lift = Mathf.Lerp(1.22f, .45f, flight);
            }
            else
            {
                grip = waiting + up * (.016f * Mathf.Sin(elapsed * 3f));
                lift = .45f;
            }
            AimArm(grip, right - up);
            var origin = _hand != null ? _hand.position : grip;
            var direction = _water - origin + up * (Vector3.Distance(origin, _water) * .42f + lift);
            _rod.SetPositionAndRotation(origin, Quaternion.LookRotation(direction));
            if (elapsed < CastRelease && !_reeling) return;

            if (_reeling)
            {
                _bobber.position = Vector3.Lerp(_reelStart, _tip.position, reel) + up * (.46f * pull);
                if (_caughtFish != null)
                {
                    _caughtFish.position = _bobber.position - up * .19f;
                    _caughtFish.rotation = Quaternion.LookRotation(new Vector3(Mathf.Sin(elapsed * 38f) * .28f, 1f, .1f));
                    _caughtFish.gameObject.SetActive(true);
                }
            }
            else if (elapsed < CastLanding)
            {
                var flight = Mathf.Clamp01((elapsed - CastRelease) / (CastLanding - CastRelease));
                _bobber.position = Vector3.Lerp(_tip.position, _water, flight) + up * (.70f * Mathf.Sin(flight * Mathf.PI));
            }
            _bobber.gameObject.SetActive(true);
            _line.enabled = true;
            var sag = !_reeling && elapsed >= CastLanding ? .19f : .045f;
            for (var i = 0; i < _line.positionCount; i++)
            {
                var t = i / (float)(_line.positionCount - 1);
                var point = Vector3.Lerp(_tip.position, _bobber.position, t) - up * (Mathf.Sin(t * Mathf.PI) * sag);
                _line.SetPosition(i, point);
            }
        }

        private void AimArm(Vector3 grip, Vector3 bend)
        {
            if (_upperArm == null || _forearm == null || _hand == null) return;
            var shoulder = _upperArm.position;
            var upper = Vector3.Distance(shoulder, _forearm.position);
            var lower = Vector3.Distance(_forearm.position, _hand.position);
            if (upper < .01f || lower < .01f) return;
            var toward = (grip - shoulder).normalized;
            var reach = Mathf.Clamp(Vector3.Distance(grip, shoulder), Mathf.Abs(upper - lower) + .002f, upper + lower - .002f);
            var side = Vector3.ProjectOnPlane(bend, toward).normalized;
            var along = (upper * upper - lower * lower + reach * reach) / (2f * reach);
            var elbow = shoulder + toward * along + side * Mathf.Sqrt(Mathf.Max(0f, upper * upper - along * along));
            _upperArm.rotation = Quaternion.FromToRotation(_forearm.position - shoulder, elbow - shoulder) * _upperArm.rotation;
            _forearm.rotation = Quaternion.FromToRotation(_hand.position - _forearm.position,
                shoulder + toward * reach - _forearm.position) * _forearm.rotation;
        }

        private void OnDisable() => Hide();

        private void OnDestroy()
        {
            if (_lineMaterial != null) Destroy(_lineMaterial);
        }
    }
}
