using UnityEngine;
using UnityEngine.InputSystem;
using Fusion.Addons.BlockingContact;
using Fusion.XR.Shared.Core;

namespace Fusion.Addons.Drawing
{
    /**
     * Pen-like drawer that generate 3D line renderer in space
     * Create drawPrefab objects containing Draw when the drawer is grabbed, and add point to the Draw when the associated input action is pressed
     *  
     * If a projection board is set, the proximity with the board is used instead of the input action to trigger point creation
     */
    [DefaultExecutionOrder(Drawer.EXECUTION_ORDER)]
    public class Drawer : NetworkBehaviour
    {
        const int EXECUTION_ORDER = INetworkHand.EXECUTION_ORDER + 10;
        public Transform tip;
        public Draw drawPrefab;
        public Draw currentDraw = null;

        public INetworkGrabbable grabbable;
        public InputActionProperty leftUseAction;
        public InputActionProperty rightUseAction;

        public Color color;

        [Header("Handle")]
        public float drawingHandleOffsetPosition = 0.2f;

        InputActionProperty UseAction => grabbable != null && grabbable.IsGrabbed && grabbable.CurrentGrabber.RigPart != null && grabbable.CurrentGrabberSide() == RigPartSide.Left ? leftUseAction : rightUseAction;
        public virtual bool IsGrabbed => grabbable != null && grabbable.IsGrabbed;
        public virtual bool IsGrabbedByLocalPLayer => (forceUse && Object &&  Object.HasStateAuthority) || (IsGrabbed && grabbable != null && grabbable.CurrentGrabber.RigPart != null && grabbable.CurrentGrabber.Object.StateAuthority == Runner.LocalPlayer);

        public IFeedbackHandler feedback;
        public string audioType = "3Dpen";

        public bool forceUse = false;

        public bool Is2DPen => drawPrefab.isBoardDrawing;
        public virtual bool IsUsed
        {
            get
            {
                if (forceUse) return true;
                if (Is2DPen)
                {
                    if(projectionBoard == null)
                    {
                        return false;
                    }
                    var coordinate = projectionBoard.transform.InverseTransformPoint(tip.position);
                    bool isUsed = true;

                    // We check if the pentip is on the board in the referential of the projectionboard
                    
                    if (coordinate.z > 0)
                    {
                        isUsed = isUsed && coordinate.z < projectionBoardPositiveProximityThresholds.z;
                    }
                    else
                    {
                        isUsed = isUsed && coordinate.z >= projectionBoardNegativeProximityThresholds.z;
                    }

                    if (coordinate.x > 0)
                    {
                        isUsed = isUsed && coordinate.x < projectionBoardPositiveProximityThresholds.x;
                    }
                    else
                    {
                        isUsed = isUsed && coordinate.x >= projectionBoardNegativeProximityThresholds.x;
                    }

                    if (coordinate.y > 0)
                    {
                        isUsed = isUsed && coordinate.y < projectionBoardPositiveProximityThresholds.y;
                    }
                    else
                    {
                        isUsed = isUsed && coordinate.y >= projectionBoardNegativeProximityThresholds.y;
                    }

                    return isUsed;
                }
                else
                {
                    return UseAction.action.ReadValue<float>() > minAction;

                }
            }
        }

        public float Pressure
        {
            get
            {
                if (forceUse)
                {
                    return 1;
                }
                if (Is2DPen)
                {
                    var coordinate = projectionBoard.transform.InverseTransformPoint(tip.position).z;
                    return coordinate > 0 ? 1f : Mathf.Abs((projectionBoardNegativeProximityThresholds.z - coordinate) / projectionBoardNegativeProximityThresholds.z);
                }
                else
                {
                    return UseAction.action.ReadValue<float>();

                }
            }
        }

        [Header("Draw point detection")]
        public float drawingMinResolution = 0.001f;
        float minAction = 0.05f;
        float minDrawnPressure = 0.2f;//TODO fix pressure code (create glitchy artefact on render texture)
        Vector3 lastDrawingPosition;

        [Header("Projection board")]
        public LayerMask detectableProjectionBoardLayer;
        public string additionalProjectionBoardLayerName;

        public Board projectionBoard;
        private Vector3 projectionBoardPositiveProximityThresholds = new Vector3 (0.5f, 0.5f, 0.045f);
        private Vector3 projectionBoardNegativeProximityThresholds = new Vector3 (-0.5f, -0.5f, -0.005f);

        public enum Status
        {
            NotGrabbed,
            DrawingPaused,
            Drawing
        }

        public Status status = Status.NotGrabbed;


