using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Code.Scripts
{
    public class ExplosionManager : MonoBehaviour
    {
        [Tooltip("A single sound will be selected at random and played on explosion")]
        public List<AudioClip> explosionSounds;
        public float explosionSoundVolume = 1;
        public VisualEffect explosionEffect;
        public Light explosionLight;
    
        public AnimationCurve lightIntensity = AnimationCurve.Linear(0, 1, 1, 0);
    
        [Tooltip("Blast duration in seconds")]
        public float blastDuration = 5;
    
        [Min(1)]
        [Tooltip("Maximum number of simultaneous explosions")]
        public int poolSize = 6;
    
        [Tooltip("Upward movement per second (Scaled)")]
        public float updraftSpeed;
        
        private readonly Queue<Explosion> _effectsPool = new();

        private Explosion MakeExplosion()
        {
            var explosionGameObject = new GameObject($"Explosion (Pool:{name})", typeof(Explosion));
            var explosion = explosionGameObject.GetComponent<Explosion>();
            explosion.updraftSpeed = updraftSpeed;
            explosion.duration = blastDuration;
            explosion.lightIntensity = lightIntensity;
            explosion.explosionSounds = explosionSounds;
            explosion.explosionSoundVolume = explosionSoundVolume;
            explosion.explosionEffectTemplate = explosionEffect;
            explosion.explosionLightTemplate = explosionLight;

            return explosion;
        }

        private void Start()
        {
            for (var i = 0; i < poolSize; i++) _effectsPool.Enqueue(MakeExplosion());
        }

        public bool Explode(Vector3 location, Quaternion rotation, Vector3 scale, int displayLayer)
        {
            // Acquire an explosion from the pool (or return failure)
            if (!_effectsPool.TryDequeue(out var explosion)) return false;
            
            explosion.Explode(location, scale, displayLayer, rotation, () => _effectsPool.Enqueue(explosion));
            return true;
        }
    }
}
