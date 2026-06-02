using Fusion.Addons.DesktopFocusAddon;
using Fusion.XR.Shared;
using Fusion.XR.Shared.Core;
using Fusion.XR.Shared.Grabbing;
using Fusion.XR.Shared.Rig;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.InteractiveMenuAddon
{
    /***
     * 
     * InteractiveMenu is in charge to display a menu that follows the target object.
     * It checks if there is an object between the user and the interactive menu and finds an unobstructed position.
     * The menu is automatically oriented towards the player's camera. 
     * A curve line is displayed between the object and the menu. It is possible to define the start & end point of the curve thanks to offset parameters.
     * The menu can be displayed a limited number of times and for a limited time.
     * 
     ***/

    [DefaultExecutionOrder(InteractiveMenu.EXECUTION_ORDER)]
    public class InteractiveMenu : NetworkBehaviour
    {
        public const int EXECUTION_ORDER = INetworkGrabbable.EXECUTION_ORDER + 10;
        public GameObject interactiveMenuGO;
        [SerializeField] GameObject curvePrefab;
        [SerializeField] GameObject targetObject;

        [Header("Display parameters")]
        [SerializeField] private bool alwaysDisplayInteractiveMenu = false;
        [SerializeField] int numberOfDisplayOfInteractiveMenu = 3;
        [SerializeField] float menuDisplayDurationWhenGrabbed = 3f;
        [SerializeField] float menuDisplayDurationWhenUnGrabbed = 0.2f;
        [Tooltip("Control the elasticity of the curve")]
        [SerializeField] float elasticSpeed = 0.02f;
        [SerializeField] int numCurvePoints = 20; // Number of points to define the curve


        [Header("Offet settings")]
        [Tooltip("Define the position of the interactive menu")]
        [SerializeField] Vector3 defaultMenuOffset = new Vector3(0f, 0.3f, -0.2f);
        [Tooltip("Offset to control the interactive menu position when focus mode is enabled")]
        public Vector3 interactiveMenuOffsetInFocusMode = new Vector3(0.1f, 0.3f, 0.2f);
        [Tooltip("Offset of the first point of the curve")]
        public Vector3 startPositionOffset = new Vector3(0, -0.12f, 0);
        [Tooltip("Offset of the last point of the curve")]
        public Vector3 endPositionOffset = new Vector3(0, -0.03f, 0);
        [Tooltip("Offset to control the bezier curve near the menu")]
        [SerializeField] Vector3 curveMenuOffset = new Vector3(0.0f, -0.3f, 0.0f);
        [Tooltip("Offset to control the bezier curve near the object")]
        [SerializeField] Vector3 curveObjectOffset = new Vector3(0, -0.2f, 0);

        [Header("Obstacle settings")]
        public LayerMask obstacleLayer;
        [SerializeField] float offsetDistanceWhenMenuTouchAnObject = 0.15f;

        [Header("Automatically set")]
        [SerializeField] private IGrabbable grabbable;
        public GameObject curveGO;
        [SerializeField] private RigInfo rigInfo;
        [SerializeField] private IHardwareRig rig;

        private LineRenderer lineRenderer;
        List<Vector3> curvePoints = new List<Vector3>(); // List to store the points of the curve
        bool isInteractiveMenuDisplayed = false;
        bool requestToHideInteractiveMenuInProgress = false;
        bool objectHasBeenUngrabbed = false;
        public bool IsGrabbed => grabbable.IsGrabbed;
        private Coroutine hideMenuCoroutine;
        DesktopFocusManager focusManager;
        private bool isMenuEnable = true;


        private void Awake()
        {
            grabbable = GetComponent<IGrabbable>();
            SetRigInfo();


            if (!interactiveMenuGO)
                Debug.LogError("Interactive Menu Game Object is not set");
            else
            {   // change the parenting to get the elastic effect & instantiate the curve 
                interactiveMenuGO.transform.SetParent(null);
                curveGO = Instantiate(curvePrefab, transform.position, Quaternion.identity);
                lineRenderer = curveGO.GetComponent<LineRenderer>();
                HideMenu();
            }
            if (focusManager == null) focusManager = FindAnyObjectByType<DesktopFocusManager>(FindObjectsInactive.Include);
        }

        public async void SetRigInfo()
        {
            while (!rigInfo)
            {
                rigInfo = FindAnyObjectByType<RigInfo>();
                await AsyncTask.Delay(100);
            }
            rig = rigInfo.localHardwareRig;
        }


        Vector3 menuOffset;


        void ConfigureOffset()
        {
            // Check is focus mode is enable
            if (focusManager && (focusManager.currentFocus != null))
            {
                // Compute menuOffset position according to the focus plan position
                var focusPlanPosition = focusManager.currentFocus.CameraAnchor.InverseTransformPoint(targetObject.transform.position);

                if (focusPlanPosition.x < 0)
                    menuOffset.x = focusPlanPosition.x + interactiveMenuOffsetInFocusMode.x;
                if (focusPlanPosition.x > 0)
                    menuOffset.x = focusPlanPosition.x - interactiveMenuOffsetInFocusMode.x;

                if (focusPlanPosition.y < 0)
                    menuOffset.y = 0.2f * focusPlanPosition.y + interactiveMenuOffsetInFocusMode.y;
                if (focusPlanPosition.y > 0)
                    menuOffset.y = 0.2f * focusPlanPosition.y - interactiveMenuOffsetInFocusMode.y;

                menuOffset.z = interactiveMenuOffsetInFocusMode.z;
            }
            else
            {
                // Restore defaultMenuOffset start value
                menuOffset = defaultMenuOffset;
            }

        }

        void ConfigureRotation()
        {
            // Check is focus mode is enable
            if (focusManager && (focusManager.currentFocus != null))
            {
                // Change InteractiveMenu orientation according to the Camera anchor orientation
                interactiveMenuGO.transform.rotation = focusManager.currentFocus.CameraAnchor.transform.rotation * Quaternion.Euler(0f, 180f, 0f);
            }
            else
            {
                // look toward the local player when focus mode is disable
                interactiveMenuGO.transform.LookAt(rig.Headset.transform);
            }

        }

        public override void Render()
        {
            if (interactiveMenuGO && isInteractiveMenuDisplayed && IsGrabbed)
            {
                 // follow the object
                Vector3 targetPosition = FindTargetPosition();
                interactiveMenuGO.transform.position = Vector3.Lerp(interactiveMenuGO.transform.position, targetPosition, elasticSpeed);

                // Update the Line Renderer positions to create a curve
                UpdateCurvePoints();
                lineRenderer.positionCount = curvePoints.Count;
                lineRenderer.SetPositions(curvePoints.ToArray());
            }
        }


        private Vector3 FindTargetPosition()
        {
            ConfigureOffset();
            ConfigureRotation();
            Vector3 targetPosition = targetObject.transform.position + interactiveMenuGO.transform.TransformVector(menuOffset);
            targetPosition = CheckTargetMenuPosition(targetPosition);
            return targetPosition;
        }

        private Vector3 CheckTargetMenuPosition(Vector3 targetPosition)
        {
            // raycast to check if there is an object between the user and the interactive menu
            RaycastHit hit;
            if (rig != null && rig.Headset != null)
            {
                Vector3 raycastOrigin = rig.Headset.transform.position;
                Vector3 direction = targetPosition - raycastOrigin;

                if (UnityEngine.Physics.Raycast(raycastOrigin, direction, out hit, direction.magnitude + offsetDistanceWhenMenuTouchAnObject, obstacleLayer))
                {
                    // Menu is obstructed, find an unobstructed position
                    // Calculate the new position with an offset
                    Vector3 offsetDirection = (raycastOrigin - hit.point);
                    targetPosition = hit.point + (offsetDirection * offsetDistanceWhenMenuTouchAnObject);
                }
            }

            return targetPosition;
        }


        public void LateUpdate()
        {
            // Hide the menu when object is ungrabbed
            if (!IsGrabbed && isInteractiveMenuDisplayed && !requestToHideInteractiveMenuInProgress)
            {
                requestToHideInteractiveMenuInProgress = true;
                if (hideMenuCoroutine != null)
                {
                    StopCoroutine(hideMenuCoroutine);
                }
                StartCoroutine(HideMenuAfterDelay(menuDisplayDurationWhenUnGrabbed));
            }

            // memorize if object has been ungrabbed to not call display menu again & exit
            if (!IsGrabbed || !isMenuEnable)
            {
                objectHasBeenUngrabbed = true;
                return;
            }

            // here the object is grabbed & menu is allowed to be displayed

            // Show the menu if object is grabbed and max number of display is not reached.
            if (!isInteractiveMenuDisplayed && numberOfDisplayOfInteractiveMenu > 0 && objectHasBeenUngrabbed && hideMenuCoroutine == null)
            {
                objectHasBeenUngrabbed = false;
                requestToHideInteractiveMenuInProgress = true;
                ShowMenu();
                hideMenuCoroutine = StartCoroutine(HideMenuAfterDelay(menuDisplayDurationWhenGrabbed));
            }
        }

        IEnumerator HideMenuAfterDelay(float displayDuration)
        {
            yield return new WaitForSeconds(displayDuration);
            HideMenu();
            hideMenuCoroutine = null; // Reset the coroutine reference

        }

        private void HideMenu()
        {
            interactiveMenuGO.SetActive(false);
            lineRenderer.enabled = false;
            isInteractiveMenuDisplayed = false;
        }

        private void ShowMenu()
        {
            interactiveMenuGO.transform.LookAt(rig.Headset.transform);

            Vector3 targetPosition = FindTargetPosition();
            interactiveMenuGO.transform.position = targetPosition;
            interactiveMenuGO.SetActive(true);
            lineRenderer.enabled = true;
            isInteractiveMenuDisplayed = true;
            if (!alwaysDisplayInteractiveMenu)
                numberOfDisplayOfInteractiveMenu--;
            requestToHideInteractiveMenuInProgress = false;
        }

        public void EnableInteractiveMenu(bool isEnable)
        {
            isMenuEnable = isEnable;
        }

        void UpdateCurvePoints()
        {
            curvePoints.Clear();

            Vector3 startPos = interactiveMenuGO.transform.TransformPoint(startPositionOffset);
            Vector3 position2 = interactiveMenuGO.transform.TransformPoint(curveMenuOffset);

            Vector3 curveObjectOffsetRescaled = Vector3.Scale(curveObjectOffset, new Vector3(1f / targetObject.transform.lossyScale.x, 1f / targetObject.transform.lossyScale.y, 1f / targetObject.transform.lossyScale.z));
            Vector3 position3 = targetObject.transform.TransformPoint(curveObjectOffsetRescaled);
            Vector3 endPositionOffsetRescaled = Vector3.Scale(endPositionOffset, new Vector3(1f / targetObject.transform.lossyScale.x, 1f / targetObject.transform.lossyScale.y, 1f / targetObject.transform.lossyScale.z));
            Vector3 endPos = targetObject.transform.TransformPoint(endPositionOffsetRescaled);


            curvePoints.Add(startPos);

            for (int i = 1; i < numCurvePoints - 1; i++)
            {
                float t = i / (float)(numCurvePoints - 1);
                Vector3 point = Bezier(startPos, position2, position3, endPos, t);
                curvePoints.Add(point);
            }

            curvePoints.Add(endPos);
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
