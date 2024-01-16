using System;
using Code.Scripts;
using JetBrains.Annotations;
using UnityEngine;

namespace Missiles.Implementations
{
    public abstract class MissileBase : MonoBehaviour, SeaWar.IGameboardFollowTarget
    {
        public delegate void InFlightUpdate(MissileBase missile, float timeOfFlight, float flightFraction);
        
        public abstract void LaunchMissile(float initialSpeed, Vector3 target, Action<FuseResult> onImpact = null, Action onExplosionComplete = null, InFlightUpdate inFlightUpdate = null, MissileFuse fuseEvaluator = null);

        public abstract void ResetMissile(Vector3 position, Quaternion rotation, Vector3 scale);

        public abstract void SetMissileParent([CanBeNull] Transform parent);
        public abstract void SetLayer(int layer);
        public abstract void DestroyMissile();

        public abstract float GetFollowZoom();

        public abstract Vector3 GetPosition();
        public abstract int GetDistanceToTarget();
        public abstract float GetFollowFinishTime();
        public abstract float GetFlightFraction();
    }
}