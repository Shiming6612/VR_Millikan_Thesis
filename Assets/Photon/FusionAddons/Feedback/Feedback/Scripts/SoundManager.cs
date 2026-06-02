using System.Collections;
using System.Collections.Generic;
using UnityEngine;


/**
 * 
 * Store all sound effect, and the default audioSource to play them if no audio source is defined
 * 
 **/

namespace Fusion.Addons.HapticAndAudioFeedback
{
    public class SoundManager : MonoBehaviour
    {
        public AudioSource defaultSceneAudioSource;
        public List<Sound> sounds = new List<Sound>();
        public List<AudioSource> dynamicAudioSources;

        private int dynamicAudioSourcesIndex = 0;

        private void Awake()
        {
            if(defaultSceneAudioSource == null)
                defaultSceneAudioSource = GetComponent<AudioSource>();

            if (defaultSceneAudioSource == null)
                Debug.LogError("defaultSceneAudioSource NOT found !");
        }

        // Try to find and return the sound manager instance
        public static SoundManager FindInstance(NetworkRunner runner = null)
        {
            SoundManager soundManager;
            if (runner != null)
            {
                // In multipeer scenerio, we will prefer to store the SoundManager under the Runner, to find the good SoundManager
                soundManager = runner.GetComponentInChildren<SoundManager>();
                if (soundManager) return soundManager;
            }

            if (NetworkProjectConfig.Global.PeerMode == NetworkProjectConfig.PeerModes.Multiple)
            {
                Debug.LogError("In multipeer mode, you should manually reference SoundManager (as several may coexists)");
                return null;
            }
            soundManager = FindAnyObjectByType<SoundManager>(FindObjectsInactive.Include);
            if (!soundManager)
            {
                Debug.LogError("Sound manager not found !");
            }
            return soundManager;
        }

        // Look for a sound in the sounds library
        public Sound SearchForSound(string soundName)
        {
            if (string.IsNullOrEmpty(soundName))
            {
                return null;
            }
            Sound s = null;

            for (int i = 0; i < sounds.Count; i++)
            {
                if (sounds[i].name == soundName)
                {
                    s = sounds[i];
                    break;
                }
            }

            if (s == null)
            {
                Debug.LogError("Sound: '" + soundName + "' not found!");
            }
            return s;
        }

        // play a sound one shot using the default scene audio source
        public void PlayOneShot(string soundName)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;
            defaultSceneAudioSource.volume = s.volume;
            defaultSceneAudioSource.PlayOneShot(s.clip);
        }

        // play a sound one shot using the audio source provided in parameter
        public void PlayOneShot(string soundName, AudioSource audioSource)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            if (audioSource == null)
            {
                audioSource = defaultSceneAudioSource;
            }
            audioSource.volume = s.volume;
            audioSource.PlayOneShot(s.clip);
        }
        
        // Play a sound with a delay
        public void PlayOneShot(string soundName, float delay)
        {
            StartCoroutine(PlaySoundWithDelay(soundName, delay));
        }

        IEnumerator PlaySoundWithDelay(string soundName, float delay)
        {
            yield return new WaitForSeconds(delay);
            PlayOneShot(soundName);
        }

        public void Play(string soundName, AudioSource audioSource, bool waitForTheEndOfPreviousClip = false)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            if (audioSource == null)
            {
                audioSource = defaultSceneAudioSource;
            }

            if (waitForTheEndOfPreviousClip && audioSource.isPlaying)
            {
                return;
            }
            else
            {
                if (audioSource.isPlaying)
                    audioSource.Stop();
                audioSource.clip = s.clip;
                audioSource.volume = s.volume;
                audioSource.loop = s.loop;
                audioSource.Play();
            }
        }

        // Play a sound selected by its name by using a dynamic audio source positionned at a specific location

        public void PlayOneShot(string soundName, Vector3 audioSourcePosition)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;
            var audioSource = FindNextDynamicAudioSource();
            if (audioSource == null)
            {
                audioSource = defaultSceneAudioSource;
            }
            audioSource.transform.position = audioSourcePosition;
            audioSource.volume = s.volume;
            audioSource.PlayOneShot(s.clip);
        }


        // Play a sound selected by its name with a random start position, using the audio source provided in parameter
        public void PlayRandomPosition(string soundName, AudioSource audioSource)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            int randomStartTime = UnityEngine.Random.Range(0, s.clip.samples - 1);
            if (audioSource == null)
            {
                audioSource = defaultSceneAudioSource;
            }
            audioSource.timeSamples = randomStartTime;
            audioSource.Play();
            StartCoroutine(FadeAudioSource.StartFade(audioSource, 0f, 2.5f, audioSource.volume));
        }

        // Play a sound selected by its name with a random start position, using the default scene audio source
        public void PlayRandomPosition(string soundName)
        {
            Sound s = SearchForSound(soundName);
            if (s == null) return;

            int randomStartTime = UnityEngine.Random.Range(0, s.clip.samples - 1);

            defaultSceneAudioSource.timeSamples = randomStartTime;
            defaultSceneAudioSource.Play();
            StartCoroutine(FadeAudioSource.StartFade(defaultSceneAudioSource, 0f, 2.5f, defaultSceneAudioSource.volume));
        }

        // Provide the next AudioSource to use
        private AudioSource FindNextDynamicAudioSource()
        {
            if (dynamicAudioSources.Count == 0)
            {
                Debug.LogError("No dynamic audio source defined");
                return null;
            }
            else
            {
                if (dynamicAudioSourcesIndex == dynamicAudioSources.Count - 1)
                {
                    dynamicAudioSourcesIndex = 0;
                }
                else
                {
                    dynamicAudioSourcesIndex++;
                }
                return dynamicAudioSources[dynamicAudioSourcesIndex];
            }

        }
    }
}
