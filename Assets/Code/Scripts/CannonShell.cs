using System;
using System.Collections.Generic;
using Code.Scripts.Utils;
using UnityEngine;
using UnityEngine.VFX;

namespace Code.Scripts
{
    public class CannonShell : MonoBehaviour, SeaWar.IGameboardFollowTarget
    {
        [Header("Flight Characteristics")]
        public Vector3 gravity;
        public float safeDistance = 500;
        public float followZoom = 1;
        
        [Header("Impact Explosion")]
        public List<AudioClip> explosionSounds;
        public float explosionSoundVolume = 1;
        public VisualEffect explosionEffectTemplate;
        public Light explosionLightTemplate;
        public AnimationCurve explosionLightIntensity = AnimationCurve.Linear(0, 1, 1, 0);
        public float explosionDuration;

        [Header("Impact Splash")]
        public float splashDuration;
        public List<AudioClip> splashSounds;
        public float splashSoundVolume;
        public VisualEffect splashEffectTemplate;
        
        public delegate void InFlightUpdate(CannonShell shell, float timeOfFlight, float flightFraction);

        /// ////////
        
        public class FireConfig
        {
            public Vector3 Scale;
            public Vector3 InitialPosition;
            public Vector3 DirectionToTarget; 
            public float LaunchAngle;
            public float LaunchVelocity;
            public float NominalTargetDistance;
            public InFlightUpdate FlightUpdate = null;
            public ShellFuse FuseEvaluator = null;
            public Action<FuseResult> OnDetonation = null;
        }
        
        private Renderer _renderer;
        private Rigidbody _shellRigidBody;
        private Collider _collider;
        
        private float _fireTime;
        private float _finishTime;
        private Explosion _explosion;
        private Explosion _splash;
        
        private Transform[] _children;
        private FireConfig _fireConfig;
        
        private Explosion MakeExplosion()
        {
            var explosionGameObject = new GameObject("Shell Explosion", typeof(Explosion));
            var explosion = explosionGameObject.GetComponent<Explosion>();
            explosion.updraftSpeed = 0;
            explosion.duration = explosionDuration;
            explosion.lightIntensity = explosionLightIntensity;
            explosion.explosionSounds = explosionSounds;
            explosion.explosionSoundVolume = explosionSoundVolume;
            explosion.explosionEffectTemplate = explosionEffectTemplate;
            explosion.explosionLightTemplate = explosionLightTemplate;

            return explosion;
        }
        
        private Explosion MakeSplash()
        {
            var splashGameObject = new GameObject("Shell Splash", typeof(Explosion));
            var splash = splashGameObject.GetComponent<Explosion>();
            splash.updraftSpeed = 0;
            splash.duration = splashDuration;
            splash.lightIntensity = null;
            splash.explosionSounds = splashSounds;
            splash.explosionSoundVolume = splashSoundVolume;
            splash.explosionEffectTemplate = splashEffectTemplate;
            splash.explosionLightTemplate = null;

            return splash;
        }
        
        private void Start()
        {
            _explosion = MakeExplosion();
            _splash = MakeSplash();
            
            // Shells shouldn't explode by hitting other shells
            tag = "NoExplode";

            _collider = GetComponent<Collider>();
            
            _shellRigidBody = GetComponent<Rigidbody>();
            _shellRigidBody.useGravity = false;

            _renderer = GetComponent<Renderer>();
            
            gameObject.SetActive(false);

            _children = GetComponentsInChildren<Transform>(includeInactive: true);
            
            ClearFinishTime();
        }

        public void SetLayer(int layer)
        {
            foreach (var child in _children) child.gameObject.layer = layer;
        }
        
        private void ClearFinishTime()
        {
            _finishTime = float.MaxValue / 2;
        }
        
        private void OnCollisionEnter(Collision other)
        {
            // Ignore 'NoExplode' objects
            if (other.gameObject.CompareTag("NoExplode")) return;
            
            _finishTime = Time.time;
            
            // Set default action : Splash in water or NoAction otherwise
            var defaultFuseResult = other.gameObject.CompareTag("Water") ? 
                FuseResult.Splash : FuseResult.NoAction;
            
            var fuseResult = _fireConfig.FuseEvaluator?.Invoke(this, other) ?? defaultFuseResult;
            switch (fuseResult)
            {
                case FuseResult.NoAction:
                    ClearFinishTime();
                    break;
                
                case FuseResult.Detonate:
                    Detonate();
                    break;
                
                case FuseResult.Terminate:
                    Terminate();
                    break;
                
                case FuseResult.Splash:
                    Splash();
                    break;
                
                default:
                    throw new ArgumentOutOfRangeException(nameof(fuseResult), fuseResult, null);
            }
        }

