using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SpraySpawner : MonoBehaviour
{
    [Header("Refs")]
    public Transform spawnOrigin;
    public Transform aimTarget;
    public OilDrop dropPrefab;

    [Header("Limits")]
    public int maxTotalDrops = 30;
    public int minDropsPerSpray = 3;
    public int maxDropsPerSpray = 5;
    public float burstDuration = 0.12f;
    public float minTimeBetweenSprays = 0.05f;

    [Header("Spawn + Launch")]
    public float spawnRadius = 0.01f;
    public bool useAimTarget = true;
    [Range(0f, 60f)] public float coneAngle = 18f;
    public float baseLaunchSpeed = 1.2f;
    public float speedRandomPercent = 0.15f;
    public float lateralJitterSpeed = 0.15f;
    public float upwardBias = 0.05f;

    [Header("Tutorial Radius Mode")]
    public bool useTutorialRadiusMode = false;
    public float tutorialRadiusMicrometer = 0.5f;

    [Tooltip("If enabled, charge is calculated automatically so that the droplet can hover around the target voltage.")]
    public bool autoChargeForTutorialRadius = true;

    [Tooltip("Target voltage used to calculate a suitable charge for the tutorial radius task.")]
    public float targetHoverVoltage = 500f;

    [Tooltip("Plate distance in meters. For this project: 6 mm = 0.006 m.")]
    public float plateSpacingMeters = 0.006f;

    [Tooltip("Used only when Auto Charge For Tutorial Radius is disabled.")]
    public int fixedTutorialChargeMultiple = 1;

    [Header("Tutorial Charge Limits")]
    public int tutorialMinChargeMultiple = 1;
    public int tutorialMaxChargeMultiple = 30;

    [Header("Random Mode After Tutorial")]
    public float randomMinRadiusMicrometer = 0.5f;
    public float randomMaxRadiusMicrometer = 1.0f;

    [Tooltip("If enabled, random droplets will still be physically hoverable within the voltage range.")]
    public bool autoChargeForRandomDrops = true;

    [Tooltip("Minimum target hover voltage for random droplets.")]
    public float randomTargetHoverVoltageMin = 300f;

    [Tooltip("Maximum target hover voltage for random droplets.")]
    public float randomTargetHoverVoltageMax = 650f;

    public int randomMinChargeMultiple = 3;
    public int randomMaxChargeMultiple = 20;

    [Header("Nozzle Feedback")]
    public AudioSource nozzleSfxSource;
    public AudioClip nozzleSfx;
    public ParticleSystem nozzleVfxPrefab;
    public Transform nozzlePoint;

    private int spawnedCount = 0;
    private float lastSprayTime = -999f;
    private Coroutine burstRoutine;
    private readonly List<OilDrop> spawnedDrops = new List<OilDrop>();

    public void SprayOnce()
    {
        if (spawnOrigin == null || dropPrefab == null)
            return;

        if (spawnedCount >= maxTotalDrops)
            return;

        if (Time.time - lastSprayTime < minTimeBetweenSprays)
            return;

        lastSprayTime = Time.time;

        int wantedCount = Random.Range(minDropsPerSpray, maxDropsPerSpray + 1);
        wantedCount = Mathf.Min(wantedCount, maxTotalDrops - spawnedCount);

        if (wantedCount <= 0)
            return;

        PlayNozzleFeedback();

        if (burstRoutine != null)
            StopCoroutine(burstRoutine);

        burstRoutine = StartCoroutine(SpawnBurst(wantedCount));
    }

    public void ResetAllDrops()
    {
        if (burstRoutine != null)
            StopCoroutine(burstRoutine);

        burstRoutine = null;

        for (int i = 0; i < spawnedDrops.Count; i++)
        {
            if (spawnedDrops[i] != null)
                Destroy(spawnedDrops[i].gameObject);
        }

        spawnedDrops.Clear();
        spawnedCount = 0;
    }

    public void EnableTutorialRadiusMode()
    {
        useTutorialRadiusMode = true;
        ResetAllDrops();
    }

    public void DisableTutorialRadiusMode()
    {
        useTutorialRadiusMode = false;
    }

    public void SetTutorialRadiusMicrometer(float radiusMicrometer, bool clearExistingDrops)
    {
        useTutorialRadiusMode = true;
        tutorialRadiusMicrometer = Mathf.Clamp(radiusMicrometer, 0.1f, 5.0f);

        if (clearExistingDrops)
            ResetAllDrops();
    }

    public void SetTutorialRadiusStep(int index)
    {
        useTutorialRadiusMode = true;

        if (index == 0)
            tutorialRadiusMicrometer = 0.5f;
        else if (index == 1)
            tutorialRadiusMicrometer = 1.0f;
        else if (index == 2)
            tutorialRadiusMicrometer = 1.5f;

        ResetAllDrops();
    }

    public void SetTeachingRadiusStep(int index)
    {
        SetTutorialRadiusStep(index);
    }

    public void SetTutorialRadius05()
    {
        SetTutorialRadiusStep(0);
    }

    public void SetTutorialRadius10()
    {
        SetTutorialRadiusStep(1);
    }

    public void SetTutorialRadius15()
    {
        SetTutorialRadiusStep(2);
    }

    public void ReturnToRandomModeAndClearDrops()
    {
        useTutorialRadiusMode = false;
        ResetAllDrops();
    }

    private IEnumerator SpawnBurst(int count)
    {
        float delay = (burstDuration <= 0f || count <= 1)
            ? 0f
            : burstDuration / (count - 1);

        for (int i = 0; i < count; i++)
        {
            SpawnOne();

            if (delay > 0f)
                yield return new WaitForSeconds(delay);
        }

        burstRoutine = null;
    }

    private void SpawnOne()
    {
        OilDrop drop = Instantiate(dropPrefab);
        spawnedDrops.Add(drop);
        spawnedCount++;

        ApplyDropProperties(drop);

        Vector2 randomOffset = Random.insideUnitCircle * spawnRadius;
        Vector3 position =
            spawnOrigin.position +
            spawnOrigin.right * randomOffset.x +
            spawnOrigin.up * randomOffset.y;

        Vector3 baseDirection = (useAimTarget && aimTarget != null)
            ? (aimTarget.position - position).normalized
            : spawnOrigin.forward;

        baseDirection = (baseDirection + Vector3.up * upwardBias).normalized;
        Vector3 direction = RandomDirectionInCone(baseDirection, coneAngle);

        float speed = baseLaunchSpeed * (1f + Random.Range(-speedRandomPercent, speedRandomPercent));

        Vector3 lateralDirection = Vector3.ProjectOnPlane(Random.onUnitSphere, direction);
        Vector3 lateral = lateralDirection.sqrMagnitude > 0.0001f
            ? lateralDirection.normalized * lateralJitterSpeed
            : Vector3.zero;

        drop.Launch(position, direction * speed + lateral);

        BottomTutorialController tutorial = FindFirstObjectByType<BottomTutorialController>();
        if (tutorial != null)
            tutorial.NotifyDropletTriggered();
    }

    private void ApplyDropProperties(OilDrop drop)
    {
        if (drop == null)
            return;

        DropProperties properties = drop.GetComponent<DropProperties>();

        if (properties == null)
            properties = drop.GetComponentInChildren<DropProperties>();

        if (properties == null)
            return;

        if (useTutorialRadiusMode)
        {
            ApplyTutorialRadiusProperties(properties);
        }
        else
        {
            ApplyRandomExperimentProperties(properties);
        }
    }

    private void ApplyTutorialRadiusProperties(DropProperties properties)
    {
        properties.minChargeMultiple = tutorialMinChargeMultiple;
        properties.maxChargeMultiple = tutorialMaxChargeMultiple;

        if (autoChargeForTutorialRadius)
        {
            properties.ApplyRadiusAndAutoCharge(
                tutorialRadiusMicrometer,
                targetHoverVoltage,
                plateSpacingMeters
            );
        }
        else
        {
            properties.ApplyRadiusAndCharge(
                tutorialRadiusMicrometer,
                fixedTutorialChargeMultiple
            );
        }
    }

    private void ApplyRandomExperimentProperties(DropProperties properties)
    {
        float radius = Random.Range(
            Mathf.Min(randomMinRadiusMicrometer, randomMaxRadiusMicrometer),
            Mathf.Max(randomMinRadiusMicrometer, randomMaxRadiusMicrometer)
        );

        if (autoChargeForRandomDrops)
        {
            properties.minChargeMultiple = randomMinChargeMultiple;
            properties.maxChargeMultiple = randomMaxChargeMultiple;

            float targetHoverVoltage = Random.Range(
                Mathf.Min(randomTargetHoverVoltageMin, randomTargetHoverVoltageMax),
                Mathf.Max(randomTargetHoverVoltageMin, randomTargetHoverVoltageMax)
            );

            properties.ApplyRadiusAndAutoCharge(
                radius,
                targetHoverVoltage,
                plateSpacingMeters
            );
        }
        else
        {
            int charge = Random.Range(
                Mathf.Min(randomMinChargeMultiple, randomMaxChargeMultiple),
                Mathf.Max(randomMinChargeMultiple, randomMaxChargeMultiple) + 1
            );

            properties.ApplyRadiusAndCharge(radius, charge);
        }
    }

    private void PlayNozzleFeedback()
    {
        if (nozzleSfxSource != null && nozzleSfx != null)
            nozzleSfxSource.PlayOneShot(nozzleSfx);

        if (nozzleVfxPrefab == null)
            return;

        Transform target = nozzlePoint != null
            ? nozzlePoint
            : spawnOrigin != null ? spawnOrigin : transform;

        ParticleSystem vfx = Instantiate(nozzleVfxPrefab, target.position, target.rotation);
        vfx.Play();

        float destroyAfter = 2f;
        ParticleSystem.MainModule main = vfx.main;

        if (!main.loop)
        {
            float lifetime = main.startLifetime.constantMax;
            destroyAfter = Mathf.Max(0.1f, main.duration + lifetime + 0.2f);
        }

        Destroy(vfx.gameObject, destroyAfter);
    }

    private static Vector3 RandomDirectionInCone(Vector3 forward, float coneHalfAngleDeg)
    {
        if (coneHalfAngleDeg <= 0.001f)
            return forward.normalized;

        float coneRad = coneHalfAngleDeg * Mathf.Deg2Rad;
        float cosMin = Mathf.Cos(coneRad);

        float z = Random.Range(cosMin, 1f);
        float theta = Random.Range(0f, Mathf.PI * 2f);
        float r = Mathf.Sqrt(1f - z * z);

        Vector3 local = new Vector3(
            r * Mathf.Cos(theta),
            r * Mathf.Sin(theta),
            z
        );

        return Quaternion.FromToRotation(Vector3.forward, forward.normalized) * local;
    }
}