using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class CoveredToggle : MonoBehaviour, IWandArcCollider
    {
        [Header("Components")]
        public GameObject cover;
        public GameObject toggleSwitch;

        [Header("Sounds")]
        public AudioClip coverOpenSound;
        public AudioClip coverCloseSound;
        public AudioClip toggleSound;

        [Header("Behavior")]
        [Min(0.1f)]
        public float slewAcceleration = 90f;
        public float slewMaxSpeed = 180f;
        public float slewSnap = 5f;

        public float coverOpenRotation = -90f;   // In the localEuler X
        public float coverClosedRotation;        // In the localEuler X
    
        public float switchOnRotation = -25f;   // In the localEuler X
        public float switchOffRotation = 25f;   // In the localEuler X

        public float autoCloseDelay = 5f;

        public bool testToggleCover;
        public bool testToggleSwitch;
        
        ///// Public for use in code /////
        
        public readonly NotifyingVariable<bool> CoverOpen = new(false);
        public readonly NotifyingVariable<bool> ToggleOn = new(false);

        /////
        
        private SnappingAngleSlewController _coverSlewController;
        private SnappingAngleSlewController _switchSlewController;

        private bool _coverWasOpen;
        private bool _toggleWasOn;
        private Coroutine _autoCloseCoroutine;

        private void Start()
        {
            var coverRotation = CoverOpen.Value ? coverOpenRotation : coverClosedRotation;
            
            _coverSlewController = new SnappingAngleSlewController(
                initialAngle: coverRotation, 
                snapAngle: slewSnap, 
                acceleration: slewAcceleration,
                maxSpeed: slewMaxSpeed
            );
            cover.transform.localEulerAngles = new Vector3(coverRotation, 0, 0);
            
            var switchRotation = ToggleOn.Value ? switchOnRotation : switchOffRotation;
            _switchSlewController = new SnappingAngleSlewController(
                initialAngle: switchRotation, 
                snapAngle: slewSnap, 
                acceleration: slewAcceleration,
                maxSpeed: slewMaxSpeed
            );
            toggleSwitch.transform.localEulerAngles = new Vector3(switchRotation, 0, 0);
            
            WandManager.Instance.RegisterArcCollider(this);
        }

        private void OnDestroy()
        {
            WandManager.Instance.DeregisterArcCollider(this);
        }

        public void ToggleCover()
        {
            CoverOpen.Value = !CoverOpen.Value;
        }
        
        public bool ToggleSwitch()
        {
            if (!CoverOpen.Value) return false;
            ToggleOn.Value = !ToggleOn.Value;
            return true;
        }
        
        private void Update()
        {
            //TODO: REMOVE TEST
            if (testToggleCover) ToggleCover();
            if (testToggleSwitch) ToggleSwitch();
            testToggleCover = testToggleSwitch = false;
            
            if (!CoverOpen.Value) ToggleOn.Value = false;
            
            MaybeToggleCover();
            MaybeToggleSwitch();
            
            _coverSlewController.Target = CoverOpen.Value ? coverOpenRotation : coverClosedRotation;
            _switchSlewController.Target = ToggleOn.Value ? switchOnRotation : switchOffRotation;
            
            if (_coverSlewController.MaybeUpdate(out var coverRotation, Time.unscaledDeltaTime)) 
                cover.transform.localEulerAngles = new Vector3(coverRotation, 0, 0);
            
            if (_switchSlewController.MaybeUpdate(out var switchRotation, Time.unscaledDeltaTime))
                toggleSwitch.transform.localEulerAngles = new Vector3(switchRotation, 0, 0);
        }
        
        private void MaybeToggleCover()
        {
            if (_coverWasOpen == CoverOpen.Value) return;
            _coverWasOpen = CoverOpen.Value;
            
            switch (CoverOpen.Value)
            {
                case true when coverOpenSound:
                    SoundManager.Instance.PlaySound(coverOpenSound, 1);
                    break;
                case false when coverCloseSound:
                    SoundManager.Instance.PlaySound(coverCloseSound, 1);
                    break;
            }

            UpdateAutoClose();
        }
        
                
        private void MaybeToggleSwitch()
        {
            if (_toggleWasOn == ToggleOn.Value) return;
            _toggleWasOn = ToggleOn.Value;
            
            if (toggleSound) SoundManager.Instance.PlaySound(toggleSound, 1);
            
            UpdateAutoClose();
        }

        private void UpdateAutoClose()
        {
            // Cancel any existing coroutine
            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }
            
            if (autoCloseDelay == 0) return;    // Return if auto-close is disabled
            if (ToggleOn.Value) return;         // Return if the switch is 'on'
            if (!CoverOpen.Value) return;       // Return if the cover is 'closed'
            
            // Wait for a period of time, then close the cover if we're not toggled on at that point
            _autoCloseCoroutine = StartCoroutine(CoroutineUtils.DelayedCoroutine(autoCloseDelay, _ =>
            {
                if (!ToggleOn.Value) CoverOpen.Value = false;
                _autoCloseCoroutine = null;
            }));
        }

        public void OnWandArcEnter(Wand wand) { }

        public void OnWandArcExit(Wand wand) { }

        public void OnTriggerPull(Wand wand)
        {
            if (!ToggleSwitch()) ToggleCover();
        }
    }
}
