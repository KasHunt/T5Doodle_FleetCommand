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
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
#if MIXCAST_STEAMVR
using Valve.VR;
#endif

namespace BlueprintReality.MixCast
{
    public class UnityInfo
    {
        public static Shared.ExpGraphicsType GetGraphicsType()
        {
            switch(SystemInfo.graphicsDeviceType)
            {
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D11:
                    return Shared.ExpGraphicsType.DX11;
                case UnityEngine.Rendering.GraphicsDeviceType.Direct3D12:
                    return Shared.ExpGraphicsType.DX12;
                default:
                    return Shared.ExpGraphicsType.UNKNOWN;
            }
        }
        public static string GetExePath()
        {
#if !UNITY_EDITOR
            System.IO.DirectoryInfo appDir = new System.IO.DirectoryInfo(Application.dataPath);
            string exeName = appDir.Name.Substring(0, appDir.Name.Length - "_Data".Length);
            appDir = appDir.Parent;
            return System.IO.Path.Combine(appDir.FullName, exeName + ".exe");
#else
            return UnityEditor.EditorApplication.applicationPath;
#endif
        }
        public static IntPtr GetMainWindowHandle()
        {
            return GetActiveWindow();
        }
        public static uint GetProcessId()
        {
            return GetCurrentProcessId();
        }

        [DllImport("user32.dll")]
        private static extern IntPtr GetActiveWindow();
        [DllImport("kernel32.dll")]
        private static extern uint GetCurrentProcessId();

        public static bool AreAllScenesLoaded()
        {
            for (int i = 0; i < SceneManager.sceneCount; i++)
            {
                if (SceneManager.GetSceneAt(i).isLoaded == false)
                {
                    return false;
                }
            }
            return true;
        }

        public static Camera FindPrimaryUserCamera()
        {
#if MIXCAST_TILTFIVE_V2
            if (XrPlatformInfo.TrackingSource == Shared.TrackingSource.TILT_FIVE)
            {
                TiltFive.ISceneInfo sceneInfo = TiltFive.TiltFiveSingletonHelper.GetISceneInfo();
                return sceneInfo.GetEyeCamera();
            }
#elif MIXCAST_TILTFIVE
            if (XrPlatformInfo.TrackingSource == Shared.TrackingSource.TILT_FIVE)
                return TiltFive.TiltFiveManager.Instance.glassesSettings.headPoseCamera;
#endif

            Camera[] allCams = Camera.allCameras;

#if MIXCAST_STEAMVR
            //Catches SteamVR 1.x.x scenario where template provides 2 cameras, both set to StereoTargetEyeMask.Both and tagged as MainCamera
            for (int i = 0; i < allCams.Length; i++)
            {
                if (allCams[i].GetComponent<SteamVR_Camera>() != null)
                    return allCams[i];
            }
#endif

            for (int i = 0; i < allCams.Length; i++)
            {
                if (!allCams[i].orthographic && allCams[i].stereoTargetEye != StereoTargetEyeMask.None && allCams[i].CompareTag("MainCamera"))
                    return allCams[i];
            }
            for (int i = 0; i < allCams.Length; i++)
            {
                if (!allCams[i].orthographic && allCams[i].stereoTargetEye != StereoTargetEyeMask.None)
                    return allCams[i];
            }

            if (Camera.main != null)
                return Camera.main;

            if (allCams.Length > 0)
                return allCams[0];

            return null;
        }
    }
}
#endif
