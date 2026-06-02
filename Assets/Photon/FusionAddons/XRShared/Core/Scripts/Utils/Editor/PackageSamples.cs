#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.UI;
using UnityEngine;

namespace Fusion.XRShared.Tools
{
    public struct SampleInfo
    {
        public string packageDisplayName;
        public string sampleName;
        public string path;
        public VersionInfo version;
    }

    public struct VersionInfo : IComparable
    {
        public string version;
        public int major;
        public int minor;
        public int patch;
        public string versionDetails;

        public VersionInfo(string version)
        {
            this.version = version;
            major = 0;
            minor = 0;
            patch = 0;
            versionDetails = "";
            var detailsParts = version.Split("-");
            if (detailsParts.Length > 0)
            {
                var versionParts = detailsParts[0].Split(".");
                try
                {
                    if (versionParts.Length > 0)
                        major = Int32.Parse(versionParts[0]);
                    if (versionParts.Length > 1)
                        minor = Int32.Parse(versionParts[1]);
                    if (versionParts.Length > 2)
                        patch = Int32.Parse(versionParts[2]);
                }
                catch (Exception e)
                {
                    // Probably not an actual version number
                    throw e;
                }
            }

            if (detailsParts.Length > 1)
            {
                versionDetails = detailsParts[1];
            }
        }

        public override string ToString()
        {
            var detailStr = (versionDetails != "") ? "/" + versionDetails : "";
            return $"{version} ({major}/{minor}/{patch}{detailStr})";
        }

        public int CompareTo(object obj)
        {
            if (obj is VersionInfo other)
            {
                var majorCompare = major.CompareTo(other.major);
                if (majorCompare != 0) return majorCompare;
                var minorCompare = minor.CompareTo(other.minor);
                if (minorCompare != 0) return minorCompare;
                var patchCompare = patch.CompareTo(other.patch);
                if (patchCompare != 0) return patchCompare;
                var otherCompare = versionDetails.CompareTo(other.versionDetails);
                return otherCompare;
            }
            if (obj is int i)
            {
                var majorCompare = major.CompareTo(i);
                if (majorCompare != 0) return majorCompare;
                return (minor > 0 || patch > 0) ? 1 : 0;
            }
            throw new Exception("Comparing VersionInfo to something else");
        }
    }

    public struct PackageSamplesInfo
    {
        public string packageName;
        public Dictionary<VersionInfo, List<SampleInfo>> samplebyVersions;

        public PackageSamplesInfo(string packageName)
        {
            this.packageName = packageName;
            samplebyVersions = new Dictionary<VersionInfo, List<SampleInfo>>();
        }

        public VersionInfo? MaxVersion
        {
            get
            {
                VersionInfo? maxVersion = null;
                if (samplebyVersions.Count > 0)
                {
                    var versions = new List<VersionInfo>(samplebyVersions.Keys);
                    // Sort to have greatest version first
                    versions.Sort((v1, v2) => -1 * v1.CompareTo(v2));
                    maxVersion = versions[0];
                }
                return maxVersion;
            }
        }
    }

    public static class PackageSamples
    {
        const string samplesFolder = "Samples";

        public static Dictionary<string, PackageSamplesInfo> FindAvailableSamples()
        {
            Dictionary<string, PackageSamplesInfo> availableSamples = new Dictionary<string, PackageSamplesInfo>();
            var assetsRelativePath = Path.GetFileName(Application.dataPath);
            var samplesRelativePath = Path.Combine(assetsRelativePath, samplesFolder);
            if (Directory.Exists(samplesRelativePath) == false)
            {
                return availableSamples;
            }
            foreach (var packageSampleFolder in Directory.GetDirectories(samplesRelativePath))
            {
                var packageNameParts = packageSampleFolder.Split(Path.DirectorySeparatorChar);
                if (packageNameParts.Length > 0)
                {
                    var packageName = packageNameParts[packageNameParts.Length - 1];
                    foreach (var packageSampleVersionFolder in Directory.GetDirectories(packageSampleFolder))
                    {
                        var versionParts = packageSampleVersionFolder.Split(Path.DirectorySeparatorChar);
                        if (versionParts.Length > 0)
                        {
                            var version = versionParts[versionParts.Length - 1];
                            if (availableSamples.ContainsKey(packageName) == false)
                            {
                                availableSamples[packageName] = new PackageSamplesInfo(packageName);
                            }
                            try
                            {
                                var versionInfo = new VersionInfo(version);
                                List<SampleInfo> samples = new List<SampleInfo>();
                                foreach (var samplePath in Directory.GetDirectories(packageSampleVersionFolder))
                                {
                                    var sampleParts = samplePath.Split(Path.DirectorySeparatorChar);
                                    if (sampleParts.Length > 0)
                                    {
                                        string sampleName = sampleParts[sampleParts.Length - 1];
                                        var sample = new SampleInfo
                                        {
                                            sampleName = sampleName,
                                            version = versionInfo,
                                            packageDisplayName = packageSampleFolder,
                                            path = samplePath
                                        };
                                        samples.Add(sample);
                                    }

                                }
                                availableSamples[packageName].samplebyVersions.Add(versionInfo, samples);
                            }
                            catch
                            {
                                // Sample in an unexpected format (no version folder)
                                // Note: we could consider each folder as a seperate sample, and give a fake version - empty result for now for those samples
                            }

                        }
                    }
                }
            }

            return availableSamples;

        }

