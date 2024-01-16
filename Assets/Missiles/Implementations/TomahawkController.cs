using System;
using Code.Scripts;
using Code.Scripts.Utils;
using JetBrains.Annotations;
using UnityEngine;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

namespace Missiles.Implementations
{
    [RequireComponent(typeof(Explosion))]
    public class TomahawkController : MissileBase
    {
        public VisualEffect launchBlast;
        public ParticleSystem plume;
        
        public Transform portWing;
        public Transform starboardWing;
        public GameObject flare;
        
        public AudioClip missileLaunchSound;

        public float boostPhaseDuration = 4;
        public float terminalPhaseDuration = 3f;
        public float cleanupDelay = 8;
        public float boostAltitude = 90;
        public float boostAltitudeJitter = 5;
        public float missileSpeed = 40;
        public float boostPhaseDistance = 60;
        public float terminalPhaseDistance = 60;
        public float spoolTime = 2;
        public float diveAngle = 80;
        public float followZoom = 2;
        
        private MeshRenderer _renderer;
        private Collider _collider;

        private const float WING_EXTENSION_POINT = 0.85f;
        private const float WING_EXTENSION_DURATION = 0.15f;

        private bool _wingsExtended;
        private bool _exploded;
        private bool _launched;
        private float _launchTime;
        private float _explosionTime;
        [CanBeNull] private Action<FuseResult> _onImpact;
        [CanBeNull] private Action _onExplosionComplete;
        [CanBeNull] private InFlightUpdate _inFlightUpdate;
        [CanBeNull] private MissileFuse _fuseEvaluator;
        private Explosion _explosion;
        private Explosion _splash;
        private float _cruiseAltitude;
        private Vector3 _target;
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
            _children = GetComponentsInChildren<Transform>(includeInactive: true);
            SetLayer(_layer);
        }

        private static Vector3 NormalizedTargetDirection(Vector3 target, Vector3 currentLocation)
        {
            var directionToTarget = target - currentLocation;
            directionToTarget.y = 0;
            return directionToTarget.normalized;
        }
        
        private static float AngleToTarget(Vector3 normalizedDirectionToTarget)
        {
            return Mathf.Atan2(normalizedDirectionToTarget.x, normalizedDirectionToTarget.z) * Mathf.Rad2Deg;
        }
        
