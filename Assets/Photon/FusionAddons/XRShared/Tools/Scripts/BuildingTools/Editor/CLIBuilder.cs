using Fusion.Photon.Realtime;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using UnityEditor;
using UnityEditor.PackageManager.UI;
using UnityEngine;
using static PlasticGui.LaunchDiffParameters;

namespace Fusion.XRShared.BuildTools
{
    /// <summary>
    /// Allows to build from the command line, with options to change app id, built scenes, or to add extra defines for this build.
    /// 
    /// Options:
    /// - `-buildTarget <Platform (Android, Win64, Win, ...)>` : define target platform       
    /// - `-extraScriptingDefines<extraScriptingDefines>` : additional scripting define symbols, separated by a ',' without spaces
    /// - `-forceScenes<scenes>` : replace scenes to build, described by a string contained in their path, separated by a ',' without spaces
    /// - `-outputPath<targetBuildPath>` : output path(project folder name will be added to it)
    /// - `-addTimestampToVersion true` :  add timestamp to app version
    /// - `-fusionAppId<FusionAppIdOverride> `: force Fusion app id
    /// - `-voiceAppid<VoiceAppIdOverride>` : force Photon voice app id
    /// /// - `-appNameSuffix<suffix>` :add suffix to productName(for alternative debug build purposes)
    /// - `-appIdentifierSuffix<suffix>` : add suffix to applicationIdentifier(for alternative debug build purposes)
    /// 
    /// Example command:
    /// 
    /// ```
    /// ./Unity.exe -projectPath C:\Dev\fusion-project -quit -batchmode -logfile C:\Dev\Build\fusion-project\build.log -executeMethod Fusion.XRShared.BuildTools.CLIBuilder.Build -outputPath C:\Dev\Build\fusion-project -buildTarget Android -extraScriptingDefines EXTRA_DEFINE, EG_CLI_BUILD, CLI_BUILD -addTimestampToVersion false
    /// ```
    /// </summary>
    public static class CLIBuilder
    {
        public struct BuildParams
        {
            public string outputPath;
            public BuildTarget buildTarget;
            // Scripting symbols,separated by ','</param>
            public string extraScriptingDefines;
            public bool addTimestampToVersion;
            public string voiceAppid;
            public string fusionAppId;
            public string forceScenes;
            public string appNameSuffix;
            public string appIdentifierSuffix;
        }

