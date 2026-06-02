using Fusion.XR.Shared.Base;
using Unity.XR.CoreUtils;
using UnityEditor;
using UnityEngine;

namespace Fusion.XR.Shared.Automatization.Rig
{
    [CustomEditor(typeof(HardwareRigAutomaticSetup))]

    public class HardwareRigAutomaticSetupEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            GUILayout.Space(20);

            HardwareRigAutomaticSetup hardwareRig = (HardwareRigAutomaticSetup)target;
            var xrOrigin = hardwareRig.GetComponent<XROrigin>();

            GUILayout.Label("Complete install", style: EditorStyles.largeLabel);
            if (GUILayout.Button($"Automatic rig setup"))
            {
                hardwareRig.AutomaticRigsetup();
            }

            GUILayout.Space(20);
            GUILayout.Label("Step by step install", style: EditorStyles.largeLabel);
            GUILayout.BeginVertical();
            GUILayout.Label("[Core XR rig]", style: EditorStyles.boldLabel);
            if (GUILayout.Button($"Add XROrigin")) hardwareRig.CheckXROrigin();
            if (GUILayout.Button($"Add cameraOffset")) hardwareRig.CheckCameraOffset(xrOrigin);
            if (GUILayout.Button($"Add camera")) hardwareRig.CheckCamera(xrOrigin);
            if (GUILayout.Button($"Add controllers")) hardwareRig.CheckControllers(xrOrigin);
            if (GUILayout.Button($"Add simulated hands for controllers")) hardwareRig.CheckSimulatedHands();
            if (GUILayout.Button($"Add XRHands hands")) hardwareRig.CheckXRHandsHands(xrOrigin);

            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Label("[HardwareRig components]", style: EditorStyles.boldLabel);
            if (GUILayout.Button($"Add hardware rig")) hardwareRig.CheckHardwareRig(xrOrigin);
            if (GUILayout.Button($"Add hardware headset")) hardwareRig.CheckHardwareHeadset();
            if (GUILayout.Button($"Add hardware controllers")) hardwareRig.CheckHardwareControllers();
            if (GUILayout.Button($"Add hardware hands")) hardwareRig.CheckXRHandsHardwareHand(xrOrigin);
            GUILayout.EndVertical();

            GUILayout.Space(10);
            GUILayout.BeginVertical();
            GUILayout.Label("[Interaction]", style: EditorStyles.boldLabel);
            if (GUILayout.Button($"Add grabbing")) hardwareRig.CheckGrabbing();
            if (GUILayout.Button($"Add locomotion")) hardwareRig.CheckLocomotion(xrOrigin);
            GUILayout.EndVertical();
        }
    }
}

