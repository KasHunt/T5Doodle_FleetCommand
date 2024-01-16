using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using UnityEngine;
using Easing = UnityEngine.UIElements.Experimental.Easing;

namespace Code.Scripts
{
    public class AircraftCarrier : CombatVesselBase, IRunwayProvider
    {
        public GameObject bigRadar;
        public GameObject upperRadar;
        public GameObject lowerRadar;
        public GameObject towerRadar;
        public List<GameObject> props;

        public Vector3 bigRadarRotation = new(0, 0, 15f);
        public Vector3 upperRadarRotation = new(0, 0, 35f);
        public Vector3 lowerRadarRotation = new(0, 0, -45f);
        public Vector3 towerRadarRotation = new(0, 0, -25f);
        public Vector3 propRotation = new(0, 45f, 0);

        public float liftSpeed = 4;
        
        public GameObject upLift;
        public GameObject downLift;
        
        public List<GameObject> upLiftPath;
        public List<GameObject> downLiftPath;
        public List<GameObject> catapultStartPath;
        public List<GameObject> catapultLaunchPath;
        public List<GameObject> patrolRoutePath;
        public List<GameObject> holdingPatternPath;
        public List<GameObject> arrestorsPath;
        public List<GameObject> parkingPath;
        public List<GameObject> approach;

        public GameObject aircraftTemplate;
        public int aircraftSpawnCount;
        public float aircraftEnableDelay = 3f;
        public float aircraftSpawnDelay = 15f;
        public float fuelToFireLimit = 20f;

        public MissilePool missilePool;
        
        private enum Lift
        {
            UpLift,
            DownLift
        }
        
        private class LiftInfo
        {
            public bool Moving;
            public IRunwayProvider.LiftState State;
            public GameObject LiftGameObject;
            public Transform ObjectOnLift;
            public float Progress;
        }
        
        private readonly Dictionary<Lift, LiftInfo> _liftInfo = new();
        private float _liftTravel;
        private bool _planeLanding;
        private bool _planeTakingOff;
        private readonly List<F35> _spawnedAircraft = new();
        private float _nextAircraftActivateTime = float.MaxValue;
        private int _lastAircraftActivateIndex;
        
        protected override List<Vector3> CellTargetOffsets => new()
        {
            new Vector3(0, 8, 140),
            new Vector3(0, 8, 70),
            new Vector3(0, 8, 0),
            new Vector3(0, 8, -70),
            new Vector3(0, 8, -140)
        };

        protected override string VesselClassName => "Aircraft Carrier";

        private class AircraftFireReservation : FireReservation
        {
            public readonly F35 PatrollingAircraft;
            public readonly F35.TargetReservation TargetReservation;
            
            public AircraftFireReservation(
                string fireOriginLabel,
                FireFunction fireFunction, 
                SeaWar.IGameboardFollowTarget followTarget,
                F35 patrollingAircraft,
                F35.TargetReservation targetReservation) : base(fireOriginLabel, fireFunction, followTarget)
            {
                PatrollingAircraft = patrollingAircraft;
                TargetReservation = targetReservation;
            }
        }
        
        private void Awake()
        {
            _liftInfo[Lift.DownLift] = new LiftInfo
            {
                State = IRunwayProvider.LiftState.Raised,
                LiftGameObject = downLift
            };
            
            _liftInfo[Lift.UpLift] = new LiftInfo
            {
                State = IRunwayProvider.LiftState.Raised,
                LiftGameObject = upLift
            };

            _liftTravel = GetRunwayAltitude() - GetParkingAltitude();
        }

        protected override void Start()
        {
            base.Start();
            if (passive) return;
            
            for (var i = 0; i < aircraftSpawnCount; i++)
            {
                var newAircraft = Instantiate(aircraftTemplate, Vector3.zero, Quaternion.identity);
                var f35 = newAircraft.GetComponent<F35>();
                f35.runway = gameObject;

                f35.PendingState = i switch
                {
                    // Stage the immediate launch fighters
                    0 => F35.AircraftState.TaxiingForTakeoff,
                    1 => F35.AircraftState.TaxiingForTakeoff,
                    _ => f35.PendingState
                };

                f35.missilePool = missilePool;

                newAircraft.SetActive(false);
                ApplyLayerToChildren(newAircraft.transform);
                _spawnedAircraft.Add(f35);
            }
        }

