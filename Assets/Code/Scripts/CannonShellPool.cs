using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

namespace Code.Scripts
{
   
    public class CannonShellPool : MonoBehaviour
    {
        [Header("Pool Properties")]
        public int ammoPoolSize = 16;
     
        [Header("Shell Properties")]
        public GameObject shellPrefab;
        public Vector3 gravity = new(0, -9.81f, 0);
        
        [Header("Impact Explosion")]
        public List<AudioClip> explosionSounds;
        public float explosionSoundVolume = 1;
        public VisualEffect explosionEffectTemplate;
        public Light explosionLightTemplate;
        public AnimationCurve explosionLightIntensity = AnimationCurve.Linear(0, 1, 1, 0);
        public float explosionDuration = 3f;

        [Header("Impact Splash")]
        public float splashDuration = 3f;
        public List<AudioClip> splashSounds;
        public float splashSoundVolume = 1f;
        public VisualEffect splashEffectTemplate;
        
        private readonly Queue<CannonShell> _available = new();

        private CannonShell MakeShell()
        {
            var shellGameObject = Instantiate(shellPrefab, Vector3.zero, Quaternion.identity);
            shellGameObject.transform.SetParent(null);
            
            var shell = shellGameObject.AddComponent<CannonShell>();
            shell.gravity = gravity;
            
            shell.explosionSounds = explosionSounds;
            shell.explosionSoundVolume = explosionSoundVolume;
            shell.explosionEffectTemplate = explosionEffectTemplate;
            shell.explosionLightTemplate = explosionLightTemplate;
            shell.explosionLightIntensity = explosionLightIntensity;
            shell.explosionDuration = explosionDuration;
            
            shell.splashSounds = splashSounds;
            shell.splashSoundVolume = splashSoundVolume;
            shell.splashEffectTemplate = splashEffectTemplate;
            shell.splashDuration = splashDuration;
            
            return shell;
        }

        private void Awake()
        {
            for (var i = 0; i < ammoPoolSize; i++) _available.Enqueue(MakeShell());
        }
        
        public void ReturnToPool(CannonShell shell) => _available.Enqueue(shell);
        
        public CannonShell TakeFromPool()
        {
            if (!_available.TryDequeue(out var shell)) shell = MakeShell();
            return shell;
        }
    }
}