using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Containment
{
    public class Container : NetworkBehaviour, IContainer
    {
        bool waitingChildExitBeforeDeletion = false;

        #region IContainer
        public NetworkTRSP LocomotionRoot { get; set; } = null;

        public List<IContainable> StoredContainables { get; set; } = new List<IContainable>();
        public bool IsVerifiedThisAfterTick { get; set; } = false;
        public bool IsVerifiedThisRender { get; set; } = false;
        public bool IsConfirmed { get; set; } = false;
        #endregion

        List<Renderer> visualizationRenderers = new List<Renderer>();
        public string debugName = null;
        string baseName;
        float deletionTime = -1;


        public static int CreatedContainers = 0;

        int nonContainableChildren = 0;

        private void Awake()
        {
            baseName = name;
            LocomotionRoot = GetComponentInParent<NetworkTRSP>();

            foreach (Transform child in transform)
            {
                if (child.TryGetComponent<IContainable>(out var part) == false)
                {
                    nonContainableChildren++;
                    foreach (var r in child.GetComponentsInChildren<Renderer>())
                    {
                        visualizationRenderers.Add(r);
                    }
                }
            }
        }

        private void OnDestroy()
        {
            //Debug.LogError("Destroyed container ");
        }

        public override void Render()
        {
            base.Render();
            if (string.IsNullOrEmpty(debugName))
            {
                debugName = $"C-{Container.CreatedContainers}";
                Container.CreatedContainers = CreatedContainers + 1;
                name = $"[{debugName}] {baseName}";
            }
            if (nonContainableChildren == transform.childCount)
            {
                // Only container visuals remain - plan to delete it if not used
                PlanDeletion();
            }
            ConfirmAnyPendingDeletion();
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            CheckExitingChild();
            CheckPendingDeletion();
        }

        void CheckExitingChild()
        {
            if (waitingChildExitBeforeDeletion && Object.HasStateAuthority)
            {
                int storedContainable = 0;
                foreach (Transform child in transform)
                {
                    if (child.GetComponent<IContainable>() != null)
                    {
                        storedContainable++;
                    }
                }
                if (storedContainable == 0)
                {
                    //Debug.LogError("Destroy a container with child " + transform.childCount);
                    waitingChildExitBeforeDeletion = false;
                    PlanDeletion();
                }
                else
                {
                    //Debug.LogError("Postponing destruction until last structure part child is removed");
                }
            }
        }

        // Wait a few seconds to be sure remote players have the opportunity to unparent 
        void PlanDeletion()
        {
            if (deletionTime != -1) return;

            //Debug.LogError($"---- Plan deletion  {debugName} {transform.childCount}/{nonPartChildren}");
            foreach (var r in visualizationRenderers)
            {
                r.enabled = false;
            }
            deletionTime = Time.time + 3;
            name = $"(Pending deletion) [{debugName}] {baseName}";
        }

        void CancelDeletion()
        {
            //Debug.LogError($"---- Cancel deletion {debugName} {transform.childCount}/{nonPartChildren}");
            foreach (var r in visualizationRenderers)
            {
                r.enabled = true;
            }
            deletionTime = -1;
            name = $"[{debugName}] {baseName}";
        }

        void ConfirmAnyPendingDeletion()
        {
            if (deletionTime != -1 && nonContainableChildren != transform.childCount)
            {
                CancelDeletion();
            }
        }

        void CheckPendingDeletion()
        {
            ConfirmAnyPendingDeletion();
            if (deletionTime != -1 && deletionTime < Time.time && Object.HasStateAuthority)
            {
                deletionTime = -1;
                Runner.Despawn(Object);
            }
        }

        public void Delete()
        {
            //Debug.LogError($"Delete {debugName} {Object.Id}");
            waitingChildExitBeforeDeletion = true;
        }

        public void OnConfirmedStatusChecked(bool isConfirmed, bool duringAfterTick)
        {
            IsConfirmed = isConfirmed;

            if (duringAfterTick)
            {
                IsVerifiedThisAfterTick = true;
            }
            else
            {
                IsVerifiedThisRender = true;
            }
        }
    }
}
