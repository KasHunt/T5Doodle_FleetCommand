using Code.Scripts.Utils;
using UnityEngine;

namespace Code.Scripts.Panel
{
    public class StatusLight : MonoBehaviour
    {
        public bool lightOn;
        public Color onColor = Color.white;
        public Color offColor = Color.white;
        public float offIntensity;
        public float onIntensity = 200000f;

        private bool _lightWasOn;
        private Material _material;
        private static readonly int EmissiveIntensity = Shader.PropertyToID("_EmissiveIntensity");
        private static readonly int EmissiveColorLDR = Shader.PropertyToID("_EmissiveColorLDR");
        private static readonly int BaseColor = Shader.PropertyToID("_BaseColor");

        private void Start()
        {
            _material = GetComponent<Renderer>().material;

            // Ensure the first call to update() sets the properties
            _lightWasOn = !lightOn;
        }

        private void Update()
        {
            if (lightOn == _lightWasOn) return;
            _lightWasOn = lightOn;

            var color = lightOn ? onColor : offColor;
            _material.SetFloat(EmissiveIntensity, lightOn ? onIntensity : offIntensity);
            _material.SetColor(BaseColor, color);
            _material.SetColor(EmissiveColorLDR, color);
            
            ColorUtils.UpdateEmissiveColor(_material);
        }
    }
}
