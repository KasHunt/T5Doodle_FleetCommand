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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using BlueprintReality.MixCast.Data;
#if UNITY_2017_3_OR_NEWER
using UnityEditor.PackageManager.Requests;
#endif

//Class that handles enforcing the script defines on project necessary for appropriate SDK interaction
namespace BlueprintReality.MixCast
{
    [InitializeOnLoad]
    public class FileDefineManager
    {
        private static string[] HdrpOnly = new string[]
        {
            "Resources/Shaders/Hidden - Hdrp - OpaqueGrab.shader",
            "Resources/Shaders/Hidden - Hdrp - ApplyDepthCutoff.shader",
        };

        private static string[] TiltFiveOnly = new string[]
        {
            "Extras/Prefabs/MixCast - TiltFive - Wands.prefab",
        };

        static FileDefineManager()
        {
            EditorApplication.delayCall += Start;
        }

        static void Start()
        {
            EditorApplication.delayCall -= Start;

            EnforceIgnoredFiles();
        }

        public static void EnforceIgnoredFiles()
        {
#if MIXCAST_HDRP
            bool ignoreHdrp = false;
#else
            bool ignoreHdrp = true;
#endif

#if MIXCAST_TILTFIVE
            bool ignoreTiltFive = false;
#else
            bool ignoreTiltFive = true;
#endif

            string[] sdkFiles = AssetDatabase.FindAssets("MixCastSdk");
            string sdkFilePath = null;
            for (int i = 0; i < sdkFiles.Length; i++)
            {
                sdkFilePath = AssetDatabase.GUIDToAssetPath(sdkFiles[i]);
                if (System.IO.Path.GetFileName(sdkFilePath) == "MixCastSdk.cs")
                    break;
            }
            string mixcastSdkPath = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath),
                System.IO.Path.GetDirectoryName(System.IO.Path.GetDirectoryName(sdkFilePath)));

            bool changed = false;
            for (int i = 0; i < HdrpOnly.Length; i++)
                changed |= EnsureFile(ignoreHdrp, System.IO.Path.Combine(mixcastSdkPath, HdrpOnly[i]));
            for (int i = 0; i < TiltFiveOnly.Length; i++)
                changed |= EnsureFile(ignoreTiltFive, System.IO.Path.Combine(mixcastSdkPath, TiltFiveOnly[i]));

            if (changed)
                AssetDatabase.Refresh();
        }


        static bool EnsureFile(bool ensureIgnored, string filePath)
        {
            string ignoredFilePath = filePath + ".ignore";
            string metaFilePath = filePath + ".meta";
            string ignoredMetaFilePath = ignoredFilePath + ".meta";

            bool changed = false;
            if (ensureIgnored)
            {
                changed |= EnsureFileAtDest(filePath, ignoredFilePath, false);
                changed |= EnsureFileAtDest(metaFilePath, ignoredMetaFilePath, true);
            }
            else
            {
                changed |= EnsureFileAtDest(ignoredFilePath, filePath, false);
                changed |= EnsureFileAtDest(ignoredMetaFilePath, metaFilePath, true);
            }
            return changed;
        }

        static bool EnsureFileAtDest(string srcFilePath, string dstFilePath, bool destroySrcIfDstExists)
        {
            if (System.IO.File.Exists(srcFilePath))
            {
                if (System.IO.File.Exists(dstFilePath))
                {
                    if (destroySrcIfDstExists)
                    {
                        System.IO.File.Delete(srcFilePath);
                        return true;
                    }
                    System.IO.File.Delete(dstFilePath);
                }
                System.IO.File.Move(srcFilePath, dstFilePath);
                return true;
            }
            return false;
        }
    }
}
