using System;
using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class LitButton : MonoBehaviour, IWandArcCollider
    {
        public GameObject litObjectOverride;

        public float downDistance = -0.1f;
        public GeometryUtils.Axis pushAxis = GeometryUtils.Axis.X;
        
        public bool lightOn;
        public Color onColor = Color.white;
        public Color offColor = Color.white;
        public float offIntensity;
        public float onIntensity = 200000f;
        public float clickDuration = 0.3f;
        public AudioClip clickSound;

        public bool testClick;
        
        ///// PUBLIC FOR CODE
        
        public event Action<LitButton> OnClicked;
        
        //////
        
        private bool _lightWasOn;
        private Material _material;
        private static readonly int EmissiveIntensity = Shader.PropertyToID("_EmissiveIntensity");
        private static readonly int EmissiveColorLDR = Shader.PropertyToID("_EmissiveColorLDR");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private float _clickStartTime = float.MinValue;
        private Vector3 _animateStartPosition;
        private bool _setPostAnimationPosition;

        private bool AnimatingClick => Time.unscaledTime < _clickStartTime + clickDuration;
        
        public void Click()
        {
            OnClicked?.Invoke(this);
            if (clickSound) SoundManager.Instance.PlaySound(clickSound, 1f);

            // Return if we're already animating
            if (AnimatingClick) return;
            _clickStartTime = Time.unscaledTime;
            _animateStartPosition = transform.localPosition;
            _setPostAnimationPosition = true;
        }
        
        private void Start()
        {
            _material = (litObjectOverride ? litObjectOverride : gameObject).GetComponent<Renderer>().material;
            
            WandManager.Instance.RegisterArcCollider(this);
        }

        private void OnDestroy()
        {
            WandManager.Instance.DeregisterArcCollider(this);
        }

        private Vector3 ToPushVector(float distance)
        {
            return pushAxis switch
            {
                GeometryUtils.Axis.X => new Vector3(distance,0, 0),
                GeometryUtils.Axis.Y => new Vector3(0,distance, 0),
                GeometryUtils.Axis.Z => new Vector3(0,0, distance),
                _ => throw new ArgumentOutOfRangeException()
            };
        } 
        
        private void Update()
        {
            if (testClick) Click();
            testClick = false;
            
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

                transform.localPosition = _animateStartPosition + ToPushVector(upDownProgress * downDistance);
            }
            else if (_setPostAnimationPosition)
            {
                _setPostAnimationPosition = false;
                transform.localPosition = _animateStartPosition;
            }
            
            if (lightOn == _lightWasOn) return;
            _lightWasOn = lightOn;

            var color = lightOn ? onColor : offColor;
            _material.SetFloat(EmissiveIntensity, lightOn ? onIntensity : offIntensity);
            _material.SetColor(BaseColor, color);
            _material.SetColor(EmissiveColorLDR, color);
            
            ColorUtils.UpdateEmissiveColor(_material);
        }

        public void OnWandArcEnter(Wand wand) { }

        public void OnWandArcExit(Wand wand) { }

        public void OnTriggerPull(Wand wand) => Click();
    }
}