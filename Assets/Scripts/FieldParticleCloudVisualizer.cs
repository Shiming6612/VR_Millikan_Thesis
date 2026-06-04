using UnityEngine;

[RequireComponent(typeof(ParticleSystem))]
public class FieldParticleCloudVisualizer : MonoBehaviour
{
    [Header("References")]
    public VoltageKnobInput voltageSource;

    [Header("Voltage Mapping")]
    public float minVoltageToShow = 1f;
    public float maxVoltageForFullEffect = 800f;

    [Header("Emission")]
    public float minEmissionRate = 0f;
    public float maxEmissionRate = 80f;

    [Header("Alpha")]
    public float minAlpha = 0f;
    public float maxAlpha = 0.18f;

    [Header("Particle Size")]
    public float minParticleSize = 0.006f;
    public float maxParticleSize = 0.012f;

    [Header("Color")]
    public Color fieldColor = new Color(0.176f, 0.612f, 0.859f, 1f);

    [Header("Runtime")]
    public bool visualizationEnabled = false;

    private ParticleSystem ps;
    private ParticleSystem.MainModule main;
    private ParticleSystem.EmissionModule emission;
    private ParticleSystem.ColorOverLifetimeModule colorOverLifetime;

    private bool wasVisibleLastFrame = false;

    private void Awake()
    {
        ps = GetComponent<ParticleSystem>();
        main = ps.main;
        emission = ps.emission;
        colorOverLifetime = ps.colorOverLifetime;

        main.playOnAwake = false;
        SetEmissionRate(0f);
        ApplyColorGradient(0f);

        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void Update()
    {
        UpdateCloud();
    }

    public void SetVisualizationEnabled(bool enabled)
    {
        visualizationEnabled = enabled;

        if (!visualizationEnabled)
        {
            wasVisibleLastFrame = false;
            SetEmissionRate(0f);
            ApplyColorGradient(0f);

            if (ps != null)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    private void UpdateCloud()
    {
        if (ps == null)
            return;

        float voltage = voltageSource != null ? Mathf.Abs(voltageSource.CurrentVoltage) : 0f;

        bool shouldShow =
            visualizationEnabled &&
            voltage >= minVoltageToShow;

        if (!shouldShow)
        {
            wasVisibleLastFrame = false;
            SetEmissionRate(0f);
            ApplyColorGradient(0f);

            if (ps.isPlaying)
                ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            return;
        }

        float t = Mathf.InverseLerp(
            minVoltageToShow,
            Mathf.Max(minVoltageToShow + 1f, maxVoltageForFullEffect),
            voltage
        );

        float alpha = Mathf.Lerp(minAlpha, maxAlpha, t);
        float emissionRate = Mathf.Lerp(minEmissionRate, maxEmissionRate, t);
        float particleSize = Mathf.Lerp(minParticleSize, maxParticleSize, t);

        main.startSize = particleSize;
        SetEmissionRate(emissionRate);
        ApplyColorGradient(alpha);

        if (!ps.isPlaying)
        {
            ps.Play();
        }

        wasVisibleLastFrame = true;
    }

    private void SetEmissionRate(float rateValue)
    {
        ParticleSystem.MinMaxCurve rate = emission.rateOverTime;
        rate.constant = rateValue;
        emission.rateOverTime = rate;
    }

    private void ApplyColorGradient(float alpha)
    {
        colorOverLifetime.enabled = true;

        Gradient gradient = new Gradient();

        gradient.SetKeys(
            new GradientColorKey[]
            {
                new GradientColorKey(fieldColor, 0f),
                new GradientColorKey(fieldColor, 0.2f),
                new GradientColorKey(fieldColor, 0.8f),
                new GradientColorKey(fieldColor, 1f)
            },
            new GradientAlphaKey[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(alpha, 0.2f),
                new GradientAlphaKey(alpha, 0.8f),
                new GradientAlphaKey(0f, 1f)
            }
        );

        colorOverLifetime.color = gradient;
    }
}