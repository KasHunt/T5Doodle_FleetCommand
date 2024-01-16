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

namespace BlueprintReality.MixCast.Cameras
{
    [CustomEditor(typeof(SetVisibilityForMixCast), true)]
    public class SetVisibilityForMixCastInspector : Editor
    {
        const string Prop_Script = "m_Script";
        const string Prop_ShowForRegularCameras = "showForRegularCameras";
        const string Prop_ShowForMixCastCameras = "showForMixCastCameras";
        const string Prop_MixCastCamerasMode = "mixCastCameraMode";
        const string Prop_RenderTypeCondition = "renderTypeCondition";
        const string Prop_BackgroundTypeCondition = "backgroundTypeCondition";
        const string Prop_PerspectiveCondition = "perspectiveCondition";

        public override void OnInspectorGUI()
        {
            string[] excludeFields = {
                Prop_Script,
                Prop_ShowForRegularCameras,
                Prop_ShowForMixCastCameras,
                Prop_MixCastCamerasMode,
                Prop_RenderTypeCondition,
                Prop_BackgroundTypeCondition,
                Prop_PerspectiveCondition,
            };

#if UNITY_2017_1_OR_NEWER
            serializedObject.UpdateIfRequiredOrScript();
#else
            serializedObject.UpdateIfDirtyOrScript();
#endif
            EditorGUILayout.PropertyField(serializedObject.FindProperty(Prop_Script));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(Prop_ShowForRegularCameras));
            EditorGUILayout.PropertyField(serializedObject.FindProperty(Prop_ShowForMixCastCameras));

            SerializedProperty mixcastCamerasBehaviour = serializedObject.FindProperty(Prop_MixCastCamerasMode);
            EditorGUILayout.PropertyField(mixcastCamerasBehaviour);
            if (mixcastCamerasBehaviour.enumValueIndex != (int)SetVisibilityForMixCast.MixCastCameraMode.Always)
            {
                EditorGUILayout.LabelField("Conditions", EditorStyles.boldLabel);
                EditorGUILayout.PropertyField(serializedObject.FindProperty(Prop_RenderTypeCondition));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(Prop_BackgroundTypeCondition));
                EditorGUILayout.PropertyField(serializedObject.FindProperty(Prop_PerspectiveCondition));
            }

            DrawPropertiesExcluding(serializedObject, excludeFields);
            serializedObject.ApplyModifiedProperties();
        }
    }
}
#endif