        public override void LaunchMissile(
            float initialSpeed, 
            Vector3 target,
            [CanBeNull] Action<FuseResult> onImpact = null,
            [CanBeNull] Action onExplosionComplete = null,
            [CanBeNull] InFlightUpdate inFlightUpdate = null,
            MissileFuse fuseEvaluator = null)
        {
            var startPosition = transform.position;
            
            _target = target;
            _onImpact = onImpact;
            _onExplosionComplete = onExplosionComplete;
            _inFlightUpdate = inFlightUpdate;
            _fuseEvaluator = fuseEvaluator;
            
            _launchTime = Time.time;
            _flightDistance = (startPosition - target).magnitude;
            _cruiseAltitude = boostAltitude + Random.Range(-boostAltitudeJitter, boostAltitudeJitter);  
            
            // Compute positions, angles and distances
            var normalizedDirectionToTarget = NormalizedTargetDirection(target, startPosition);
            var angleToTarget = AngleToTarget(normalizedDirectionToTarget);
            
            var boostPhaseTarget = startPosition + normalizedDirectionToTarget * boostPhaseDistance;
            boostPhaseTarget.y = startPosition.y + _cruiseAltitude;
            var cruisePhaseTarget = target - normalizedDirectionToTarget * terminalPhaseDistance;
            cruisePhaseTarget.y = startPosition.y + _cruiseAltitude;
            
            var cruisePhaseDistance  = (cruisePhaseTarget - boostPhaseTarget).magnitude;
            var cruisePhaseDuration = cruisePhaseDistance / missileSpeed;
            
            // Prepare boost phase animation
            Action<(float value, bool complete)> boostFn = progress =>
                BoostPhaseAnimationStep(progress, startPosition, boostPhaseTarget, angleToTarget);
            
            // Prepare cruise phase animation
            Action<(float value, bool complete)> cruiseFn = progress => 
                CruisePhaseAnimationStep(boostPhaseTarget, cruisePhaseTarget, progress);
            
            // Prepare terminal phase animation
            Action<(float value, bool complete)> terminalFn = progress => 
                TerminalPhaseAnimationStep(target, progress, startPosition,
                    cruisePhaseTarget, normalizedDirectionToTarget, angleToTarget);
            
            // Prepare animation coroutine chain
            var cleanupCr = CoroutineUtils.DelayedCoroutine(cleanupDelay, _ => _onExplosionComplete?.Invoke());
            var terminalCr = CoroutineUtils.TimedCoroutine(terminalPhaseDuration, 0, null, terminalFn, terminalFn, cleanupCr);
            var cruiseCr = CoroutineUtils.TimedCoroutine(cruisePhaseDuration, 0, _ =>
            {
                // Only enable collision after we're at cruise
                _collider.enabled = true;
            }, cruiseFn, cruiseFn, terminalCr);
            var boostCr = CoroutineUtils.TimedCoroutine(boostPhaseDuration, spoolTime, null, boostFn, boostFn, cruiseCr);

            // Detach the missile from its parent
            gameObject.SetActive(true);
            SetMissileParent(null);
            BoostPhaseAnimationStep((0, false), startPosition, boostPhaseTarget, angleToTarget);
            
            // Light the engine and show the launch blast
            PositionAndActivateLaunchBlast(startPosition);
            
            // Enable the object and light the engine
            LightEngine();
            
            // Play the launch sound
            SoundManager.Instance.PlaySound(missileLaunchSound, 0.2f);
            
            // Start the animation coroutine chain
            StartCoroutine(boostCr);
        }

        private void Update()
        {
            if (_exploded) return;
            
            var timeOfFlight = Time.time - _launchTime;
            _inFlightUpdate?.Invoke(this, timeOfFlight, GetFlightFraction());
        }
        
        public override void SetLayer(int layer)
        {
            gameObject.layer = layer;
            
            _layer = layer;
            foreach (var child in _children) child.gameObject.layer = layer;
        }
        
        public override void SetMissileParent(Transform parent)
        {
            transform.SetParent(parent);
        }

