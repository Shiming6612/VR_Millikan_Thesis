using UnityEngine;

[RequireComponent(typeof(AudioSource))]
public class VoltageHumAudio : MonoBehaviour
{
    public VoltageKnobInput voltageSource;
    public float maxVoltage = 800f;
    [Range(0f, 1f)] public float maxVolume = 0.8f;

    AudioSource audioSource;

    void Awake()
    {
        audioSource = GetComponent<AudioSource>();
        audioSource.loop = true;
        audioSource.playOnAwake = false;
        audioSource.volume = 0f;

        if (audioSource.clip != null)
            audioSource.Play();
    }

    void Update()
    {
        float v = voltageSource != null ? Mathf.Abs(voltageSource.CurrentVoltage) : 0f;
        audioSource.volume = Mathf.Clamp01(v / Mathf.Max(1f, maxVoltage)) * maxVolume;
    }
}