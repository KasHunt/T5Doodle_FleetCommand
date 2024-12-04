using System;
using System.Collections.Generic;
using UnityEngine;

namespace Code.Scripts
{
    public class Battleship : CombatVesselBase
    {
        [Header("Components")]
        public List<GameObject> radar = new();
        public List<GameObject> props = new();
        public List<GunTurret> gunTurrets = new();

        [Header("Rotation Speeds")]
        public Vector3 radarRotation = new(0, 0, -25f);
        public Vector3 propRotation = new(0, 45f, 0);
        
        public CannonShellPool cannonShellPool;
        
        /////////////

        private class BattleshipFireReservation : FireReservation
        {
            public readonly List<GunTurret.FirePackage> FirePackages;
            
            public BattleshipFireReservation(
                string fireOriginLabel,
                FireFunction fireFunction, 
                SeaWar.IGameboardFollowTarget followTarget,
                List<GunTurret.FirePackage> firePackages) : base(fireOriginLabel, fireFunction, followTarget)
            {
                FirePackages = firePackages;
            }
        }
        
        protected override List<Vector3> CellTargetOffsets => new()
        {
            new Vector3(0, 8, 75),
            new Vector3(0, 12, 0),
            new Vector3(0, 12, -50),
            new Vector3(0, 8, -140)
        };

        protected override void Start()
        {
            base.Start();
            foreach (var gunTurret in gunTurrets) 
                gunTurret.cannonShellPool = cannonShellPool;
        }

        protected override void Update()
        {
            // Update the common combat vessel, and skip the rest if we're destroyed
            base.Update();
            if (IsDestroyed()) return;
            
            RotatePropsAndRadar();
        }

        private void RotatePropsAndRadar()
        {
            foreach (var prop in props) prop.transform.Rotate(propRotation  * Time.deltaTime);
            foreach (var obj in radar) obj.transform.Rotate(radarRotation  * Time.deltaTime);
        }

        public override bool PrepareToFire(int initialLayer, out FireReservation reservation)
        {
            Debug.Log("Battleship preparing to fire");
            SeaWar.IGameboardFollowTarget followTarget = null;
            var firePackages = new List<GunTurret.FirePackage>();
            foreach (var gunTurret in gunTurrets)
            {
                firePackages.Add(gunTurret.PrepareFirePackage(initialLayer,1, out var leadShell));
                followTarget ??= leadShell;
            }
            
            reservation = new BattleshipFireReservation(GetIdent(), FireAtTarget, followTarget, firePackages);
            return true;
        }
        
        private void FireAtTarget(FireReservation reservation, Vector3 target, bool targetIsVessel, Action onImpactCallback)
        {
            var battleshipFireReservation = reservation as BattleshipFireReservation;
            Debug.Assert(battleshipFireReservation != null, nameof(battleshipFireReservation) + " != null");
            
            var impactResult = targetIsVessel ? FuseResult.Detonate : FuseResult.Splash;
            for (var i = 0; i < gunTurrets.Count; i++)
            {
                gunTurrets[i].SubmitFirePackage(battleshipFireReservation.FirePackages[i], target, onImpactCallback,0, 
                    (_, _) => impactResult);
            }
        }

        public override void Enable() {}
        public override void Terminate() {}
        
        protected override void ApplyCapsizeEffect(float progress, bool isCapsizing)
        {
            foreach (var gunTurret in gunTurrets) gunTurret.LockTurret(isCapsizing);
        }
        
        protected override string VesselClassName => "Battleship";
    }
}