        private void BoostPhaseAnimationStep((float value, bool complete) progress, Vector3 startPosition, Vector3 boostPhaseTarget,
            float angleToTarget)
        {
            _launched = true;
            
            // Compute altitude
            var altitude = _cruiseAltitude * 
                           Easing.OutQuad(AnimationUtils.ComputeSubAnimationTime(progress.value, 0f, 0.6f));

            // Compute position
            var positionTime = Easing.InQuad(AnimationUtils.ComputeSubAnimationTime(progress.value, 0.1f, 0.9f));
            var newPosition = Vector3.Lerp(startPosition, boostPhaseTarget, positionTime);
            newPosition.y = startPosition.y + altitude;
            transform.position = newPosition;

            // Compute rotation
            var levelOff = Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(progress.value, 0.1f, 0.9f));
            var rotateToTarget = Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(progress.value, 0.1f, 0.3f));
            transform.rotation = Quaternion.Euler(levelOff * 90, rotateToTarget * angleToTarget, 0f) *
                                 Quaternion.Euler(-90, 0, 90);

            // Animate sub-components
            MaybeExtendWings(progress.value);
        }
        
        private void CruisePhaseAnimationStep(Vector3 boostPhaseTarget, Vector3 cruisePhaseTarget,
            (float value, bool complete) progress)
        {
            var newPosition = Vector3.Lerp(boostPhaseTarget, cruisePhaseTarget, progress.value);
            transform.position = newPosition;
        }
        
        private void TerminalPhaseAnimationStep(Vector3 target, (float value, bool complete) progress,
            Vector3 startPosition, Vector3 cruisePhaseTarget, Vector3 normalizedDirectionToTarget, float angleToTarget)
        {
            // Compute altitude
            var altitudeValue = Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(progress.value, 0.2f, 0.8f));
            var altitude = Mathf.Lerp(startPosition.y + _cruiseAltitude, target.y, altitudeValue);

            // Compute position
            var newPosition = Vector3.Lerp(cruisePhaseTarget, target + normalizedDirectionToTarget * 0.2f,
                Easing.OutQuad(progress.value));
            newPosition.y = altitude;
            transform.position = newPosition;

            // Compute rotation
            var dive = Easing.OutQuad(AnimationUtils.ComputeSubAnimationTime(progress.value, 0f, 1f));
            transform.rotation = Quaternion.Euler(90 + dive * diveAngle, angleToTarget, 0f) * Quaternion.Euler(-90, 0, 90);

            // Destroy the visible missile effects when the animation is complete
            //
            // Root GameObject isn't destroyed yet - we delay doing that so the particle
            // effects can fade out after the explosion.
            if (progress.complete) HideMissile();
        }

        private void OnCollisionEnter(Collision other)
        {
            if (_exploded) return;
            
            // Ignore 'NoExplode' objects
            if (other.gameObject.CompareTag("NoExplode")) return;

            _explosionTime = Time.time;
            
            // Set default action : Splash in water or NoAction otherwise
            var defaultFuseResult = other.gameObject.CompareTag("Water") ? 
                FuseResult.Splash : FuseResult.NoAction;
            
            var fuseResult = _fuseEvaluator?.Invoke(this, other) ?? defaultFuseResult;
            DoExplosion(fuseResult);
        }

        private void DoExplosion(FuseResult fuseResult)
        {
            _exploded = true;
            
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
            explosion.Explode(transform.position, transform.localScale, 0,null, () =>
            {
                _onImpact?.Invoke(fuseResult);
                _onExplosionComplete?.Invoke();
            });
        }

        private void PositionAndActivateLaunchBlast(Vector3 position)
        {
            // Create the launch smoke effect
            launchBlast.transform.position = position;
            launchBlast.Play();
        }

        private void LightEngine()
        {
            plume.Play(true);
            flare.SetActive(true);
        }
        
        private void MaybeExtendWings(float progress)
        {
            if (progress < WING_EXTENSION_POINT) return;
            if (_wingsExtended) return;
            
            var wingProgress = AnimationUtils.ComputeSubAnimationTime(progress, WING_EXTENSION_POINT, WING_EXTENSION_DURATION);
            portWing.transform.localRotation = Quaternion.Euler(wingProgress * 90, 0, 45);
            starboardWing.transform.localRotation = Quaternion.Euler(wingProgress * -90, 0, 45);

            if (wingProgress >= 1f) _wingsExtended = true;
        }
        
        private void HideMissile()
        {
            _renderer.enabled = false;
            portWing.gameObject.SetActive(false);
            starboardWing.gameObject.SetActive(false);
            flare.SetActive(false);
            plume.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
        
        public override void ResetMissile(Vector3 position, Quaternion rotation, Vector3 scale)
        {
            // Reset the wings
            portWing.gameObject.SetActive(true);
            starboardWing.gameObject.SetActive(true);
            portWing.transform.localRotation = Quaternion.Euler(0, 0, 45);
            starboardWing.transform.localRotation = Quaternion.Euler(0, 0, 45);
            _wingsExtended = false;
            
            // Reset the launch blast, plume and smoke particle effects 
            plume.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            launchBlast.Stop();
            
            // Turn off the flare
            flare.SetActive(false);

            // Reset the explosion
            _explosionTime = float.MaxValue / 2;
            _exploded = false;
            _launched = false;
            
            // Enabled rendering of the missile
            _renderer.enabled = true;

            // Disable the collider until we reach cruising altitude
            _collider.enabled = false;
            
            // Set the missile position
            transform.position = position;
            transform.localRotation = rotation;
            transform.localScale = scale;
        }

        public override void DestroyMissile()
        {
            Destroy(gameObject);
        }

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
