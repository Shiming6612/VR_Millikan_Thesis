using Fusion.XR.Shared.Core;
using UnityEngine;

namespace Fusion.XRShared.Demo
{
    /**
     * Spawns a prefab when the spawnerGrabbableReference (a Grabbable - no need of a NetworkGrabble or any network object) is far enough
     * 
     * If the prefab contains a NetworkObject, uses the runner to spawn it properly.
     */

    public class GrabbablePrefabSpawner : MonoBehaviour
    {
        public GameObject prefab;
        [SerializeField]
        NetworkRunner runner;
        public IGrabbable spawnerGrabbableReference;
        public GameObject spawnerGrabbableReferenceGameObject;

        public bool searchGrabbableInChildrenOnly = false;

        [SerializeField]
        float liberationDistance = .1f;

        Pose defaultPosition;

        [Header("Feedback")]
        [SerializeField] IFeedbackHandler feedback;
        [SerializeField] string audioType;

        protected virtual void Awake()
        {
            if (spawnerGrabbableReference == null && spawnerGrabbableReferenceGameObject != null)
            {
                spawnerGrabbableReference = spawnerGrabbableReferenceGameObject.GetComponentInChildren<IGrabbable>();
            }
                
            if (spawnerGrabbableReference == null)
            {
                if(searchGrabbableInChildrenOnly == true)
                {
                    foreach (Transform child in transform)
                    {
                        spawnerGrabbableReference = child.GetComponentInChildren<IGrabbable>();
                        if (spawnerGrabbableReference != null)
                            break;
                    }
                }
                else
                {
                    spawnerGrabbableReference = GetComponentInChildren<IGrabbable>(true);
                }
            }

            if (spawnerGrabbableReference != null)
            {
                defaultPosition.position = spawnerGrabbableReference.transform.localPosition;
                defaultPosition.rotation = spawnerGrabbableReference.transform.localRotation;
                spawnerGrabbableReference.OnUngrab.AddListener(OnSpawnerUngrab);
            }
            else
            {
                Debug.LogError("A spawnerGrabbableObject, containing a Grabbable component, is required");
            }

            if (feedback == null)
                feedback = GetComponent<IFeedbackHandler>();
        }

        private void OnSpawnerUngrab()
        {
            if (Vector3.Distance(transform.position, spawnerGrabbableReference.transform.position) > liberationDistance)
            {
                Spawn();
            }
            ResetReferencePose();
        }

        protected virtual GameObject Spawn()
        {

            if (runner == null || runner.IsRunning == false)
            {
                // Try to find a runner
                runner = NetworkRunner.GetRunnerForGameObject(gameObject);

                if (runner == null || runner.IsRunning == false)
                {
                    if (runner == null)
                        Debug.LogError("Can not spawn object because the Runner is null");
                    else if (runner.IsRunning == false)
                        Debug.LogError("Can not spawn object because the Runner is not running");

                    return null;
                }
            }

                GameObject spawnedObject = null;

            if (runner == null || runner.IsRunning == false) return null;
            if (prefab.GetComponentInChildren<NetworkObject>())
            {
                var no = runner.Spawn(prefab, spawnerGrabbableReference.transform.position, spawnerGrabbableReference.transform.rotation);
                spawnedObject = no.gameObject;
            }
            else
            {
                spawnedObject = GameObject.Instantiate(prefab, spawnerGrabbableReference.transform.position, spawnerGrabbableReference.transform.rotation);
            }

            if (feedback != null && feedback.IsAudioFeedbackIsPlaying() == false)
            {
                feedback.PlayAudioFeedback(audioType);
            }
            return spawnedObject;
        }

        protected virtual void ResetReferencePose()
        {
            spawnerGrabbableReference.transform.localPosition = defaultPosition.position;
            spawnerGrabbableReference.transform.localRotation = defaultPosition.rotation;
        }
    }

}
