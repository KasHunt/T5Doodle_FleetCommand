using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Code.Scripts.Utils;
using JetBrains.Annotations;
using Missiles;
using Missiles.Implementations;
using UnityEngine;
using Random = UnityEngine.Random;

namespace Code.Scripts
{
    public class F35 : MonoBehaviour, SeaWar.IGameboardFollowTarget
    {
        [Header("Aircraft")]
        public GameObject aircraft;
        
        [Header("Internal Bay")]
        public List<GameObject> portBayDoors;
        public List<GameObject> starboardBayDoors;
        public GameObject portBayHardpoint;
        public GameObject starboardBayHardpoint;

        [Header("Gear Doors")]
        public List<GameObject> noseGearDoors;
        public List<GameObject> mainGearForeDoors;
        public List<GameObject> mainGearAftDoors;
    
        [Header("Gear")]
        public GameObject noseGear;
        public List<GameObject> mainGear;
        public List<GameObject> mainGearUpperSupports;
        public List<GameObject> mainGearLowerSupports;
        public List<GameObject> mainGearWheelhubs;

        [Header("Control Surfaces")]
        public GameObject portElevator;
        public GameObject starboardElevator;
        public GameObject portFlaperon;
        public GameObject starboardFlaperon;
        public List<GameObject> rudders;

        [Header("Timings")]
        [Min(0.1f)] public float gearDoorTime = 2f;
        [Min(0.1f)] public float gearActuateTime = 2f;
        
        [Min(0.1f)] public float bayActuateTime = 1f;
        [Min(0.1f)] public float bayDoorHoldTime = 1f;

        [Header("Speed & Acceleration")]
        [Min(0f)] public float taxiSpeed = 0.2f;
        [Min(0f)] public float landingSpeed = 2f;
        [Min(0f)] public float flightSpeed = 4f;
        
        [Min(0f)] public float launchAcceleration = 5f;
        [Min(0f)] public float flightAcceleration = 2f;
        [Min(0f)] public float arrestingAcceleration = 8f;
        [Min(0f)] public float groundAcceleration = 1f;
        
        [Header("Flight Characteristics")]
        [Min(0f)] public float airTurnRate = 90f;
        [Min(0f)] public float airTurnRateSpeedCoefficient = 1f;
        [Min(0f)] public float groundTurnRate = 360f;
        [Min(0f)] public float climbRate = 1f;

        public ParticleSystem exhaustParticleSystem;

        public float followOriginZoom = 5f;
        
        public float flightAltitude = 80f;
        public float flightAltitudeJitter = 5f;
        [Min(0f)] public float maxRoll = 45f;
        [Min(0.1f)] public float rollAngleMultiplier = 40f;
        [Min(0.1f)] public float pitchAngleMultiplier = 40f;
        [Min(0.1f)] public float flaperonRollMultiplier = 40f;
        [Min(1f)] public float maxOffAxisFire = 15f;
        [Min(1f)] public float overshootHoldTime = 2f;
        [Range(0, 1)] public float rollAngleSmoothing = 0.05f;
        [Range(0, 1)] public float flaperonAngleSmoothing = 0.05f;
        [Range(0, 1)] public float heightDeltaSmoothing = 0.05f;
        
        public AudioClip takeoffSound;
        public AudioClip touchdownSound;
        public AudioClip targetConfirmedSound;
        
        public float waypointTolerance = 0.3f;

        public float startingFuel = 50f;
        public float fuelBurnPerSecond = 1f;
        public float returnToBaseFuelLevel = 40f;
        
        public bool patrolEnabled;
        
        public Missile missileTemplate;
        
        public GameObject runway;
        public AircraftState? PendingState;

        public float FuelRemaining { get; private set; }

        public MissilePool missilePool;
        
        ///////////////////////

        public enum AircraftState
        {                           //  TARGET SPEED │     ACCELERATION      │ TARGET ALTITUDE
                                    // ──────────────┼───────────────────────┼──────────────────
            Parked,                 //             0 │  groundAcceleration   │ parkingAltitude
            WaitingForLift,         //             0 │         N/A           | parkingAltitude
            TaxiingFromParking,     //     taxiSpeed │  groundAcceleration   │ parkingAltitude
            LiftAscending,          //      N/A      │         N/A           │     N/A
            TaxiingForTakeoff,      //     taxiSpeed │  groundAcceleration   │ runwayAltitude
            Launching,              //   flightSpeed │  launchAcceleration   │ runwayAltitude
            Patrolling,             //   flightSpeed │  flightAcceleration   │ flightAltitude
            Attacking,              //   flightSpeed │  flightAcceleration   │ flightAltitude
            WaitingToLand,          //   flightSpeed │  flightAcceleration   │ flightAltitude
            Approach,               //   flightSpeed │  flightAcceleration   │ flightAltitude
            Landing,                //  landingSpeed │  flightAcceleration   │ runwayAltitude
            Arresting,              //             0 │ arrestingAcceleration │ runwayAltitude
            TaxiingAfterLanding,    //     taxiSpeed │  groundAcceleration   │ runwayAltitude
            LiftDescending,         //      N/A      │         N/A           │     N/A
            TaxiingToParking,       //     taxiSpeed │  groundAcceleration   │ parkingAltitude
            Crashing
        }
        
