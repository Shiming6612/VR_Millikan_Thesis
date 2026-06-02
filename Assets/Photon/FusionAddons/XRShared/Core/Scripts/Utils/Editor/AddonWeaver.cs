#if UNITY_EDITOR
using Fusion;
using Fusion.Editor;
using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.Build;

namespace Fusion.XR.Shared.Utils
{
    public static class AddonWeaver
    {
        public static void AddAssemblyToWeaver(string asmDefName)
        {
            if (NetworkProjectConfigAsset.TryGetGlobal(out var global))
            {
                var config = global.Config;
                string[] current = config.AssembliesToWeave;
                if (Array.IndexOf(current, asmDefName) < 0)
                {
                    config.AssembliesToWeave = new string[current.Length + 1];
                    for (int i = 0; i < current.Length; i++)
                    {
                        config.AssembliesToWeave[i] = current[i];
                    }
                    config.AssembliesToWeave[current.Length] = asmDefName;
                    NetworkProjectConfigUtilities.SaveGlobalConfig();
                }
            }
        }

        public static bool IsNetworkProjectConfigAvailable()
        {
#if !FUSION_WEAVER
            return false;
#endif
            return NetworkProjectConfigAsset.TryGetGlobal(out _);
        }

        public static bool IsAddonWeaved(string asmDefName)
        {
            if (NetworkProjectConfigAsset.TryGetGlobal(out var global))
            {
                var config = global.Config;
                string[] current = config.AssembliesToWeave;
                if (Array.IndexOf(current, asmDefName) >= 0)
                {
                    return true;
                }
            }
            return false;
        }

        public static bool CheckAssemblyPresence(string asmDefName, out Assembly assembly)
        {
            assembly = null;
            try
            {
                assembly = Assembly.Load(asmDefName);
                return true;

            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
#endif
