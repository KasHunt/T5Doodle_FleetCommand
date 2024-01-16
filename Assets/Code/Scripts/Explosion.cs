using System;
using System.Collections.Generic;
using Code.Scripts.Utils;
using UnityEngine;
using UnityEngine.VFX;
using Random = UnityEngine.Random;

namespace Code.Scripts
{
    public class Explosion : MonoBehaviour
    {
        [Tooltip("A single sound will be selected at random and played on explosion")]
        public List<AudioClip> explosionSounds;
        public float explosionSoundVolume = 1;
        public VisualEffect explosionEffectTemplate;
        public Light explosionLightTemplate;
    
        public AnimationCurve lightIntensity = AnimationCurve.Linear(0, 1, 1, 0);
    
        [Tooltip("Upward movement per second (Scaled)")]
        public float updraftSpeed;
        
        [Tooltip("Blast duration in seconds")]
        public float duration;
        
        /////////////
        
        private Vector3 _updraft;
        private VisualEffect _explosionEffect;
        private Light _explosionLight;
        private float _explosionLightStartingIntensity;
        private float _startTime;
        private Action _onComplete;
        private bool _completed = true;

        private void Start()
        {
            if (explosionEffectTemplate == null)
            {
                Debug.LogWarning($"Template null for Explosion ({DebugUtils.PrintAncestors(transform)})");
                return;
            }
            
            // Instantiate the explosion and make sure it's not running
            _explosionEffect = Instantiate(explosionEffectTemplate);
            _explosionEffect.Stop();
            
            // Instantiate the light and make sure it's off
            if (!explosionLightTemplate) return;
            _explosionLight = Instantiate(explosionLightTemplate);
            _explosionLight.enabled = false;
            _explosionLightStartingIntensity = _explosionLight.intensity;
        }

        private void Update()
        {
            if (_completed) return;
            
            // Compute progress
            var now = Time.time;
            var elapsed = now - _startTime;
            var progress = Mathf.Clamp01(elapsed / duration);

            // Set the light position and intensity
            if (_explosionLight)
            {
                _explosionLight.intensity = _explosionLightStartingIntensity * lightIntensity.Evaluate(progress);
                _explosionLight.transform.position += Time.deltaTime * _updraft;   
            }
        
            // Check for completed explosions
            if (now >= _startTime + duration) FinishExplosion();
        }

        private void FinishExplosion()
        {
            // Stop the visual effect (if running)
            _explosionEffect.Stop();
            
            // Turn the light off
            if (_explosionLight) _explosionLight.enabled = false;
            
            // Notify the invoker
            _onComplete?.Invoke();
            _completed = true;
        }
        
        private void PlayExplosionSound()
        {
            var count = explosionSounds.Count;
            if (count == 0) return;
            
            var index = Random.Range(0, count);
            var sound = explosionSounds[index];
            SoundManager.Instance.PlaySound(sound, explosionSoundVolume);
        }

        private void SetInitialTransforms(Vector3 location, Quaternion rotation, Vector3 scale)
        {
            var explosionTransform = _explosionEffect.transform;
            
            scale.Scale(explosionTransform.localScale);
            
            explosionTransform.rotation = rotation;
            explosionTransform.localScale = scale;
            explosionTransform.position = location;

            if (!_explosionLight) return;
            var lightTransform = _explosionLight.transform;
            lightTransform.localScale = scale;
            lightTransform.position = location;
        }
        
        public void Explode(Vector3 location, Vector3 scale, int? layer = null, Quaternion? rotation = null, Action onComplete = null)
        {
            rotation ??= Quaternion.identity;
            
            // Store parameters
            _completed = false;
            _onComplete = onComplete;
            _updraft = new Vector3(0, updraftSpeed * scale.y, 0);
            _startTime = Time.time;

            SetInitialTransforms(location, rotation.Value, scale);
            
            // Turn the light on
            if (_explosionLight)
            {
                _explosionLight.renderingLayerMask = layer ?? 0;
                _explosionLight.enabled = true;
            }
            
            // Play the visual effect
            _explosionEffect.gameObject.layer = layer ?? 0;
            _explosionEffect.Play();
            
            // Play the explosion sound
            PlayExplosionSound();
        }
    }
}
