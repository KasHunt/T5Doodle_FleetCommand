using System;
using System.Collections;
using System.Collections.Generic;
using Code.Scripts.Utils;
using JetBrains.Annotations;
using UnityEngine;

namespace Code.Scripts
{
    [RequireComponent(typeof(ExplosionManager))]
    [RequireComponent(typeof(CannonShellPool))]
    public class GunTurret : MonoBehaviour, SeaWar.IGameboardFollowTarget
    {
        [Min(0.1f)] public float maxElevationDistance = 500;
        [Range(0, 90)] public float maxElevation = 45;
        public AudioClip rotateSound;
        public AudioClip fireSound;
        public float followZoom = 1;
        
        public float elevationSnapAngle = 5;
        public float rotationSnapAngle = 5;
        
        public List<Vector3> multiBarrelOffsets = new();
        
        [Tooltip("Degrees/second/second")]
        public float rotationAcceleration = 90f;
        [Tooltip("Degrees/second")]
        public float maxRotationSpeed = 90f;
        [Tooltip("Degrees/second/second")]
        public float elevationAcceleration = 10f;
        [Tooltip("Degrees/second")]
        public float maxElevationSpeed = 20f;
        
        public CannonShellPool cannonShellPool;
        
        ///////////
        
        private ExplosionManager _muzzleFlashExplosionManager;

        private GameObject _barrel;
        private GameObject _muzzle;

        private bool _locked;
        
        private SnappingAngleSlewController _rotationSlewController;
        private SnappingAngleSlewController _elevationSlewController;
        private readonly Queue<FirePackage> _firePackageQueue = new();
        [CanBeNull] private FirePackage _currentFirePackage;

        private void Awake()
        {
            // Get the explosion manager
            _muzzleFlashExplosionManager = GetComponent<ExplosionManager>();
        
            // Get the barrel
            if (transform.childCount == 0)
            {
                Debug.LogError($"Gun Turret ({name}) is missing a barrel (Child GameObject of turret)");
                return;
            }
            _barrel = transform.GetChild(0).gameObject;
        
            // Get the muzzle
            if (_barrel.transform.childCount == 0)
            {
                Debug.LogError($"Gun Turret ({name}) is missing a muzzle (Child GameObject of barrel)");
                return;
            }
            _muzzle = _barrel.transform.GetChild(0).gameObject;
            
            // Prepare rotation slew controller
            _rotationSlewController = new SnappingAngleSlewController(
                initialAngle: transform.localEulerAngles.y, 
                snapAngle: rotationSnapAngle, 
                acceleration: rotationAcceleration,
                maxSpeed: maxRotationSpeed
            );
            
            // Prepare elevation slew controller
            _elevationSlewController = new SnappingAngleSlewController(
                initialAngle: _barrel.transform.localEulerAngles.x, 
                snapAngle: elevationSnapAngle, 
                acceleration: elevationAcceleration,
                maxSpeed: maxElevationSpeed
            );
        }

        public void LockTurret(bool locked)
        {
            _locked = locked;
        }

        private void AimAt(Vector3 target, FirePackage firePackage)
        {
            var position = transform.position;
            var delta = position - target;

            // Set the target rotation
            var newAim = Mathf.Atan2(-delta.x, -delta.z) * Mathf.Rad2Deg;
            firePackage.Rotation = newAim > 0 ? newAim : newAim + 360;
            
            // Set the target elevation
            var elevation = Mathf.Clamp01(delta.sqrMagnitude / (maxElevationDistance * maxElevationDistance)) * maxElevation;
            firePackage.Elevation = elevation;
            
            // Compute and set the launch speed
            firePackage.AimPower = CannonShell.ComputeFiringSolution(position, target, elevation, 19.81f);
            
            // Store the nominal distance to the target
            firePackage.NominalTargetDistance = delta.magnitude;
        }

        private void Fire(FirePackage firePackage)
        {
            StartCoroutine(FireCoroutine(firePackage));
        }
        
