using System;
using System.Collections.Generic;
using System.Linq;
using JetBrains.Annotations;
using Missiles.Implementations;
using UnityEngine;

namespace Code.Scripts
{
    public class Submarine : CombatVesselBase
    {
        public MissilePool missilePool;
        
        private class SubFireReservation : FireReservation
        {
            public MissileInfo Info;
            
            public SubFireReservation(
                string fireOriginLabel,
                FireFunction fireFunction, 
                SeaWar.IGameboardFollowTarget followTarget, 
                MissileInfo info) : base(fireOriginLabel, fireFunction, followTarget)
            {
                Info = info;
            }
        }
        
        private enum HatchOpenState {
            Closed,
            Opening,
            Open,
            Closing
        }

        private enum HatchAction {
            Close,
            Open,
            NoAction
        }
        
        public GameObject hullFore;
        public GameObject hullAft;
        
        public GameObject props;
        public Vector3 propRotation = new(0, 45f, 0);
        public List<GameObject> missileHatches = new();

        public List<GameObject> hatch1HardPoints = new();

        public float hatchOpenTime = 1f;
        public float hatchDwellTime = 4f;
        public float hatchCloseTime = 1f;

        public float capsizeFractureAngle = 30;
        
        private class HatchState
        {
            public Queue<(MissileInfo info, Vector3 target)> LaunchQueue;
            public HatchOpenState OpenState;
            public float Angle;
            public float CloseTime;
        }
        private Dictionary<GameObject, HatchState> _hatchStates = new();

        private struct MissileInfo
        {
            [CanBeNull] public MissileBase Missile;
            public readonly int HatchIndex;
            [CanBeNull] public Action OnImpact;
            public FuseResult ImpactResult;

            public MissileInfo(int hatchIndex)
            {
                HatchIndex = hatchIndex;
                OnImpact = null;
                Missile = null;
                ImpactResult = FuseResult.NoAction;
            }
        } 
        
        private readonly Queue<MissileInfo> _availableMissiles = new();
        private int _launchIndex;
        private float _hatchOpenDegreesPerSecond;
        private float _hatchCloseDegreesPerSecond;
        
        protected override List<Vector3> CellTargetOffsets => new()
        {
            new Vector3(0, 0, 50),
            new Vector3(0, 0, 10),
            new Vector3(0, 0, -70)
        };

        protected override void Start()
        {
            base.Start();
            
            // Create missiles
            LoadMissiles();

            // Prepare the hatch state dictionary
            _hatchStates = missileHatches.ToDictionary(hatch => hatch, _ => new HatchState
            {
                LaunchQueue = new Queue<(MissileInfo info, Vector3 target)>(),
                OpenState = HatchOpenState.Closed,
                Angle = 0,
                CloseTime = 0
            });
            
            // Compute rotation speeds
            _hatchOpenDegreesPerSecond = -90f / hatchOpenTime;
            _hatchCloseDegreesPerSecond = 90f / hatchCloseTime;
        }

        private Vector3 PositionForHatch(int index)
        {
            var (hatch, siloIndex) = GetHatch(index);
            var siloPosition = GetSiloOffset(siloIndex);
            siloPosition.Scale(transform.localScale);
            return hatch.transform.position + siloPosition;
        }
        
        private void LoadMissiles()
        {
            for (var i = 0; i < 16; i++)
            {
                _availableMissiles.Enqueue(new MissileInfo(i));
            }
        }
        
        private (GameObject hatch, int siloIndex) GetHatch(int hatchIndex)
        {
            var siloIndex = hatchIndex % 4;
            var hatch = hatchIndex / 4;

            return (missileHatches[hatch], siloIndex);
        }
        
        private Vector3 GetSiloOffset(int siloIndex)
        {
            if (siloIndex >= hatch1HardPoints.Count) throw new ArgumentOutOfRangeException();
            return hatch1HardPoints[siloIndex].transform.position - missileHatches[0].transform.position; 
        }

        private static HatchAction GetHatchAction(HatchState currentState)
        {
            var queueEmpty = currentState.LaunchQueue.Count == 0;
            var closeTimeExceeded = Time.time > currentState.CloseTime;
            var shouldClose = queueEmpty && closeTimeExceeded;

            return shouldClose
                ? currentState.OpenState == HatchOpenState.Closed ? HatchAction.NoAction : HatchAction.Close
                : currentState.OpenState == HatchOpenState.Open ? HatchAction.NoAction : HatchAction.Open;
        }
        
        private void OpenHatch(GameObject hatch, HatchState currentState)
        {
            currentState.OpenState = HatchOpenState.Opening;
            currentState.Angle = Mathf.Clamp(currentState.Angle + 
                                             _hatchOpenDegreesPerSecond * Time.deltaTime, -90, 0);
            hatch.transform.localRotation = Quaternion.Euler(0,  0, currentState.Angle);

            // Flag as open if the hatch is now open
            if (Math.Abs(currentState.Angle - -90) < 0.1f) currentState.OpenState = HatchOpenState.Open;
        }

