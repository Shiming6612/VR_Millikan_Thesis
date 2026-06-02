using UnityEngine;

public class ForcesVisualizer : MonoBehaviour
{
    public DropSelectionManager selectionManager;
    public VoltageKnobInput voltageSource;
    public ElectricFieldVolume fieldVolume;

    public GameObject forcesRootPrefab;

    public string arrowFgName = "Arrow_Fg";
    public string arrowFbName = "Arrow_Fb";
    public string arrowFelName = "Arrow_Fel";

    public Vector3 localOffset = Vector3.zero;
    public Vector3 localEuler = Vector3.zero;
    public bool billboardToCamera = true;
    public float billboardSmooth = 0f;

    public float lengthScale = 0.02f;
    public float minLen = 0.02f;
    public float maxLen = 0.35f;

    public bool useSimpleBuoyancyRatio = true;
    [Range(0f, 1f)] public float buoyancyRatio = 0.2f;

    public bool showFelOnlyWhenVoltageNonZero = true;
    public float voltageEpsilon = 0.1f;
    public bool assumeFieldUp = true;

    GameObject inst;
    Transform fg;
    Transform fb;
    Transform fel;

    SelectableDrop lastSelected;
    Transform targetDrop;
    DropProperties dropProperties;
    Rigidbody rb;

    void Start()
    {
        if (forcesRootPrefab == null)
        {
            Debug.LogError("[ForcesVisualizer] forcesRootPrefab is null.");
            return;
        }

        inst = Instantiate(forcesRootPrefab);
        inst.name = "ForcesRoot(Clone)";
        inst.SetActive(false);

        fg = FindChild(inst.transform, arrowFgName);
        fb = FindChild(inst.transform, arrowFbName);
        fel = FindChild(inst.transform, arrowFelName);

        if (fg == null || fb == null || fel == null)
            Debug.LogError("[ForcesVisualizer] Arrow child not found. Check prefab child names.");
    }

    void Update()
    {
        if (inst == null || selectionManager == null) return;

        var sel = selectionManager.CurrentSelected;

        if (sel != lastSelected)
        {
            lastSelected = sel;
            OnSelectionChanged(sel);
        }

        if (targetDrop == null || !inst.activeSelf) return;

        if (billboardToCamera)
            FaceCamera();

        UpdateForcesAndArrows();
    }

    void OnSelectionChanged(SelectableDrop sel)
    {
        if (sel == null)
        {
            targetDrop = null;
            dropProperties = null;
            rb = null;
            inst.SetActive(false);
            return;
        }

        targetDrop = sel.transform;
        dropProperties = sel.GetComponent<DropProperties>();
        if (dropProperties == null) dropProperties = sel.GetComponentInParent<DropProperties>();
        if (dropProperties == null) dropProperties = sel.GetComponentInChildren<DropProperties>();

        rb = sel.GetComponent<Rigidbody>();
        if (rb == null) rb = sel.GetComponentInParent<Rigidbody>();
        if (rb == null) rb = sel.GetComponentInChildren<Rigidbody>();

        inst.transform.SetParent(targetDrop, false);
        inst.transform.localPosition = localOffset;
        inst.transform.localRotation = Quaternion.Euler(localEuler);
        inst.transform.localScale = Vector3.one;
        inst.SetActive(true);
    }

    void UpdateForcesAndArrows()
    {
        if (fg == null || fb == null || fel == null) return;

        float mass = rb != null ? Mathf.Max(1e-18f, rb.mass) : 1e-18f;

        Vector3 gravity = Physics.gravity;
        if (rb != null)
        {
            var oil = rb.GetComponent<OilDrop>();
            if (oil != null)
                gravity = oil.customGravity;
        }

        float gMag = gravity.magnitude;
        float Fg = mass * gMag;

        float Fb = useSimpleBuoyancyRatio ? buoyancyRatio * Fg : 0f;

        float voltage = voltageSource != null ? voltageSource.CurrentVoltage : 0f;
        if (fieldVolume != null && fieldVolume.invertVoltage) voltage = -voltage;

        bool hasVoltage = Mathf.Abs(voltage) > voltageEpsilon;
        fel.gameObject.SetActive(!showFelOnlyWhenVoltageNonZero || hasVoltage);

        float q = dropProperties != null ? dropProperties.ChargeC : 0f;
        float Fel = 0f;

        if (fieldVolume != null)
        {
            float d = fieldVolume.GetPlateSpacingMeters();
            if (d > 1e-6f)
            {
                float eField = Mathf.Abs(voltage) / d * Mathf.Max(1e-6f, fieldVolume.fieldScale);
                Fel = Mathf.Abs(q) * eField;
            }
        }

        SetArrow(fg, Fg, false);
        SetArrow(fb, Fb, true);

        if (fel.gameObject.activeSelf)
        {
            bool felUp = assumeFieldUp ? q >= 0f : q < 0f;
            SetArrow(fel, Fel, felUp);
        }
    }

    void SetArrow(Transform t, float forceValue, bool isUp)
    {
        if (t == null) return;

        float len = Mathf.Clamp(forceValue * lengthScale, minLen, maxLen);

        Vector3 s = t.localScale;
        s.y = len;
        t.localScale = s;

        t.localRotation = isUp
            ? Quaternion.identity
            : Quaternion.Euler(0f, 0f, 180f);
    }

    void FaceCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return;

        Quaternion targetRot = Quaternion.LookRotation(cam.transform.forward, cam.transform.up);

        if (billboardSmooth <= 0f)
            inst.transform.rotation = targetRot;
        else
            inst.transform.rotation = Quaternion.Slerp(inst.transform.rotation, targetRot, Time.deltaTime * billboardSmooth);
    }

    Transform FindChild(Transform root, string childName)
    {
        Transform[] all = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < all.Length; i++)
        {
            if (all[i].name == childName)
                return all[i];
        }
        return null;
    }
}