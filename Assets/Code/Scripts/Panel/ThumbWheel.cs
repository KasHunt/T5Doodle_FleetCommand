using System;
using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class ThumbWheel : MonoBehaviour
    {
        [Header("Components")]
        public GameObject wheel;
        public GameObject decrementObject;
        public GameObject incrementObject;

        [Header("Position/Range")]
        [Vector2IntRange(0, 9)]
        public Vector2Int range;
        public NotifyingVariable<int> Position = new(0);
        public bool continuous;
        public GeometryUtils.Axis rotationAxis = GeometryUtils.Axis.X;
        public bool invertRotation;
        
        [Header("Slew Control")]
        [Min(1)]
        public float slewAcceleration = 180f;
        [Min(1)]
        public float slewMaxSpeed = 360f;
        public float slewSnap = 5f;

        public AudioClip stopClick;
        
        /////

        private ThumbWheelWandArcCollider _decrementArcCollider;
        private ThumbWheelWandArcCollider _incrementArcCollider;
        
        private int _position;
        private float _lastClickAngle;
        private SnappingAngleSlewController _slewController;

        private float AngleForPosition => Position.Value % 10 * 36f;

        public bool Increment()
        {
            if (Position.Value >= range.y && !continuous) return false;
            Position.Value = Position.Value == range.y ? range.x : (Position.Value + 1) % 10;
            return true;
        }
        
        public bool Decrement()
        {
            if (Position.Value <= range.x && !continuous) return false;
            Position.Value = Position.Value == range.x ? range.y : (Position.Value - 1) % 10;
            return true;
        }
        
        private void Start()
        {
            var angle = AngleForPosition;
            _slewController = new SnappingAngleSlewController(
                initialAngle: angle,
                snapAngle: slewSnap,
                acceleration: slewAcceleration,
                maxSpeed: slewMaxSpeed
            );
            _lastClickAngle = angle;
            wheel.transform.localEulerAngles = WheelRotation(angle);

            _incrementArcCollider = new ThumbWheelWandArcCollider(() => Increment());
            _decrementArcCollider = new ThumbWheelWandArcCollider(() => Decrement());
            
            WandManager.Instance.RegisterArcCollider(decrementObject.transform, _decrementArcCollider);
            WandManager.Instance.RegisterArcCollider(incrementObject.transform, _incrementArcCollider);
        }

        private void OnDestroy()
        {
            WandManager.Instance.DeregisterArcCollider(decrementObject.transform);
            WandManager.Instance.DeregisterArcCollider(incrementObject.transform);
        }

        private Vector3 WheelRotation(float angle)
        {
            if (invertRotation) angle *= -1;
            return rotationAxis switch
            {
                GeometryUtils.Axis.X => new Vector3(angle,0, 0),
                GeometryUtils.Axis.Y => new Vector3(0,angle, 0),
                GeometryUtils.Axis.Z => new Vector3(0,0, angle),
                _ => throw new ArgumentOutOfRangeException()
            };
        } 

        private void Update()
        {
            _slewController.Target = AngleForPosition;
            
            if (!_slewController.MaybeUpdate(out var angle, Time.unscaledDeltaTime)) return;
            wheel.transform.localEulerAngles = WheelRotation(angle);
                
            // Check for crossing a 36-degree 'stop' (offset to the middle of a thumb wheel face) and play sound
            var clickAngle = angle + 18f;
            if (Mathf.FloorToInt(clickAngle / 36) != Mathf.FloorToInt(_lastClickAngle / 36))
            {
                if (stopClick) SoundManager.Instance.PlaySound(stopClick, 1f);
            }
            _lastClickAngle = clickAngle;
        }
    }

    internal class ThumbWheelWandArcCollider : IWandArcCollider
    {
        private readonly Action _onClick;

        public ThumbWheelWandArcCollider(Action onClick)
        {
            _onClick = onClick;
        }

        public void OnWandArcEnter(Wand wand) { }

        public void OnWandArcExit(Wand wand) { }

        public void OnTriggerPull(Wand wand) => _onClick?.Invoke();
    }
}
