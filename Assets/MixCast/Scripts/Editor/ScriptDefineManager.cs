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
    public class ScriptDefineManager
    {
        public class FileDrivenDefine
        {
            public string defineFlag;
            public string fileName;
            public string prereqFlag;
            public string systemName;
        }
        public class PackageDrivenDefine
        {
            public string defineFlag;
            public string packageName;
            public string systemName;
            public string minVersion;
            public string maxVersion;
        }

        private const string BaseDefineFlag = "MIXCAST";

        public static readonly FileDrivenDefine[] FileDrivenDefines = new FileDrivenDefine[]
        {
            new FileDrivenDefine()
            {
                defineFlag = "MIXCAST_STEAMVR",
                fileName = "SteamVR.cs",
                systemName = "SteamVR"
            },
            new FileDrivenDefine()
            {
                defineFlag = "MIXCAST_OCULUS",
                fileName = "OVRManager.cs",
                systemName = "Oculus"
            },
            new FileDrivenDefine()
            {
                defineFlag = "MIXCAST_TILTFIVE",
                fileName = "TiltFiveManager.cs",
                systemName = "Tilt Five"
            },
			    new FileDrivenDefine()
			    {
				    defineFlag = "MIXCAST_TILTFIVE_V2",
                    prereqFlag = "MIXCAST_TILTFIVE",
					fileName = "TiltFiveManager2.cs",
			    },
		};
        public static readonly PackageDrivenDefine[] PackageDrivenDefines = new PackageDrivenDefine[]
        {
            new PackageDrivenDefine()
            {
                defineFlag = "MIXCAST_LWRP",
                packageName = "com.unity.render-pipelines.lightweight",
                systemName = "Lightweight Render Pipeline",
                minVersion = "6.9.0"
            },
            new PackageDrivenDefine()
            {
                defineFlag = "MIXCAST_URP",
                packageName = "com.unity.render-pipelines.universal",
                systemName = "Universal Render Pipeline",
                minVersion = "7.1.8",
            },
                new PackageDrivenDefine()
                {
                    defineFlag = "MIXCAST_URP_V1",
                    packageName = "com.unity.render-pipelines.universal",
                    minVersion = "7.1.8",
                    maxVersion = "10.0.0"
                },
                new PackageDrivenDefine()
                {
                    defineFlag = "MIXCAST_URP_V2",
                    packageName = "com.unity.render-pipelines.universal",
                    minVersion = "10.0.0",
                    maxVersion = "11.0.0"
                },
                new PackageDrivenDefine()
                {
                    defineFlag = "MIXCAST_URP_V3",
                    packageName = "com.unity.render-pipelines.universal",
                    minVersion = "11.0.0",
                    maxVersion = "12.0.0"
                },
                new PackageDrivenDefine()
                {
                    defineFlag = "MIXCAST_URP_V4",
                    packageName = "com.unity.render-pipelines.universal",
                    minVersion = "12.0.0"
                },

            new PackageDrivenDefine()
            {
                defineFlag = "MIXCAST_HDRP",
                packageName = "com.unity.render-pipelines.high-definition",
                systemName = "High Definition Render Pipeline",
                minVersion = "12.1.0"
            },

#if UNITY_2019_1_OR_NEWER
            new PackageDrivenDefine()
            {
                defineFlag = "MIXCAST_XRMAN",
                packageName = "com.unity.xr.management",
                systemName = "XR Management Plugin",
                minVersion = "3.0.0"
            }
#endif
        };

#if UNITY_2017_3_OR_NEWER
        static ListRequest listPackageReq;
        static List<UnityEditor.PackageManager.PackageInfo> loadedPackages = new List<UnityEditor.PackageManager.PackageInfo>();
#endif

        static ScriptDefineManager()
        {
            EditorApplication.delayCall += Start;
            EditorApplication.update += Update;
        }

        static void Start()
        {
            EditorApplication.delayCall -= Start;

#if UNITY_2018_1_OR_NEWER
            listPackageReq = UnityEditor.PackageManager.Client.List(true);
#elif UNITY_2017_3_OR_NEWER
            listPackageReq = UnityEditor.PackageManager.Client.List();
#else
            if (MixCastSdkData.ProjectSettings != null && MixCastSdkData.ProjectSettings.applySdkFlagsAutomatically)
                EnforceAppropriateScriptDefines();
#endif
        }


        private static void Update()
        {
#if UNITY_2017_3_OR_NEWER
            if (listPackageReq != null && listPackageReq.IsCompleted)
            {
                if (listPackageReq.Error != null || listPackageReq.Result == null)
                {
                    listPackageReq = null;
                    return;
                }

                loadedPackages.Clear();

                foreach (UnityEditor.PackageManager.PackageInfo package in listPackageReq.Result)
                {
                    loadedPackages.Add(package);
                }

                listPackageReq = null;

                if (MixCastSdkData.ProjectSettings != null && MixCastSdkData.ProjectSettings.applySdkFlagsAutomatically)
                    EnforceAppropriateScriptDefines();
            }
#endif
        }

        public static bool EnforceAppropriateScriptDefines(BuildTargetGroup buildTargetGroup = BuildTargetGroup.Unknown)
        {
            if (buildTargetGroup == BuildTargetGroup.Unknown)
                buildTargetGroup = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            string defineStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTargetGroup);

            List<string> defineList = new List<string>();
            if (!string.IsNullOrEmpty(defineStr))
                defineList.AddRange(defineStr.Split(';'));

            bool anyChanges = false;

            anyChanges |= EnforceDefine(BaseDefineFlag, defineList);

            for (int i = 0; i < FileDrivenDefines.Length; i++)
            {
                FileDrivenDefine define = FileDrivenDefines[i];
                bool changed = EnforceFileDefineAutomatically(define, defineList);
                if (changed && defineList.Contains(define.defineFlag) && !string.IsNullOrEmpty(define.systemName))
                    Debug.Log("Enforced " + define.systemName + " support for MixCast");
                anyChanges |= changed;
            }
#if UNITY_2017_3_OR_NEWER
            for (int i = 0; i < PackageDrivenDefines.Length; i++)
            {
                PackageDrivenDefine define = PackageDrivenDefines[i];
                bool changed = EnforcePackageDefineAutomatically(define.defineFlag, define.packageName, defineList, define.minVersion, define.maxVersion);
                if (changed && defineList.Contains(define.defineFlag) && !string.IsNullOrEmpty(define.systemName))
                    Debug.Log("Enforced " + define.systemName + " support for MixCast");
                anyChanges |= changed;
            }
#endif

            if (anyChanges)
            {
                defineStr = string.Join(";", defineList.ToArray());
                PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTargetGroup, defineStr);
            }

            return anyChanges;
        }

        //Returns true if the define list has been modified
        public static bool EnforceDefine(string flag, List<string> currentDefines)
        {
            bool modifying = !currentDefines.Contains(flag);
            if (modifying)
                currentDefines.Add(flag);
            return modifying;
        }
        public static bool EnforceFileDefineAutomatically(FileDrivenDefine define, List<string> currentDefines)
        {
            bool libraryFound = (string.IsNullOrEmpty(define.prereqFlag) || currentDefines.Contains(define.prereqFlag)) && 
                System.IO.Directory.GetFiles(Application.dataPath, define.fileName, System.IO.SearchOption.AllDirectories).Length > 0;
            bool modifying = currentDefines.Contains(define.defineFlag) != libraryFound;
            if (modifying)
            {
                if (libraryFound)
                    currentDefines.Add(define.defineFlag);
                else
                    currentDefines.Remove(define.defineFlag);
            }
            return modifying;
        }
