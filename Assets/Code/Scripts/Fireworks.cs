using System.Collections.Generic;
using System.Linq;
using Code.Scripts.Utils;
using UnityEngine;
using UnityEngine.VFX;

namespace Code.Scripts
{
    public class Fireworks : MonoBehaviour
    {
        public VisualEffect fireworkTemplate;
        public float fireworkDuration = 3f;
    
        private readonly List<VisualEffect> _fireworks = new();
    
        public void Clear()
        {
            foreach (var visualEffect in _fireworks)
            {
                visualEffect.Stop();
                Destroy(visualEffect, fireworkDuration);
            }
            _fireworks.Clear();
        }

        public void Spawn(Color color, IEnumerable<Vector3> launcherPositions) => Spawn(color, launcherPositions, Vector3.one);
        
        public void Spawn(Color color, IEnumerable<Vector3> launcherPositions, Vector3 scale)
        {
            Color.RGBToHSV(color, out var hue, out _, out _);
            
            fireworkTemplate.SetFloat("Base Hue", hue);
            fireworkTemplate.transform.localScale.Scale(scale);
            
            foreach (var newFirework in launcherPositions.Select(position => Instantiate(fireworkTemplate, position, Quaternion.identity)))
            {
                newFirework.name = $"Fireworks (Color {color}/{hue}) [Parent:{gameObject}]";
                
                // Set the scale
                var fireworksTransform = newFirework.transform;
                fireworksTransform.localScale = fireworksTransform.localScale.Multiply(scale);
                
                // Store the fireworks
                _fireworks.Add(newFirework);
            }
        }
    }
}
