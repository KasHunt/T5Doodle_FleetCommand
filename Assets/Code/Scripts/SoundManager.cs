using System;
using System.Collections;
using System.Collections.Generic;
using Code.Scripts.Utils;
using TiltFive.Logging;
using Unity.VisualScripting;
using UnityEngine;

namespace Code.Scripts
{
    public class SoundManager : MonoBehaviour
    {
        public int maxSimultaneousSounds = 10;
        public AudioClip effectVolumeRefSound;
        public AudioClip ambientSound;
        public List<AudioClip> backgroundMusic;
        public float effectVolumeRefWaitTime = 0.2f;
        public float musicTrackInterval = 3f;
        public float backgroundFadeDuration = 3f;

        public float prefSaveDelay = 3f;

        private const string SND_AMBIENT_VOLUME = "SND_Ambient_Volume";
        private const string SND_MUSIC_VOLUME = "SND_Music_Volume";
        private const string SND_EFFECTS_VOLUME = "SND_Effects_Volume";
        
        private readonly Queue<AudioSource> _availableSources = new();
        private AudioSource _ambientAudioSource;
        private AudioSource _musicAudioSource;

        private float _prefsSaveTime = float.MaxValue;
        private float _refEffectPlayTime = float.MaxValue;
        
        public NotifyingVariable<float> AmbientVolume;
        public NotifyingVariable<float> EffectVolume;
        public NotifyingVariable<float> MusicVolume;

        private int _nextBackgroundMusic;
        
        // Static instance of SoundManager, accessible from anywhere
        public static SoundManager Instance { get; private set; }

        public class ClipController
        {
            internal ClipController()
            {
                IsValid = false;
            }
            
            internal ClipController(AudioSource audioSource)
            {
                _source = audioSource;
                IsValid = true;
                ClipLength = audioSource.clip.length;
            }

            private readonly AudioSource _source;
            
            public bool IsValid { get; private set; }
            public float ClipLength { get; }

            public void Stop() => _source.Stop();
            
            public void FadeOut(float duration)
            {
                if (!IsValid) return;
                Instance.StartCoroutine(FadeOutCoroutine(duration));
            }

            public AudioSource ReleaseSource()
            {
                IsValid = false;
                return _source;
            }
            
            private IEnumerator FadeOutCoroutine(float duration) {
                var startVolume = _source.volume;

                var elapsed = 0f;
                while (_source.volume > 0 && IsValid)
                {
                    elapsed += Time.deltaTime;
                    _source.volume = Mathf.Lerp(startVolume, 0, elapsed / duration);
                    yield return null;
                }
 
                if (IsValid) _source.Stop();
            }
        }
        
        private void Awake()
        {
            // Check for existing instances and destroy this instance if we've already got a one
            if (Instance != null && Instance != this)
            {
                Log.Warn("Destroying duplicate SoundManager");
                Destroy(gameObject);
                return;
            }
            
            // Set this instance as the Singleton instance
            Instance = this;
            
            PrepareAudioSources();
            
            LoadPreferences();
            
            // Persist across scenes
            DontDestroyOnLoad(gameObject);
        }

        private void OnDestroy()
        {
            AmbientVolume.Unsubscribe(UpdateAmbientSourceVolume);
            MusicVolume.Unsubscribe(UpdateMusicSourceVolume);
            EffectVolume.Unsubscribe(UpdateEffectsVolume);
        }

        private void Update()
        {
            MaybeUpdateFadeVolumes();
            MaybeSavePreferences();
            MaybePlayReferenceEffect();
            MaybePlayNextTrack();
        }

        private void MaybeUpdateFadeVolumes()
        {
            var now = Time.unscaledTime;
            var elapsed = now - _fadeStartTime;
            var fadeProgress = Mathf.Clamp01(elapsed / backgroundFadeDuration);
            if (Math.Abs(fadeProgress - _lastFadeProgress) < 0.0001f) return;
            _lastFadeProgress = fadeProgress; 
            
            var volume = _fadeUp ? fadeProgress : 1 - fadeProgress;
            _ambientAudioSource.volume = volume * AmbientVolume.Value;
            _musicAudioSource.volume = volume * MusicVolume.Value;
        }

        private void MaybePlayNextTrack()
        {
            var now = Time.unscaledTime;
            if (now < _playNextAudioTime) return;
            _playNextAudioTime = now + PlayNextMusic() + musicTrackInterval;
        }

        private void MaybeSavePreferences()
        {
            var now = Time.unscaledTime;
            if (now < _prefsSaveTime) return;
            _prefsSaveTime = float.MaxValue;
            PlayerPrefs.Save();
        }
        
