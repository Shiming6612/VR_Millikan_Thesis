using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.StructureCohesion
{
    /// <summary>
    /// This class use the StructurePartsManager to display a visual guide between compatible attachment points when a user is grabbing a part with a minimal impact on performance.
    /// </summary>
    public class StructurePartVisualGuide : MonoBehaviour
    {
        [SerializeField] GrabbableStructurePart grabbableStructurePart;
        [SerializeField] StructurePartsManager structurePartsManager;

        [Header("Line settings")]
        [SerializeField] int numCurvePoints = 20;                        // Number of points to define the curve
        [SerializeField] float minDistanceForCurveLine = 0.05f;
        List<Vector3> curvePoints = new List<Vector3>();                    // List to store the points of the curve

        List<LineRenderer> lines = new List<LineRenderer>();
        List<VisualGuide> visualGuideList = new List<VisualGuide>();
        bool linesRendererUpdated = false;

        [System.Serializable]
        public struct VisualGuide
        {
            public AttachmentPoint attachmentPoint;
            public LineRenderer lineRenderer;
        };


        // Start is called before the first frame update
        void Start()
        {
            if (grabbableStructurePart == null)
                grabbableStructurePart = GetComponentInParent<GrabbableStructurePart>();
            if (grabbableStructurePart == null)
                Debug.LogError("grabbableStructurePart not found");

            if (structurePartsManager == null)
                structurePartsManager = FindAnyObjectByType<StructurePartsManager>(FindObjectsInactive.Include);

            foreach (var attachmentPoint in GetComponentsInChildren<AttachmentPoint>())
            {
                visualGuideList.Add(new VisualGuide { attachmentPoint = attachmentPoint, lineRenderer = attachmentPoint.GetComponentInChildren<LineRenderer>() });
            }
        }

        // Update is called once per frame
        void Update()
        {
            if (grabbableStructurePart && grabbableStructurePart.Object && grabbableStructurePart.IsMoving)
            {
                // Dictionary to track the closest attachmentPoint for each compatibleAttachmentPointTag
                Dictionary<string, (AttachmentPoint attachmentPoint, float minDistance, AttachmentPoint closestAttachmentPoint)> tagClosestPointMap = new();


                // Check each AttachmentPoints of the GrabbableStructurePart
                foreach (var visualGuide in visualGuideList)
                {
                    // No need to display the lineRenderer if there is already an attached point on the attachment Point
                    if (visualGuide.attachmentPoint.RelatedPoint != null)
                        continue;

                    var lineRenderer = visualGuide.lineRenderer;

                    foreach (var compatibleAttachmentPointTag in visualGuide.attachmentPoint.compatibleAttachmentPointTags)
                    {
                        // If this tag is not in the dictionnary, we add it
                        if (tagClosestPointMap.ContainsKey(compatibleAttachmentPointTag) == false)
                        {
                            tagClosestPointMap[compatibleAttachmentPointTag] = (visualGuide.attachmentPoint, float.MaxValue, default);
                        }

                        // check StructurePartsManager cache to get the list of AttachmentPoints compatible with the compatibleAttachmentPointTags
                        if (structurePartsManager.attachmentPointTags.TryGetValue(compatibleAttachmentPointTag, out var compatibleAttachmentPoints))
                        {
                            foreach (var compatibleAttachmentPoint in compatibleAttachmentPoints)
                            {
                                if (compatibleAttachmentPoint == visualGuide.attachmentPoint) continue;

                                // check that the compatibleAttachmentPoint is not already connected to another attachmentPoint
                                if (compatibleAttachmentPoint.RelatedPoint != null) continue;

                                // Calculate distance
                                float distance = Vector3.Distance(visualGuide.attachmentPoint.transform.position, compatibleAttachmentPoint.transform.position);

                                // Update the dictionary only if this distance is shorter
                                if (distance < tagClosestPointMap[compatibleAttachmentPointTag].minDistance)
                                {
                                    tagClosestPointMap[compatibleAttachmentPointTag] = (visualGuide.attachmentPoint, distance, compatibleAttachmentPoint);
                                }
                            }
                        }
                    }
                }

                // Disable all LineRenders
                foreach (var visualGuide in visualGuideList)
                {
                    visualGuide.lineRenderer.enabled = false;
                }

                // Render lines for the closest points
                foreach (var tagClosestPointMapEntry in tagClosestPointMap)
                {
                    var attachmentPoint = tagClosestPointMapEntry.Value.attachmentPoint;
                    var closestAttachmentPoint = tagClosestPointMapEntry.Value.closestAttachmentPoint;
                    LineRenderer attachmentPointLineRenderer = null;
                    LineRenderer closestAttachmentPointLineRenderer = closestAttachmentPoint.GetComponentInChildren<LineRenderer>();

                    foreach (var visualGuide in visualGuideList)
                    {
                        if (visualGuide.attachmentPoint == attachmentPoint)
                        {
                            attachmentPointLineRenderer = visualGuide.lineRenderer;
                            continue;
                        }
                    }

                    if (attachmentPointLineRenderer != null && closestAttachmentPointLineRenderer != null)
                    {
                        // Update the Line Renderer positions to create a curve
                        // The start position is the attachmentPoint position
                        // The start offset position is the position of the attachmentPoint LineRenderer
                        // The end position is the closestAttachmentPoint position
                        // The end offset position is the position of the closestAttachmentPoint LineRenderer
                        if (tagClosestPointMapEntry.Value.minDistance > minDistanceForCurveLine)
                            UpdateCurvePoints(attachmentPoint.transform.position, attachmentPointLineRenderer.transform.position, closestAttachmentPoint.transform.position, closestAttachmentPointLineRenderer.transform.position);
                        else
                            UpdateCurvePoints(attachmentPoint.transform.position, attachmentPoint.transform.position, closestAttachmentPoint.transform.position, attachmentPoint.transform.position);

                        attachmentPointLineRenderer.positionCount = curvePoints.Count;
                        attachmentPointLineRenderer.SetPositions(curvePoints.ToArray());
                        attachmentPointLineRenderer.enabled = true;
                        lines.Add(attachmentPointLineRenderer);
                        linesRendererUpdated = false;
                    }
                }
            }
            else
            {
                // disable all the lineRenderer
                if (linesRendererUpdated == false)
                {
                    foreach (var line in lines)
                    {
                        line.enabled = false;
                    }
                    lines.Clear();
                    linesRendererUpdated = true;
                }
            }

        }


        void UpdateCurvePoints(Vector3 startCurvePosition, Vector3 curveStartPositionOffset, Vector3 EndCurvePosition, Vector3 curveEndPositionOffset)

        {
            curvePoints.Clear();

            curvePoints.Add(startCurvePosition);

            for (int i = 1; i < numCurvePoints - 1; i++)
            {
                float t = i / (float)(numCurvePoints - 1);
                Vector3 point = Bezier(startCurvePosition, curveStartPositionOffset, curveEndPositionOffset, EndCurvePosition, t);
                curvePoints.Add(point);
            }

            curvePoints.Add(EndCurvePosition);
        }

        Vector3 Bezier(Vector3 a, Vector3 b, float t)
        {
            return Vector3.Lerp(a, b, t);
        }

        Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, float t)
        {
            return Vector3.Lerp(Bezier(a, b, t), Bezier(b, c, t), t);
        }

        Vector3 Bezier(Vector3 a, Vector3 b, Vector3 c, Vector3 d, float t)
        {
            return Vector3.Lerp(Bezier(a, b, c, t), Bezier(b, c, d, t), t);
        }
    }

}