        protected override void Update()
        {
            // Update the common combat vessel, and skip the rest if we're destroyed
            base.Update();
            if (IsDestroyed()) return;
            
            MaybeActuateLift(Lift.DownLift);
            MaybeActuateLift(Lift.UpLift);
            RotateRadars();
            RotateProps();
            MaybeActivateAircraft();
        }

        private void StartAircraftEnable()
        {
            // Position the aircraft
            for (var i = 0; i < aircraftSpawnCount; i++)
            {
                var spawnedAircraft = _spawnedAircraft[i];
                var aircraftTransform = spawnedAircraft.transform;
                var position = i switch {
                    < 2 => catapultStartPath[0].transform.position + new Vector3(i * 10, 0, 0),
                    _ => parkingPath[^1].transform.position + new Vector3(i * -10, 0, 0)
                };
                aircraftTransform.localRotation = transform.localRotation;
                aircraftTransform.position = position;
                aircraftTransform.gameObject.SetActive(true);
            }

            // Schedule delayed enablement of the aircraft
            StartCoroutine(EnableAircraftCoroutine(aircraftEnableDelay, 2f));
        }

        private IEnumerator EnableAircraftCoroutine(float delay, float interval)
        {
            yield return new WaitForSeconds(delay);
            if (_spawnedAircraft.Count >= 1)
            {
                _spawnedAircraft[0].patrolEnabled = true;
                _lastAircraftActivateIndex++;
            }
            yield return new WaitForSeconds(interval);
            if (_spawnedAircraft.Count >= 2)
            {
                _spawnedAircraft[1].patrolEnabled = true;
                _lastAircraftActivateIndex++;
            }
            
            // Begin takeoff for the next aircraft in parking
            _nextAircraftActivateTime = Time.time;
        }
        
        private void MaybeActivateAircraft()
        {
            var now = Time.time;
            
            if (_lastAircraftActivateIndex >= _spawnedAircraft.Count) return;
            if (now < _nextAircraftActivateTime) return;
            
            _spawnedAircraft[_lastAircraftActivateIndex].patrolEnabled = true;
            _lastAircraftActivateIndex++;
            _nextAircraftActivateTime = now + aircraftSpawnDelay;
        }
        
        private void RotateRadars()
        {
            lowerRadar.transform.Rotate(lowerRadarRotation * Time.deltaTime);
            upperRadar.transform.Rotate(upperRadarRotation * Time.deltaTime);
            bigRadar.transform.Rotate(bigRadarRotation * Time.deltaTime);
            towerRadar.transform.Rotate(towerRadarRotation * Time.deltaTime);
        }

        private void RotateProps()
        {
            foreach (var prop in props) prop.transform.Rotate(propRotation  * Time.deltaTime);
        }

        private void MaybeActuateLift(Lift lift)
        {
            var liftInfo = _liftInfo[lift];
            
            // Return if there's nothing to do
            if (!liftInfo.Moving) return;

            // Update the progress
            liftInfo.Progress += Mathf.Clamp01(Time.deltaTime / liftSpeed) *
                                 (liftInfo.State == IRunwayProvider.LiftState.Lowered ? 1 : -1);
            
            // Apply animations
            var animationProgress = Easing.InOutQuad(liftInfo.Progress);
            var liftPositionY = GetRunwayAltitude() - _liftTravel * animationProgress;

            // Update the lift position
            var transformPosition = liftInfo.LiftGameObject.transform.position;
            transformPosition.y = liftPositionY;
            liftInfo.LiftGameObject.transform.position = transformPosition;

            // Update the lifted object position
            var objectOnLift = liftInfo.ObjectOnLift;
            if (objectOnLift)
            {
                transformPosition = objectOnLift.position;
                transformPosition.y = liftPositionY;
                objectOnLift.position = transformPosition;
            }

            // Change state and the clear the lifting object if we've finished actuating
            switch (liftInfo.Progress)
            {
                case >= 1 when liftInfo.State == IRunwayProvider.LiftState.Lowered:
                    liftInfo.Moving = false;
                    liftInfo.ObjectOnLift = null;
                    break;
                
                case <= 0 when liftInfo.State == IRunwayProvider.LiftState.Raised:
                    liftInfo.Moving = false;
                    liftInfo.ObjectOnLift = null;
                    break;
            }

            // Store the updated state
            _liftInfo[lift] = liftInfo;
        }
        
        private float GetParkingAltitude() => parkingPath[0].transform.position.y;
        private float GetRunwayAltitude() => catapultStartPath[0].transform.position.y;