        private Queue<CannonShell> ObtainShells(int count)
        {
            var shells = new Queue<CannonShell>();
            
            for (var i = 0; i < count; i++)
                shells.Enqueue(cannonShellPool.TakeFromPool());

            return shells;
        }

        public class FirePackage
        {
            public Queue<CannonShell> CannonShells;
            public int BurstCount;
            public float BurstInterval;
            public Action OnImpact;
            public float AimPower;
            public float NominalTargetDistance;
            public float Elevation;
            public float Rotation;
            public int InitialLayer;
            public ShellFuse FuseEvaluator;
            public SeaWar.FollowTargetProxy FollowTargetProxy;
        }
        
        public FirePackage PrepareFirePackage(
            int initialLayer,
            int burstCount,
            [CanBeNull] out SeaWar.IGameboardFollowTarget followTarget)
        {
            // Compute the total number of shells in a salvo
            var shellCount = MuzzleCount() * burstCount;

            // Create the fire package
            var firePackage = new FirePackage
            {
                CannonShells = ObtainShells(shellCount),
                BurstCount = burstCount,
                InitialLayer = initialLayer,
                FollowTargetProxy = new SeaWar.FollowTargetProxy(this)
            };

            // Initially follow the gun turret
            followTarget = firePackage.FollowTargetProxy;
            
            // Apply the follow zoom from the turret to the shell that will be followed
            var shell = firePackage.CannonShells.Peek();
            shell.followZoom = followZoom;
            
            return firePackage;
        }

        public void SubmitFirePackage(
            FirePackage firePackage,
            Vector3 target, 
            Action onImpact,
            float burstInterval = 0.6f,
            ShellFuse fuseEvaluator = null)
        {
            // Play the 'rotating turret' sound
            SoundManager.Instance.PlaySound(rotateSound, 1f);
            
            // Set up for firing at target
            AimAt(target, firePackage);
            firePackage.BurstInterval = burstInterval;
            firePackage.OnImpact = onImpact;
            firePackage.FuseEvaluator = fuseEvaluator;
            
            _firePackageQueue.Enqueue(firePackage);
        }
        
        private IEnumerator FireCoroutine(FirePackage firePackage)
        {
            // Follow the first shell from the fire package
            firePackage.FollowTargetProxy.Principal = firePackage.CannonShells.Peek(); 
            
            var fired = 0;
            for (;;)
            {
                // Break if we've fired the requested burst count
                if (fired >= firePackage.BurstCount) break;
                
                // Fire
                SoundManager.Instance.PlaySound(fireSound, 0.6f);
                BurstFire(firePackage);

                // Increment and wait if we've got more shots to fire
                fired++;
                if (fired < firePackage.BurstCount) yield return new WaitForSeconds(firePackage.BurstInterval);
            }
        }

        private int MuzzleCount() => Math.Max(multiBarrelOffsets.Count, 1);
        
        private void BurstFire(FirePackage firePackage)
        {
            // Get positions, scales and rotations
            var barrelRotation = _barrel.transform.rotation;
            var muzzlePosition = _muzzle.transform.position;
            var muzzleElevation = -_barrel.transform.localEulerAngles.x;
            var scale = transform.localScale;
            
            var rotationInRadians = barrelRotation.eulerAngles.y * Mathf.Deg2Rad;
            var directionToTarget = new Vector3(Mathf.Sin(rotationInRadians), 0, Mathf.Cos(rotationInRadians));
            
            // Fire the muzzle flash effect and sound
            SoundManager.Instance.PlaySound(fireSound, 1);

            // Get the multiple barrels (adding a single barrel if we don't have multiple)
            var muzzleOffsets = new List<Vector3>();
            muzzleOffsets.AddRange(multiBarrelOffsets);
            if (muzzleOffsets.Count == 0) muzzleOffsets.Add(Vector3.zero);
            
            foreach (var muzzleOffset in muzzleOffsets)
            {
                muzzleOffset.Scale(scale);
                var rotatedOffset = barrelRotation * muzzleOffset;
                var offsetMuzzlePosition = muzzlePosition + rotatedOffset;
                
                _muzzleFlashExplosionManager.Explode(offsetMuzzlePosition, barrelRotation, scale, firePackage.InitialLayer);
                
                FireBarrel(scale, directionToTarget, offsetMuzzlePosition, muzzleElevation, firePackage);
            }
        }