        private enum Side
        {
            Port,
            Starboard
        }
        
        private enum LandingGearState
        {
            Raised,
            Raising,
            Lowering,
            Lowered
        }

        private enum BayDoorState
        {
            Closed,
            Closing,
            Opening,
            Open
        }

        private enum TargetDirection
        {
            Unknown,
            Ahead,
            Behind
        }

        private Dictionary<GameObject, Quaternion> _initialRotations = new();

        private readonly Queue<float> _rollAngles = new();
        private readonly Queue<float> _flaperonAngles = new();
        private readonly Queue<float> _heightDeltas = new();

        private const int SMOOTHING_QUEUE_LENGTH = 30;

        private AircraftState _aircraftState = AircraftState.Parked;
        private float _landingGearProgress;
        private LandingGearState _landingGearState = LandingGearState.Lowering;
        
        private readonly LinkedList<Vector3> _waypoints = new();
        private Vector3 _lastWaypoint;

        [CanBeNull] private Action _missileOnImpact;
        private FuseResult _missileImpactResult;
        private Vector3? _missileTarget;
        private readonly SeaWar.FollowTargetProxy _followTargetProxy;
        private float _currentSpeed;
        private float _squaredDistanceToWaypoint;
        private IRunwayProvider _runwayProvider;
        private Rigidbody _rigidBody;
        private ParticleSystem _engineParticleSystem;
        
        private class MissileBayInfo
        {
            public bool Fired;
            public bool Available = true;
            public float BayProgress;
            public BayDoorState BayState = BayDoorState.Closed;
            public Vector3 InitialPosition;
            public List<GameObject> BayDoors;
        }
        
        private readonly Dictionary<Side, MissileBayInfo> _missiles = new();
        
        private TargetDirection _waypointDirection = TargetDirection.Unknown;
        private float _overshootHoldUntil;
        private float _previousRoll;
        private float _previousAngleBetween;
        private bool _haveLandingClearance;
        private TargetReservation _pendingReservation;

        public F35()
        {
            _followTargetProxy = new SeaWar.FollowTargetProxy(this);
        }

        private float ClimbRatePhaseMultiplier => _aircraftState is AircraftState.Launching ? 2 : 1;
        
        private float TargetSpeedForAircraftState => _aircraftState switch
            {
                AircraftState.Parked => 0,
                AircraftState.WaitingForLift => 0,
                AircraftState.TaxiingFromParking => taxiSpeed,
                AircraftState.LiftAscending => 0,
                AircraftState.TaxiingForTakeoff => taxiSpeed,
                AircraftState.Launching => flightSpeed,
                AircraftState.Patrolling => flightSpeed,
                AircraftState.Attacking => flightSpeed,
                AircraftState.WaitingToLand => flightSpeed,
                AircraftState.Approach => flightSpeed,
                AircraftState.Landing => landingSpeed,
                AircraftState.Arresting => 0,
                AircraftState.TaxiingAfterLanding => taxiSpeed,
                AircraftState.LiftDescending => 0,
                AircraftState.TaxiingToParking => taxiSpeed,
                AircraftState.Crashing => 0,
                _ => throw new ArgumentOutOfRangeException()
            };
        
        private float AccelerationForAircraftState => _aircraftState switch
        {
            AircraftState.Parked => groundAcceleration,
            AircraftState.WaitingForLift => groundAcceleration,
            AircraftState.TaxiingFromParking => groundAcceleration,
            AircraftState.LiftAscending => groundAcceleration,
            AircraftState.TaxiingForTakeoff => groundAcceleration,
            AircraftState.Launching => launchAcceleration,
            AircraftState.Patrolling => flightAcceleration,
            AircraftState.Attacking => flightAcceleration,
            AircraftState.WaitingToLand => flightAcceleration,
            AircraftState.Approach => flightAcceleration,
            AircraftState.Landing => flightAcceleration,
            AircraftState.Arresting => arrestingAcceleration,
            AircraftState.TaxiingAfterLanding => groundAcceleration,
            AircraftState.LiftDescending => groundAcceleration,
            AircraftState.TaxiingToParking => groundAcceleration,
            AircraftState.Crashing => 0,
            _ => throw new ArgumentOutOfRangeException()
        };