        private void Awake()
        {            
            grabbable = GetComponent<INetworkGrabbable>();
            feedback = GetComponent<IFeedbackHandler>();

            leftUseAction.action.Enable();
            rightUseAction.action.Enable();

            var blockableTip = GetComponent<BlockableTip>();
            if(blockableTip) blockableTip.audioType = audioType;

            if (string.IsNullOrEmpty(additionalProjectionBoardLayerName) == false)
            {
                int layer = LayerMask.NameToLayer(additionalProjectionBoardLayerName);
                if (layer == -1)
                {
                    Debug.LogError($"Please add a {additionalProjectionBoardLayerName} layer. Required by {gameObject.name}");
                }
                else
                {
                    detectableProjectionBoardLayer |= (1 << layer);
                }
            }
            if (detectableProjectionBoardLayer == default)
            {
                Debug.LogError("Drawer need a ProjectionBoardLayer configured");
                detectableProjectionBoardLayer = ~0;
            }


        }


        public void LateUpdate()
        {
            if (!IsGrabbedByLocalPLayer)
            {
                return;
            }

            if (IsUsed && status == Status.Drawing)
            {
                OnDrawing();
            }
        }

        public override void FixedUpdateNetwork()
        {
            if (CheckUngrab())
            {
                return;
            }
            CheckUseChange();
        }

        void CheckUseChange()
        {
            if (IsUsed)
            {
                if (status != Status.Drawing)
                {
                    status = Status.Drawing;
                    ResumeDrawing();
                }
            }
            else
            {
                if (status == Status.Drawing)
                {
                    status = Status.DrawingPaused;
                    PauseDrawing();
                }
            }
        }

        bool CheckUngrab()
        {
            if (!IsGrabbedByLocalPLayer)
            {
                if (status == Status.Drawing)
                {
                    PauseDrawing();
                }
                if (status != Status.NotGrabbed)
                {
                    FinishDraw();                 
                    status = Status.NotGrabbed;
                }
                return true;
            }
            return false;
        }

        public override void Render()
        {
            base.Render();
            CheckUngrab();
        }

        void CreateDraw(Vector3 position, Quaternion rotation)
        {
            if (!IsGrabbedByLocalPLayer)
            {
                Debug.LogError("Draw should be spawned by drawer");
                return;
            }
            currentDraw = Object.Runner.Spawn(drawPrefab, position, rotation, Runner.LocalPlayer);
            currentDraw.StartDraw(color, projectionBoard);
            currentDraw.DrawingDrawer = this;
        }

        void OnDrawing()
        {
            float drawPressure = minDrawnPressure + (1f - minDrawnPressure) * Mathf.Clamp01(Pressure);
            Vector3 drawingPosition = tip.position;
            if (Vector3.Distance(lastDrawingPosition, drawingPosition) > drawingMinResolution)
            {
                lastDrawingPosition = drawingPosition;
                if (currentDraw)
                {
                    currentDraw.AddDrawingPoint(drawingPosition, drawPressure);                    
                }

                if (feedback != null)
                    feedback.PlayAudioAndHapticFeedback(audioType);

            }
        }

        // Finish current line; the full drawing is not yet finishes (still grabbing the pen)
        public void PauseDrawing()
        {
            if (currentDraw != null)
            {
                currentDraw.PauseDrawing();
            }
            if (feedback != null)
                feedback.PauseAudioFeedback();
        }

        // Start a line (and a full draw if not yet started)
        public void ResumeDrawing()
        {
            if (currentDraw == null)
            {
                CreateDraw(tip.position + drawingHandleOffsetPosition * Vector3.up, Quaternion.identity);
            }
            if (currentDraw) currentDraw.ResumeDrawing(color);

            if (feedback != null)
                feedback.PlayAudioAndHapticFeedback(audioType);
        }

        void FinishDraw()
        {
            currentDraw.FinishDraw();
            // Destroy the drawing if no actual drawing point has been set (quick press/release of the use button)
            if (currentDraw != null && currentDraw.lines.Count == 0)
            {
                Object.Runner.Despawn(currentDraw.Object);
            }
            currentDraw = null;
            
            if (feedback != null)
                feedback.StopAudioFeedback();
        }

        private void OnTriggerEnter(Collider other)
        {
            bool objectInBoardLayerMask = detectableProjectionBoardLayer == (detectableProjectionBoardLayer | (1 << other.gameObject.layer));
            if (objectInBoardLayerMask)
            {
                if (other.gameObject != projectionBoard)
                {
                    var board = other.gameObject.GetComponentInParent<Board>();
                    if (board)
                    {
                        var projectionBoardChange = projectionBoard != board;

                        //Debug.LogError("Changing board to " + other.gameObject);
                        projectionBoard = board;
                        if(currentDraw != null)
                        {
                            if(currentDraw.ProjectionBoard == null && projectionBoardChange == false)
                            {
                                //Debug.LogError("DRAW: Changing board to " + other.gameObject);
                                currentDraw.ProjectionBoard = board;
                            } else
                            {
                                //Debug.LogError("DRAW: board already set to " + other.gameObject);
                                if (projectionBoardChange && currentDraw.ProjectionBoard != board)
                                {
                                    //Debug.LogError("DRAW: stopping current draw on " + currentDraw.ProjectionBoard + " as pen in moving to "+ board);
                                    FinishDraw();
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
