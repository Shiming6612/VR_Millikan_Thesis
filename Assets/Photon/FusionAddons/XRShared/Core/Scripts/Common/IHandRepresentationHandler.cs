
using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    public interface IHandRepresentationHandler
    {
        public void RestoreHandInitialMaterial();
    }

    public interface IHandRepresentation
    {
        public GameObject gameObject { get; }
    }

    public interface IColoredHandRepresentation
    {
        public void SetHandColor(Color color);
        public void SetHandMaterial(Material material);
        public void DisplayMesh(bool shouldDisplay);
        public bool IsMeshDisplayed { get; }
    }

    // Structure representing the inputs driving a hand pose 
    [System.Serializable]
    public struct HandCommand : INetworkStruct
    {
        public float thumbTouchedCommand; // Normally 0 or 1
        public float indexTouchedCommand; // Normally 0 or 1
        public float gripCommand;
        public float triggerCommand;
        // Optionnal commands
        public int poseCommand;
        public float pinchCommand;// Can be computed from triggerCommand by default

        public static HandCommand Interpolate(HandCommand from, HandCommand to, float alpha)
        {
            HandCommand command = default;
            command.thumbTouchedCommand = (alpha < 0.5) ? from.thumbTouchedCommand : to.thumbTouchedCommand;
            command.indexTouchedCommand = (alpha < 0.5) ? from.indexTouchedCommand : to.indexTouchedCommand;
            command.gripCommand = Mathf.Lerp(from.gripCommand, to.gripCommand, alpha);
            command.triggerCommand = Mathf.Lerp(from.triggerCommand, to.triggerCommand, alpha);
            return command;
        }
    }

    [System.Serializable]
    public struct CompressedHandCommand : INetworkStruct
    {
        // TODO merge thumbTouchedCommand and indexTouchedCommand as there are normally just 0 or 1 (devices where it is not true ? )
        public byte thumbTouchedCommand;
        public byte indexTouchedCommand;
        public byte gripCommand;
        public byte triggerCommand;

        public static CompressedHandCommand FromHandCommand(HandCommand command)
        {
            CompressedHandCommand compressedCommand = default;
            compressedCommand. thumbTouchedCommand = (byte)(command.thumbTouchedCommand * 255);
            compressedCommand.indexTouchedCommand = (byte)(command.indexTouchedCommand * 255);
            compressedCommand.gripCommand = (byte)(command.gripCommand * 255);
            compressedCommand.triggerCommand = (byte)(command.triggerCommand * 255);
            return compressedCommand;
        }

        public static HandCommand ToHandCommand(CompressedHandCommand compressedCommand)
        {
            HandCommand command = default;
            command.thumbTouchedCommand = (float)(compressedCommand.thumbTouchedCommand) / 255f;
            command.indexTouchedCommand = (float)(compressedCommand.indexTouchedCommand) / 255f;
            command.gripCommand = (float)(compressedCommand.gripCommand) / 255f;
            command.triggerCommand = (float)(compressedCommand.triggerCommand) / 255f;
            return command;
        }
    }


    public interface IHandCommandProvider
    {
        public HandCommand HandCommand { get; }
    }

    public interface IHandCommandHandler
    {
        public void SetHandCommand(HandCommand command);
    }


}