        public static (VersionInfo?, List<SampleInfo>) MaxPackageInstalledSample(string packageDisplayName)
        {
            var samples = FindAvailableSamples();
            if (samples.ContainsKey(packageDisplayName))
            {
                var maxVersion = samples[packageDisplayName].MaxVersion;
                if (maxVersion is VersionInfo version)
                {
                    return (version, samples[packageDisplayName].samplebyVersions[version]);
                }
            }
            return (null, null);
        }

        public enum SampleInstallationResult
        {
            SampleAlreadyAvailable,
            SampleInstallationAccepted,
            PackageUnavailable,
            SampleInstallationRefused,
            SampleInstallationFail,
        }

        public delegate void SampleInstallationResultCallback(SampleInstallationResult result);

        public static void SuggestToInstallSample(string packageId, string sampleName, string requesterName = "", string reason = "", bool forceCheck = false, bool requiredPackage = false, SampleInstallationResultCallback callback = null)
        {
            string searchKey = $"STIS_{packageId}{sampleName}";
            string resultKey = $"STIS_{packageId}{sampleName}_Path";
            bool alreadyChecked = SessionState.GetBool(searchKey, false);
            string previouslyFoundSamplePath = SessionState.GetString(resultKey, "");

            if (previouslyFoundSamplePath != "" && Directory.Exists(previouslyFoundSamplePath) == false)
            {
                SessionState.EraseString(resultKey);

                Debug.LogError($"[Sample dependencies check] {previouslyFoundSamplePath} has been deleted. We have to look for it again, to suggest installing it (once, for this Unity launch)");
                alreadyChecked = false;
            }

            if (forceCheck == false && alreadyChecked) return;

            string reasonStr = "";
            if (reason != "") reasonStr = "" + reason;

            PackagePresenceCheck.LookForPackage(packageId, (info) =>
            {
                // Store that the search occured
                SessionState.SetBool(searchKey, true);

                //Debug.LogError($"Info {packageId}: {info?.version}");
                if (info != null)
                {
                    (var maxVersion, var samples) = PackageSamples.MaxPackageInstalledSample(info.displayName);
                    bool installedRequired = false;
                    if (maxVersion is VersionInfo version)
                    {
                        bool sampleFound = false;
                        foreach (var sampleInfo in samples)
                        {
                            if (sampleInfo.sampleName == sampleName)
                            {
                                sampleFound = true;
                                SessionState.SetString(resultKey, sampleInfo.path);
                                if (callback != null) callback(SampleInstallationResult.SampleAlreadyAvailable);
                                break;
                            }
                        }
                        if (sampleFound == false)
                        {
                            Debug.LogError($"[{requesterName}] {packageId} does NOT contains required sample {sampleName} (or not in latest version)");
                            installedRequired = true;
                        }
                    }
                    else
                    {
                        Debug.LogError($"[{requesterName}] {packageId} no sample installed, does NOT contains required sample {sampleName}");
                        installedRequired = true;
                    }
                    if (installedRequired)
                    {
                        string requesterStr = "An add-on";
                        if (requesterName != "")
                        {
                            requesterStr = $"The {requesterName}";
                        }
                        ConfirmWindow.ShowConfirmation(
                            $"Install {info.displayName} required sample",
                            $"{requesterStr} requires the '{info.displayName}' package's '{sampleName}' to be installed{reasonStr}. \n\nDo you want to install it (recommended) ?",
                            confirmCallback: () => {
                                Debug.LogError("Installing...");
                                var availableSamples = Sample.FindByPackage(packageId, info.version);
                                Debug.LogError($"Sample => {availableSamples}");
                                if (availableSamples == null)
                                {
                                    if (callback != null) callback(SampleInstallationResult.SampleInstallationFail);
                                    throw new Exception("Available samples not found for " + packageId);
                                }
                                foreach (var availableSample in availableSamples)
                                {
                                    if (availableSample.displayName == sampleName)
                                    {
                                        availableSample.Import(Sample.ImportOptions.OverridePreviousImports);
                                        if (callback != null) callback(SampleInstallationResult.SampleInstallationAccepted);
                                    }
                                }
                            },
                            cancelCallback: () => {
                                Debug.LogError($"'{info.displayName}' package's '{sampleName}' not installed. Won't ask for this session again");
                                if (callback != null) callback(SampleInstallationResult.SampleInstallationRefused);
                            }
                        );
                    }
                }
                else
                {
                    if (requiredPackage)
                    {
                        Debug.LogError($"[{requesterName}] {packageId} not installed: the presence of its '{sampleName}' is required{reasonStr}.");
                    }
                    if (callback != null) callback(SampleInstallationResult.PackageUnavailable);
                }
            });
        }

    }
}
#endif