        private void FireBarrel(Vector3 scale, Vector3 directionToTarget, Vector3 muzzlePosition, float muzzleElevation,
            FirePackage firePackage)
        {
            var shell = firePackage.CannonShells.Dequeue();
            
            // Set the shell as initially only visible to a specific layer
            // We set it to 'visible to all' halfway through the flight
            shell.SetLayer(firePackage.InitialLayer);
            var haveMadeVisible = false;

            // Fire the shell, returning it to the available pool when it's complete
            shell.Fire(new CannonShell.FireConfig
            {
                Scale = scale,
                InitialPosition = muzzlePosition,
                DirectionToTarget = directionToTarget,
                LaunchAngle = muzzleElevation,
                LaunchVelocity = firePackage.AimPower,
                NominalTargetDistance = firePackage.NominalTargetDistance,
                FlightUpdate = (_, flightTime, flightFraction) =>
                {
                    // Once the shell is past it's halfway distance, make it visible to all players
                    if (!haveMadeVisible && flightFraction > 0.5)
                    {
                        haveMadeVisible = true;
                        shell.SetLayer(0);
                    }
                    
                    if (flightTime > 30) shell.Terminate();
                },
                FuseEvaluator = firePackage.FuseEvaluator ?? ((_, _) => FuseResult.Detonate),
                OnDetonation = _ => {
                    cannonShellPool.ReturnToPool(shell);
                    firePackage.OnImpact?.Invoke();
                }
            });
        }

        private bool IsOnTarget() => _rotationSlewController.IsOnTarget && _elevationSlewController.IsOnTarget;
        
        // Update is called once per frame
        private void Update()
        {
            if (_locked) return;

            // Rotate the turret
            var rotation = _rotationSlewController.Update(Time.deltaTime);
            transform.eulerAngles = _rotationSlewController.IsOnTarget ? 
                new Vector3(0, _rotationSlewController.Target, 0) : new Vector3(0, rotation, 0);
            
            // Elevate the barrel
            var elevation = _elevationSlewController.Update(Time.deltaTime);
            _barrel.transform.localEulerAngles = _elevationSlewController.IsOnTarget ? 
                new Vector3(-_elevationSlewController.Target, 0, 0) : new Vector3(-elevation, 0, 0);

            // Fire if we're on target, and we've been commanded to fire on the target, do so.
            MaybeFireOnTarget();
        }

        private void MaybeFireOnTarget()
        {
            // Get the next fire package in the queue if we don't have one
            if (_currentFirePackage == null)
            {
                if (_firePackageQueue.TryDequeue(out var package))
                {
                    _currentFirePackage = package;
                    _rotationSlewController.Target = package.Rotation;
                    _elevationSlewController.Target = package.Elevation;
                }
            }

            // If we still don't have a fire package, return
            if (_currentFirePackage == null) return;

            // Return if we're not on target, otherwise fire and clear the target package
            if (!IsOnTarget()) return;
            
            // Halt the turret rotation, and jump to snap transform - this is a bit of a hack to
            // ensure that burst fires doesn't ent up landing in different tiles.
            // This is desirable for this game, but probably isn't the best general behavior.
            _rotationSlewController.Reset(_rotationSlewController.Target);
            _elevationSlewController.Reset(_elevationSlewController.Target);
            transform.eulerAngles = new Vector3(0, _rotationSlewController.Target, 0);
            _barrel.transform.localEulerAngles = new Vector3(-_elevationSlewController.Target, 0, 0);
            
            Fire(_currentFirePackage);
            _currentFirePackage = null;
        }

        public float GetFollowZoom() => followZoom;
        public Vector3 GetPosition() => transform.position;
        public float GetFlightFraction() => 0;
        public int GetDistanceToTarget() => -1;
        public float GetFollowFinishTime() => float.MaxValue;
    }
}