#if UNITY_2017_3_OR_NEWER
        public static bool EnforcePackageDefineAutomatically(string defineFlag, string checkPackageId, List<string> currentDefines, 
            string minVersion = null, string maxVersion = null)
        {
            UnityEditor.PackageManager.PackageInfo info = loadedPackages.Find(i => i.name == checkPackageId);
            bool packageFound = info != null && 
                (minVersion == null || !MixCastSdk.IsVersionBLaterThanVersionA(info.version.Replace("-preview", ""), minVersion)) &&
                (maxVersion == null || MixCastSdk.IsVersionBLaterThanVersionA(info.version.Replace("-preview", ""), maxVersion));
            
            bool modifying = currentDefines.Contains(defineFlag) != packageFound;
            if (modifying)
            {
                if (packageFound)
                    currentDefines.Add(defineFlag);
                else
                    currentDefines.Remove(defineFlag);
            }
            return modifying;
        }
#endif
        public static bool IsDefineEnabled(string flag)
        {
            string defineStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget));
            List<string> defineList = new List<string>(defineStr.Split(';'));
            return defineList.Contains(flag);
        }
        public static bool TryEnableDefine(FileDrivenDefine define)
        {
            if (IsDefineEnabled(define.defineFlag))
                return true;
            bool libraryFound = System.IO.Directory.GetFiles(Application.dataPath, define.fileName, System.IO.SearchOption.AllDirectories).Length > 0;
            if (!libraryFound)
                return false;
            BuildTargetGroup buildTarget = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            string defineStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
            defineStr += ";" + define.defineFlag;
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTarget, defineStr);
            return true;
        }
        public static void DisableDefine(FileDrivenDefine define)
        {
            if (!IsDefineEnabled(define.defineFlag))
                return;
            BuildTargetGroup buildTarget = BuildPipeline.GetBuildTargetGroup(EditorUserBuildSettings.activeBuildTarget);
            string defineStr = PlayerSettings.GetScriptingDefineSymbolsForGroup(buildTarget);
            List<string> defineList = new List<string>(defineStr.Split(';'));
            defineList.Remove(define.defineFlag);
            defineStr = string.Join(";", defineList.ToArray());
            PlayerSettings.SetScriptingDefineSymbolsForGroup(buildTarget, defineStr);
        }
    }
}
