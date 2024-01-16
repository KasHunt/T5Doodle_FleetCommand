using System.Collections.Generic;
using JetBrains.Annotations;
using UnityEngine;

namespace Code.Scripts
{
    public interface IRunwayProvider
    {
        public enum LiftState
        {
            Raised,
            Lowered
        }
        
        public enum RunwayWaypoints
        {
            UpLift,
            DownLift,
            CatapultStart,
            CatapultEnd,
            PatrolRoute,
            HoldingPattern,
            Arrestors,
            ParkingStand,
            Approach
        }

        public IEnumerable<Vector3> GetRunwayWaypoints(RunwayWaypoints waypoint);
        public void OperateLift(Vector3 liftPosition, LiftState targetState, [CanBeNull] Transform moveTarget = null);
        public LiftState? GetLiftState(Vector3 liftPosition);
        public bool RequestLandingClearance();
        public void ReleaseLandingClearance();
        public bool RequestTakeoffClearance();
        public void ReleaseTakeoffClearance();
        public bool IsDestroyed();
    }
}
