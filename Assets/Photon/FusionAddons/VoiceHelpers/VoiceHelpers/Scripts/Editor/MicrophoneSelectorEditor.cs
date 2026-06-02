#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MicrophoneSelector))]
public class MicrophoneSelectorEditor : Editor
{
    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
#if FUSION_WEAVER
#if PHOTON_VOICE_AVAILABLE
        MicrophoneSelector microphoneSelector = (MicrophoneSelector)target;

        if (GUILayout.Button($"Refresh mic list"))
        {
            microphoneSelector.RefreshMicrophoneList();
        }

        // Create buttons with mic selection
        int index = 0;
        foreach (var micName in microphoneSelector.microphoneNames)
        {
            var selected = microphoneSelector.selectedMicrophoneIndex == index;;
            var selectedText = selected ? " (current)" : "";

            if (GUILayout.Button($"{selectedText} Select {micName}"))
            {
                microphoneSelector.SelectMicrophoneAtIndex(index);
                break;
            }
            index++;
        }
#endif
#endif
    }

}
#endif