        private void CloseHatch(GameObject hatch, HatchState currentState)
        {
            currentState.OpenState = HatchOpenState.Closing;
            currentState.Angle = Mathf.Clamp(currentState.Angle + 
                                             _hatchCloseDegreesPerSecond * Time.deltaTime, -90, 0);
            hatch.transform.localRotation = Quaternion.Euler(0,0, currentState.Angle);
                    
            // Flag as close if the hatch is now closed
            if (currentState.Angle == 0) currentState.OpenState = HatchOpenState.Closed;
        }

        private void UpdateHatch(GameObject hatch, HatchState currentState)
        {
            switch (GetHatchAction(currentState))
            {
                case HatchAction.NoAction:
                    return;
                case HatchAction.Open:
                    OpenHatch(hatch, currentState);
                    break;
                case HatchAction.Close:
                    CloseHatch(hatch, currentState);
                    break;
                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void MaybeFireQueuedMissiles(HatchState currentState)
        {
            // If the hatch is open, fire any missiles that are waiting for it to be open and update the close time
            if (currentState.LaunchQueue.Count <= 0 || currentState.OpenState != HatchOpenState.Open) return;

            while (currentState.LaunchQueue.TryDequeue(out var element))
            {
                var haveMadeVisible = false;
                element.info.Missile.LaunchMissile(0, element.target, 
                    _ => element.info.OnImpact?.Invoke(), 
                    () =>
                    {
                        missilePool.ReturnToPool(element.info.Missile);
                        element.info.Missile = null;
                        
                        _availableMissiles.Enqueue(element.info);
                    },
                    (_, _, flightFraction) =>
                    {
                        // Once the missile is past it's halfway distance, make it visible to all players
                        if (haveMadeVisible || !(flightFraction > 0.5)) return;
                        haveMadeVisible = true;
                        element.info.Missile.SetLayer(0);
                    },
                    (_, _) => element.info.ImpactResult);
            }

            currentState.CloseTime = Time.time + hatchDwellTime;
        }
        
        protected override void Update()
        {
            // Update the common combat vessel, and skip the rest if we're destroyed
            base.Update();
            if (IsDestroyed()) return;
            
            // Spin the props
            props.transform.Rotate(propRotation * Time.deltaTime);
            
            // Actuate the missile hatches and launch missiles when hatches are open
            foreach (var (hatch, currentState) in _hatchStates)
            {
                UpdateHatch(hatch, currentState);
                MaybeFireQueuedMissiles(currentState);
            }
        }

        public override bool PrepareToFire(int initialLayer, out FireReservation reservation)
        {
            reservation = new FireReservation();
            
            if (IsDestroyed()) return false;
            
            // Get the next available missile
            var available = _availableMissiles.TryDequeue(out var missileInfo);
            if (!available) return false;
            var missile = missilePool.TakeFromPool();
            
            // Reattach the missile to the vessel
            missile.SetMissileParent(transform);
                
            // Reset the missile, and set it's initial visibility
            var missilePosition = PositionForHatch(missileInfo.HatchIndex);
            missile.ResetMissile(missilePosition, Quaternion.identity, transform.localScale);
            missile.SetLayer(initialLayer);
            
            missileInfo.Missile = missile;
            reservation = new SubFireReservation(GetIdent(), FireAtTarget, missileInfo.Missile, missileInfo);
            return true;
        }
        
        private void FireAtTarget(FireReservation reservation, Vector3 target, bool targetIsVessel, Action onImpactCallback)
        {
            // Cast back to our submarine specific fire reservation
            var fireReservation = (SubFireReservation) reservation;
            var missile = fireReservation.Info.Missile;
            if (!missile) return;

            // Determine what the missile should do when it impacts it's target
            fireReservation.Info.ImpactResult = targetIsVessel ? FuseResult.Detonate : FuseResult.Splash;
            
            // Store the impact callback
            fireReservation.Info.OnImpact = onImpactCallback;
            
            // Queue the missile for launch by a specific hatch
            var (hatch, _) = GetHatch(fireReservation.Info.HatchIndex);
            _hatchStates[hatch].LaunchQueue.Enqueue((fireReservation.Info, target));
        }

        public override void Enable() {}
        
        public override void Terminate() {}
        
        protected override void ApplyCapsizeEffect(float progress, bool isCapsizing)
        {
            hullFore.transform.localEulerAngles = new Vector3(progress * capsizeFractureAngle, 0, 0);
            hullAft.transform.localEulerAngles = new Vector3(-progress * capsizeFractureAngle, 0, 0);
        }
        
        protected override GameObject GetFireParent(int index)
        {
            return index == 2 ? hullAft : hullFore;
        }
        
        protected override string VesselClassName => "Submarine";
    }
}
