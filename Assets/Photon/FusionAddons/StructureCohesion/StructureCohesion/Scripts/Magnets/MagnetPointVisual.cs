using Fusion.Addons.StructureCohesion;
using Fusion.XRShared.GrabbableMagnet;
using UnityEngine;


public class MagnetPointVisual : MonoBehaviour
{
    [Header("Attractable")]
    [SerializeField]
    bool displayAttractableVisual = true;
    public float attractableVisualSize = 0.025f;
    [SerializeField]
    GameObject attractableVisual;
    [SerializeField]
    AttractableMagnet attractableMagnet;

    [Header("Attractor")]
    [SerializeField]
    bool displayAttractorVisual = true;
    public float attractorVisualSize = 0.025f;
    [SerializeField]
    GameObject attractorVisuals;
    [SerializeField]
    GameObject attractorVisualWithoutOrthogonalRotationAndPreciseMagnetPosition;
    [SerializeField]
    GameObject attractorVisualWithoutOrthogonalRotationAndLooseMagnetPosition;
    [SerializeField]
    GameObject attractorVisualWithOrthogonalRotationAndPreciseMagnetPosition;
    [SerializeField]
    GameObject attractorVisualWithOrthogonalRotationAndLooseMagnetPosition;
    [SerializeField]
    AttractorMagnet attractorMagnet;


    [Header("Magnet Power Visual")]
    [SerializeField]
    bool displayPowerVisualForAttractableMagnet = true;
    [SerializeField]
    bool displayPowerVisualForAttractorMagnet = true;
    [SerializeField]
    GameObject magnetPowerVisuals;
    [SerializeField]
    GameObject attractorMagnetPowerVisual;
    [SerializeField]
    GameObject attractableMagnetPowerVisual;
    [SerializeField]
    float visualSizeRadiusForAttractor = 0.01f;

    float visualSizeRadius = 0.025f;

    StructurePart structurePart;
    AttachmentPoint structureAttachmentPoint;

    private void Awake()
    {
        // Find magnets
        if (attractableMagnet == null)
            attractableMagnet = GetComponentInParent<AttractableMagnet>();

        if (attractorMagnet == null)
            attractorMagnet = GetComponentInParent<AttractorMagnet>();


        // check magnet to display
        if (attractorVisuals && attractorMagnet)
            attractorVisuals.SetActive(displayAttractorVisual);
        else
            attractorVisuals.SetActive(false);


        if (attractableVisual && attractableMagnet)
            attractableVisual.SetActive(displayAttractableVisual);
        else
            attractableVisual.SetActive(false);

        if (magnetPowerVisuals)
            magnetPowerVisuals.SetActive(displayPowerVisualForAttractableMagnet);

        // resize and select the magnet to display
        if (displayAttractableVisual && attractableVisual)
        {
            attractableVisual.transform.localScale = new Vector3(attractableVisualSize, attractableVisualSize, attractableVisualSize);
        }

        if (displayAttractorVisual && attractorVisuals && attractorMagnet)
        {
            attractorVisuals.transform.localScale = new Vector3(attractorVisualSize, attractorVisualSize, attractorVisualSize);

            attractorVisualWithoutOrthogonalRotationAndPreciseMagnetPosition.SetActive(false);
            attractorVisualWithoutOrthogonalRotationAndLooseMagnetPosition.SetActive(false);
            attractorVisualWithOrthogonalRotationAndPreciseMagnetPosition.SetActive(false);
            attractorVisualWithOrthogonalRotationAndLooseMagnetPosition.SetActive(false);

            // Check which attractor visual to display
            if (attractorMagnet.attractedMagnetRotation == AttractedMagnetRotation.MatchAlignmentAxisWithOrthogonalRotation)
            {
                if (attractorMagnet.attractedMagnetMove == AttractedMagnetMove.AttractOnlyOnAlignmentAxis)
                    attractorVisualWithOrthogonalRotationAndLooseMagnetPosition.SetActive(true);
                else
                    attractorVisualWithOrthogonalRotationAndPreciseMagnetPosition.SetActive(true);
            }
            else
            {
                if (attractorMagnet.attractedMagnetMove == AttractedMagnetMove.AttractOnlyOnAlignmentAxis)
                    attractorVisualWithoutOrthogonalRotationAndLooseMagnetPosition.SetActive(true);
                else
                    attractorVisualWithoutOrthogonalRotationAndPreciseMagnetPosition.SetActive(true);

            }
        }


        if (structureAttachmentPoint == null)
            structureAttachmentPoint = GetComponentInParent<MagnetStructureAttachmentPoint>();

        // check if Attractable magnet visual should be displayed
        if (displayPowerVisualForAttractableMagnet && attractableMagnet)
        {
            if (attractableMagnet.MagnetCoordinator != null && attractableMagnet.MagnetCoordinator.overrideMagnetRadius == true)
            {
                visualSizeRadius = attractableMagnet.MagnetCoordinator.magnetRadius;
            }
            else
            {
                visualSizeRadius = attractableMagnet.magnetRadius * 2;
            }

            attractableMagnetPowerVisual.transform.localScale = new Vector3(visualSizeRadius, visualSizeRadius, visualSizeRadius);
            attractableMagnetPowerVisual.SetActive(true);
            magnetPowerVisuals.SetActive(true);
        }
        else
        {
            attractableMagnetPowerVisual.SetActive(false);
        }

        // check if Attractor magnet visual should be displayed
        if (displayPowerVisualForAttractorMagnet && attractorMagnet)
        {
            attractorMagnetPowerVisual.transform.localScale = new Vector3(visualSizeRadiusForAttractor * 2, visualSizeRadiusForAttractor * 2, visualSizeRadiusForAttractor * 2); ;
            attractorMagnetPowerVisual.SetActive(true);
            magnetPowerVisuals.SetActive(true);
        }
        else
        {
            attractorMagnetPowerVisual.SetActive(false);
        }


        // Add Structure part listeners
        if (structurePart == null) structurePart = GetComponentInParent<StructurePart>();
        if (structurePart != null)
        {
            structurePart.onRegisterAttachmentEvent.AddListener(OnSnap);
            structurePart.onUnregisterAttachmentEvent.AddListener(OnUnsnap);
        }

    }


    private void OnSnap(StructurePart part, AttachmentPoint attachmentPoint, Vector3 snapPosition, bool reverseAttachement)
    {
        if (structureAttachmentPoint == attachmentPoint)
            UpdateVisuals(false);
    }

    private void OnUnsnap(StructurePart part, AttachmentPoint attachmentPoint, Vector3 unsnapPosition, bool reverseAttachement)
    {
        if (structureAttachmentPoint == attachmentPoint)
            UpdateVisuals(true);
    }

    public void UpdateVisuals(bool shouldDisplayVisuals)
    {

        if (displayPowerVisualForAttractableMagnet)
            attractableMagnetPowerVisual.SetActive(shouldDisplayVisuals);

        if (displayPowerVisualForAttractorMagnet)
            attractorMagnetPowerVisual.SetActive(shouldDisplayVisuals);

        if (displayAttractableVisual && attractableVisual && attractableVisual)
            attractableVisual.SetActive(shouldDisplayVisuals);

        if (displayAttractorVisual && attractorVisuals && attractorMagnet)
            attractorVisuals.SetActive(shouldDisplayVisuals);
    }

    private void OnDestroy()
    {
        structurePart?.onRegisterAttachmentEvent?.RemoveListener(OnSnap);
        structurePart?.onUnregisterAttachmentEvent?.RemoveListener(OnUnsnap);
    }
}

