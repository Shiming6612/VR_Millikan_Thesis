using System.Collections.Generic;
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    public class NetworkControllerCommand : NetworkBehaviour
    {
        [Networked]
        [SerializeField] CompressedHandCommand CompressedCommand { get; set; }

        INetworkController networkController;
        IHandCommandProvider localCommandProvider;

        [HideInInspector]
        public List<IHandCommandHandler> commandHandlers = new List<IHandCommandHandler>();

        [Header("Interpolation/extrapolation")]
        public bool extrapolateOnStateAuthorityDuringRender = true;

        protected virtual void Awake()
        {
            networkController = GetComponentInParent<INetworkController>();
            if (networkController == null) throw new System.Exception("Should be placed under a INetworkController hierarchy");
            commandHandlers = new List<IHandCommandHandler>(GetComponentsInChildren<IHandCommandHandler>());
        }

        public override void Render()
        {
            base.Render();

            if (Object.HasStateAuthority)
            {
                if (localCommandProvider == null && networkController != null && networkController.LocalHardwareRigPart != null)
                {
                    localCommandProvider = networkController.LocalHardwareRigPart.gameObject.GetComponentInChildren<IHandCommandProvider>(true);
                }
                if (extrapolateOnStateAuthorityDuringRender && localCommandProvider != null)
                {
                    var command = localCommandProvider.HandCommand;
                    StoreHandCommand(command);
                    ApplyHandComand(command);
                }
            }
            else
            {
                if(TryGetSnapshotsBuffers(out var from, out var to, out var alpha)){
                    var commandReader = GetPropertyReader<CompressedHandCommand>(nameof(CompressedCommand));
                    var fromCommand = CompressedHandCommand.ToHandCommand(commandReader.Read(from));
                    var toCommand = CompressedHandCommand.ToHandCommand(commandReader.Read(to));
                    var command = HandCommand.Interpolate(fromCommand, toCommand, alpha);
                    ApplyHandComand(command);
                }
            }
        }

        void StoreHandCommand(HandCommand command)
        {
            CompressedCommand = CompressedHandCommand.FromHandCommand(command);
        }

        void ApplyHandComand(HandCommand command)
        {
            foreach (var h in commandHandlers) h.SetHandCommand(command);
        }
    }

}
