using System;
using System.Collections.Generic;
using UnityEngine;
using Debug = System.Diagnostics.Debug;

namespace Code.Scripts
{
    public class Lcs : CombatVesselBase
    {
        [Header("Components")]
        public List<GameObject> radar = new();
        public List<GameObject> props = new();
        public GunTurret deckGun;
        
        [Header("Rotation Speeds")]
        public Vector3 radarRotation = new(0, 0, -25f);
        public Vector3 propRotation = new(0, 45f, 0);
        
        [Header("Turret Behaviour")]
        public int burstCount = 3;
        public float burstDelay = 0.6f;
        
        public CannonShellPool cannonShellPool;
        
        /////////////

        private class LcsFireReservation : FireReservation
        {
            public readonly GunTurret.FirePackage FirePackage;
            
            public LcsFireReservation(
                string fireOriginLabel,
                FireFunction fireFunction, 
                SeaWar.IGameboardFollowTarget followTarget,
                GunTurret.FirePackage firePackage) : base(fireOriginLabel, fireFunction, followTarget)
            {
                FirePackage = firePackage;
            }
        }
        
        protected override List<Vector3> CellTargetOffsets => new()
        {
            new Vector3(0, 7, 0),
            new Vector3(0, 7, -20)
        };

        protected override void Start()
        {
            base.Start();
            if (deckGun != null) deckGun.cannonShellPool = cannonShellPool;
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
            UnityEngine.Debug.Log("LCS preparing to fire");
            var firePackage = deckGun.PrepareFirePackage(initialLayer, burstCount, out var followTarget);
            reservation = new LcsFireReservation(GetIdent(), FireAtTarget, followTarget, firePackage);
            return true;
        }
        
        private void FireAtTarget(FireReservation reservation, Vector3 target, bool targetIsVessel, Action onImpactCallback)
        {
            var lcsFireReservation = reservation as LcsFireReservation;
            Debug.Assert(lcsFireReservation != null, nameof(lcsFireReservation) + " != null");
            
            deckGun.SubmitFirePackage(lcsFireReservation.FirePackage, target, onImpactCallback, burstDelay, 
                (_, _) => targetIsVessel ? FuseResult.Detonate : FuseResult.Splash);
        }
        
        public override void Enable() {}
        
        public override void Terminate() {}

        protected override void ApplyCapsizeEffect(float progress, bool isCapsizing)
        {
            if (deckGun) deckGun.LockTurret(isCapsizing);
        }
        
        protected override string VesselClassName => "Littoral Combat Ship";
    }
}
