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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
#if MIXCAST_LWRP 
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;
#elif MIXCAST_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace BlueprintReality.MixCast {
    [CustomEditor(typeof(MixCastProjectSettings))]
	public class MixCastProjectSettingsInspector : Editor {
        public const string SUPPORT_URL = "https://mixcast.me/route.php?dest=support";

        private readonly static string[] INTEGRATION_URLS = new string[]
        {
            "https://mixcast.me/route.php?dest=steamvrsdk",     //MIXCAST_STEAMVR
            "https://mixcast.me/route.php?dest=oculussdk",     //MIXCAST_OCULUS
        };

        static GUIStyle groupBoxStyle;
        static GUIStyle headerStyle;
        static GUIStyle subHeaderStyle;

        public override void OnInspectorGUI()
        {
            DrawInspector(serializedObject, true);
        }

        public static void DrawInspector(SerializedObject serializedObject, bool drawTitle)
        {
            if (serializedObject == null)
                return;

            if (groupBoxStyle == null)
            {
                groupBoxStyle = new GUIStyle(EditorStyles.helpBox);
                groupBoxStyle.padding = new RectOffset(10, 10, 10, 10);
                groupBoxStyle.margin = new RectOffset(10, 10, 20, 20);

                headerStyle = new GUIStyle(EditorStyles.whiteLargeLabel);
                headerStyle.fontSize = 26;
                headerStyle.margin = new RectOffset(0, 0, 10, 10);

                subHeaderStyle = new GUIStyle(EditorStyles.whiteLargeLabel);
                subHeaderStyle.fontSize = 18;
                subHeaderStyle.margin = new RectOffset(0, 0, 8, 32);
            }

#if UNITY_5_6_OR_NEWER
            serializedObject.UpdateIfRequiredOrScript();
#else
            serializedObject.UpdateIfDirtyOrScript();
#endif
            if (drawTitle)
                EditorGUILayout.LabelField("MixCast Project Settings", headerStyle, GUILayout.Height(36));

            EditorGUIUtility.labelWidth *= 1.5f;

            DrawGeneratedGroup(serializedObject);
            DrawPluginGroup(serializedObject);

            EditorGUI.BeginDisabledGroup(Application.isPlaying);
            DrawPixelDataGroup(serializedObject);

#if MIXCAST_URP
            if (!(GraphicsSettings.renderPipelineAsset is UniversalRenderPipelineAsset))
                DrawQualityGroup(serializedObject);
#else
            DrawQualityGroup(serializedObject);
#endif

            DrawCustomizationGroup(serializedObject);

            //DrawEffectsGroup(serializedObject);
            DrawEditorGroup(serializedObject);
            EditorGUI.EndDisabledGroup();

            EditorGUIUtility.labelWidth /= 1.5f;

            serializedObject.ApplyModifiedProperties();
        }

        static void DrawCustomizationGroup(SerializedObject serializedObject)
        {
            SerializedProperty layerCamPrefabProp = serializedObject.FindProperty("layerCamPrefab");
            //SerializedProperty finalPassCamPrefabProp = serializedObject.FindProperty("finalPassCamPrefab");

            SerializedProperty canRenderOpaqueBgProp = serializedObject.FindProperty("canRenderOpaqueBg");
            SerializedProperty opaqueOnlyBgLayersProp = serializedObject.FindProperty("opaqueOnlyBgLayers");
            SerializedProperty canRenderTransparentBgProp = serializedObject.FindProperty("canRenderTransparentBg");
            SerializedProperty transparentOnlyBgLayersProp = serializedObject.FindProperty("transparentOnlyBgLayers");


            EditorGUILayout.BeginVertical(groupBoxStyle);
            EditorGUILayout.LabelField("Customization", subHeaderStyle, GUILayout.Height(28));
            EditorGUI.indentLevel++;

            Object oldVal = layerCamPrefabProp.objectReferenceValue;
            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(layerCamPrefabProp);
            if( EditorGUI.EndChangeCheck() )
            {
                if( layerCamPrefabProp.objectReferenceValue != null )
                {
                    Camera[] camComps = (layerCamPrefabProp.objectReferenceValue as GameObject).GetComponentsInChildren<Camera>(true);
                    if(camComps.Length == 0)
                    {
                        layerCamPrefabProp.objectReferenceValue = oldVal;
                        EditorUtility.DisplayDialog("Error", "Camera Prefab must contain a Camera component for MixCast to use. Please correct the issue and try again", "OK");
                    }
                    else
                    {
#if MIXCAST_URP
                        bool isUrp = GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset;
#else
                        bool isUrp = false;
#endif
                        if (!isUrp)
                        {
                        bool conflictingDepths = false;
                        Dictionary<string, Camera> depthCams = new Dictionary<string, Camera>();
                        for (int i = 0; i < camComps.Length; i++)
                        {
                            conflictingDepths |= depthCams.ContainsKey(camComps[i].depth.ToString());
                            depthCams[camComps[i].depth.ToString()] = camComps[i];
                        }
                        if (conflictingDepths)
                        {
                            layerCamPrefabProp.objectReferenceValue = oldVal;
                            EditorUtility.DisplayDialog("Error", "Camera Prefab can't contain multiple Camera components set to have the same depth. Please correct the issue and try again", "OK");
                            }
                        }
                    }
                }
            }

            GUILayout.Space(16);
            EditorGUILayout.PropertyField(canRenderOpaqueBgProp);
            if (canRenderOpaqueBgProp.boolValue && canRenderTransparentBgProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(opaqueOnlyBgLayersProp);
                EditorGUI.indentLevel--;
            }
            EditorGUILayout.PropertyField(canRenderTransparentBgProp);
            if (canRenderOpaqueBgProp.boolValue && canRenderTransparentBgProp.boolValue)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.PropertyField(transparentOnlyBgLayersProp);
                EditorGUI.indentLevel--;
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        static void DrawPixelDataGroup(SerializedObject serializedObject)
        {
            SerializedProperty usingPmaProp = serializedObject.FindProperty("usingPremultipliedAlpha");
            SerializedProperty grabUnfilteredAlphaProp = serializedObject.FindProperty("grabUnfilteredAlpha");
            SerializedProperty doesntOutputGoodDepthProp = serializedObject.FindProperty("doesntOutputGoodDepth");

            EditorGUILayout.BeginVertical(groupBoxStyle);
            EditorGUILayout.LabelField("Pixel Data", subHeaderStyle, GUILayout.Height(28));
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(usingPmaProp);
            if (usingPmaProp.boolValue)
            {
                EditorGUILayout.BeginVertical(groupBoxStyle);

                EditorGUILayout.LabelField("Ensure your project's shaders are compatible with PMA");
                if (GUILayout.Button("Open Wizard"))
                {
                    ShaderTransparencyWizard.ShowWindow();
                }

                EditorGUILayout.EndVertical();
            }

            EditorGUILayout.PropertyField(grabUnfilteredAlphaProp, new GUIContent("Take Pre-PostProcess Alpha"), true);

            GUILayout.Space(16);
            EditorGUILayout.PropertyField(doesntOutputGoodDepthProp, new GUIContent("Doesn't Output Accurate Depth"), true);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        static void DrawEffectsGroup(SerializedObject serializedObject)
        {
            SerializedProperty subjectLayerProp = serializedObject.FindProperty("subjectLayerName");
            SerializedProperty specifyLightsManuallyProp = serializedObject.FindProperty("specifyLightsManually");
            SerializedProperty directionalLightPowerProp = serializedObject.FindProperty("directionalLightPower");
            SerializedProperty pointLightPowerProp = serializedObject.FindProperty("pointLightPower");

            EditorGUILayout.BeginVertical(groupBoxStyle);
            EditorGUILayout.LabelField("Effects", subHeaderStyle, GUILayout.Height(28));
            EditorGUI.indentLevel++;

            EditorGUILayout.LabelField("Subject Relighting", EditorStyles.boldLabel);
            EditorGUI.indentLevel++;

            int oldLayerIndex = -1;
            if (!string.IsNullOrEmpty(subjectLayerProp.displayName))
            {
                for (int i = 0; i < UnityEditorInternal.InternalEditorUtility.layers.Length; i++)
                    if (UnityEditorInternal.InternalEditorUtility.layers[i] == subjectLayerProp.stringValue)
                        oldLayerIndex = i;
            }
            int newLayerIndex = EditorGUILayout.Popup(subjectLayerProp.displayName, oldLayerIndex, UnityEditorInternal.InternalEditorUtility.layers);
            if (newLayerIndex != -1)
                subjectLayerProp.stringValue = UnityEditorInternal.InternalEditorUtility.layers[newLayerIndex];
            else
                subjectLayerProp.stringValue = "";

            EditorGUILayout.PropertyField(specifyLightsManuallyProp);

            EditorGUILayout.PropertyField(directionalLightPowerProp);
            if (directionalLightPowerProp.floatValue < 0)
                directionalLightPowerProp.floatValue = 0;

            EditorGUILayout.PropertyField(pointLightPowerProp);
            if (pointLightPowerProp.floatValue < 0)
                pointLightPowerProp.floatValue = 0;

            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        static void DrawEditorGroup(SerializedObject serializedObject)
        {
            SerializedProperty requireCommandLineArgProp = serializedObject.FindProperty("requireCommandLineArg");
            SerializedProperty enableInEditorProp = serializedObject.FindProperty("enableMixCastInEditor");
            //SerializedProperty displaySubjectInSceneProp = serializedObject.FindProperty("displaySubjectInScene");
            SerializedProperty applyFlagsProp = serializedObject.FindProperty("applySdkFlagsAutomatically");

            EditorGUILayout.BeginVertical(groupBoxStyle);
            EditorGUILayout.LabelField("Editor/Build", subHeaderStyle, GUILayout.Height(28));
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(enableInEditorProp, new GUIContent("Enable MixCast in Editor"));

            //if (enableInEditorProp.boolValue)
            //    EditorGUILayout.PropertyField(displaySubjectInSceneProp, new GUIContent("Visualize Subject in Scene View"));

            EditorGUILayout.PropertyField(requireCommandLineArgProp);
            if( requireCommandLineArgProp.boolValue )
            {
                EditorGUILayout.HelpBox("With this setting enabled, users must launch the application with the command line argument \"-mixcast\" for MixCast's logic to execute", MessageType.Warning);
            }

            EditorGUI.BeginChangeCheck();
            EditorGUILayout.PropertyField(applyFlagsProp, new GUIContent("Apply Platform Flags Automatically"), true);
            if (EditorGUI.EndChangeCheck() && applyFlagsProp.boolValue)
                ScriptDefineManager.EnforceAppropriateScriptDefines();

            EditorGUI.indentLevel++;
            if( applyFlagsProp.boolValue )
                EditorGUI.BeginDisabledGroup(true);

            for( int i = 0; i < ScriptDefineManager.FileDrivenDefines.Length; i++ )
            {
                ScriptDefineManager.FileDrivenDefine define = ScriptDefineManager.FileDrivenDefines[i];
                bool wasEnabled = ScriptDefineManager.IsDefineEnabled(define.defineFlag);
                string labelStr = string.Format("Enable {0} support", define.systemName);
                bool isEnabled = EditorGUILayout.Toggle(labelStr, wasEnabled);
                if (wasEnabled != isEnabled)
                {
                    if (isEnabled)
                    {
                        isEnabled = ScriptDefineManager.TryEnableDefine(define);
                        if (!isEnabled)
                        {
                            bool ok = EditorUtility.DisplayDialog(
                                "Missing Dependency",
                                "You haven't imported the required plugin for " + define.systemName + " support",
                                "Get It Now",
                                "Cancel");
                            if (ok)
                                Application.OpenURL(INTEGRATION_URLS[i]);
                        }
                    }
                    else
                    {
                        ScriptDefineManager.DisableDefine(define);
                    }
                }
            }
            if (applyFlagsProp.boolValue)
                EditorGUI.EndDisabledGroup();
            EditorGUI.indentLevel--;

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        static void DrawPluginGroup(SerializedObject serializedObject)
        {
            EditorGUILayout.BeginVertical(groupBoxStyle);

            EditorGUILayout.BeginHorizontal(GUILayout.Height(28));
            EditorGUILayout.LabelField("Links", subHeaderStyle, GUILayout.Height(28));
            EditorGUILayout.EndHorizontal();

            EditorGUI.indentLevel++;

            //EditorGUILayout.LabelField("Version:", MixCastSdk.VERSION_STRING, subHeaderStyle, GUILayout.Height(20));
            if (GUILayout.Button("Go To Website"))
                MixCastSdk.GoToWebsite();
            if (GUILayout.Button("Get Support"))
                Application.OpenURL(SUPPORT_URL);
            if (GUILayout.Button("Check for Updates"))
                UpdateChecker.RunCheck();

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        static void DrawGeneratedGroup(SerializedObject serializedObject)
        {
            SerializedProperty idProp = serializedObject.FindProperty("projectId");

            EditorGUILayout.BeginVertical(groupBoxStyle);
            EditorGUILayout.LabelField("Info", subHeaderStyle, GUILayout.Height(28));
            EditorGUI.indentLevel++;

            DrawValue("Project ID", idProp.stringValue);
            DrawValue("MixCast SDK Version", MixCastSdk.VERSION_STRING);

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }
        static void DrawQualityGroup(SerializedObject serializedObject)
        {
            SerializedProperty overrideAAProp = serializedObject.FindProperty("overrideQualitySettingsAA");
            SerializedProperty aaValProp = serializedObject.FindProperty("overrideAntialiasingVal");

            EditorGUILayout.BeginVertical(groupBoxStyle);
            EditorGUILayout.LabelField("Quality", subHeaderStyle, GUILayout.Height(28));
            EditorGUI.indentLevel++;

            EditorGUILayout.PropertyField(overrideAAProp);
            if( overrideAAProp.boolValue )
            {
                aaValProp.intValue = EditorGUILayout.IntPopup("Anti Aliasing", aaValProp.intValue,
                    new string[] { "Disabled", "2x Multi Sampling", "4x Multi Sampling", "8x Multi Sampling" },
                    new int[] { 0, 1, 2, 3 });
            }

            EditorGUI.indentLevel--;
            EditorGUILayout.EndVertical();
        }

        [MenuItem("MixCast/Open Project Settings", priority = 4)]
        public static void OpenProjectSettings()
        {
#if UNITY_2018_3_OR_NEWER
            SettingsService.OpenProjectSettings("Project/MixCast");
#else
            string[] assetGuids = AssetDatabase.FindAssets("t:MixCastProjectSettings");
            if( assetGuids.Length > 0 )
            {
                string assetPath = AssetDatabase.GUIDToAssetPath(assetGuids[0]);
                Selection.activeObject = AssetDatabase.LoadAssetAtPath<MixCastProjectSettings>(assetPath);
            }
#endif
        }

        static void DrawValue(string prefix, string value)
        {
            float oldPrefixSize = EditorGUIUtility.labelWidth;
            EditorGUIUtility.labelWidth = 0;

            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField(prefix, EditorStyles.label, GUILayout.MaxWidth(180));
            EditorGUILayout.LabelField(value, EditorStyles.boldLabel);
            EditorGUILayout.EndHorizontal();

            EditorGUIUtility.labelWidth = oldPrefixSize;
        }
    }
}
