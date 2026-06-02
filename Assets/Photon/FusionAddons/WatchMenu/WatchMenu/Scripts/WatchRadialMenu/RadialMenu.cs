using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.WatchMenu
{
    /// <summary>
    /// This class spawns the buttons and shows or hides them depending on the user’s gaze, using the `OpenRadialMenu()` and `CloseRadialMenu()` functions called by `WatchAim`.
    /// The button list can be edited in the Unity Editor to add or remove watch menu buttons.
    /// The positions of the buttons are calculated automatically in order to place them around the watch.
    /// </summary>

    public class RadialMenu : MonoBehaviour
    {
        [SerializeField] List<GameObject> buttonPrefabList = new List<GameObject>();
        [SerializeField] Canvas canvas;
        [SerializeField] float distanceFromCenter = 60;
        [SerializeField] float animationDuration = 0.25f;
        [SerializeField] public float delayBetweenButtonAnimation = 0.15f;
        [SerializeField] float menuActionBounceProtection = 1f;
        [SerializeField] bool animateMenu = true;


        [Header("Set automatically")]
        [SerializeField] WatchWindowsHandler watchMenuHandler;
        public bool menuIsDisplayed = false;

        List<RadialMenuButtonAction> radialMenuButtonList = new List<RadialMenuButtonAction>();

        float angleBetweenButtons = 0;
        private int numberOfButtons = 0;
        float lastMenuActionTime = -1;
        bool isAntibounceEnabled = false;       // for edge case (switching from hardwarerig to networkrig with headset looking to the watch at start)

        private void Awake()
        {
            if (watchMenuHandler == null)
            {
                watchMenuHandler = GetComponentInParent<WatchWindowsHandler>();
            }
        }

        private void Start()
        {
            if (canvas == null)
                canvas = GetComponentInChildren<Canvas>();
            if (canvas == null)
                Debug.LogError("Canvas not defined");

            numberOfButtons = buttonPrefabList.Count;

            if (numberOfButtons > 0)
            {
                angleBetweenButtons = Mathf.PI / numberOfButtons;
            }
            SpawnButtons();
        }

        private void SpawnButtons()
        {
            for (int i = 0; i < buttonPrefabList.Count; i++)
            {
                GameObject button = Instantiate(buttonPrefabList[i], canvas.transform);
                RadialMenuButtonAction radialMenuButton = button.GetComponent<RadialMenuButtonAction>();
                radialMenuButtonList.Add(radialMenuButton);
                radialMenuButton.transform.localScale = Vector3.zero;
                radialMenuButton.transform.localPosition = Vector3.zero;

                RadialMenuButtonWindows settingsAction = button.GetComponentInChildren<RadialMenuButtonWindows>();
                if (settingsAction)
                {
                    settingsAction.watchWindowsHandler = watchMenuHandler;
                }
            }
        }

        [ContextMenu("OpenRadialMenu")]
        public void OpenRadialMenu()
        {
            if (menuIsDisplayed) return;

            if (lastMenuActionTime + menuActionBounceProtection > Time.time) return;

            int nbOfButtonOpenned = 0;
            for (int i = 0; i < radialMenuButtonList.Count; i++)
            {
                if (radialMenuButtonList[i].shouldBeDisplayed == true)
                {
                    Vector3 buttonTargetPosition = new Vector3(-distanceFromCenter * Mathf.Sin(angleBetweenButtons * nbOfButtonOpenned), distanceFromCenter * Mathf.Cos(angleBetweenButtons * nbOfButtonOpenned), 0);
                    float timing = nbOfButtonOpenned * delayBetweenButtonAnimation;
                    OpenButton(i, buttonTargetPosition, timing);
                    nbOfButtonOpenned++;
                }
            }
            menuIsDisplayed = true;
            lastMenuActionTime = Time.time;
        }

        [ContextMenu("CloseRadialMenu")]
        public void CloseRadialMenu()
        {
            if (menuIsDisplayed == false) return;
            
            if (isAntibounceEnabled && lastMenuActionTime + menuActionBounceProtection > Time.time) return;

            int nbOfButtonClosed = 0;

            for (int i = 0; i < radialMenuButtonList.Count; i++)
            {
                if(radialMenuButtonList[i].gameObject.activeInHierarchy == true)
                {
                    Vector3 buttonTargetPosition = Vector3.zero;
                    float timing = nbOfButtonClosed * delayBetweenButtonAnimation;
                    CloseButton(i, buttonTargetPosition, timing);
                    nbOfButtonClosed++;
                }
            }
            menuIsDisplayed = false;
            lastMenuActionTime = Time.time;
            if (isAntibounceEnabled == false) isAntibounceEnabled = true;
        }

        private void OnDisable()
        {
            StopAllCoroutines();
            for (int i = 0; i < radialMenuButtonList.Count; i++)
            {
                radialMenuButtonList[i].gameObject.SetActive(false);
            }
        }

        void OpenButton(int buttonIndex, Vector3 buttonTargetPosition, float timing)
        {
            if (animateMenu)
            {
                StartCoroutine(ButtonOpenAnimation(radialMenuButtonList[buttonIndex].transform, buttonTargetPosition, timing));
            }
            else
            {
                var rect = radialMenuButtonList[buttonIndex].transform;
                rect.localScale = Vector3.one;
                rect.localPosition = buttonTargetPosition;
                rect.gameObject.SetActive(true);
            }
        }

        void CloseButton(int buttonIndex, Vector3 buttonTargetPosition, float timing)
        {
            if (animateMenu)
            {
                StartCoroutine(ButtonCloseAnimation(radialMenuButtonList[buttonIndex].transform, buttonTargetPosition, timing));
            }
            else
            {
                var rect = radialMenuButtonList[buttonIndex].transform;
                rect.localScale = Vector3.zero;
                rect.localPosition = buttonTargetPosition;
                rect.gameObject.SetActive(false);
            }
        }

        IEnumerator ButtonOpenAnimation(Transform transformToAnimate, Vector3 targetPosition, float delay)
        {
            yield return new WaitForSeconds(delay);

            transformToAnimate.gameObject.SetActive(true);

            float currentTime = 0f;
            Vector3 startPos = Vector3.zero;

            while (currentTime < animationDuration)
            {
                currentTime += Time.deltaTime;
                float progress = currentTime / animationDuration;
                float step = Mathf.SmoothStep(0f, 1f, progress);
                transformToAnimate.localScale = Vector3.one * step;
                transformToAnimate.localPosition = Vector3.Lerp(startPos, targetPosition, step);
                yield return null;
            }
            transformToAnimate.localScale = Vector3.one;
            transformToAnimate.localPosition = targetPosition;
        }

        IEnumerator ButtonCloseAnimation(Transform transformToAnimate, Vector3 targetPosition, float delay)
        {
            yield return new WaitForSeconds(delay);

            float currentTime = 0f;
            Vector3 startPos = transformToAnimate.localPosition;

            while (currentTime < animationDuration)
            {
                currentTime += Time.deltaTime;
                float progress = currentTime / animationDuration;
                float step = Mathf.SmoothStep(1f, 0f, progress);
                transformToAnimate.localScale = Vector3.one * step;
                transformToAnimate.localPosition = Vector3.Lerp(startPos, targetPosition, 1 - step);

                yield return null;
            }
            transformToAnimate.localScale = Vector3.zero;
            transformToAnimate.localPosition = targetPosition;
            transformToAnimate.gameObject.SetActive(false);
        }
    }
}