        public IEnumerable<Vector3> GetRunwayWaypoints(IRunwayProvider.RunwayWaypoints waypoint) => waypoint switch
        {
            IRunwayProvider.RunwayWaypoints.UpLift => upLiftPath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.DownLift => downLiftPath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.CatapultStart => catapultStartPath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.CatapultEnd => catapultLaunchPath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.PatrolRoute => patrolRoutePath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.HoldingPattern => holdingPatternPath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.Arrestors => arrestorsPath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.ParkingStand => parkingPath.Select(obj => obj.transform.position),
            IRunwayProvider.RunwayWaypoints.Approach => approach.Select(obj => obj.transform.position),
            _ => throw new ArgumentOutOfRangeException(nameof(waypoint), waypoint, null)
        };

        private Lift GetClosestLift(Vector3 position)
        {
            var downLiftDistance = (downLift.transform.position - position).sqrMagnitude;
            var upLiftDistance = (upLift.transform.position - position).sqrMagnitude;
            return downLiftDistance < upLiftDistance ? Lift.DownLift : Lift.UpLift; 
        }
        
        private void SetLiftState(Lift lift, IRunwayProvider.LiftState liftState)
        {
            _liftInfo[lift].State = liftState;
            _liftInfo[lift].Moving = true;
        }

        private void UseLift(Lift lift, IRunwayProvider.LiftState liftState, Transform moveTarget)
        {
            if (moveTarget) _liftInfo[lift].ObjectOnLift = moveTarget;
            SetLiftState(lift, liftState);
        }
        
        public void OperateLift(Vector3 liftPosition, IRunwayProvider.LiftState targetState, Transform moveTarget) =>
            UseLift(GetClosestLift(liftPosition), targetState, moveTarget);

        public IRunwayProvider.LiftState? GetLiftState(Vector3 liftPosition)
        {
            var info = _liftInfo[GetClosestLift(liftPosition)];
            return info.Moving ? null : info.State;
        }
        
        public bool RequestLandingClearance()
        {
            if (_planeLanding) return false;
            if (IsDestroyed()) return false;
            
            _planeLanding = true;
            return true;
        }

        public void ReleaseLandingClearance()
        {
            _planeLanding = false;
            SetLiftState(Lift.DownLift, IRunwayProvider.LiftState.Raised);
        }
        
        public bool RequestTakeoffClearance()
        {
            if (_planeTakingOff) return false;
            
            _planeTakingOff = true;
            return true;
        }

        public void ReleaseTakeoffClearance()
        {
            _planeTakingOff = false;
        }
        
        public override bool PrepareToFire(int initialLayer, out FireReservation reservation)
        {
            reservation = new FireReservation();
            
            // Fail if the aircraft carrier has been destroyed
            if (IsDestroyed()) return false;
            
            // Get a patrolling plane or exit failure
            var patrolling = GetPatrollingPlane(fuelToFireLimit);
            if (!patrolling) return false;

            // See if we can actually fire (missile available)
            // This should always be true since planes without
            // missiles aren't considered to be patrolling
            if (!patrolling.PrepareToFire(initialLayer, out var targetReservation))
            {
                Debug.LogFormat("Patrolling plane is not ready to fire!");
                return false;
            }

            var followTargetProxy = new SeaWar.FollowTargetProxy(patrolling);
            reservation = new AircraftFireReservation(GetIdent(), FireAtTarget, followTargetProxy, patrolling, targetReservation);
            return true;
        }
        
        private void FireAtTarget(FireReservation reservation, Vector3 target, bool targetIsVessel, Action onImpact)
        {
            var fireReservation = (AircraftFireReservation) reservation;
            var followMissile = fireReservation.PatrollingAircraft.FireAtTarget(fireReservation.TargetReservation, target, onImpact, targetIsVessel);
            ((SeaWar.FollowTargetProxy)fireReservation.FollowTarget).Principal = followMissile;
        }

        public override void Enable() => StartAircraftEnable();
        
        public override void Terminate() {
            _spawnedAircraft.ForEach(aircraft => Destroy(aircraft.gameObject));
        }

        [CanBeNull]
        private F35 GetPatrollingPlane(float fuelBeforeRtbLimit)
        {
            return _spawnedAircraft
                .Where(aircraft => aircraft.IsOnPatrol && 
                                   aircraft.FuelRemaining - aircraft.returnToBaseFuelLevel > fuelBeforeRtbLimit)
                .OrderBy(aircraft => aircraft.FuelRemaining)
                .FirstOrDefault();
        }
    }
}
