using Fusion.XR.Shared.Utils;
using System;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEngine;

namespace Fusion.XRShared.Tools
{
    [InitializeOnLoad]
    public class KnownAddonsWeaving
    {
        public const string usualAddonsRelativePath = "/Photon/FusionAddons";

        static KnownAddonsWeaving()
        {
            if (AddonWeaver.IsNetworkProjectConfigAvailable() == false)
            {
                // NetworkProjectConfig not yet available: probably first launch of the project
                return;
            }

            // Note: do not list here assembly not dependent of XRShared.Core. for those one, call directly AddonWeaver.AddAssemblyToWeaver in an Editor script in those addons
            string[] addonsAssembliesToWeave = new string[] {
                "BlockingContact",
                "TextureDrawing",
                "XRShared.Interaction.HardwareBasedGrabbing",
                "MXInkIntegration",
                "DataSyncHelpers.DataTools",
                "XRShared.Core.Tools",
                "TextureDrawing.Pen",
                "InteractiveMenu",
                "AudioRoom",
                "LocomotionValidation",
                "ChatBubble",
                "Magnets",
                "StructureCohesion",
                "MetaCoreIntegration",
                "Screensharing",
                "SocialDistancing",
                "Drawing",
                "UISynchronization",
                "VisionOSHelpers",
                "Anchors",
                "LineDrawing.XRShared",
                "MetaCoreIntegration.Grabbing",
                // ------ Suggested list by GeneralCheck --------
                "Anchors.MRUKQRCode",
                "Anchors.OpenCV",
                "ConnectionManager",
                //"DesktopFocus",
                //"ExtendedRigSelection",
                //"Feedback",
                "MXIntegration.Logitech",
                "PositionDebugging",
                "Screensharing.MetaWebcam",
                "Screensharing.uWindowsCapture",
                "Spaces",
                "StickyNotes",
                "StructureCohesion.HardwareBasedGrabbing",
                //"VirtualKeyboard",
                "VoiceHelpers",
                "VoiceHelpers.Tools",
                //"WatchMenu",
                "XRShared.DesktopSimulation",
                "XRShared.SimpleHands",
                "XRHandsSynchronization.Demo",
                "XRShared.Interaction.Beamer",
                "XRShared.Interaction.Locomotion",
                "XRShared.RemoteBasedGrabbing",
                "XRShared.Interaction.Touch",
                "XRShared.Interaction.UI",
                "XRShared.Interaction.PhysicsGrabbing",
                "XRShared.AutomaticSetup",
                // ------ End of suggested list by GeneralCheck --------
            };
            foreach (var assemblyName in addonsAssembliesToWeave)
            {
                WeaveIfAssemblyIsAvailable(assemblyName);
            }
            GeneralCheck();
        }

        public static void GeneralCheck()
        {
            var path = Application.dataPath + usualAddonsRelativePath;
            int assembliesNotWeaved = 0;
            int assembliesNotUnsafe = 0;
            string notWeavedAssembliesDescription = "Fusion addon's folder assemblies not weaved:\n";
            string notUnsafeAssembliesDescription = "";
            string[] assembliesSubstringToIgnore = new string[] {
                "Editor",
                "DesktopFocus",
                "ExtendedRigSelection",
                "Feedback",
                "PositionDebugging",
                "VirtualKeyboard",
                "WatchMenu",
                "Fusion.Addons.Physics",
                "MetaCoreIntegration.CameraSample",
            };
            if (Directory.Exists(path))
            {
                foreach (var file in Directory.EnumerateFiles(path, "*.asmdef", SearchOption.AllDirectories))
                {
                    string assemblyName = Path.GetFileName(file).Replace(".asmdef", "");
                    bool isWeaved = AddonWeaver.IsAddonWeaved(assemblyName);
                    bool shouldIgnore = false;
                    foreach (var s in assembliesSubstringToIgnore)
                    {
                        if (assemblyName.Contains(s))
                        {
                            shouldIgnore = true;
                            break;
                        }
                    }
                    if (shouldIgnore)
                    {
                        continue;
                    }
                    bool isAssemblyPresent = AddonWeaver.CheckAssemblyPresence(assemblyName, out var assembly);
                    if (isAssemblyPresent == false)
                    {
                        // The file might be present, but the assembly load could be cancelled due to a missing dependancy define
                        continue;
                    }

                    // Based on Fusion.Unity.Editor
                    var assemblyInfo = JsonUtility.FromJson<AssemblyInfo>(File.ReadAllText(file));
                    if (assemblyInfo.allowUnsafeCode == false)
                    {
                        assembliesNotUnsafe++;
                        notUnsafeAssembliesDescription += $"                \"{assemblyName}\",\n";
                    }

                    if (isWeaved == false)
                    {
                        assembliesNotWeaved++;
                        notWeavedAssembliesDescription += $"                \"{assemblyName}\",\n";
                    }
                }
            }
            if (assembliesNotWeaved != 0)
            {
                Debug.LogError($"{assembliesNotWeaved} {notWeavedAssembliesDescription}");
            }
            if (assembliesNotUnsafe != 0)
            {
                Debug.LogError($"[Error] {assembliesNotUnsafe} Fusion addon's folder assemblies without 'Allow unsafe code' checked:\n{notUnsafeAssembliesDescription}");
            }
        }

        public static void WeaveIfAssemblyIsAvailable(string assemblyName)
        {
            bool isAssemblyPresent = AddonWeaver.CheckAssemblyPresence(assemblyName, out var assembly);
            if (isAssemblyPresent)
            {
                bool isWeaved = AddonWeaver.IsAddonWeaved(assemblyName);
                if (isWeaved == false)
                {
                    Debug.LogError($"{assemblyName} not yet added to assemblies to weave, adding it.");
                    AddonWeaver.AddAssemblyToWeaver(assemblyName);
                }
            }
        }

        // Based on Fusion.Unity.Editor
        [Serializable]
        private class AssemblyInfo
        {
            public string[] includePlatforms = Array.Empty<string>();
            public string name = string.Empty;
            public bool allowUnsafeCode;
        }
    }
}