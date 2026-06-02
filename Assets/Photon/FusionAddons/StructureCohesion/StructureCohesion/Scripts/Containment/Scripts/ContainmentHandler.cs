using System.Collections.Generic;
using UnityEngine;

namespace Fusion.Addons.Containment
{
    public interface IContainmentHandler
    {
        IContainer CurrentContainer { get; }
        IContainer ConfirmedContainer { get; set;  }
        void OnContainablePairsChanged();
#pragma warning disable IDE1006 // Naming Styles
        public Transform transform { get; }
#pragma warning restore IDE1006 // Naming Styles
    }

    public interface IContainable
    {
        // If true, will get higher priority to be the pair from which parenting is organized. also, if several pairs are leader, not doing parenting yet (should not be possible)
        bool IsContainmentLeader { get; }
        bool ShouldBecontained { get; }
        IEnumerable<IContainable> ContainmentPairs { get; }
        int ContainmentPairsCount { get; }
        void OnContainmentChange(IContainer container);
        NetworkObject Object { get; }
        IContainmentHandler ContainmentHandler { get; }
#pragma warning disable IDE1006 // Naming Styles
        Transform transform { get; }
#pragma warning restore IDE1006 // Naming Styles
        // Identify a group of pair (tested with ==), to avoid mixing two group of pairs with a different PairReference
        object PairsReference { get; }
    }

    public interface IContainer
    {
#pragma warning disable IDE1006 // Naming Styles
        Transform transform { get; }
#pragma warning restore IDE1006 // Naming Styles
        NetworkObject Object { get; }
        NetworkTRSP LocomotionRoot { get; }
        // Keep track of stored containable to avoid mixing two group of pairs with a different PairReference
        List<IContainable> StoredContainables { get; }

        // Cache to limit checks: store on the Container if, and when, it has been considered as a confirmed container for all its store containable
        void OnConfirmedStatusChecked(bool isConfirmed, bool duringAfterTick);
        // Cache to limit checks: the container was already verified once during this AfterTick phase
        bool IsVerifiedThisAfterTick { get; set; }
        // Cache to limit checks: the container was already verified once during this Render phase
        bool IsVerifiedThisRender { get; set; }
        // Cache to limit checks: what was the result of the confirmation check already done in this phase
        bool IsConfirmed { get; set; }

    }

    [DefaultExecutionOrder(ContainmentHandler.EXECUTION_ORDER)]
    public class ContainmentHandler : NetworkBehaviour, IAfterTick, IContainmentHandler
    {
        const int EXECUTION_ORDER = 10_000;

        IContainable containable;
        public IContainer ConfirmedContainer { get; set; } = null;
        IContainer selectedCandidateContainer;

        List<IContainer> candidateContainers = new List<IContainer>();

        [SerializeField]
        [Tooltip("Spawned container when a container is needed. Must contain a component implementing IContainer")]
        NetworkObject containerPrefab;

        NetworkObject ContainerPrefab => containerPrefab != null ? containerPrefab : ContainmentManager.SharedInstance?.containerPrefab;

        IContainer lastStoredInContainer = null;
        IContainer _currentContainer;
        public IContainer CurrentContainer
        {
            get
            {                
                if ((Object)_currentContainer == null || _currentContainer.transform != transform.parent)
                {
                    if (transform.parent)
                    {
                        _currentContainer = transform.parent.GetComponent<IContainer>();
                    }
                }
                return _currentContainer;
            }
        }

        int minPairsCountForContainment = 2;

        bool DeepDebug => ContainmentManager.SharedInstance?.deepDebug ?? false;

        private void Awake()
        {
            containable = GetComponent<IContainable>();
        }

        public void OnContainablePairsChanged()
        {
            if (DeepDebug) Debug.LogError($"[{Runner.Tick}] OnContainablePairsChanged");
            ConfirmedContainer = null;
            selectedCandidateContainer = null;
        }

        public override void FixedUpdateNetwork()
        {
            base.FixedUpdateNetwork();
            if (ConfirmedContainer != null)
            {
                // We note that the container has note been verified in the next AfterTick (to do it once for all the pairs)
                ConfirmedContainer.IsVerifiedThisAfterTick = false;
            }
        }

        private void LateUpdate()
        {
            if (ConfirmedContainer != null)
            {
                // We note that the container has note been verified in the next Render (to do it once for all the pairs)
                ConfirmedContainer.IsVerifiedThisRender = false;
            }
        }

