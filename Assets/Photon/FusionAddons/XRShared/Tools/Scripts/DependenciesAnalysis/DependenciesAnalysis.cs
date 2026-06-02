using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>
/// Gen erate assembly dependencies diagram
/// result in PlantUML format
/// Can be used here: http://www.plantuml.com/plantuml/uml/
/// </summary>
[CreateAssetMenu(fileName = "DependenciesAnalysis", menuName = "Fusion Addons/XRSharedCore/DependenciesAnalysis")]
public class DependenciesAnalysis : ScriptableObject
{
#if UNITY_EDITOR
    public string basePath = "<Application.dataPath>/Photon/FusionAddons";
    [Tooltip("Check to start analysis")]
    public bool isRequestingAnalysis = false;


    [Header("Package")]
    public bool usePackage = true;
    public bool detectSubPackage = true;
    public string basePackageFolder = "<Application.dataPath>/Photon/FusionAddons";
    [Tooltip("Useful to avoid collision with assembly names")]
    public string packagePrefix = "AddOn";
    public bool usePackageLinks = false;
    public bool onlyDisplayPackages = false;
    [Tooltip("Only used if onlyDisplayPackages is true")]
    public bool displayPackagesAsClasses = false;

    [Header("Filters")]
    public List<string> filterTermsInAnalysis = new List<string> { "Legacy", "Automation", "XRShared.Core", "GesturesDetection", "HandRepresentation", "Editor", "Desktop" };
    public List<string> requiredTermInDependentAssemblies = new List<string> { };
    public List<string> requiredTermInDependenciesAssemblies = new List<string> { };
    public List<string> requiredTermInPackages = new List<string> { };
    public List<StringReplacement> replacementInNames = new List<StringReplacement> { 
        new StringReplacement { from = "AddOn.XRShared", to = "AddOn.XRShared", padSpaces = 342, exactMatch = true }
    };
    [Tooltip("Will filter assemblies with no dependencies. Will be also considered as true if requiredTermInDependenciesAssemblies count is greater than 0")]
    public bool onlyDisplayAssemblyWithLinks = false;

    [Header("Settings")]
    public string header = "@startuml\n"
            + "hide circle\n"
            + "hide empty methods\n"
            + "hide empty members\n"
            + "skinparam linetype ortho\n";
    public string footer = "@enduml\n";

    [Header("Results")]
    public string analysisResult = "";

    public List<Assembly> assemblies = new List<Assembly>();
    private void OnEnable()
    {
        Analyse();
    }
    private void OnValidate()
    {
        Analyse();
    }

    
    [System.Serializable]
    public struct StringReplacement
    {
        public string from;
        public string to;
        public int padSpaces;
        public bool exactMatch;
    }

    public class Assembly {
        public string name;
        public string package;
        [HideInInspector]
        public string guid;
        [HideInInspector]
        public List<string> dependenciesGuids = new List<string>();
        public List<Assembly> dependencies = new List<Assembly>();

        public Assembly(string name)
        {
            this.name = name;
            dependencies = new List<Assembly>();
            dependenciesGuids = new List<string>();
            package = "";
            guid = null;
        }

        public string EscapedName()
        {
            return Assembly.EscapedName(this.name);
        }

        public static string DisplayName(string s, List<StringReplacement> replacements = null, string prefix = null)
        {
            var result = s;
            if (string.IsNullOrEmpty(prefix) == false)
            {
                result = $"{prefix}.{result}";
            }
            if (replacements != null)
            {
                foreach (var r in replacements)
                {
                    if (r.exactMatch && result != r.from)
                    {
                        continue;
                    }
                    result = result.Replace(r.from, r.to);
                    if (result.Contains(r.from) && r.padSpaces > 0)
                    {
                        var leftPad = r.padSpaces / 2;
                        result = result.PadLeft(leftPad, ' ');
                        result = result.PadRight(r.padSpaces, ' ');
                    }
                }
            }
            return result;
        }

        public static string EscapedName(string s)
        {
            var result = s.Replace(".", "_");
            return result;
        }
    }

    [System.Serializable]
    public struct AssemblyFile
    {
        public List<string> references;
    }

    public bool FilterPackageBasedOnTheirDependencies => onlyDisplayAssemblyWithLinks || requiredTermInDependenciesAssemblies.Count > 0;

