using System;
using UnityEngine;

namespace Code.Scripts.Utils
{
    public class SlewController {
        private readonly float _acceleration;
        private readonly float _maxSpeed;
        protected float CurrentSpeed;
        protected float CurrentRotation;

        protected SlewController(float acceleration, float maxSpeed)
        {
            _acceleration = acceleration;
            _maxSpeed = maxSpeed;
        }

        protected void Halt()
        {
            CurrentSpeed = 0;
        }
        
        protected float UpdateSpeed(float distanceToTarget, float deltaTime) {
            // Calculate the time needed to come to a stop
            var timeToStop = Mathf.Abs(CurrentSpeed) / _acceleration;
            var angleToStop = Mathf.Abs(CurrentSpeed) * timeToStop + 0.5f * -_acceleration * Mathf.Pow(timeToStop, 2);

            // If we're on target, and stationary, don't change speed
            if (distanceToTarget == 0 && CurrentSpeed == 0) return 0;
            
            float deltaSpeed;
            if ((int)Mathf.Sign(distanceToTarget) != (int)Mathf.Sign(CurrentSpeed) && CurrentSpeed != 0)
            {
                // Moving in the wrong direction; prioritize reversing
                deltaSpeed = _acceleration * -Mathf.Sign(CurrentSpeed);
            }
            else
            {
                if (CurrentSpeed != 0)
                {
                    // Calculate time it would take to reach the target at current speed
                    var timeToTarget = distanceToTarget / CurrentSpeed;
                
                    // If we're going to overshoot the target in the next frame, set speed to hit target exactly
                    if (timeToTarget <= deltaTime)
                    {
                        Halt();
                        CurrentSpeed = distanceToTarget / deltaTime;
                        return CurrentSpeed;
                    }                    
                }
                
                // Accelerate or decelerate depending on the position relative to the angle needed to stop at the target
                deltaSpeed = (Mathf.Abs(distanceToTarget) > Mathf.Abs(angleToStop) ? _acceleration : -_acceleration) * Mathf.Sign(distanceToTarget);
            }

            // Apply change and clamp
            CurrentSpeed = Mathf.Clamp(CurrentSpeed + deltaSpeed * deltaTime, -_maxSpeed, _maxSpeed);
            return CurrentSpeed;
        }
    }
    
    public class SnappingAngleSlewController : SlewController
    {
        private float _lastAngle;
        
        public float Target;
        
        private readonly float _snapAngle;
        private const float CHANGED_THRESHOLD = 0.01f;

        public SnappingAngleSlewController(float initialAngle, float snapAngle, float acceleration, float maxSpeed) : base(acceleration, maxSpeed)
        {
            Target = initialAngle;
            CurrentRotation = initialAngle;
            _snapAngle = snapAngle;
        }

        public float CurrentDelta => Mathf.DeltaAngle(CurrentRotation, Target); 
        public bool IsOnTarget => Mathf.Abs(CurrentDelta) < _snapAngle;

        public void Reset(float angle)
        {
            Halt();
            Target = angle;
            CurrentRotation = angle;
        }
        
        public float Update(float deltaTime)
        {
            var rotationAngleDifference = CurrentDelta;
            UpdateSpeed(rotationAngleDifference, deltaTime);
            CurrentRotation = CurrentRotation.AddAndClamp360(CurrentSpeed * deltaTime);
            
            return IsOnTarget ? Target : CurrentRotation;
        }
        
        public bool MaybeUpdate(out float value, float deltaTime, float updateThreshold)
        {
            value = Update(deltaTime);
            var changed = Math.Abs(value - _lastAngle) > updateThreshold;
            if (changed) _lastAngle = value;
            
            return changed;
        }
        
        public bool MaybeUpdate(out float value, float deltaTime) =>
            MaybeUpdate(out value, deltaTime, CHANGED_THRESHOLD);
        
        public bool MaybeUpdate(out float value) =>
            MaybeUpdate(out value, Time.deltaTime, CHANGED_THRESHOLD);
    }
}