        public static void Build()
        {
            BuildParams buildParams = default;

            var args = System.Environment.GetCommandLineArgs();
            string previousParam = "";
            for (int i = 0; i < args.Length; i++)
            {
                if (previousParam == "-outputPath")
                {
                    buildParams.outputPath = args[i];
                }
                if (previousParam == "-voiceAppid")
                {
                    buildParams.voiceAppid = args[i];
                }
                if (previousParam == "-fusionAppId")
                {
                    buildParams.fusionAppId = args[i];
                }
                if (previousParam == "-appNameSuffix")
                {
                    buildParams.appNameSuffix = args[i];
                }
                if (previousParam == "-appIdentifierSuffix")
                {
                    buildParams.appIdentifierSuffix = args[i];
                }
                if (previousParam == "-buildTarget")
                {
                    if (args[i].ContainsInvariantCultureIgnoreCase("win64"))
                    {
                        buildParams.buildTarget = BuildTarget.StandaloneWindows64;
                    }
                    else if (args[i].ContainsInvariantCultureIgnoreCase("win"))
                    {
                        buildParams.buildTarget = BuildTarget.StandaloneWindows;
                    }
#if UNITY_2022_1_OR_NEWER
                    else if (args[i].ContainsInvariantCultureIgnoreCase("visionos"))
                    {
                        buildParams.buildTarget = BuildTarget.VisionOS;
                    }
#endif
                    else
                    {
                        Enum.TryParse(args[i], out buildParams.buildTarget);
                    }
                }
                if (previousParam == "-extraScriptingDefines")
                {
                    buildParams.extraScriptingDefines = args[i];
                }
                if (previousParam == "-forceScenes")
                {
                    buildParams.forceScenes = args[i];
                }
                if (previousParam == "-addTimestampToVersion")
                {
                    buildParams.addTimestampToVersion = args[i].ContainsInvariantCultureIgnoreCase("true");
                }
                previousParam = args[i];
            }

            bool shouldRestoreVersion = false;
            bool shouldRestoreFusionAppId = false;
            bool shouldRestoreVoiceAppid = false;
            var originalVersion = PlayerSettings.bundleVersion;
            string originalFusionAppId = PhotonAppSettings.Global.AppSettings.AppIdFusion;
            string originalVoiceAppId = PhotonAppSettings.Global.AppSettings.AppIdVoice;
            string originalAppId = PlayerSettings.applicationIdentifier;
            string originalAppName = PlayerSettings.productName;

            if (string.IsNullOrEmpty(buildParams.fusionAppId) == false)
            {
                PhotonAppSettings.Global.AppSettings.AppIdFusion = buildParams.fusionAppId;
                shouldRestoreFusionAppId = true;
            }
            if (string.IsNullOrEmpty(buildParams.voiceAppid) == false)
            {
                PhotonAppSettings.Global.AppSettings.AppIdVoice = buildParams.voiceAppid;
                shouldRestoreVoiceAppid = true;
            }
            if (string.IsNullOrEmpty(buildParams.appNameSuffix) == false)
            {
                PlayerSettings.productName += buildParams.appNameSuffix;
            }
            if (string.IsNullOrEmpty(buildParams.appIdentifierSuffix) == false)
            {
                PlayerSettings.applicationIdentifier += buildParams.appIdentifierSuffix;
            }

            Debug.Log($"Build: {buildParams.outputPath} {buildParams.buildTarget} {buildParams.extraScriptingDefines} ");

            List<string> scenes = new List<string>();

            //string fileName = Path.GetFileName(Path.GetDirectoryName(Application.dataPath));
            string fileName = PlayerSettings.productName.Replace(" ", "_") + "--" + PlayerSettings.applicationIdentifier;
            string extension = "apk";
            if (buildParams.buildTarget == BuildTarget.StandaloneWindows || buildParams.buildTarget == BuildTarget.StandaloneWindows64)
            {
                extension = "exe";
            }

            string fileNameSuffix = "";
            if (shouldRestoreFusionAppId || shouldRestoreVoiceAppid)
            {
                fileNameSuffix = "-AppIdModified";
            }

            List<string> forceScenesKeywords = new List<string>();
            bool shouldOverrideScenes = string.IsNullOrEmpty(buildParams.forceScenes) == false;
            if (shouldOverrideScenes)
            {
                Debug.Log("[CLIBuilder] Overriding scenes to build. Selected scenes:");
                forceScenesKeywords.AddRange(buildParams.forceScenes.Split(','));
            }
            bool isFirstSceneSuffixAdded = false;
            foreach (var s in EditorBuildSettings.scenes)
            {
                bool shoudBeBuilt = s.enabled;
                if (shouldOverrideScenes)
                {
                    shoudBeBuilt = false;
                    foreach (var keyword in forceScenesKeywords)
                    {
                        if (s.path.ContainsInvariantCultureIgnoreCase(keyword))
                        {
                            Debug.Log(" - "+s.path);
                            shoudBeBuilt = true;
                            break;
                        }
                    }
                }
                if (shoudBeBuilt)
                {
                    if (isFirstSceneSuffixAdded == false)
                    {
                        fileNameSuffix += "--" + Path.GetFileName(s.path).Replace(" ", "_").Replace(".unity", "");
                        isFirstSceneSuffixAdded = true;
                    }
                    scenes.Add(s.path);
                }
            }

            try
            {
                var timestamp = System.DateTime.UtcNow.ToString("yyyyMMddHHmm");
                var appName = $"{fileName}{fileNameSuffix}-{PlayerSettings.bundleVersion}-{timestamp}.{extension}";
                Debug.Log($"[CLIBuilder] All scenes to build: \"{String.Join("\",\"", scenes)}\". App: {appName}");

                var locationPathName = $"{buildParams.outputPath}\\{appName}";

                if (buildParams.addTimestampToVersion)
                {
                    PlayerSettings.bundleVersion += "." + timestamp;
                    shouldRestoreVersion = true;
                    locationPathName = $"{buildParams.outputPath}\\{fileName}{fileNameSuffix}-{PlayerSettings.bundleVersion}.{extension}";
                }

                var options = new BuildPlayerOptions
                {
                    scenes = scenes.ToArray(),
                    target = buildParams.buildTarget,
                    locationPathName = locationPathName,
                };
                Debug.Log("[CLIBuilder] Target path: " + options.locationPathName);

                if (string.IsNullOrEmpty(buildParams.extraScriptingDefines) == false)
                {
                    options.extraScriptingDefines = buildParams.extraScriptingDefines.Split(',');
                }

                BuildPipeline.BuildPlayer(options);
            }
            catch (Exception e)
            {
                throw e;
            }
            finally
            {
                if (shouldRestoreVersion)
                {
                    PlayerSettings.bundleVersion = originalVersion;
                    AssetDatabase.SaveAssets();
                }
                if (shouldRestoreFusionAppId)
                {
                    PhotonAppSettings.Global.AppSettings.AppIdFusion = originalFusionAppId;
                    AssetDatabase.SaveAssets();
                }
                if (shouldRestoreVoiceAppid)
                {
                    PhotonAppSettings.Global.AppSettings.AppIdVoice = originalVoiceAppId;
                    AssetDatabase.SaveAssets();
                }
                if (PlayerSettings.applicationIdentifier != originalAppId)
                {
                    PlayerSettings.applicationIdentifier = originalAppId;
                    AssetDatabase.SaveAssets();
                }
                if (PlayerSettings.productName != originalAppName)
                {
                    PlayerSettings.productName = originalAppName;
                    AssetDatabase.SaveAssets();
                }
            }
        }
    }
}
