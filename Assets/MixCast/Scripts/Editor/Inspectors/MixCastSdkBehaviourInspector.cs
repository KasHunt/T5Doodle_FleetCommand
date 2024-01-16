/**********************************************************************************
* Blueprint Reality Inc. CONFIDENTIAL
* 2023 Blueprint Reality Inc.
* All Rights Reserved.
*
* NOTICE:  All information contained herein is, and remains, the property of
* Blueprint Reality Inc. and its suppliers, if any.  The intellectual and
* technical concepts contained herein are proprietary to Blueprint Reality Inc.
* and its suppliers and may be covered by Patents, pending patents, and are
* protected by trade secret or copyright law.
*
* Dissemination of this information or reproduction of this material is strictly
* forbidden unless prior written permission is obtained from Blueprint Reality Inc.
***********************************************************************************/

#if UNITY_STANDALONE_WIN
using UnityEngine;
using System.Collections;
using UnityEditor;

namespace BlueprintReality.MixCast
{
    [CustomEditor(typeof(MixCastSdkBehaviour))]
    public class MixCastSdkBehaviourInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            string[] excludeFields = { };
            bool atRoot = true;
            for (int i = 0; i < serializedObject.targetObjects.Length; i++)
                if ((serializedObject.targetObjects[i] as MixCastSdkBehaviour).transform.parent != null)
                    atRoot = false;
            if( atRoot )
                excludeFields = new string[] { "reparentToSceneRootOnStart" };
            DrawPropertiesExcluding(serializedObject, excludeFields);
            if (GUILayout.Button("Open Project Settings"))
                MixCastProjectSettingsInspector.OpenProjectSettings();
        }
    }
}
#endif