        private bool IsInAir => _aircraftState is 
            AircraftState.Launching or
            AircraftState.Patrolling or
            AircraftState.Attacking or 
            AircraftState.WaitingToLand or
            AircraftState.Approach or
            AircraftState.Landing;

        private bool IsOnLift => _aircraftState is AircraftState.LiftAscending or AircraftState.LiftDescending;

        private bool IsLanding => _aircraftState is AircraftState.Landing or AircraftState.Arresting;
        
        private LandingGearState GearStateForAircraftState => 
            _aircraftState is 
                AircraftState.Patrolling or
                AircraftState.Attacking or 
                AircraftState.WaitingToLand ? 
                    LandingGearState.Raising : LandingGearState.Lowering;
        
        private void SetBayOpen(Side side, bool open)
        {
            switch (open)
            {
                // Return early for requests to open if we already are open or opening
                case true when _missiles[side].BayState == BayDoorState.Open:
                case true when _missiles[side].BayState == BayDoorState.Opening:
                
                // Return early for requests to close if we already are closed or closing
                case false when _missiles[side].BayState == BayDoorState.Closed:
                case false when _missiles[side].BayState == BayDoorState.Closing:
                    return;
                default:
                    _missiles[side].BayState = open ? BayDoorState.Opening : BayDoorState.Closing;
                    break;
            }
        }

        private void ApplyGearStateForAircraftState()
        {
            var state = GearStateForAircraftState;
            switch (state)
            {
                // Discard invalid state requests
                case LandingGearState.Lowered:
                case LandingGearState.Raised:
                    return;
                    
                // Return early for requests to lower if we already are lowered or lowering
                case LandingGearState.Lowering when _landingGearState == LandingGearState.Lowered:
                case LandingGearState.Lowering when _landingGearState == LandingGearState.Lowering:
                
                // Return early for requests to raise if we already are raised or raising
                case LandingGearState.Raising when _landingGearState == LandingGearState.Raised:
                case LandingGearState.Raising when _landingGearState == LandingGearState.Raising:
                    return;
                
                default:
                    _landingGearState = state;
                    break;
            }
        }

        public bool IsOnPatrol => _aircraftState == AircraftState.Patrolling;
        
        public class TargetReservation
        {
            public readonly MissileBase Missile;

            public TargetReservation(MissileBase missile)
            {
                Missile = missile;
            }
        }
        
        public bool PrepareToFire(int initialLayer, out TargetReservation targetReservation)
        {
            targetReservation = new TargetReservation(null);
            
            // Get the missile that will be fired (or null if no missile is available)
            if (!_missiles.TryFirstOrDefault(ele => ele.Value.Available, out var available)) return false;

            Debug.Log("F35 preparing to fire");

            available.Value.Available = false;
            targetReservation = new TargetReservation(missilePool.TakeFromPool());
            targetReservation.Missile.SetLayer(initialLayer);
            
            // Switch to attack state
            PendingState = AircraftState.Attacking;
            return true;
        }

        public SeaWar.IGameboardFollowTarget FireAtTarget(TargetReservation reservation, Vector3 target, Action onImpact, bool targetIsVessel)
        {
            SoundManager.Instance.PlaySound(targetConfirmedSound, 0.5f);
            
            target.y = flightAltitude;
            _waypoints.Clear();
            _waypoints.AddLast(target);
            _waypointDirection = TargetDirection.Unknown;

            target.y = 0.4f;
            _missileTarget = target;
            _missileOnImpact = onImpact;
            _missileImpactResult = targetIsVessel ? FuseResult.Detonate : FuseResult.Splash;
            _pendingReservation = reservation;
            _followTargetProxy.Principal = this;
            
            return _followTargetProxy;
        }
        
