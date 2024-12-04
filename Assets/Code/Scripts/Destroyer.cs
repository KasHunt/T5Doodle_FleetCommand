using System;
using System.Collections.Generic;
using Code.Scripts.Utils;
using JetBrains.Annotations;
using Missiles.Implementations;
using UnityEngine;

namespace Code.Scripts
{
    public class Destroyer : CombatVesselBase
    {
        public List<GameObject> props;
        
        public Vector3 propRotation = new(0, 45f, 0);
        
        public List<GameObject> missileHatches = new();
        public GameObject missileHatch1Hardpoint;
        public GameObject missileHatch8Hardpoint;
        
        public MissilePool missilePool;
        
        private struct MissileInfo
        {
            [CanBeNull] public MissileBase Missile;
            public readonly GameObject Hatch;
            public int HardpointIndex;

            public MissileInfo(GameObject hatch, int hardpointIndex)
            {
                Hatch = hatch;
                Missile = null;
                HardpointIndex = hardpointIndex;
            }
        }
        
        private class DestroyerFireReservation : FireReservation
        {
            public readonly MissileInfo Info;

            public DestroyerFireReservation(
                string fireOriginLabel,
                FireFunction fireFunction, 
                SeaWar.IGameboardFollowTarget followTarget, 
                MissileInfo info) : base(fireOriginLabel, fireFunction, followTarget)
            {
                Info = info;
            }
        }

        private Vector3 _hatch1To4Offset;
        private Vector3 _hatch5To8Offset;
        private readonly Queue<MissileInfo> _availableMissiles = new();
        private int _launchIndex;
        private Dictionary<GameObject, Quaternion> _initialRotations = new();

        protected override List<Vector3> CellTargetOffsets => new()
        {
            new Vector3(0, 7, 50),
            new Vector3(0, 7, 0),
            new Vector3(0, 7, -70)
        };
        
        private void Awake()
        {
            _hatch1To4Offset = missileHatches[0].transform.position - missileHatch1Hardpoint.transform.position;
            _hatch5To8Offset = missileHatches[7].transform.position - missileHatch8Hardpoint.transform.position;
            
            // Create missiles
            LoadMissiles();
        }

        protected override void Start()
        {
            base.Start();
            
            _initialRotations = RotationUtils.CaptureRotations(new List<List<GameObject>>
            {
                missileHatches
            });
        }

        private void LoadMissiles()
        {
            for (var i = 0; i < 8; i++)
                _availableMissiles.Enqueue(new MissileInfo(missileHatches[i], i));
        }
        
        private Vector3 GetHardpointPosition(int index) =>
            missileHatches[index].transform.position - (index < 4 ? _hatch1To4Offset : _hatch5To8Offset);
        
        protected override void Update()
        {
            // Update the common combat vessel, and skip the rest if we're destroyed
            base.Update();
            if (IsDestroyed()) return;
            
            foreach (var prop in props) prop.transform.Rotate(propRotation  * Time.deltaTime);
        }

        public override bool PrepareToFire(int initialLayer, out FireReservation reservation)
        {
            reservation = new FireReservation();
            
            if (IsDestroyed()) return false;

            Debug.Log("Destroyer preparing to fire");

            if (missilePool == null)
            {
                Debug.LogError("Missile pool is not initialized");
                return false;
            }
            
            // Get the next available missile
            if (!_availableMissiles.TryDequeue(out var missileInfo))
            {
                Debug.LogWarning("No available missiles to fire");
                return false;
            }

            var missile = missilePool.TakeFromPool();
            if (missile == null)
            {
                Debug.LogError("Failed to take missile from pool.");
                return false;
            }

            // Reattach the missile to the vessel
            missile.SetMissileParent(transform);
            
            // Reset the missile, and set it's initial visibility
            var missilePosition = GetHardpointPosition(missileInfo.HardpointIndex);
            missile.ResetMissile(missilePosition, Quaternion.identity, transform.localScale);
            missile.SetLayer(initialLayer);
            
            missileInfo.Missile = missile;
            reservation = new DestroyerFireReservation(GetIdent(), FireAtTarget, missileInfo.Missile, missileInfo);
            return true;
        }
        
        private void FireAtTarget(FireReservation reservation, Vector3 target, bool targetIsVessel, Action onImpactCallback)
        {
            var fireReservation = (DestroyerFireReservation) reservation;
            var missile = fireReservation.Info.Missile;
            if (!missile) return;
            
            var haveMadeVisible = false;
            
            // Animation functions
            Action<(float progress, bool complete)> openFn = f =>
            {
                RotationUtils.SetLocalRotationZ(fireReservation.Info.Hatch, f.progress * 90, _initialRotations);

                if (!f.complete) return;
                missile.LaunchMissile(0, target, 
                    _ => onImpactCallback?.Invoke(), 
                    () =>
                    {
                        if (missile != null)
                        {
                            Debug.Log($"destroyer returning missile to pool");
                            _availableMissiles.Enqueue(fireReservation.Info);
                            missilePool.ReturnToPool(missile);
                        }
                        else
                        {
                            Debug.Log($"destroyer skipping null missile");
                        }
                    },
                    (_, _, flightFraction) =>
                    {
                        // Once the missile is past it's halfway distance, make it visible to all players
                        if (haveMadeVisible || !(flightFraction > 0.5)) return;
                        haveMadeVisible = true;
                        missile.SetLayer(0);
                    },
                    // Missile has hit something, however it might not have hit
                    // what it was meant to hit - IE it may have been targeted at
                    // an occupied cell, but narrowly missed the vessel, or targeted
                    // to an empty cell that has part of a large vessel overhanging it.
                    (_, _) => targetIsVessel ? FuseResult.Detonate : FuseResult.Splash);
            };
        
            Action<(float progress, bool complete)> closeFn = f =>
            {
                RotationUtils.SetLocalRotationZ(fireReservation.Info.Hatch, (1 - f.progress) * 90, _initialRotations);
            };
        
            var closeCr = CoroutineUtils.TimedCoroutine(1, 5, null, closeFn, closeFn);
            var openCr = CoroutineUtils.TimedCoroutine(0.1f, 0, null, openFn, openFn, closeCr);
            
            StartCoroutine(openCr);
        }
        
        public override void Enable() {}
        
        public override void Terminate() {}
        
        protected override string VesselClassName => "Destroyer";
    }
}
