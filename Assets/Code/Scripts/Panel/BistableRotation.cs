using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class BistableRotation : MonoBehaviour
    {
        [Header("Sounds")]
        public AudioClip openSound;
        public AudioClip closeSound;

        [Header("Behavior")]
        [Min(0.1f)]
        public float slewAcceleration = 90f;
        public float slewMaxSpeed = 180f;
        public float slewSnap = 5f;

        public float openRotation = -90f;   // In the localEuler X
        public float closedRotation;        // In the localEuler X

        public bool initialState;
        
        ///// Public for use in code /////
        
        public readonly NotifyingVariable<bool> Open = new(false);

        /////
        
        private SnappingAngleSlewController _slewController;
        
        private bool _wasOpen;
        private Coroutine _autoCloseCoroutine;

        private void ToggleOpen()
        {
            Open.Value = !Open.Value;
        }
        
        private void Start()
        {
            Open.Value = initialState;
            
            var rotation = Open.Value ? openRotation : closedRotation;
            
            _slewController = new SnappingAngleSlewController(
                initialAngle: rotation, 
                snapAngle: slewSnap, 
                acceleration: slewAcceleration,
                maxSpeed: slewMaxSpeed
            );
            transform.localEulerAngles = new Vector3(rotation, 0, 0);
        }
        
        private void OnEnable()
        {
            // Reset the rotation if the toggle is disabled
            var rotation = Open.Value ? openRotation : closedRotation;
            _slewController?.Reset(rotation);
            transform.localEulerAngles = new Vector3(rotation, 0, 0);
        }
        
        private void Update()
        {
            MaybePlayToggleSound();
            
            _slewController.Target = Open.Value ? openRotation : closedRotation;
            
            if (_slewController.MaybeUpdate(out var coverRotation, Time.unscaledDeltaTime)) 
                transform.localEulerAngles = new Vector3(coverRotation, 0, 0);
        }
        
        private void MaybePlayToggleSound()
        {
            if (_wasOpen == Open.Value) return;
            _wasOpen = Open.Value;
            
            switch (Open.Value)
            {
                case true when openSound:
                    SoundManager.Instance.PlaySound(openSound, 1);
                    break;
                case false when closeSound:
                    SoundManager.Instance.PlaySound(closeSound, 1);
                    break;
            }
        }
    }
}