        private void MaybeActuateGear()
        {
            ApplyGearStateForAircraftState();
            
            // Return if there's nothing to do
            if (_landingGearState is LandingGearState.Lowered or LandingGearState.Raised) return;

            // Update the progress
            var totalTime = (gearDoorTime * 2) + gearActuateTime;
            var doorFraction = gearDoorTime / totalTime;
            var gearFraction = 1 - doorFraction * 2;
            var doorClosePoint = 1 - doorFraction;

            var stepFraction = Time.deltaTime / totalTime;
            var progressDirection = _landingGearState == LandingGearState.Raising ? 1 : -1;
            _landingGearProgress = Mathf.Clamp01(_landingGearProgress + stepFraction * progressDirection);
        
            // Compute component progress
            float foreDoorProgress;
            if (_landingGearProgress <= doorFraction)
            {
                foreDoorProgress = 1 - Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(_landingGearProgress, 0, doorFraction));    
            }
            else
            {
                foreDoorProgress = Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(_landingGearProgress, doorClosePoint, doorFraction));
            }
            var aftDoorProgress = Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(_landingGearProgress, doorClosePoint, doorFraction));
            var gearProgress = Easing.InOutQuad(AnimationUtils.ComputeSubAnimationTime(_landingGearProgress, doorFraction, gearFraction));
        
            // Apply door animations
            foreach (var door in noseGearDoors) RotationUtils.SetLocalRotationY(door, aftDoorProgress * 90, _initialRotations);
            foreach (var door in mainGearForeDoors) RotationUtils.SetLocalRotationY(door, foreDoorProgress * 60, _initialRotations);
            foreach (var door in mainGearAftDoors) RotationUtils.SetLocalRotationY(door, aftDoorProgress * 60, _initialRotations);
            
            // Apply nose gear animations
            RotationUtils.SetLocalRotationX(noseGear, gearProgress * -110, _initialRotations);
            
            // Apply main gear animations
            foreach (var gear in mainGear) RotationUtils.SetLocalRotationX(gear, gearProgress * -90, _initialRotations);
            foreach (var gear in mainGearWheelhubs) RotationUtils.SetLocalRotationZ(gear, gearProgress * -90, _initialRotations);
            foreach (var gear in mainGearLowerSupports) RotationUtils.SetLocalRotationX(gear, gearProgress * -45, _initialRotations);
            foreach (var gear in mainGearUpperSupports) RotationUtils.SetLocalRotationX(gear, gearProgress * 170, _initialRotations);

            _landingGearState = _landingGearProgress switch
            {
                // Change state if we've finished actuating
                >= 1 when _landingGearState == LandingGearState.Raising => LandingGearState.Raised,
                <= 0 when _landingGearState == LandingGearState.Lowering => LandingGearState.Lowered,
                _ => _landingGearState
            };
        }
    
        private void MaybeActuateBay(Side side)
        {
            var info = _missiles[side];
            
            // Return if there's nothing to do
            if (info.BayState is BayDoorState.Closed or BayDoorState.Open) return;

            // Update the progress
            info.BayProgress = Mathf.Clamp01(info.BayProgress + Time.deltaTime / bayActuateTime * (info.BayState == BayDoorState.Opening ? 1 : -1));
            var doorProgress = Easing.InOutQuad(info.BayProgress);
        
            // Apply animations
            foreach (var door in info.BayDoors) RotationUtils.SetLocalRotationZ(door, doorProgress * -90, _initialRotations);

            // Change state if we've finished actuating
            info.BayState = doorProgress switch
            {
                >= 1 when info.BayState == BayDoorState.Opening => BayDoorState.Open,
                <= 0 when info.BayState == BayDoorState.Closing => BayDoorState.Closed,
                _ => info.BayState
            };
        }

        private float GetAveragedRoll(float angle)
        {
            while (_rollAngles.Count >= SMOOTHING_QUEUE_LENGTH) _rollAngles.Dequeue();
            _rollAngles.Enqueue(angle);
            
            return _rollAngles.DecayingAverage(f => f, rollAngleSmoothing);
        }
        
        private float GetAveragedHeightDelta(float heightDelta)
        {
            while (_heightDeltas.Count >= SMOOTHING_QUEUE_LENGTH) _heightDeltas.Dequeue();
            _heightDeltas.Enqueue(heightDelta);
            
            return _heightDeltas.DecayingAverage(f => f, heightDeltaSmoothing);
        }
        
        private float GetAveragedFlaperon(float angle)
        {
            while (_flaperonAngles.Count >= SMOOTHING_QUEUE_LENGTH) _flaperonAngles.Dequeue();
            _flaperonAngles.Enqueue(angle);
            
            return _flaperonAngles.DecayingAverage(f => f, flaperonAngleSmoothing);
        }

        private void MovePlane(out float horizontalAngleToTarget)
        {
            // Plans on lift are handled separately
            if (IsOnLift)
            {
                horizontalAngleToTarget = 0;
                return;
            }
            
            UpdateSpeed();

            // Interpolate between current y and target y to limit vertical movement
            var target = _waypoints.FirstOrDefault();
            var targetDelta = target - transform.position;
            var yDelta = targetDelta.y;
            if (!IsInAir) yDelta = 0;
            
            // Compute the angle in the X/Z plane between the forward vector, and the target vector
            targetDelta.y = 0;
            horizontalAngleToTarget = Vector3.SignedAngle(transform.forward, targetDelta, Vector3.up);
            var deltaAltitude = Mathf.Clamp(yDelta, -climbRate, climbRate) * ClimbRatePhaseMultiplier * Time.deltaTime;
            
            RotateTowardsTarget(horizontalAngleToTarget);

            var forwardPositionChange = _currentSpeed * Time.deltaTime * Vector3.forward;
            var heightPositionChange = new Vector3(0, deltaAltitude, 0);
            transform.Translate(forwardPositionChange + heightPositionChange);                
            
            ApplyPitchAndRoll(horizontalAngleToTarget, deltaAltitude);
            DetectOvershoot(horizontalAngleToTarget);
        }
        
        private void RotateTowardsTarget(float horizontalAngleToTarget)
        {
            // Return early if we're holding course due to an overshoot 
            if (!(Time.time >= _overshootHoldUntil)) return;
            var turnDegrees = IsInAir switch
            {
                true => airTurnRate * Mathf.Lerp(1, _currentSpeed,airTurnRateSpeedCoefficient),
                false => groundTurnRate
            } * Time.deltaTime;
            
            transform.Rotate(Vector3.up, Mathf.Clamp(horizontalAngleToTarget, -turnDegrees, turnDegrees));
        }

        /// Accelerate/Decelerate to the target speed for the current aircraft state
        private void UpdateSpeed()
        {
            var targetSpeed = TargetSpeedForAircraftState;
            var acceleration = AccelerationForAircraftState;
            _currentSpeed = true switch
            {
                true when _currentSpeed < targetSpeed => Mathf.Min(_currentSpeed + acceleration * Time.deltaTime, targetSpeed),
                true when _currentSpeed > targetSpeed => Mathf.Max(_currentSpeed - acceleration * Time.deltaTime, targetSpeed),
                _ => _currentSpeed
            };
        }

        /// Fire a missile if it's pending and we're sufficiently aligned with the target
        private void MaybeFireMissile(float angleBetween)
        {
            // Return if we have no target, or we're off-axis for firing
            if (!_missileTarget.HasValue || Mathf.Abs(angleBetween) > maxOffAxisFire) return;
            
            FireMissile(_missileTarget.Value, _missileOnImpact, _missileImpactResult, _pendingReservation.Missile, _followTargetProxy);
            _missileTarget = null;
            _pendingReservation = null;
            
            // Return to patrolling state
            PendingState = AircraftState.Patrolling;
        }

        private void DetectOvershoot(float angleBetween)
        {
            var targetWasAhead = _waypointDirection == TargetDirection.Ahead;
            _waypointDirection = Mathf.Abs(angleBetween) < 90 ? TargetDirection.Ahead : TargetDirection.Behind;
            if (targetWasAhead && _waypointDirection != TargetDirection.Ahead)
            {
                _overshootHoldUntil = Time.time + overshootHoldTime;
            }
        }

        private void ApplyPitchAndRoll(float angleBetween, float deltaAltitude)
        {
            // Compute the pitch
            var pitchAngle = GetAveragedHeightDelta(deltaAltitude) * pitchAngleMultiplier;
            if (IsLanding) pitchAngle *= -1;
            
            // Compute roll
            var rawRollAngle = Mathf.Clamp((angleBetween - _previousAngleBetween) * rollAngleMultiplier, -maxRoll, maxRoll);
            var rollAngle = GetAveragedRoll(rawRollAngle);

            // If we're grounded, zero roll and pitch
            if (!IsInAir)
            {
                pitchAngle = 0;
                rollAngle = 0;
            }
            
            // Apply the pitch and roll
            aircraft.transform.localRotation = Quaternion.Euler(-pitchAngle,0,rollAngle);
            
            // Apply the second difference of the angle to the flaperons
            var rollDelta = GetAveragedFlaperon(rollAngle - _previousRoll);
            _previousRoll = rollAngle;
            
            portFlaperon.transform.localRotation = Quaternion.Euler(0, 14,0) * Quaternion.Euler(-rollDelta * flaperonRollMultiplier,0, 0);
            starboardFlaperon.transform.localRotation = Quaternion.Euler(0, -14,0) * Quaternion.Euler(rollDelta * flaperonRollMultiplier,0, 0);
            
            _previousAngleBetween = angleBetween;
        }

        private void FireMissile(Vector3 target, Action onImpact, FuseResult fuseOnImpact, MissileBase missile, SeaWar.FollowTargetProxy followTargetProxy)
        {
            if (!_missiles.TryFirstOrDefault(ele => !ele.Value.Fired, out var available)) return;
            
            StartCoroutine(FireMissileCoroutine(available.Key, target, onImpact, fuseOnImpact, missile, followTargetProxy));
        }
        
        private IEnumerator FireMissileCoroutine(Side side, Vector3 target, Action onImpact, FuseResult fuseOnImpact, MissileBase missile, SeaWar.FollowTargetProxy followTargetProxy)
        {
            var info = _missiles[side];
            info.Fired = true;
            
            // Open doors and wait for them to open
            SetBayOpen(side, true);
            while (info.BayState != BayDoorState.Open) yield return new WaitForSeconds(0.1f);
            
            // Launch missile and wait for it to leave the bay
            missile.SetMissileParent(transform);
            missile.ResetMissile(Vector3.zero, Quaternion.identity, transform.localScale);

            followTargetProxy.Principal = missile;
            var haveMadeVisible = false;
            missile.LaunchMissile(_currentSpeed, target, _ =>
            {
                if (missile != null)
                {
                    Debug.Log($"F35 returning missile to pool");
                    missilePool.ReturnToPool(missile);
                    onImpact?.Invoke();
                }
                else
                {
                    Debug.Log($"F35 skipping null missile");
                }
            },
            () => { },
                (_, _, flightFraction) =>
            {
                // Once the missile is past it's halfway distance, make it visible to all players
                if (haveMadeVisible || !(flightFraction > 0.5)) return;
                haveMadeVisible = true;
                missile.SetLayer(0);
            }, (_, _) => fuseOnImpact);
            yield return new WaitForSeconds(bayDoorHoldTime);
            
            // Return to patrolling if that's the state we're in after firing
            // (IE Don't wait until we reach the target waypoint before continuing)
            if (_aircraftState == AircraftState.Patrolling) RemoveNextWaypoint();
            
            // Close doors and wait for them to close
            SetBayOpen(side, false);
            while (info.BayState != BayDoorState.Closed) yield return new WaitForSeconds(0.1f);
        }
        
        private void CreateMissiles()
        {
            // Get center position between bay doors
            var bayPositions = new Dictionary<Side, GameObject>
            {
                [Side.Port] = portBayHardpoint,
                [Side.Starboard] = starboardBayHardpoint
            };
            
            // Create the missiles
            foreach (var (side, hardpoint) in bayPositions)
            {
                // Instantiate missiles
                var position = hardpoint.transform.localPosition;
                _missiles[side] = new MissileBayInfo
                {
                    BayDoors = side == Side.Port ? portBayDoors : starboardBayDoors,
                    InitialPosition = position
                };
            }

            RefuelAndRearm();
        }

        private void ResetMissiles()
        {
            foreach (var (_, value) in _missiles)
            {
                value.Available = true;
                value.Fired = false;
                value.BayState = BayDoorState.Closing;
            }
        }

        private void UpdateDistanceToWaypoint()
        {
            var waypoint = _waypoints.FirstOrDefault();
            var currentPosition = transform.position;

            // Remove relative altitude from the distance to waypoint check
            currentPosition.y = 0;
            waypoint.y = 0;
            
            _squaredDistanceToWaypoint = (waypoint - currentPosition).sqrMagnitude;
        }

        private void Start()
        {
            _engineParticleSystem = GetComponentInChildren<ParticleSystem>();
            _rigidBody = GetComponentInChildren<Rigidbody>();
            
            _runwayProvider = runway.GetComponentInChildren<IRunwayProvider>();
            if (_runwayProvider == null)
            {
                Debug.LogWarning("Runway object does not implement IRunwayProvider interface");
                Destroy(this);
            }
            
            CreateMissiles();
            
            _initialRotations = RotationUtils.CaptureRotations(new List<List<GameObject>>
            {
                mainGear,
                mainGearLowerSupports,
                mainGearUpperSupports,
                mainGearWheelhubs,
                mainGearAftDoors,
                mainGearForeDoors,
                new() { noseGear },
                noseGearDoors,
                portBayDoors,
                starboardBayDoors
            });
        }
        
        private void Update()
        {
            // If we're crashing, don't update
            if (!_rigidBody.isKinematic) return;
            
            // State
            UpdateDistanceToWaypoint();
            UpdateState();
            
            // Movement
            BurnFuel();
            MovePlane(out var angleToTarget);
            
            // Weapons
            MaybeFireMissile(angleToTarget);
            MaybeActuateBay(Side.Port);
            MaybeActuateBay(Side.Starboard);
            
            // Gear
            MaybeActuateGear();
        }

        private void BurnFuel()
        {
            // Treat grounded aircraft as not consuming fuel
            if (!IsInAir) return;
            
            // Burn up some fuel
            FuelRemaining -= fuelBurnPerSecond * Time.deltaTime;
        }

        private void RemoveNextWaypoint()
        {
            if (_waypoints.Count == 0) return;
            _waypoints.RemoveFirst();
            _waypointDirection = TargetDirection.Unknown;
        }
        
        private void UpdateState()
        {
            // If we're on the ground, and patrolling isn't enabled, don't change states
            if (!IsInAir && !patrolEnabled) return;
            
            // Check for return-to-base conditions
            var fuelLow = FuelRemaining < returnToBaseFuelLevel;
            var fuelEmpty = FuelRemaining <= 0;
            var ammoLow = _missiles.Count(ele => !ele.Value.Fired) == 0;
            
            // Pop the waypoint if we've reached it
            if (_waypoints.Count > 0 && IsNearWaypoint()) RemoveNextWaypoint();
            var waypointsComplete = _waypoints.Count == 0;
            
            // Compute the state transition
            var currentState = _aircraftState;
            var newState = PendingState ?? currentState switch
            {
                AircraftState.Parked when CanTakeOff() => AircraftState.WaitingForLift,
                AircraftState.WaitingForLift when IsLiftDown(_lastWaypoint) => AircraftState.TaxiingFromParking,
                AircraftState.TaxiingFromParking when waypointsComplete => AircraftState.LiftAscending,
                AircraftState.LiftAscending when IsLiftUp(_lastWaypoint) => AircraftState.TaxiingForTakeoff,
                AircraftState.TaxiingForTakeoff when waypointsComplete => AircraftState.Launching,
                AircraftState.Launching when waypointsComplete => AircraftState.Patrolling,
                AircraftState.Patrolling when fuelLow => AircraftState.WaitingToLand,
                AircraftState.Patrolling when ammoLow => AircraftState.WaitingToLand,
                AircraftState.WaitingToLand when CanLand() => AircraftState.Approach,
                AircraftState.Approach when waypointsComplete => AircraftState.Landing,
                AircraftState.Landing when waypointsComplete => AircraftState.Arresting,
                AircraftState.Arresting when _currentSpeed == 0 => AircraftState.TaxiingAfterLanding,
                AircraftState.TaxiingAfterLanding when waypointsComplete => AircraftState.LiftDescending,
                AircraftState.LiftDescending when IsLiftDown(_lastWaypoint) => AircraftState.TaxiingToParking,
                AircraftState.TaxiingToParking when waypointsComplete => AircraftState.Parked,
                _ => currentState // Otherwise don't change state
            };
            PendingState = null;

            // Crash if we've run out of fuel, or if the runway is destroyed while we're on the ground
            if (fuelEmpty || !IsInAir && _runwayProvider.IsDestroyed()) newState = AircraftState.Crashing;
            
            // Return if we've not changed state
            var stateChanged = newState != currentState;
            var patrolRouteRequired = newState == AircraftState.Patrolling && waypointsComplete;
            var waitingToLandWaypointRequired = newState == AircraftState.WaitingToLand && waypointsComplete;
            
            if (!stateChanged && !patrolRouteRequired && !waitingToLandWaypointRequired) return;
            
            // Set the new state
            _aircraftState = newState;
            
            PerformStateEntryAction(newState);
        }

        private void PerformStateEntryAction(AircraftState newState)
        {
            // Do one-time events on state transition
            switch (newState)
            {
                // States with actions on enter
                case AircraftState.WaitingForLift:
                    RefuelAndRearm();
                    SetWaypoints(IRunwayProvider.RunwayWaypoints.UpLift);
                    _runwayProvider.OperateLift(_lastWaypoint, IRunwayProvider.LiftState.Lowered);
                    break;
                case AircraftState.LiftAscending:
                    _runwayProvider.OperateLift(_lastWaypoint, IRunwayProvider.LiftState.Raised, transform);
                    break;
                case AircraftState.TaxiingForTakeoff:
                    SetWaypoints(IRunwayProvider.RunwayWaypoints.CatapultStart);
                    break;
                case AircraftState.Launching:
                    SetWaypoints(IRunwayProvider.RunwayWaypoints.CatapultEnd);
                    SoundManager.Instance.PlaySound(takeoffSound, 0.5f);
                    _runwayProvider.ReleaseTakeoffClearance();
                    exhaustParticleSystem.Play();
                    break;
                case AircraftState.Patrolling:
                    SetPatrolRoute();
                    break;
                case AircraftState.WaitingToLand:
                    SetLandingStackWaypoint();
                    break;
                case AircraftState.Approach:
                    SetWaypoints(IRunwayProvider.RunwayWaypoints.Approach);
                    break;
                case AircraftState.Landing:
                    SetWaypoints(IRunwayProvider.RunwayWaypoints.Arrestors);
                    SoundManager.Instance.PlaySound(touchdownSound, 0.5f);
                    break;
                case AircraftState.Attacking:
                    break;
                case AircraftState.TaxiingAfterLanding:
                    SetWaypoints(IRunwayProvider.RunwayWaypoints.DownLift);
                    break;
                case AircraftState.LiftDescending:
                    _runwayProvider.OperateLift(_lastWaypoint, IRunwayProvider.LiftState.Lowered, transform);
                    break;
                case AircraftState.TaxiingToParking:
                    SetWaypoints(IRunwayProvider.RunwayWaypoints.ParkingStand);
                    break;
                case AircraftState.Parked:
                    _runwayProvider.ReleaseLandingClearance();
                    _haveLandingClearance = false;
                    break;
                case AircraftState.Crashing:
                    CrashPlane();
                    if (_haveLandingClearance) _runwayProvider.ReleaseLandingClearance();
                    break;

                // States that require no action on entry
                case AircraftState.TaxiingFromParking:
                case AircraftState.Arresting:
                    exhaustParticleSystem.Stop();
                    break;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void CrashPlane()
        {
            _rigidBody.isKinematic = false;
            _rigidBody.velocity = transform.forward * _currentSpeed;

            var crashAngularVelocity = new Vector3();
            crashAngularVelocity.Jitter(new Vector3(1,1,1));
            crashAngularVelocity.Normalize();

            // Apply angular velocity
            _rigidBody.angularVelocity = crashAngularVelocity * 5f;

            // Disable the engine exhaust
            _engineParticleSystem.Stop();
        }

        private void SetPatrolRoute()
        {
            var waypoints = _runwayProvider.GetRunwayWaypoints(IRunwayProvider.RunwayWaypoints.PatrolRoute);
            var altitude = flightAltitude + Random.Range(-flightAltitudeJitter, flightAltitudeJitter);
            SetWaypoints(waypoints.Select(waypoint => new Vector3(waypoint.x, altitude, waypoint.z)));
        }

        private void SetLandingStackWaypoint()
        {
            var waypoints = _runwayProvider.GetRunwayWaypoints(IRunwayProvider.RunwayWaypoints.HoldingPattern);
            var altitude = flightAltitude + Random.Range(-flightAltitudeJitter, flightAltitudeJitter);
            SetWaypoints(waypoints.Select(waypoint => new Vector3(waypoint.x, altitude, waypoint.z)));
        }
        
        private void RefuelAndRearm()
        {
            ResetMissiles();
            FuelRemaining = startingFuel;
        }

        private void SetWaypoints(IEnumerable<Vector3> waypoints)
        {
            _waypoints.Clear();
            foreach (var newWaypoint in waypoints) _waypoints.AddLast(newWaypoint);

            if (_waypoints.Count > 0) _lastWaypoint = _waypoints.Last.Value;
        }
        
        private void SetWaypoints(IRunwayProvider.RunwayWaypoints waypoint) =>
            SetWaypoints(_runwayProvider.GetRunwayWaypoints(waypoint));

        private bool IsLiftUp(Vector3 position) =>
            _runwayProvider.GetLiftState(position) is IRunwayProvider.LiftState.Raised;
        
        private bool IsLiftDown(Vector3 position) =>
            _runwayProvider.GetLiftState(position) is IRunwayProvider.LiftState.Lowered;

        private bool CanLand() => _haveLandingClearance = _runwayProvider.RequestLandingClearance();
        
        private bool CanTakeOff() => _runwayProvider.RequestTakeoffClearance();
        
        private bool IsNearWaypoint() => _squaredDistanceToWaypoint < waypointTolerance * waypointTolerance;

        public float GetFollowZoom() => followOriginZoom;

        public Vector3 GetPosition() => transform.position;
        
        public float GetFlightFraction() => 0;

        public int GetDistanceToTarget() => -1;

        public float GetFollowFinishTime() => float.MaxValue;
    }
}
