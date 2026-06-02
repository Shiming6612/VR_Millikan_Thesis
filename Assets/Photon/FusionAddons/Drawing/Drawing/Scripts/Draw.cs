using System.Collections.Generic;
using UnityEngine;
using Fusion.Addons.DataSyncHelpers;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.Drawing
{

    [System.Serializable]
    public struct DrawPoint : RingBuffer.IRingBufferEntry
    {
        public Vector3 localPosition;                   // for a draw pressure to NEW_LINE_PRESSURE, we use this field for the color
        public float drawPressure;                      // A draw pressure of NEW_LINE_PRESSURE marks the start of a line

        public byte[] AsByteArray => SerializationTools.AsByteArray(localPosition, drawPressure);

        public void FillFromBytes(byte[] entryBytes)
        {
            int unserializePosition = 0;
            SerializationTools.Unserialize(entryBytes, ref unserializePosition, out localPosition);
            SerializationTools.Unserialize(entryBytes, ref unserializePosition, out drawPressure);
        }
    }

    /**
     * Class representing a 3D drawing
     * DrawPoints are stored in a networked var, and once its capacity reached, we can create new Draw object that will follow the first one * 
     */
    [DefaultExecutionOrder(Draw.EXECUTION_ORDER)]
    public class Draw : RingBufferLosslessSyncBehaviour<DrawPoint>
    {
        const int EXECUTION_ORDER = INetworkGrabbable.EXECUTION_ORDER + 10;
        public static float NEW_LINE_PRESSURE = -1;

        Color currentLinecolor;

        // Track if a drawing is finished, to display its handle upon completion.
        [Networked]
        public NetworkBool DrawIsFinished { get; set; }

        [Networked]
        public Board ProjectionBoard { get; set; }

        [Networked]
        public Drawer DrawingDrawer { get; set; }

        Board _cachedProjectionBoard;
        private Board previousProjectionBoard;

        public LineRenderer linePrefab;

        public LineRenderer currentLine;
        public List<LineRenderer> lines = new List<LineRenderer>();

        public List<Vector3> drawingPoints = new List<Vector3>();
        public List<float> drawingPressures = new List<float>();
        public List<float> drawingPathLength = new List<float>();
        public Vector3 lastPoint;

        [SerializeField] private List<MeshRenderer> handleRenderers;
        [SerializeField] private GameObject pin;

        public Collider handleCollider;

        // 2D boards
        public bool isBoardDrawing = false;


        bool previousDrawIsFinished = false;

        public List<DrawPoint> drawnPoints = new List<DrawPoint>();

        Color handleColor = Color.clear;
        bool isHandleColorSet = false;

        Vector3 lastRecordedPosition;
        float minMovementSqr = 0.001f * 0.001f;

        Vector3 lastDrawingTipPosition = Vector3.zero;


        #region Data handling
        public override void OnNewEntries(byte[] newPaddingStartBytes, DrawPoint[] newEntries)
        {
            int i = 0;
            foreach (var entry in newEntries)
            {
                i++;
                if (lossRanges.Count == 0)
                {
                    // No waiting loss request: we can add the entry
                    drawnPoints.Add(entry);
                    DrawPointAdded(entry);
                }
            }
        }

        protected override void OnNoLossRemaining()
        {
            DrawPoint[] entriesArray = SplitCompleteData();
            
            int previouslyKnownPoints = drawnPoints.Count;
            Debug.Log($"Draw: Received all data: splitting it to {entriesArray.Length} entries" +
                $" (new entries: {entriesArray.Length - previouslyKnownPoints}");
            drawnPoints = new List<DrawPoint>(entriesArray);
            int i = 0;
            foreach(var drawPoint in drawnPoints)
            {
                // We do not redraw already known points
                if (i < previouslyKnownPoints) continue;
                DrawPointAdded(drawPoint);
                i++;
            }
            RefreshHandle();
        }
        #endregion


        #region Interface for local Drawer or ColorSelection
        public void PauseDrawing()
        {
        }

        public void ResumeDrawing(Color color)
        {
            AddLineStart(color);
        }

        public void StartDraw(Color color, Board board)
        {
            // Should only be called y the Drawer just at the Draw creation
            if (Object.HasStateAuthority == false) return;

            ProjectionBoard = board;
        }

        public void FinishDraw()
        {
            if (DrawIsFinished == false) DrawIsFinished = true;
        }

        public void AddLineStart(Color color)
        {
            // Line start: here, the position field is used to store the color of the line
            var colorData = new Vector3(color.r, color.g, color.b);
            var point = new DrawPoint() { localPosition = colorData, drawPressure = NEW_LINE_PRESSURE };
            AddPoint(point); // Header Point
        }

        public void AddDrawingPoint(Vector3 worldDrawingPosition, float drawPRessure)
        {
            // We store the local position, relatively to the current drawing (to have a stable referential if the drawing is moved)
            var localDrawingPosition = transform.InverseTransformPoint(worldDrawingPosition);

            var point = new DrawPoint() { localPosition = localDrawingPosition, drawPressure = drawPRessure };
            AddPoint(point); // Actual drawing point
        }

        void AddPoint(DrawPoint point)
        {
            AddEntry(point);

            drawnPoints.Add(point);
            DrawPointAdded(point);
        }
        #endregion

        #region Handle
        public void DisplayHandle(bool isEnable)
        {
            foreach (var handleRenderer in handleRenderers)
            {
                handleRenderer.enabled = isEnable;
                handleCollider.enabled = isEnable;
            }
        }

        private void UpdateHandleRotation()
        {
            if (ProjectionBoard)
            {
                // Change handle orientation according to the ProjectionBoard orientation
                Quaternion targetRotation = Quaternion.FromToRotation(pin.transform.up, -ProjectionBoard.transform.forward);
                pin.transform.rotation = targetRotation * pin.transform.rotation;
            }
        }

        void ChangeHandleColor(Color color)
        {
            if (isHandleColorSet) return;
            isHandleColorSet = true;
            handleColor = color;
            handleColor.a = 0.6f;
        }

        public void ApplyHandleColor()
        {
            foreach (var handleRenderer in handleRenderers)
            {
                handleRenderer.material.color = handleColor;
            }
        }

        void RefreshHandle()
        {
            if (DrawIsFinished)
            {
                ApplyHandleColor();
                UpdateHandleRotation();
                DisplayHandle(true);
            }
        }

        #endregion

        List<float> waitingPointsReceptionTime = new List<float>();
        List<DrawPoint> waitingDrawPoints = new List<DrawPoint>();
        #region Parsing of DrawPoint and actual drawing
        public void DrawPointAdded(DrawPoint newPoint)
        {
            // If the drawer does not exists anymore, no need to sync with it (it is an old draw)
            bool drawImmediatly = Object.HasStateAuthority || DrawingDrawer == null;
            if (drawImmediatly)
            {
                DrawPointAddedImmediatly(newPoint);
            }
            else
            {
                waitingDrawPoints.Add(newPoint);
                waitingPointsReceptionTime.Add(Time.realtimeSinceStartup);
            }
        }
        public void DrawPointAddedImmediatly(DrawPoint newPoint)
        {

            if (newPoint.drawPressure == NEW_LINE_PRESSURE)
            {
                if (currentLine)
                {
                    // We end the previous line, if any
                    currentLine = null;
                }
                currentLinecolor = new Color(newPoint.localPosition.x, newPoint.localPosition.y, newPoint.localPosition.z);
            }
            else
            {
                LocalAddDrawingPoint(newPoint);
            }
        }

        public void LocalAddDrawingPoint(DrawPoint newPoint)
        {
            if (currentLine == null)
            {
                CreateLine(currentLinecolor);
            }
            var drawingPosition = currentLine.transform.TransformPoint(newPoint.localPosition);
            var drawPRessure = newPoint.drawPressure;

            Vector3 localDrawingPosition = Vector3.zero;

            localDrawingPosition = currentLine.transform.InverseTransformPoint(drawingPosition);

            drawingPoints.Add(localDrawingPosition);
            drawingPressures.Add(drawPRessure);
            currentLine.positionCount = drawingPoints.Count;
            currentLine.SetPositions(drawingPoints.ToArray());
            if (drawingPathLength.Count > 0)
            {
                var lastLength = drawingPathLength[drawingPathLength.Count - 1];
                var total = lastLength + Vector3.Distance(lastPoint, localDrawingPosition);
                drawingPathLength.Add(total);
                AnimationCurve widthCurve = new AnimationCurve();
                int index = 0;
                foreach (var length in drawingPathLength)
                {
                    if (index < drawingPressures.Count)
                    {
                        widthCurve.AddKey(length / total, drawingPressures[index]); ;
                    }
                    index++;

                }
                currentLine.widthCurve = widthCurve;
            }
            else
            {
                drawingPathLength.Add(0);
            }
            lastPoint = localDrawingPosition;

            OnDrawChange();
        }

        void CreateLine(Color color)
        {
            drawingPoints.Clear();
            drawingPressures.Clear();
            drawingPathLength.Clear();
            currentLine = GameObject.Instantiate(linePrefab);

            // Store the line at the local (0,0,0) position of the draw object
            currentLine.transform.SetParent(transform);
            currentLine.transform.position = transform.position;
            currentLine.transform.rotation = transform.rotation;

            lines.Add(currentLine);
            LocalChangeLineColor(color);
        }

        void LocalChangeLineColor(Color color)
        {
            if (currentLine == null)
            {
                return;
            }

            Gradient gradient = new Gradient();
            GradientColorKey[] colorKeys = new GradientColorKey[2];
            colorKeys[0] = new GradientColorKey(color, 0f);
            colorKeys[1] = new GradientColorKey(color, 1f);
            gradient.colorKeys = colorKeys;
            currentLine.colorGradient = gradient;

            // The handle takes the pen color of the first line, the closest to the handle position)
            ChangeHandleColor(color);
        }
        #endregion

        #region Projection board notification 
        void OnDrawChange()
        {
            if (isBoardDrawing && ProjectionBoard != null)
            {
                ProjectionBoard.ActivateBoard();
                _cachedProjectionBoard = ProjectionBoard;
            }
        }
        #endregion

        #region Monobehaviour
        private void Awake()
        {
            if (handleCollider == null) handleCollider = GetComponentInChildren<Collider>();
            if (pin == null)
                Debug.LogError("pin is not set");
            DisplayHandle(false);
        }

        private void OnDestroy()
        {
            if (_cachedProjectionBoard)
            {
                _cachedProjectionBoard.ActivateBoard();
            }
        }
        #endregion

        #region NetworkBehaviour
        public override void Render()
        {
            base.Render();
            DrawWaitingPoints();
            
            if (isBoardDrawing)
            {
                Vector3 interpolatedPosition = transform.position;
                // If the drawing has been moved, we should restart recording boards
                if ((interpolatedPosition - lastRecordedPosition).sqrMagnitude > minMovementSqr)
                {
                    if(ProjectionBoard != null)
                    {
                        ProjectionBoard.ActivateBoard();
                        _cachedProjectionBoard = ProjectionBoard;
                    }
                    lastRecordedPosition = interpolatedPosition;
                }
            }

            if (previousProjectionBoard != ProjectionBoard)
            {
                previousProjectionBoard = ProjectionBoard;
                RefreshHandle();
            }

            //TODO Check if data are here before displaying handle
            if (previousDrawIsFinished != DrawIsFinished)
            {
                previousDrawIsFinished = DrawIsFinished;
                RefreshHandle();
            }
        }

        // The drawing is based on the extrapolated hand position: it is done in the "future" relatively to the actual pen position, received and interpolated on the remote
        // So we have to delay the drawing a bit to avoid a "future pguessing" effect
        void DrawWaitingPoints()
        {
            const float maxWaitingTime = 0.3f;
            const float drawingDistance = 0.01f;
            const float drawingProjectionDistance = 0.1f;
            const float drawingDistanceSqr = drawingDistance * drawingDistance;
            const float drawingProjectionDistanceSqr = drawingProjectionDistance * drawingProjectionDistance;

            while (waitingDrawPoints.Count > 0)
            {
                var firstPointTime = waitingPointsReceptionTime[0];
                var firstPoint = waitingDrawPoints[0];
                var delay = Time.realtimeSinceStartup - firstPointTime;
                bool shouldDrawPoint = false;
                var pointPosition = transform.TransformPoint(firstPoint.localPosition);
                if (firstPoint.drawPressure == NEW_LINE_PRESSURE)
                {
                    shouldDrawPoint = true;
                }
                else if (delay > maxWaitingTime)
                {
                    shouldDrawPoint = true;
                }
                else if (DrawingDrawer && (DrawingDrawer.tip.position - pointPosition).sqrMagnitude < drawingDistanceSqr)
                {
                    shouldDrawPoint = true;
                }
                else if (DrawingDrawer)
                {
                    var trajectory = DrawingDrawer.tip.position - lastDrawingTipPosition;
                    var fromLast = pointPosition - lastDrawingTipPosition;
                    var fromCurrent = pointPosition - DrawingDrawer.tip.position;
                    var d1 = Vector3.Dot(trajectory, fromLast);
                    var d2 = Vector3.Dot(trajectory, fromCurrent);
                    if (d1 >= 0 && d2 <= 0)
                    {
                        // The point is "between" the 2 tip positions. Check its projection in between
                        var projection = lastDrawingTipPosition + Vector3.Project(fromLast, trajectory);
                        if ((projection - pointPosition).sqrMagnitude < drawingProjectionDistanceSqr)
                        {
                            shouldDrawPoint = true;
                        }
                    }

                }

                if (shouldDrawPoint)
                {
                    DrawPointAddedImmediatly(firstPoint);
                    waitingDrawPoints.RemoveAt(0);
                    waitingPointsReceptionTime.RemoveAt(0);
                    lastDrawingTipPosition = pointPosition;

                    if (waitingDrawPoints.Count == 0)
                    {
                        // To apply colors properly if the draw is finished now (maybe RefreshHandle() was called before the line points were properly drawn)
                        RefreshHandle();
                    }
                }
                else
                {
                    // No point drawn and removed
                    break;
                }
            }

            if (DrawingDrawer && DrawingDrawer.tip)
                lastDrawingTipPosition = DrawingDrawer.tip.position;

        }
        #endregion
    }
}