        #region IAfterTick
        public void AfterTick()
        {
            if (ContainmentManager.SharedInstance && ContainmentManager.SharedInstance.disableContainerParenting)
            {
                return;
            }
            CheckContainment(out var candidateContainerCreationRequired, duringAfterTick: true);
            if (candidateContainerCreationRequired)
            {
                if (ContainerPrefab)
                {
                    var container = Runner.Spawn(ContainerPrefab, transform.position);
                    ConfirmedContainer = null;
                    selectedCandidateContainer = container.GetComponentInChildren<IContainer>();
                }
                if ((Object)selectedCandidateContainer == null || ContainerPrefab == null)
                {
                    throw new System.Exception("[ContainMentHandler error] containerPrefab does not contain a IContainer component");
                }
            }

            CheckParenting();
        }
        #endregion

        public override void Render()
        {
            base.Render();
            if (ContainmentManager.SharedInstance && ContainmentManager.SharedInstance.disableContainerParenting)
            {
                return;
            }
            UpdateCurrentContainerContainables();
            if (Object.HasStateAuthority == false)
            {
                // AfterTick won't be called on proxies: to ensure that ConfirmedContainer is updated even on proxies, we update containment in Render for them 
                CheckContainment(out var _, duringAfterTick: false);
            }
        }
       

        void UpdateCurrentContainerContainables()
        {
            var currentContainer = CurrentContainer;
            if ((Object)currentContainer != null)
            {
                if (currentContainer != lastStoredInContainer && (Object)lastStoredInContainer != null)
                {
                    lastStoredInContainer.StoredContainables.Remove(containable);
                }
                if (currentContainer.StoredContainables.Contains(containable) == false)
                {
                    currentContainer.StoredContainables.Add(containable);
                }
            }
            else if ((Object)lastStoredInContainer != null)
            {
                lastStoredInContainer.StoredContainables.Remove(containable);
            }
        }

        void OnContainmentChange()
        {
            containable.OnContainmentChange(CurrentContainer);
        }

        // Update ConfirmedContainer and / or selectedCandidateContainer to match Containable pairs needs
        void CheckContainment(out bool candidateContainerCreationRequired, bool duringAfterTick)
        {
            candidateContainerCreationRequired = false;
            if (containable.ShouldBecontained)
            {
                if ((Object)ConfirmedContainer != null)
                {
                    var isVerified = duringAfterTick ? ConfirmedContainer.IsVerifiedThisAfterTick : ConfirmedContainer.IsVerifiedThisRender;
                    if (isVerified)
                    {
                        // Another pair already checked it
                        if(ConfirmedContainer.IsConfirmed == false)
                        {
                            ConfirmedContainer = null;
                        }
                    }
                    else
                    {
                        // First pair to run it
                        var checkContainer = ConfirmedContainer;
                        CheckConfirmedContainerValidity();

                        checkContainer.OnConfirmedStatusChecked(isConfirmed: ConfirmedContainer != null, duringAfterTick);
                    }
                }

                if ((Object)ConfirmedContainer == null)
                {
                    LookForConfirmedContainer(out candidateContainerCreationRequired, duringAfterTick);
                }
            }
            else
            {
                ConfirmedContainer = null;
                selectedCandidateContainer = null;
            }
        }

