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
#if MIXCAST_LWRP 
using UnityEngine.Rendering;
using UnityEngine.Rendering.LWRP;
#elif MIXCAST_URP
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
#endif

namespace BlueprintReality.MixCast
{
    [ExecuteInEditMode]
    [RequireComponent(typeof(Camera))]
    public class SetCameraParametersFromMainCamera : MonoBehaviour
    {
        public bool clearSettings = true;
        public bool cullingMask = true;
        public LayerMask forceExcluded = 0;
        public LayerMask forceIncluded = 0;
        public bool clippingPlanes = true;
        public bool hdr = true;
#if MIXCAST_LWRP || MIXCAST_URP
        public bool postProcessing = true;
#endif
#if MIXCAST_URP
        public bool antiAliasing = true;
#endif

        private Camera mainCam;
        private Camera cam;

#if MIXCAST_LWRP
        private LWRPAdditionalCameraData mainCamLwrp;
        private LWRPAdditionalCameraData camLwrp;
#elif MIXCAST_URP
        private UniversalAdditionalCameraData mainCamUrp;
        private UniversalAdditionalCameraData camUrp;
#endif

        private void Awake()
        {
            cam = GetComponent<Camera>();
#if MIXCAST_LWRP
            if (GraphicsSettings.renderPipelineAsset is LightweightRenderPipelineAsset)
                camLwrp = cam.GetComponent<LWRPAdditionalCameraData>();
#elif MIXCAST_URP
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset)
                camUrp = cam.GetUniversalAdditionalCameraData();
#endif
            LateUpdate();
        }
        private void LateUpdate()
        {
            if (mainCam == null || !mainCam.isActiveAndEnabled)
            {
                mainCam = UnityInfo.FindPrimaryUserCamera();
#if MIXCAST_LWRP
                if (camLwrp != null && mainCam != null)
                {
                    mainCamLwrp = mainCam.GetComponent<LWRPAdditionalCameraData>();
                }
                else
                    mainCamLwrp = null;
#elif MIXCAST_URP
                if (camUrp != null && mainCam != null)
                {
                    mainCamUrp = mainCam.GetUniversalAdditionalCameraData();
                    if(antiAliasing)
                    {
                        camUrp.antialiasing = mainCamUrp.antialiasing;
                        camUrp.antialiasingQuality = mainCamUrp.antialiasingQuality;
                    }
                }
                else
                    mainCamUrp = null;
#endif
            }

            if (mainCam == null)
                return;

            if (clearSettings)
            {
                cam.clearFlags = mainCam.clearFlags;
                cam.backgroundColor = mainCam.backgroundColor;
            }
            if( cullingMask)
            {
                cam.cullingMask = (mainCam.cullingMask | forceIncluded) & ~forceExcluded;
            }
            if (clippingPlanes)
            {
                cam.nearClipPlane = Mathf.Max(0.01f, mainCam.nearClipPlane); 
                cam.farClipPlane = Mathf.Min(1e+36f, mainCam.farClipPlane);
            }
            if( hdr )
            {
#if UNITY_5_6_OR_NEWER
                cam.allowHDR = mainCam.allowHDR;
#else
                cam.hdr = mainCam.hdr;
#endif
            }
#if MIXCAST_LWRP
            if (postProcessing && camLwrp != null)
            {
                camLwrp.requiresColorOption = mainCamLwrp.requiresColorOption;
                camLwrp.requiresDepthOption = mainCamLwrp.requiresDepthOption;
            }
#elif MIXCAST_URP
            if (postProcessing && camUrp != null)
            {
                camUrp.renderPostProcessing = mainCamUrp.renderPostProcessing;
                camUrp.volumeLayerMask = mainCamUrp.volumeLayerMask;
                camUrp.dithering = mainCamUrp.dithering;
                camUrp.requiresColorOption = mainCamUrp.requiresColorOption;
                camUrp.requiresDepthOption = mainCamUrp.requiresDepthOption;
            }
#endif
        }
    }
}
#endif
