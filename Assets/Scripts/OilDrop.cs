using UnityEngine;

[RequireComponent(typeof(Rigidbody))]
[RequireComponent(typeof(Collider))]
public class OilDrop : MonoBehaviour
{
    [Header("Calculation")]
    public Vector3 customGravity = new Vector3(0f, -9.81f, 0f);

    [Header("Launch")]
    public float launchPhaseDuration = 0.45f;
    public float launchVelocityScale = 0.65f;
    public float maxLaunchSpeed = 0.8f;

    [Header("Movement")]
    public float baseFallSpeed = 0.16f;
    public float maxVerticalSpeed = 0.35f;
    public float velocitySmoothing = 6f;
    public float horizontalDamping = 4f;

    [Header("Radius Effect")]
    public bool radiusAffectsFallSpeed = true;
    public float referenceRadiusMicrometer = 1.0f;
    public float radiusFallSpeedPower = 1.5f;

    [Header("Electric Field")]
    public float hoverDeadZone = 0.03f;
    public float electricResponsePower = 1.0f;
    public bool resetFieldRatioWhenOutside = true;

    [Header("Collision")]
    public bool destroyOnCollision = false;

    private Rigidbody rb;
    private DropProperties dropProperties;

    private Vector3 startPosition;
    private bool activeDrop;
    private float launchStartTime;

    private float electricFieldRatio = 0f;
    private bool receivedFieldRatioThisFrame = false;

    private void Awake()
    {
        rb = GetComponent<Rigidbody>();
        dropProperties = GetComponent<DropProperties>();

        if (dropProperties == null)
            dropProperties = GetComponentInChildren<DropProperties>();

        rb.useGravity = false;
        rb.isKinematic = false;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
        rb.interpolation = RigidbodyInterpolation.Interpolate;
        rb.linearDamping = 0f;
        rb.angularDamping = 0f;

        Collider col = GetComponent<Collider>();
        col.isTrigger = false;

        gameObject.SetActive(false);
    }

    public void Launch(Vector3 worldPos, Vector3 initialVelocity)
    {
        startPosition = worldPos;
        transform.position = worldPos;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        Vector3 launchVelocity = initialVelocity * Mathf.Max(0f, launchVelocityScale);
        rb.linearVelocity = Vector3.ClampMagnitude(launchVelocity, maxLaunchSpeed);

        electricFieldRatio = 0f;
        receivedFieldRatioThisFrame = false;

        launchStartTime = Time.time;
        activeDrop = true;

        gameObject.SetActive(true);
    }

    public void ResetDrop()
    {
        activeDrop = false;

        rb.linearVelocity = Vector3.zero;
        rb.angularVelocity = Vector3.zero;

        electricFieldRatio = 0f;
        receivedFieldRatioThisFrame = false;

        transform.position = startPosition;
        gameObject.SetActive(false);
    }

    public void SetElectricFieldRatio(float ratio)
    {
        electricFieldRatio = Mathf.Max(0f, ratio);
        receivedFieldRatioThisFrame = true;
    }

    private void FixedUpdate()
    {
        if (!activeDrop)
            return;

        if (Time.time - launchStartTime < launchPhaseDuration)
        {
            rb.linearVelocity = Vector3.ClampMagnitude(rb.linearVelocity, maxLaunchSpeed);
            receivedFieldRatioThisFrame = false;
            return;
        }

        if (resetFieldRatioWhenOutside && !receivedFieldRatioThisFrame)
            electricFieldRatio = 0f;

        ApplyObservationMotion();

        receivedFieldRatioThisFrame = false;
    }

    private void ApplyObservationMotion()
    {
        Vector3 currentVelocity = rb.linearVelocity;

        Vector3 horizontalVelocity = new Vector3(currentVelocity.x, 0f, currentVelocity.z);
        horizontalVelocity = Vector3.Lerp(
            horizontalVelocity,
            Vector3.zero,
            Time.fixedDeltaTime * horizontalDamping
        );

        float baseSpeed = GetBaseFallSpeedByRadius();
        float ratio = electricFieldRatio;

        float verticalSpeed;

        if (Mathf.Abs(ratio - 1f) <= hoverDeadZone)
        {
            verticalSpeed = 0f;
        }
        else
        {
            float signedFactor = 1f - ratio;
            float magnitude = Mathf.Pow(
                Mathf.Abs(signedFactor),
                Mathf.Max(0.01f, electricResponsePower)
            );

            verticalSpeed = -Mathf.Sign(signedFactor) * baseSpeed * magnitude;
        }

        verticalSpeed = Mathf.Clamp(verticalSpeed, -maxVerticalSpeed, maxVerticalSpeed);

        Vector3 targetVelocity = new Vector3(
            horizontalVelocity.x,
            verticalSpeed,
            horizontalVelocity.z
        );

        rb.linearVelocity = Vector3.Lerp(
            currentVelocity,
            targetVelocity,
            Time.fixedDeltaTime * velocitySmoothing
        );
    }

    private float GetBaseFallSpeedByRadius()
    {
        float radiusFactor = 1f;

        if (radiusAffectsFallSpeed && dropProperties != null && dropProperties.RadiusMicrometer > 0f)
        {
            radiusFactor = dropProperties.RadiusMicrometer / Mathf.Max(0.01f, referenceRadiusMicrometer);
            radiusFactor = Mathf.Pow(radiusFactor, Mathf.Max(0f, radiusFallSpeedPower));
        }

        return Mathf.Max(0.01f, baseFallSpeed * radiusFactor);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (destroyOnCollision)
            ResetDrop();
    }
}