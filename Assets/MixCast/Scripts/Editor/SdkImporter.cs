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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BlueprintReality.MixCast.Data;
#if MIXCAST_LWRP || MIXCAST_URP || MIXCAST_HDRP
using UnityEngine.Rendering;
#endif
#if MIXCAST_LWRP
using UnityEngine.Rendering.LWRP;
#elif MIXCAST_URP
using UnityEngine.Rendering.Universal;
#endif
#if MIXCAST_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace BlueprintReality.MixCast {
    [InitializeOnLoad]
    public class SdkImporter : AssetPostprocessor {
        const string PROJECT_SETTINGS_FILENAME = "MixCast_ProjectSettings.asset";

        static SdkImporter()
        {
            EnsureSdkProjectSettingsExist();

            ValidateProjectSettings();
        }

        static void EnsureSdkProjectSettingsExist()
        {
            string[] settingsPaths = System.IO.Directory.GetFiles(Application.dataPath, PROJECT_SETTINGS_FILENAME, System.IO.SearchOption.AllDirectories);
            if (settingsPaths.Length > 0)
                return;

            string resourcesPath = Application.dataPath + "/Resources";
            if (!System.IO.Directory.Exists(resourcesPath))
                System.IO.Directory.CreateDirectory(resourcesPath);

            string settingsPath = resourcesPath + "/" + PROJECT_SETTINGS_FILENAME;
            int assetFolderIndex = settingsPath.LastIndexOf("/Assets/");
            settingsPath = settingsPath.Substring(assetFolderIndex + 1);

            MixCastProjectSettings settingsAsset = ScriptableObject.CreateInstance<MixCastProjectSettings>();
            settingsAsset.name = System.IO.Path.GetFileNameWithoutExtension(PROJECT_SETTINGS_FILENAME);

            AssetDatabase.CreateAsset(settingsAsset, settingsPath);
            AssetDatabase.SaveAssets();
        }

        public static void ValidateProjectSettings()
        {
#if MIXCAST_HDRP
            HashSet<string> compatibleAssets = new HashSet<string>();
            HashSet<string> incompatibleAssets = new HashSet<string>();

            for ( int i = 0; i < QualitySettings.names.Length; i++ )
            {
                HDRenderPipelineAsset qualityPipeline = (QualitySettings.GetRenderPipelineAssetAt(i) ?? GraphicsSettings.defaultRenderPipeline) as HDRenderPipelineAsset;
                if (qualityPipeline != null)
                {
                    bool correct = HdrpBufferSettingsCorrect(qualityPipeline.currentPlatformRenderPipelineSettings);
                    if (correct)
                        compatibleAssets.Add(qualityPipeline.name);
                    else
                        incompatibleAssets.Add(qualityPipeline.name);
                }
            }

            if (compatibleAssets.Count > 0 || incompatibleAssets.Count > 0)
            {
                if (compatibleAssets.Count == 0)
                    Debug.LogError("The MixCast SDK requires that HDRP settings for the Color Buffer include an Alpha Channel, but no compatible Settings assets found! " +
                        "Please assign a HDRP settings asset with the Color Buffer configured to a format with an Alpha Channel.");
                else if (incompatibleAssets.Count > 0)
                    Debug.LogWarning(string.Format("The MixCast SDK requires that HDRP settings for the Color Buffer include an Alpha Channel, but not all assigned Settings assets are compatible. " +
                        "Please ensure that MixCast is only used with compatible settings: {0}!", string.Join(", ", new List<string>(compatibleAssets).ToArray())));
            }

            compatibleAssets.Clear();
            incompatibleAssets.Clear();

            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                HDRenderPipelineAsset qualityPipeline = (QualitySettings.GetRenderPipelineAssetAt(i) ?? GraphicsSettings.defaultRenderPipeline) as HDRenderPipelineAsset;
                if (qualityPipeline != null)
                {
                    bool correct = HdrpCustomPassSettingCorrect(qualityPipeline.currentPlatformRenderPipelineSettings);
                    if (correct)
                        compatibleAssets.Add(qualityPipeline.name);
                    else
                        incompatibleAssets.Add(qualityPipeline.name);
                }
            }
            if (compatibleAssets.Count > 0 || incompatibleAssets.Count > 0)
            {
                if (compatibleAssets.Count == 0)
                    Debug.LogError("The MixCast SDK requires that HDRP settings include Custom Pass support, but no compatible Settings assets found! " +
                        "Please assign a HDRP settings asset with Custom Passes enabled.");
                else if (incompatibleAssets.Count > 0)
                    Debug.LogWarning(string.Format("The MixCast SDK requires that HDRP settings include Custom Pass support, but not all assigned Settings assets are compatible. " +
                        "Please ensure that MixCast is only used with compatible settings: {0}!", string.Join(", ", new List<string>(compatibleAssets).ToArray())));
            }
#endif
        }

#if MIXCAST_HDRP
        static bool HdrpBufferSettingsCorrect(RenderPipelineSettings settings)
        {
            return settings.colorBufferFormat != RenderPipelineSettings.ColorBufferFormat.R11G11B10;
        }
        static bool HdrpCustomPassSettingCorrect(RenderPipelineSettings settings)
        {
            return settings.supportCustomPass;
        }
#endif

        static void OnPostprocessAllAssets(string[] importedAssets, string[] deletedAssets, string[] movedAssets, string[] movedFromAssetPaths)
        {
            bool deletingProjectSettings = false;
            for (int i = 0; i < deletedAssets.Length; i++)
            {
                if (deletedAssets[i].EndsWith(PROJECT_SETTINGS_FILENAME))
                    deletingProjectSettings = true;
            }
            for( int i = 0; i < importedAssets.Length; i++ )
            {
                if (importedAssets[i].EndsWith(PROJECT_SETTINGS_FILENAME))
                    deletingProjectSettings = false;
            }

            if( deletingProjectSettings )
            {
                Debug.LogError("MixCast requires that its Project Settings are included in the project!");
                EnsureSdkProjectSettingsExist();
            }
        }
    }
}
#endif
