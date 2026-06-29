using System;
using UnityEngine;

namespace TankTrouble.Audio
{
    [RequireComponent(typeof(AudioSource))]
    public sealed class AudioManager : MonoBehaviour
    {
        public const float ShootClipDuration = 0.1f;
        public const float ExplosionClipDuration = 0.2f;

        private const int SampleRate = 44100;
        private const int SourcePoolSize = 6;
        private const float ShootVolume = 0.38f;
        private const float ExplosionVolume = 0.5f;

        private static AudioManager instance;
        private static AudioClip shootClip;
        private static AudioClip explosionClip;
        private static bool audioUnavailable;

        private readonly AudioSource[] sources = new AudioSource[SourcePoolSize];
        private int nextSource;

        private void Awake()
        {
            if (instance != null && instance != this)
            {
                Destroy(gameObject);
                return;
            }

            instance = this;
            DontDestroyOnLoad(gameObject);
            EnsureAudioListener();
            ConfigureSource();
            EnsureClips();
        }

        public static void PlayShoot()
        {
            var manager = EnsureInstance();
            if (manager != null)
                manager.Play(shootClip, ShootVolume);
        }

        public static void PlayExplosion()
        {
            var manager = EnsureInstance();
            if (manager != null)
                manager.Play(explosionClip, ExplosionVolume);
        }

        private static AudioManager EnsureInstance()
        {
            if (audioUnavailable)
                return null;

            try
            {
                if (instance != null)
                    return instance;

                var go = new GameObject("AudioManager");
                go.AddComponent<AudioSource>();
                instance = go.AddComponent<AudioManager>();
                return instance;
            }
            catch (Exception)
            {
                audioUnavailable = true;
                return null;
            }
        }

        private static void EnsureAudioListener()
        {
            if (FindObjectOfType<AudioListener>() != null)
                return;

            var camera = Camera.main != null ? Camera.main : FindObjectOfType<Camera>();
            if (camera != null)
            {
                camera.gameObject.AddComponent<AudioListener>();
                return;
            }

            new GameObject("AudioListener").AddComponent<AudioListener>();
        }

        private void ConfigureSource()
        {
            sources[0] = GetComponent<AudioSource>();
            ConfigureSource(sources[0]);

            for (var i = 1; i < sources.Length; i++)
            {
                var child = new GameObject($"SfxSource_{i}");
                child.transform.SetParent(transform, false);
                sources[i] = child.AddComponent<AudioSource>();
                ConfigureSource(sources[i]);
            }
        }

        private static void ConfigureSource(AudioSource audioSource)
        {
            audioSource.playOnAwake = false;
            audioSource.loop = false;
            audioSource.spatialBlend = 0f;
            audioSource.ignoreListenerPause = true;
        }

        private void Play(AudioClip clip, float volume)
        {
            EnsureClips();
            if (clip == null)
                return;

            var audioSource = GetNextSource();
            if (audioSource != null)
                audioSource.PlayOneShot(clip, volume);
        }

        private AudioSource GetNextSource()
        {
            for (var i = 0; i < sources.Length; i++)
            {
                nextSource = (nextSource + 1) % sources.Length;
                if (sources[nextSource] != null)
                    return sources[nextSource];
            }

            return null;
        }

        private static void EnsureClips()
        {
            shootClip ??= CreateShootClip();
            explosionClip ??= CreateExplosionClip();
        }

        private static AudioClip CreateShootClip()
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * ShootClipDuration);
            var data = new float[sampleCount];
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var normalized = t / ShootClipDuration;
                var frequency = Mathf.Lerp(240f, 110f, normalized);
                var square = Mathf.Sin(2f * Mathf.PI * frequency * t) >= 0f ? 1f : -1f;
                var envelope = 1f - normalized;
                data[i] = square * envelope * envelope;
            }

            return CreateClip("TankTroubleShoot", data);
        }

        private static AudioClip CreateExplosionClip()
        {
            var sampleCount = Mathf.CeilToInt(SampleRate * ExplosionClipDuration);
            var data = new float[sampleCount];
            var random = new System.Random(73129);
            for (var i = 0; i < sampleCount; i++)
            {
                var t = i / (float)SampleRate;
                var normalized = t / ExplosionClipDuration;
                var noise = (float)(random.NextDouble() * 2.0 - 1.0);
                var low = Mathf.Sin(2f * Mathf.PI * 65f * t);
                var envelope = Mathf.Pow(1f - normalized, 2.2f);
                data[i] = (noise * 0.65f + low * 0.35f) * envelope;
            }

            return CreateClip("TankTroubleExplosion", data);
        }

        private static AudioClip CreateClip(string clipName, float[] data)
        {
            var clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