        private void Update()
        {
            if (_renderer.enabled == false) return;
            if (_fireConfig.FlightUpdate == null) return;
            
            var flightTime = Time.time - _fireTime;
            var distanceTravelled = transform.position - _fireConfig.InitialPosition;
            _fireConfig.FlightUpdate.Invoke(this, flightTime, GetFlightFraction());

            // Enable the collider for detonation after we're at a safe distance
            if (!_collider.enabled && distanceTravelled.magnitude > safeDistance) _collider.enabled = true;
        }

        private void FixedUpdate()
        {
            if (_renderer.enabled == false) return;
            
            var velocity = _shellRigidBody.velocity;
            velocity += gravity * Time.fixedDeltaTime;
            _shellRigidBody.velocity = velocity;
            transform.rotation = Quaternion.LookRotation(velocity.normalized);
        }
        
        public void Fire(FireConfig fireConfig)
        {
            gameObject.SetActive(true);
            _collider.enabled = false;

            _fireConfig = fireConfig;
            
            _fireTime = Time.time;
            ClearFinishTime();
            
            // Compute the 3D launch velocity
            var angleInRadians = fireConfig.LaunchAngle * Mathf.Deg2Rad;
            var planarDirection = new Vector3(fireConfig.DirectionToTarget.x, 0, fireConfig.DirectionToTarget.z).normalized;
            var launchDirection = planarDirection * Mathf.Cos(angleInRadians) + Vector3.up * Mathf.Sin(angleInRadians);
            _shellRigidBody.isKinematic = false;
            _shellRigidBody.velocity = launchDirection * fireConfig.LaunchVelocity;
            
            // Move and scale the shell
            transform.localScale = fireConfig.Scale;
            transform.position = fireConfig.InitialPosition;

            // Enable the shell
            _renderer.enabled = true;
        }

        private void Detonate()
        {
            // Explode the shell
            _explosion.Explode(transform.position, transform.localScale);
            
            // Hide the shell
            _renderer.enabled = false;
            _shellRigidBody.isKinematic = true;
            
            if (_fireConfig.OnDetonation != null) ExecuteDelayed(explosionDuration, () =>
            {
                gameObject.SetActive(false);
                _fireConfig.OnDetonation(FuseResult.Detonate);
            });
        }

        private void Splash()
        {
            // Splash the shell
            _splash.Explode(transform.position, transform.localScale);
            
            // Hide the shell
            _renderer.enabled = false;
            _shellRigidBody.isKinematic = true;
            
            if (_fireConfig.OnDetonation != null) ExecuteDelayed(explosionDuration, () =>
            {
                gameObject.SetActive(false);
                _fireConfig.OnDetonation(FuseResult.Splash);
            });
        }

        public void Terminate()
        {
            _renderer.enabled = false;
            _shellRigidBody.isKinematic = true;
        }
        
        private void ExecuteDelayed(float delay, Action action) => 
            StartCoroutine(CoroutineUtils.DelayedCoroutine(delay, _ => action()));
        
        // Compute the required launch velocity to hit the target for a given launchAngle
        public static float ComputeFiringSolution(Vector3 initialPosition, Vector3 targetPosition, float launchAngle, float gravity = 9.81f)
        {
            var angleInRadians = launchAngle * Mathf.Deg2Rad;

            // Compute target direction and distance vertically, and in the horizontal plane
            var directionToTarget = targetPosition - initialPosition;
            var horizontalDistance = new Vector3(directionToTarget.x, 0, directionToTarget.z).magnitude;
            var verticalDistance = targetPosition.y - initialPosition.y;  // Height difference
            
            // See https://en.wikipedia.org/wiki/Projectile_motion#Displacement
            var a = horizontalDistance * horizontalDistance * gravity;
            var b = horizontalDistance * Mathf.Sin(2 * angleInRadians);
            var c = Mathf.Cos(angleInRadians);
            var d = 2 * verticalDistance * c * c;
            return Mathf.Sqrt(a / (b - d));
        }

        public float GetFollowZoom() => followZoom;

        public Vector3 GetPosition() => transform.position;

        public float GetFlightFraction()
        {
            if (_fireConfig == null) return 0;
            return (GetPosition() - _fireConfig.InitialPosition).magnitude / _fireConfig.NominalTargetDistance;   
        }

        public int GetDistanceToTarget()
        {
            return _fireConfig == null ? -1 : Mathf.RoundToInt((1 - GetFlightFraction()) * _fireConfig.NominalTargetDistance);
        }

        public float GetFollowFinishTime() => _finishTime;
    }
}