        void LookForConfirmedContainer(out bool candidateContainerCreationRequired, bool duringAfterTick)
        {
            candidateContainerCreationRequired = false;
            // Try to find candidate containers
            candidateContainers.Clear();
            IContainer firstCandidate = null;
            IContainer leaderCandidate = null;
            bool severalCandidateLeader = false;
            bool sameParentForAllPairs = true;

            // Check if all pairs "agreed" on a common container
            foreach (var part in containable.ContainmentPairs)
            {
                //if (DeepDebug) Debug.LogError($"[{name}] Pair {part} / parent: {part.ContainmentHandler.transform.parent}");
                if (part.ContainmentHandler.transform.parent != null)
                {
                    var candidate = part.ContainmentHandler.transform.parent.GetComponent<IContainer>();

                    // Check if pair container contains also a containable that should not be a pair (so the container could not be trusted)
                    if (candidate != null && IsContainerStoringNonPairsContainable(candidate) == false)
                    {
                        // The container only contains pairs. Might be a good reliable candidate
                        if ((Object)firstCandidate == null)
                        {
                            firstCandidate = candidate;
                        }
                        else if (candidate != firstCandidate && part.Object.HasStateAuthority == false)
                        {
                            // A pair, on which we don't have control (so we can't change it during this frame) is in another container that the first candidate container found
                            //  So it means that we have multiple candidate containers: the choice is not clear
                            sameParentForAllPairs = false;
                        }
                        candidateContainers.Add(candidate);
                        if (part.IsContainmentLeader)
                        {
                            if ((Object)leaderCandidate == null)
                            {
                                leaderCandidate = candidate;
                            }
                            else
                            {
                                severalCandidateLeader = true;
                            }
                        }
                    }
                    else if (part.Object.HasStateAuthority == false)
                    {
                        sameParentForAllPairs = false;
                    }
                }
            }
            if (firstCandidate != null && sameParentForAllPairs)
            {
                // All pairs have the same container (except the ones owned)
                if (DeepDebug) Debug.LogError($"----  [{name}] Container found {firstCandidate}");
                ConfirmedContainer = firstCandidate;

                // To avoid pairs spending time finding the now found confirmed container, we warn them directly
                ConfirmedContainer.OnConfirmedStatusChecked(isConfirmed: ConfirmedContainer != null, duringAfterTick);
                foreach (var pair in containable.ContainmentPairs)
                {
                    pair.ContainmentHandler.ConfirmedContainer = ConfirmedContainer;
                }
            }
            else if (severalCandidateLeader)
            {
                // Do nothing: we wait for a stabilized status (they can't stay in the same structure)
                if (DeepDebug) Debug.LogError("----  severalCandidateLeader");
            }
            else if (leaderCandidate != null)
            {
                if (DeepDebug) Debug.LogError("---- leaderCandidate" + leaderCandidate);
                selectedCandidateContainer = leaderCandidate;
            }
            else if (candidateContainers.Count > 0)
            {
                // We select here the candidate with the lowest object id
                uint lowestObjectId = int.MaxValue;
                foreach (var candidate in candidateContainers)
                {
                    if (candidate.Object.Id.Raw < lowestObjectId)
                    {
                        selectedCandidateContainer = candidate;
                        lowestObjectId = candidate.Object.Id.Raw;
                    }
                }
                if (DeepDebug) Debug.LogError("---- selecting candidate with lower object id" + selectedCandidateContainer);
            }
            else
            {
                if (DeepDebug) Debug.LogError($"---- [{Runner.Tick}]-[{name}] No candidates (sameParentForAllPairs: {sameParentForAllPairs})");
                uint lowestObjectId = int.MaxValue;
                IContainable spawningContainable = null;
                // One pair will spawn a container to act as a candidate for every pairs. We select the containable with the lowest id to ensure only once spawns the container
                foreach (var containable in containable.ContainmentPairs)
                {
                    if (containable.Object.Id.Raw < lowestObjectId)
                    {
                        spawningContainable = containable;
                        lowestObjectId = containable.Object.Id.Raw;
                    }
                }
                if (spawningContainable.Object.HasStateAuthority && spawningContainable == containable)
                {
                    if (DeepDebug) Debug.LogError($"---- Creating a potential cnadidate container from Spawn part: {spawningContainable}");
                    // We do not trust (yet) the temporary container we will spawn
                    ConfirmedContainer = null;
                    selectedCandidateContainer = null;

                    candidateContainerCreationRequired = true;
                }
            }
        }

        bool IsContainerStoringNonPairsContainable(IContainer container)
        {
            if ((Object)container == null) return false;
            var pairsRef = containable.PairsReference;
            foreach (var c in container.StoredContainables)
            {
                if (c.PairsReference != pairsRef)
                {
                    return true;
                }
            }
            return false;
        }

        void CheckConfirmedContainerValidity()
        {
            // Check if a pair containable did not changed its container
            bool containerTrusted = true;
            foreach (var part in containable.ContainmentPairs)
            {
                var partCurrentContainer = part.ContainmentHandler.CurrentContainer;
                if (partCurrentContainer != null && partCurrentContainer != ConfirmedContainer)
                {
                    containerTrusted = false;
                    if (DeepDebug) Debug.LogError($"---- Different parents {part.transform.parent} / {ConfirmedContainer.transform}: stop trusting the container");
                }
            }

            // Number of pairs high enough ?
            if (containerTrusted && containable.ContainmentPairsCount < minPairsCountForContainment)
            {
                if (DeepDebug) Debug.LogError("---- [Error] Not enough parts: stop trusting the container. The structure should not exist");
                containerTrusted = false;
            }

            if(containerTrusted && IsContainerStoringNonPairsContainable(ConfirmedContainer) == true)
            {
                if (DeepDebug) Debug.LogError("---- The container also contains non pairs: stop trusting it ");
                containerTrusted = false;
            }

            if (containerTrusted == false)
            {
                ConfirmedContainer = null;
            }
        }

        bool TryChangeParent(Transform parent)
        {
            if (transform.parent != parent && Object.HasStateAuthority)
            {
                if (DeepDebug) Debug.LogError($"[{Runner.Tick}]-[{name}] Parent {name} to {parent}");
                transform.parent = parent;
                OnContainmentChange();
                return true;
            }
            return false;
        }

        void CheckParenting() {
            if (ConfirmedContainer != null)
            {
                // Confirmed container is set, so we trust it to be the container for this containable (and pairs)
                TryChangeParent(ConfirmedContainer.transform);
            }
            else if (selectedCandidateContainer != null)
            {
                TryChangeParent(selectedCandidateContainer.transform);
            }
            else
            {
                TryChangeParent(null);
            }
        }
    }
}