        private void MaybePlayReferenceEffect()
        {
            var now = Time.unscaledTime;
            if (now < _refEffectPlayTime) return;
            _refEffectPlayTime = float.MaxValue;
            PlaySound(effectVolumeRefSound, 1);
        }

        private void LoadPreferences()
        {
            AmbientVolume = new NotifyingVariable<float>(PlayerPrefs.GetFloat(SND_AMBIENT_VOLUME, 1f));
            MusicVolume = new NotifyingVariable<float>(PlayerPrefs.GetFloat(SND_MUSIC_VOLUME, 1f));
            EffectVolume = new NotifyingVariable<float>(PlayerPrefs.GetFloat(SND_EFFECTS_VOLUME, 1f));

            _ambientAudioSource.volume = AmbientVolume.GetAndSubscribe(UpdateAmbientSourceVolume);
            _musicAudioSource.volume = MusicVolume.GetAndSubscribe(UpdateMusicSourceVolume);
            EffectVolume.GetAndSubscribe(UpdateEffectsVolume);
        }

        private void UpdateAmbientSourceVolume(float volume)
        {
            _ambientAudioSource.volume = volume;
            PlayerPrefs.SetFloat(SND_AMBIENT_VOLUME, volume);
            _prefsSaveTime = Time.unscaledTime + prefSaveDelay;
        }
        
        private void UpdateMusicSourceVolume(float volume)
        {
            _musicAudioSource.volume = volume;
            PlayerPrefs.SetFloat(SND_MUSIC_VOLUME, volume);
            _prefsSaveTime = Time.unscaledTime + prefSaveDelay;
        }
        
        private void UpdateEffectsVolume(float volume)
        {
            // No source to set - effect volume will apply to next played clip
            PlayerPrefs.SetFloat(SND_EFFECTS_VOLUME, volume);
            _prefsSaveTime = Time.unscaledTime + prefSaveDelay;
            _refEffectPlayTime = Time.unscaledTime + effectVolumeRefWaitTime;
        }
        
        private bool _valueHasBeenSet;
        private float _fadeStartTime;
        private bool _fadeDirection;
        private float _playNextAudioTime = float.MaxValue;
        private bool _fadeUp;
        private float _ambientVolume;
        private float _musicVolume;
        private float _lastFadeProgress;

        private void PrepareAudioSources()
        {
            // Prepare and play the ambient audio source
            _ambientAudioSource = transform.AddComponent<AudioSource>();
            _ambientAudioSource.loop = true;
            _ambientAudioSource.volume = 0;
            _ambientAudioSource.clip = ambientSound;

            // Prepare the music audio source
            _musicAudioSource = transform.AddComponent<AudioSource>();
            _musicAudioSource.loop = false;
            _musicAudioSource.volume = 0;
            
            AllocateInstances();
        }

        public void StartAmbientAndMusic()
        {
            _fadeStartTime = Time.unscaledTime;
            _fadeUp = true;
            
            _ambientAudioSource.PlayDelayed(1);
            _playNextAudioTime = _fadeStartTime + PlayNextMusic() + musicTrackInterval;
        }

        public void StopAmbientAndMusic()
        {
            _fadeStartTime = Time.unscaledTime;
            _fadeUp = false;
        }
        
        private float PlayNextMusic()
        {
            if (backgroundMusic.Count == 0) return 0;
            _nextBackgroundMusic = (_nextBackgroundMusic + 1) % backgroundMusic.Count;
            var clip = backgroundMusic[_nextBackgroundMusic];

            _musicAudioSource.clip = clip;
            _musicAudioSource.Play();
            
            return clip.length;
        }
        
        private void AllocateInstances()
        {
            while (_availableSources.Count < maxSimultaneousSounds)
            {
                var soundObj = new GameObject("SoundManager_AudioSource_Instance");
                var source = soundObj.AddComponent<AudioSource>();
                soundObj.transform.SetParent(transform);
                _availableSources.Enqueue(source);
            }
        }
        
        public ClipController PlaySound(AudioClip clip, float clipVolume)
        {
            // Early exit if no clip is provided
            if (!clip) return new ClipController();
            
            // Obtain an available source, or exit if we've exhausted them
            if (!_availableSources.TryDequeue(out var source)) return new ClipController();
            source.clip = clip;
            source.volume = clipVolume * EffectVolume.Value;
            source.Play();
            
            var controller = new ClipController(source);
            
            StartCoroutine(ReturnSourceWhenFinished(controller));

            return controller;
        }

        private IEnumerator ReturnSourceWhenFinished(ClipController controller)
        {
            yield return new WaitForSeconds(controller.ClipLength);
            controller.Stop();
            _availableSources.Enqueue(controller.ReleaseSource());
        }
    }
}