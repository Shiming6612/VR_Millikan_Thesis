#if UNITY_EDITOR
using Fusion.XRShared.Tools;
using UnityEditor;
using UnityEngine;

[InitializeOnLoad]
public static class XRITIntegrationRequiredSamplesCheck
{
    // Set it to true to always ignore this check
    const bool disableCheck = false;
    // Set it to true to ignore any previous attempt this session
    const bool forceCheck = false;

    const string xritPackageId = "com.unity.xr.interaction.toolkit";
    const string xritStarterAssetsSample = "Starter Assets";
    const string xritHandInteractionSample = "Hands Interaction Demo";
    const string xrHandsPackageId = "com.unity.xr.hands";
    const string xritHandVisualizerSample = "HandVisualizer";
    const string sampleInstallationReason = " (if you want to use the predefined prefabs, otherwise, remove the XRITIntegrationRequiredSamplesCheck script)";

    static XRITIntegrationRequiredSamplesCheck()
    {
#pragma warning disable CS0162 // Unreachable code
        if (disableCheck) return;
#pragma warning restore CS0162 // Unreachable code

        // We first try to install the first sample. If the callback does not warn us about XRIT not being install, we continue with the other one
        //  (we do this first check to avoid requesting XRHands sample installation, when xRIT is not installed, and XRHands is)
        PackageSamples.SuggestToInstallSample(xritPackageId, xritStarterAssetsSample, requesterName: "XRITIntegration add-on", reason: sampleInstallationReason, forceCheck: forceCheck, callback: ContinueOnXRITPackageAvailable);
    }

    static void ContinueOnXRITPackageAvailable(PackageSamples.SampleInstallationResult result)
    {
        if (result != PackageSamples.SampleInstallationResult.PackageUnavailable)
        {
            PackageSamples.SuggestToInstallSample(xritPackageId, xritHandInteractionSample, requesterName: "XRITIntegration add-on", reason: sampleInstallationReason, forceCheck: forceCheck);
            PackageSamples.SuggestToInstallSample(xrHandsPackageId, xritHandVisualizerSample, requesterName: "XRITIntegration add-on", reason: sampleInstallationReason, forceCheck: forceCheck);
        }
    }
}
#endif