using System;
using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class CoveredButton : MonoBehaviour, IWandArcCollider
    {
        [Header("Components")]
        public GameObject cover;
        public GameObject pushButton;

        [Header("Sounds")]
        public AudioClip coverOpenSound;
        public AudioClip coverCloseSound;

        [Header("Behavior")]
        [Min(0.1f)]
        public float slewAcceleration = 90f;
        public float slewMaxSpeed = 180f;
        public float slewSnap = 5f;

        public float coverOpenRotation = -90f;   // In the localEuler X
        public float coverClosedRotation;        // In the localEuler X
    
        public float downZ = -0.1f;
        public AudioClip clickSound;
        public float clickDuration = 0.3f;

        public float autoCloseDelay = 5f;

        public bool testToggleCover;
        public bool testClick;
        
        ///// Public for use in code /////
        
        public event Action<CoveredButton> OnClicked;
        public readonly NotifyingVariable<bool> CoverOpen = new(false);

        /////
        
        private SnappingAngleSlewController _coverSlewController;

        private bool _coverWasOpen;
        private bool _toggleWasOn;
        private Coroutine _autoCloseCoroutine;
        
        private float _clickStartTime = float.MinValue;
        private Vector3 _animateStartPosition;
        private bool _setPostAnimationPosition;
        
        private bool AnimatingClick => Time.unscaledTime < _clickStartTime + clickDuration;
        
        public bool Click()
        {
            if (!CoverOpen.Value) return false;
            
            OnClicked?.Invoke(this);
            if (clickSound) SoundManager.Instance.PlaySound(clickSound, 1f);

            // Return if we're already animating
            if (AnimatingClick) return false;
            _clickStartTime = Time.unscaledTime;
            _animateStartPosition = (pushButton ? pushButton : gameObject).transform.localPosition;
            _setPostAnimationPosition = true;
            
            return true;
        }
        
        private void Start()
        {
            // Cover rotation
            var coverRotation = CoverOpen.Value ? coverOpenRotation : coverClosedRotation;
            
            _coverSlewController = new SnappingAngleSlewController(
                initialAngle: coverRotation, 
                snapAngle: slewSnap, 
                acceleration: slewAcceleration,
                maxSpeed: slewMaxSpeed
            );
            cover.transform.localEulerAngles = new Vector3(coverRotation, 0, 0);
            
            WandManager.Instance.RegisterArcCollider(this);
        }

        private void OnEnable()
        {
            // Reset the cover if the button is disabled
            CoverOpen.Value = false;
            var coverRotation = CoverOpen.Value ? coverOpenRotation : coverClosedRotation;
            _coverSlewController?.Reset(coverRotation);
            cover.transform.localEulerAngles = new Vector3(coverRotation, 0, 0);
        }

        private void OnDestroy()
        {
            WandManager.Instance.DeregisterArcCollider(this);
        }

        public void ToggleCover()
        {
            CoverOpen.Value = !CoverOpen.Value;
        }
        
        private void Update()
        {
            //TODO: REMOVE TEST
            if (testToggleCover) ToggleCover();
            if (testClick) Click();
            testClick = testToggleCover = false;
            
            if (AnimatingClick)
            {
                var elapsed = Time.unscaledTime - _clickStartTime;
                var progress = Mathf.Clamp01(elapsed / clickDuration);
                var upDownProgress = progress switch
                {
                    <= 0.5f => progress * 2,
                    > 0.5f => 1 - (progress - 0.5f) * 2,
                    _ => throw new ArgumentOutOfRangeException()
                };

                (pushButton ? pushButton : gameObject).transform.localPosition =
                    _animateStartPosition + new Vector3(0, 0, upDownProgress * downZ);
            }
            else if (_setPostAnimationPosition)
            {
                _setPostAnimationPosition = false;
                (pushButton ? pushButton : gameObject).transform.localPosition =_animateStartPosition;
            }
            
            MaybeToggleCover();
            
            _coverSlewController.Target = CoverOpen.Value ? coverOpenRotation : coverClosedRotation;
            
            if (_coverSlewController.MaybeUpdate(out var coverRotation, Time.unscaledDeltaTime)) 
                cover.transform.localEulerAngles = new Vector3(coverRotation, 0, 0);
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

        private void UpdateAutoClose()
        {
            // Cancel any existing coroutine
            if (_autoCloseCoroutine != null)
            {
                StopCoroutine(_autoCloseCoroutine);
                _autoCloseCoroutine = null;
            }
            
            if (autoCloseDelay == 0) return;    // Return if auto-close is disabled
            if (!CoverOpen.Value) return;       // Return if the cover is 'closed'
            
            // Wait for a period of time, then close the cover
            _autoCloseCoroutine = StartCoroutine(CoroutineUtils.DelayedCoroutine(autoCloseDelay, _ =>
            {
                CoverOpen.Value = false;
                _autoCloseCoroutine = null;
            }));
        }
        
        public void OnWandArcEnter(Wand wand) { }

        public void OnWandArcExit(Wand wand) { }

        public void OnTriggerPull(Wand wand)
        {
            if (!Click()) ToggleCover();
        }
    }
}
