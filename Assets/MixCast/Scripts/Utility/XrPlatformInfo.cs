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

using BlueprintReality.MixCast.Shared;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR;
#else
using XRSettings = UnityEngine.VR.VRSettings;
using XRStats = UnityEngine.VR.VRStats;
#endif
#if MIXCAST_XRMAN
using UnityEngine.XR.Management;
#endif

namespace BlueprintReality.MixCast
{
    public class XrPlatformInfo : MonoBehaviour
    {
        private const string OpenVrDeviceName = "OpenVR";
        private const string OculusDeviceName = "Oculus";

        public static TrackingSource TrackingSource
        {
            get
            {
                if (destroyed)
                    return TrackingSource.UNKNOWN;
                if (instance == null)
                {
                    GameObject instanceObj = new GameObject("XrInfo") { hideFlags = HideFlags.HideAndDontSave };
                    DontDestroyOnLoad(instanceObj);
                    instance = instanceObj.AddComponent<XrPlatformInfo>();
                }
                return instance.activeTracking;
            }
        }
        public static event System.Action OnPlatformLoaded;

        private static XrPlatformInfo instance;
        private static bool destroyed = false;


        private TrackingSource activeTracking;

        private bool lastXrSettingsEnabled;
#if MIXCAST_XRMAN
        private XRLoader lastXrLoader;
#endif


        private void Awake()
        {
            instance = this;
            RefreshRuntimeInfo();
        }
        private void OnDestroy()
        {
            destroyed = true;
        }

        private void Update()
        {
            RefreshRuntimeInfo();
        }
        void RefreshRuntimeInfo()
        {
            TrackingSource newPlatform = TrackingSource.UNKNOWN;

#if MIXCAST_TILTFIVE
#if MIXCAST_TILTFIVE_V2
			TiltFive.ISceneInfo sceneInfo;
#endif
			if (
#if MIXCAST_TILTFIVE_V2
                (TiltFive.TiltFiveSingletonHelper.TryGetISceneInfo(out sceneInfo) && sceneInfo.IsActiveAndEnabled())
#else
                TiltFive.TiltFiveManager.Instance.isActiveAndEnabled
#endif
                && TiltFive.Glasses.configured)
			{
                newPlatform = TrackingSource.TILT_FIVE;
                if (newPlatform != activeTracking)
                {
                    activeTracking = newPlatform;
                    if (OnPlatformLoaded != null)
                        OnPlatformLoaded();
                }
                return;
            }
#endif

#if UNITY_2017_1_OR_NEWER
            bool renderedOneFrame = XRSettings.isDeviceActive && (activeTracking != TrackingSource.UNKNOWN || !string.IsNullOrEmpty(XRSettings.loadedDeviceName));
#else
            bool renderedOneFrame = XRStats.gpuTimeLastFrame > 0;
#endif
            bool isReady = XRSettings.enabled && renderedOneFrame;
            bool xrSettingsChanged = lastXrSettingsEnabled != isReady;
            lastXrSettingsEnabled = isReady;

#if MIXCAST_XRMAN
            lastXrLoader = null;
            if (XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null)
            {
                lastXrLoader = XRGeneralSettings.Instance.Manager.activeLoader;
                if(lastXrLoader != null)
                {
                    if (XRGeneralSettings.Instance.Manager.isInitializationComplete)
                    {
                        string loaderName = lastXrLoader.name;
                        if (loaderName.IndexOf("Open VR", System.StringComparison.OrdinalIgnoreCase) != -1)
                            newPlatform = TrackingSource.STEAMVR;
                        else if (loaderName.IndexOf("Oculus", System.StringComparison.OrdinalIgnoreCase) != -1)
                            newPlatform = TrackingSource.OCULUS;
                        else if (loaderName.IndexOf("Open XR", System.StringComparison.OrdinalIgnoreCase) != -1)
                            newPlatform = TrackingSource.OPENXR;
                    }

                    if (activeTracking != newPlatform)
                    {
                        activeTracking = newPlatform;
                        if (OnPlatformLoaded != null)
                            OnPlatformLoaded();
                    }
                    return;
                }
            }
#endif

            if (!lastXrSettingsEnabled)
                newPlatform = TrackingSource.UNKNOWN;
            else
            {
                if (xrSettingsChanged || (lastXrSettingsEnabled && activeTracking == TrackingSource.UNKNOWN))
                {
                    string deviceName = XRSettings.loadedDeviceName;
                    if (deviceName == OpenVrDeviceName)
                        newPlatform = TrackingSource.STEAMVR;
                    else if (deviceName == OculusDeviceName)
                        newPlatform = TrackingSource.OCULUS;
                }
                else
                    newPlatform = activeTracking;
            }

            if (newPlatform != activeTracking)
            {
                activeTracking = newPlatform;
                if (OnPlatformLoaded != null)
                    OnPlatformLoaded();
            }
        }
    }
}