    void Analyse() {
        if (isRequestingAnalysis == false) return;
        isRequestingAnalysis = false;

        analysisResult = "";
        var path = basePath.Replace("<Application.dataPath>", Application.dataPath);
        var packageFolder = basePackageFolder.Replace("<Application.dataPath>", Application.dataPath);
        Debug.Log("Starting dependencies analysis at path "+path);
        Dictionary<string, string> guidByAssemblyName = new Dictionary<string, string>();
        Dictionary<string, Assembly> assembliesByGuid = new Dictionary<string, Assembly>();
        Dictionary<string, Assembly> assembliesByFileName = new Dictionary<string, Assembly>();

        if (Directory.Exists(path))
        {

            foreach (var file in Directory.EnumerateFiles(path, "*.asmdef*", SearchOption.AllDirectories))
            {
                string assemblyName = Path.GetFileName(file).Replace(".asmdef.meta", "").Replace(".asmdef", "");
                if (file.EndsWith(".meta"))
                {
                    string guid = null;
                    foreach (var line in File.ReadAllLines(file))
                    {
                        if (line.StartsWith("guid:"))
                        {
                            guid = line.Replace("guid:", "", System.StringComparison.OrdinalIgnoreCase).Trim();
                        }
                    }

                    if (guid != null)
                    {
                        guidByAssemblyName[assemblyName] = guid;
                    }
                }
                else
                {
                    var assembly = new Assembly(assemblyName);
                    if (string.IsNullOrEmpty(packageFolder) == false && file.StartsWith(packageFolder))
                    {
                        var package = file.Replace(packageFolder, "").TrimStart(Path.DirectorySeparatorChar);
                        if (detectSubPackage)
                        {
                            // Remove the assembly name
                            package = package.Replace(Path.GetFileName(file), "").Trim(Path.DirectorySeparatorChar);
                            // For Editor assemblies, they might be located deep under the Scripts folder. We trim this part
                            package = package.Split(new string[] { "Scripts" }, StringSplitOptions.None)[0].Trim(Path.DirectorySeparatorChar); ;
                            // Remove the last folder level (module, same name than the assembly)
                            var packageParts = package.Split(Path.DirectorySeparatorChar);
                            if (packageParts.Length > 1)
                            {
                                // Remove the last folder (module level)
                                package = "";
                                for (int i = 0; i < (packageParts.Length - 1); i++)
                                {
                                    if (package != "") package += Path.DirectorySeparatorChar;
                                    package += packageParts[i];
                                }
                            }
                            package = package.Replace(Path.GetFileName(file), "");
                        } else
                        {
                            // Just take the first folder
                            package = package.Split(Path.DirectorySeparatorChar)[0];
                        }

                        assembly.package = package;
                    }
                    var info = JsonUtility.FromJson<AssemblyFile>(File.ReadAllText(file));
                    foreach (var dependency in info.references)
                    {
                        var cleanedDependency = dependency.Trim().Replace("guid:", "", System.StringComparison.OrdinalIgnoreCase).Trim();
                        assembly.dependenciesGuids.Add(cleanedDependency);
                    }
                    assembliesByFileName[assemblyName] = assembly;
                }
            }

            foreach (var assemblyName in guidByAssemblyName.Keys)
            {
                var guid = guidByAssemblyName[assemblyName];
                foreach (var name in assembliesByFileName.Keys)
                {
                    if (name == assemblyName)
                    {
                        assembliesByFileName[name].guid = guid;
                        assembliesByGuid[guid] = assembliesByFileName[name];
                    }
                }
            }

            foreach (var guid in assembliesByGuid.Keys)
            {
                foreach (var depGuid in assembliesByGuid[guid].dependenciesGuids)
                {
                    if (assembliesByGuid.ContainsKey(depGuid))
                    {
                        assembliesByGuid[guid].dependencies.Add(assembliesByGuid[depGuid]);
                    }
                }
            }

            assemblies = new List<Assembly>(assembliesByGuid.Values);

            analysisResult = header;

            List<Assembly> requiredAssemblies = new List<Assembly>();
            foreach (var assembly in assemblies)
            {
                bool isValidAssembly = true;
                if (ContainsFilteredKeyword(assembly.name, filterTermsInAnalysis)) isValidAssembly = false;
                if (requiredTermInPackages.Count > 0 && ContainsFilteredKeyword(assembly.package, requiredTermInPackages) == false) isValidAssembly = false;
                if (requiredTermInDependentAssemblies.Count > 0 && ContainsFilteredKeyword(assembly.name, requiredTermInDependentAssemblies) == false) isValidAssembly = false;

                if (isValidAssembly)
                {
                    if (FilterPackageBasedOnTheirDependencies == false && requiredAssemblies.Contains(assembly) == false) requiredAssemblies.Add(assembly);
                    foreach (var dep in assembly.dependencies)
                    {
                        bool isvalidDependencyRelation = true;
                        if (ContainsFilteredKeyword(dep.name, filterTermsInAnalysis)) isvalidDependencyRelation = false;
                        if (requiredTermInDependenciesAssemblies.Count > 0 && ContainsFilteredKeyword(dep.name, requiredTermInDependenciesAssemblies) == false)
                        {
                            isvalidDependencyRelation = false;
                        }
                        if (requiredTermInPackages.Count > 0 && ContainsFilteredKeyword(dep.package, requiredTermInPackages) == false) isvalidDependencyRelation = false;
                        if (isvalidDependencyRelation)
                        {
                            if (FilterPackageBasedOnTheirDependencies && requiredAssemblies.Contains(assembly) == false) requiredAssemblies.Add(assembly);
                            if (requiredAssemblies.Contains(dep) == false) requiredAssemblies.Add(dep);
                        }
                    }
                }
            }

            Dictionary<string, List<Assembly>> requiredAssembliesByPackage = new Dictionary<string, List<Assembly>>();

            foreach (var assembly in requiredAssemblies)
            {
                if(requiredAssembliesByPackage.ContainsKey(assembly.package) == false)
                {
                    requiredAssembliesByPackage[assembly.package] = new List<Assembly>();
                }
                requiredAssembliesByPackage[assembly.package].Add(assembly);
            }

            if (usePackage)
            {
                foreach (var requiredPackage in requiredAssembliesByPackage.Keys)
                {
                    if (onlyDisplayPackages && displayPackagesAsClasses)
                    {
                        analysisResult += $"class \"{Assembly.DisplayName(requiredPackage, replacementInNames, packagePrefix)}\" as {packagePrefix}_{Assembly.EscapedName(requiredPackage)}\n";
                    }
                    else
                    {
                        analysisResult += $"package \"{Assembly.DisplayName(requiredPackage, replacementInNames, packagePrefix)}\" as {packagePrefix}_{Assembly.EscapedName(requiredPackage)} {{ \n";
                    }
                        

                    foreach (var assembly in requiredAssembliesByPackage[requiredPackage])
                    {
                        if (onlyDisplayPackages == false)
                        {
                            analysisResult += $"  class \"{Assembly.DisplayName(assembly.name, replacementInNames)}\" as {Assembly.EscapedName(assembly.name)} \n";
                        }
                    }
                    if (onlyDisplayPackages == false || displayPackagesAsClasses == false)
                    {
                        analysisResult += $"}}\n";
                    }                        
                }
            } 
            else
            {
                foreach (var assembly in requiredAssemblies)
                {
                    analysisResult += $"class \"{Assembly.DisplayName(assembly.name, replacementInNames)}\" as {Assembly.EscapedName(assembly.name)} \n";
                }
            }

            HashSet<string> uniqueLinks = new HashSet<string>();
            foreach (var assembly in requiredAssemblies)
            {
                foreach (var dep in assembly.dependencies)
                {
                    if (requiredAssemblies.Contains(dep))
                    {
                        if (usePackageLinks)
                        {
                            if (assembly.package != dep.package && assembly.package != "" && dep.package != "")
                            {
                                uniqueLinks.Add($"{packagePrefix}_{Assembly.EscapedName(assembly.package)} --> {packagePrefix}_{Assembly.EscapedName(dep.package)}\n");
                            } 
                            else if(onlyDisplayPackages == false)
                            {
                                uniqueLinks.Add($"{Assembly.EscapedName(assembly.name)} --> {Assembly.EscapedName(dep.name)}\n");
                            }
                        }
                        else
                        {
                            uniqueLinks.Add($"{Assembly.EscapedName(assembly.name)} --> {Assembly.EscapedName(dep.name)}\n");
                        }
                    }
                }
            }

            foreach(var link in uniqueLinks)
            {
                analysisResult += link;
            }

            analysisResult += footer;
        }
    }

    bool ContainsFilteredKeyword(string s, List<string> filterTerms)
    {
        foreach (var filter in filterTerms)
        {
            if (s.Contains(filter)) return true;
            if (Assembly.EscapedName(s).Contains(filter)) return true;
        }
        return false;
    }
#endif
}
