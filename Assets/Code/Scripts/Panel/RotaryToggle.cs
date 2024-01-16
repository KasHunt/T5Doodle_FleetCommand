using System;
using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class RotaryToggle : MonoBehaviour, IWandArcCollider
    {
        [Header("Sounds")]
        public AudioClip toggleSound;
        
        [Header("Behavior")]
        [Min(0.1f)]
        public float slewAcceleration = 90f;
        public float slewMaxSpeed = 180f;
        public float slewSnap = 5f;
        public float switchOnRotation = -25f;   // In the localEuler Z
        public float switchOffRotation = 25f;   // In the localEuler Z
        public GeometryUtils.Axis rotationAxis = GeometryUtils.Axis.X;

        public bool initialState;
        public bool testToggleSwitch;

        public bool locked;
        
        ///// Public for use in code /////
        
        public readonly NotifyingVariable<bool> SwitchOn = new(false); 
        
        /////
        
        private SnappingAngleSlewController _switchSlewController;
        private bool _toggleWasOn;
        
        public void ToggleSwitch()
        {
            if (locked) return;
            
            SwitchOn.Value = !SwitchOn.Value;            
        }

        private void Start()
        {
            SwitchOn.Value = initialState;
            
            var switchRotation = SwitchOn.Value ? switchOnRotation : switchOffRotation;
            _switchSlewController = new SnappingAngleSlewController(
                initialAngle: switchRotation, 
                snapAngle: slewSnap, 
                acceleration: slewAcceleration,
                maxSpeed: slewMaxSpeed
            );
            transform.localEulerAngles = new Vector3(0, 0, switchRotation);
            
            WandManager.Instance.RegisterArcCollider(this);
            if (!GetComponent<Collider>())
            {
                Debug.LogWarning($"'{name}' has no collider - wand arc interaction disabled");                
            }
        }
        
        private void OnEnable()
        {
            // Reset the rotation if the toggle is disabled
            SwitchOn.Value = initialState;
            
            var switchRotation = SwitchOn.Value ? switchOnRotation : switchOffRotation;
            _switchSlewController?.Reset(switchRotation);
            transform.localEulerAngles = ComputeRotation(switchRotation);
        }
        
        private void OnDestroy()
        {
            WandManager.Instance.DeregisterArcCollider(this);
        }
        
        private Vector3 ComputeRotation(float angle)
        {
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
            if (testToggleSwitch) ToggleSwitch();
            testToggleSwitch = false;
            
            MaybeToggleSwitch();
            
            _switchSlewController.Target = SwitchOn.Value ? switchOnRotation : switchOffRotation;
            
            if (_switchSlewController.MaybeUpdate(out var switchRotation, Time.unscaledDeltaTime))
                transform.localEulerAngles = ComputeRotation(switchRotation);
        }
        
        private void MaybeToggleSwitch()
        {
            if (_toggleWasOn == SwitchOn.Value) return;
            _toggleWasOn = SwitchOn.Value;
            
            if (toggleSound) SoundManager.Instance.PlaySound(toggleSound, 1);
        }
        
        public void OnWandArcEnter(Wand wand) { }

        public void OnWandArcExit(Wand wand) { }

        public void OnTriggerPull(Wand wand) => ToggleSwitch();
    }
}
