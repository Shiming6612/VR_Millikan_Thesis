using UnityEngine;

namespace Fusion.XR.Shared.Core
{
    public delegate void ValidationCallback();
    public static class ValidationUtils
    {
        /// <summary>
        /// Helper method to call in a OnValidate() method to trigger editor checks, only when the scene is opened. Won't raise exceptions bu default.
        /// </summary>
        public static void SceneEditionValidate(GameObject gameObject, ValidationCallback callback, bool blockExceptions = true)
        {
#if UNITY_EDITOR
            if (gameObject == null) return;
            if(Application.IsPlaying(gameObject) == false)
            {
                if (gameObject?.scene != UnityEngine.SceneManagement.SceneManager.GetActiveScene())
                {
                    // OnValidate called during loading the script, not due to it being present in the scen
                    return;
                }

                // Delay the call to ensure the checks do not collide with maintenance operations
                UnityEditor.EditorApplication.delayCall += () => {
                    try
                    {
                        if (callback != null) callback();
                    } catch(System.Exception e){
                        // We ignore the validation errors by default - this method should just report the on purpose error the callback will trigger
                        if(blockExceptions == false)
                        {
                            Debug.LogError($"[SceneEditionValidate] Exception during validation: {e?.Message}\n{e}");
                            throw e;
                        }
                    }
};
            }
#endif
        }
    }

}
