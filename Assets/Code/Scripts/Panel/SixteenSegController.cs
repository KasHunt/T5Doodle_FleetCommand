using UnityEngine;

namespace Code.Scripts.Panel
{
    public class SixteenSegController : MonoBehaviour
    {
        public char asciiChar = 'A';

        private char _lastAsciiChar;
        private Material _material;
        private readonly Vector2 _scale = new(1.0f / 16.0f, 1.0f / 16.0f);
        private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");
        private static readonly int EmissiveColorMap = Shader.PropertyToID("_EmissiveColorMap");

        private void Start()
        {
            _material = Application.isPlaying ? GetComponent<Renderer>().material : new Material(GetComponent<Renderer>().sharedMaterial);
            _material.SetTextureScale(BaseColorMap, _scale);
            _material.SetTextureScale(EmissiveColorMap, _scale);
        }

        private void Update()
        {
            // Check if an update is necessary
            if (asciiChar == _lastAsciiChar) return;
            
            UpdateMaterial();
            _lastAsciiChar = asciiChar;
        }

        private void UpdateMaterial()
        {
            // Compute ASCII character offset 
            var computedOffset = ComputeOffsetFromAscii(asciiChar);

            // Adjust texture offsets
            _material.SetTextureOffset(BaseColorMap, computedOffset);
            _material.SetTextureOffset(EmissiveColorMap, computedOffset);
        }
        
        private static Vector2 ComputeOffsetFromAscii(char asciiIndex)
        {
            var xIndex = asciiIndex % 16;
            var yIndex = asciiIndex / 16;

            // Unity textures start from bottom-left - flip yIndex to match sprite sheet
            yIndex = 15 - yIndex;

            return new Vector2(xIndex / 16.0f, yIndex / 16.0f);
        }
    }
}
