#if UNITY_EDITOR
using UnityEditor;
using Fusion.Editor;
using System;

namespace Fusion.Addons.DataSyncHelpers
{
    [InitializeOnLoad]
    public class DataSyncHelpersWeaver
    {
        public const string ADDON_ASMDEF_NAME_ASMDEF_NAME = "DataSyncHelpers";

        static DataSyncHelpersWeaver()
        {
            AddAddonAsmdef(ADDON_ASMDEF_NAME_ASMDEF_NAME);
        }

        // Duplicate of Fusion.XR.Shared.Utils.AddonWeaver.AddAddonAsmdef, added here to avoid introducing an unneeded dependency (this package could be used out of an XR context)
        public static void AddAddonAsmdef(string asmDefName)
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

    }
}
#endif