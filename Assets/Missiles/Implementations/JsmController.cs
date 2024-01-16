using System;
using Code.Scripts;
using JetBrains.Annotations;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Missiles.Implementations
{
    [RequireComponent(typeof(Explosion))]
    public class JsmController : MissileBase
    {
        public ParticleSystem plume;
        
        public Transform portWing;
        public Transform starboardWing;
        public GameObject flare;
        
        public AudioClip missileLaunchSound;
        
        public float cruiseAltitude = 90;
        public float cruiseAltitudeJitter = 5;
        public float missileSpeed = 40;
        public float diveDistance = 2f;
        public float freeFallTime = 1f;
        public float wingExtendTime = 0.2f;
        public float wingExtendDuration = 0.5f;
        public float accelerateDuration = 0.5f;
        public float followZoom = 3;
        
        private MeshRenderer _renderer;
        private Collider _collider;
        
        private Vector3 _target;
        private float _initialSpeed;
        private float _launchTime;

        private bool _launched;
        private float _cruiseAltitude;
        private bool _engineLit;
        private bool _wingsExtended;
        private bool _exploded;
        [CanBeNull] private Action<FuseResult> _onImpact;
        [CanBeNull] private Action _onExplosionComplete;
        [CanBeNull] private InFlightUpdate _inFlightUpdate;
        [CanBeNull] private MissileFuse _fuseEvaluator;
        private SoundManager.ClipController _launchSoundController;
        private Explosion _explosion;
        private Explosion _splash;
        private float _freeFallSpeed;
        private float _explosionTime;
        private float _flightDistance;
        private Transform[] _children = {};
        private int _layer;

        private void Awake()
        {
            _renderer = GetComponent<MeshRenderer>();
            _collider = GetComponent<Collider>();
            
            var components = GetComponents<Explosion>();
            _explosion = components[0];
            _splash = components[1];
        }

        private void Start()
        {
            var wasLaunched = _launched;
            _launched = wasLaunched;
            
            _children = plume.GetComponentsInChildren<Transform>(includeInactive: true);
            SetLayer(_layer);
        }

        private static float GroundDistanceToTarget(Vector3 from, Vector3 to)
        {
            from.y = 0;
            to.y = 0;
            return Vector3.Distance(from, to);
        }
        
        private void Update()
        {
            // Don't update if we've not launched yet, or if we've exploded
            if (!_launched || _exploded) return;
            
            var timeSinceLaunch = Time.time - _launchTime;
            
            _inFlightUpdate?.Invoke(this, timeSinceLaunch, GetFlightFraction());
            
            // Extend wings as necessary
            if (timeSinceLaunch > wingExtendTime) MaybeExtendWings(timeSinceLaunch);

            // Perform powered turning
            var groundDistanceToTarget = GroundDistanceToTarget(transform.position, _target);
            var target = _target;
            target.y = groundDistanceToTarget < diveDistance ? Mathf.Lerp(_cruiseAltitude, target.y, 1 - groundDistanceToTarget / diveDistance) : _cruiseAltitude;
            var targetDelta = target - transform.position;
            
            var speed = _initialSpeed;
            
            // Fly missing (and ignite engine after freeFall)
            if (timeSinceLaunch > freeFallTime)
            {
                MaybeLightEngine();

                // Speed up the missile after the engine is ignited
                var progress = Mathf.Clamp01((timeSinceLaunch - freeFallTime) / accelerateDuration);
                speed = Mathf.Lerp(_initialSpeed, missileSpeed, progress);

                // The step size is equal to speed times frame time
                var singleStep = speed * Time.deltaTime;

                // Rotate the forward vector towards the target direction by one step
                var newDirection = Vector3.RotateTowards(transform.forward, targetDelta, singleStep, 0.0f);

                // Calculate a rotation a step closer to the target and applies rotation to this object
                transform.rotation = Quaternion.LookRotation(newDirection);
                
                // Move forward
                transform.Translate(speed * Time.deltaTime * Vector3.forward);
            }
            else
            {
                _freeFallSpeed += Time.deltaTime * 9.81f;
                
                // Move forward, and drop under free-fall
                transform.Translate(speed * Time.deltaTime * Vector3.forward);
                transform.Translate(_freeFallSpeed * Time.deltaTime * Vector3.down);
            }
        }
        
        public override void LaunchMissile(
            float initialSpeed, 
            Vector3 target, 
            [CanBeNull] Action<FuseResult> onImpact = null,
            [CanBeNull] Action onExplosionComplete = null,
            [CanBeNull] InFlightUpdate inFlightUpdate = null,
            MissileFuse fuseEvaluator = null)
        {
            _initialSpeed = initialSpeed;
            _target = target;
            _onImpact = onImpact;
            _onExplosionComplete = onExplosionComplete;
            _inFlightUpdate = inFlightUpdate;
            _fuseEvaluator = fuseEvaluator;
            
            _launched = true;

            _cruiseAltitude = cruiseAltitude + Random.Range(-cruiseAltitudeJitter, cruiseAltitudeJitter);
            _flightDistance = (transform.position - target).magnitude;
            
            _launchTime = Time.time;
            _freeFallSpeed = 0;

            // Detach the missile from it's parent
            SetMissileParent(null);
            
            gameObject.SetActive(true);
        }

        private void MaybeLightEngine()
        {
            if (_engineLit) return;
            _engineLit = true;
            
            // Play the launch sound
            _launchSoundController = SoundManager.Instance.PlaySound(missileLaunchSound, 0.2f);
            
            plume.Play(true);
            flare.SetActive(true);
            
            // Only enable collision after we're at in flight
            _collider.enabled = true;
        }
        
        private void MaybeExtendWings(float timeSinceLaunch)
        {
            if (_wingsExtended) return;
            
            var progress = Mathf.Clamp01((timeSinceLaunch - wingExtendTime) / wingExtendDuration);
            portWing.transform.localRotation = Quaternion.Euler(0, 0, (1-progress) * 115);
            starboardWing.transform.localRotation = Quaternion.Euler(0, 0, (1 - progress) * -115);

            if (progress >= 1f) _wingsExtended = true;
        }

        
        private void OnCollisionEnter(Collision other)
        {
            if (_exploded) return;
            
            // Ignore 'NoExplode' objects
            if (other.gameObject.CompareTag("NoExplode")) return;
            
            // Set default action : Splash in water or NoAction otherwise
            var defaultFuseResult = other.gameObject.CompareTag("Water") ? 
                FuseResult.Splash : FuseResult.NoAction;
            
            var fuseResult = _fuseEvaluator?.Invoke(this, other) ?? defaultFuseResult;
            DoExplosion(fuseResult);
        }
        
        private void DoExplosion(FuseResult fuseResult)
        {
            _explosionTime = Time.time;
            _exploded = true;
            _renderer.enabled = false;
            _collider.enabled = false;
            portWing.gameObject.SetActive(false);
            starboardWing.gameObject.SetActive(false);
            flare.SetActive(false);
            plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            
            _launchSoundController.FadeOut(1f);

            // Perform the appropriate explosion
            var explosion = fuseResult switch
            {
                FuseResult.Detonate => _explosion,
                FuseResult.Splash => _splash,
                _ => null
            };
            if (!explosion)
            {
                _onImpact?.Invoke(fuseResult);
                _onExplosionComplete?.Invoke();
                return;
            }
            
            // Fire the explosion, notifying listeners on completion
            explosion.Explode(_target, transform.localScale, null, Quaternion.identity, () =>
            {
                _onImpact?.Invoke(fuseResult);
                _onExplosionComplete?.Invoke();
            });
        }
        
        public override void ResetMissile(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // Reset the wings
            portWing.gameObject.SetActive(true);
            starboardWing.gameObject.SetActive(true);
            portWing.transform.localRotation = Quaternion.Euler(0, 0, 115);
            starboardWing.transform.localRotation = Quaternion.Euler(0, 0, -115);
            _wingsExtended = false;
            
            // Reset the plume and smoke particle effects
            _engineLit = false;
            _launched = false;
            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            
            // Turn off the flare
            flare.SetActive(false);

            // Reset the explosion
            _explosionTime = float.MaxValue / 2;
            _exploded = false;
            
            // Enabled rendering of the missile
            _renderer.enabled = true;

            // Set the missile position
            transform.localPosition = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
        }
        
        public override void SetLayer(int layer)
        {
            gameObject.layer = layer;
            
            _layer = layer;
            foreach (var child in _children) child.gameObject.layer = layer;
        }

        public override void SetMissileParent(Transform parent) => transform.SetParent(parent, true);

        public override void DestroyMissile() => Destroy(gameObject);
        public override float GetFollowZoom() => followZoom;
        public override Vector3 GetPosition() => transform.position;

        public override int GetDistanceToTarget() => true switch
            {
                true when _exploded => 0,
                true when !_launched => -1,
                _ => Mathf.RoundToInt((1 - GetFlightFraction()) * _flightDistance)
            };

        public override float GetFollowFinishTime() => _explosionTime;
        
        public override float GetFlightFraction() => true switch
        {
            true when _exploded => 1,
            true when !_launched => 0,
            _ => 1 - (transform.position - _target).magnitude / _flightDistance
        };
    }
}
