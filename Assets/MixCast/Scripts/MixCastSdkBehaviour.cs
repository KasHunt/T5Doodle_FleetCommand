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
using UnityEngine;
using System.IO;
using Thrift.Configuration;
#if MIXCAST_LWRP || MIXCAST_URP || MIXCAST_HDRP
using UnityEngine.Rendering;
#endif
#if MIXCAST_LWRP 
using UnityEngine.Rendering.LWRP;
#endif
#if MIXCAST_URP
using UnityEngine.Rendering.Universal;
#endif
#if MIXCAST_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif
#if UNITY_STANDALONE_WIN
using Thrift.Unity;
using BlueprintReality.MixCast.Data;
using BlueprintReality.MixCast.Experience;
using BlueprintReality.MixCast.Thrift;
using BlueprintReality.MixCast.Viewfinders;
using BlueprintReality.MixCast.Cameras;
using System.Reflection;
using System.Collections.Generic;
#endif

namespace BlueprintReality.MixCast
{
    public class MixCastSdkBehaviour : MonoBehaviour
    {
        public bool reparentToSceneRootOnStart = true;

#if UNITY_STANDALONE_WIN
        public static MixCastSdkBehaviour Instance { get; protected set; }
        public static void EnsureExists()
        {
            if (Instance == null && FindObjectOfType<MixCastSdkBehaviour>() == null)
            {
                GameObject prefab = Resources.Load<GameObject>("Prefabs/MixCast SDK");
                GameObject go = Instantiate(prefab);
                go.name = prefab.name;
            }
        }

        public SdkCustomTrackedObjectMediator VirtualTrackedObjectManager { get; protected set; }

        public SDK_Service.Client ClientConnection { get; protected set; }

        public System.Diagnostics.Process ClientProc { get; protected set; }

        public bool Initialized { get; protected set; }

        private Client.ServiceMutexCheck serviceCheck;
        private bool lostService;


#if MIXCAST_LWRP || MIXCAST_URP || MIXCAST_HDRP
        private RenderPipeline lastRenderPipeline;
#endif

        private void Awake()
        {
            SdkSharedTextureReceiver.ApplyOverrideToCreateFunc();
        }
        private void OnEnable()
        {
            Initialized = false;
            StartCoroutine(Initialize());
        }

        private IEnumerator Initialize()
        {
            bool cantActivate = false;

            if (Instance != null)
                cantActivate = true;

#if UNITY_EDITOR
            if (!MixCastSdkData.ProjectSettings.enableMixCastInEditor)
                cantActivate = true;
#else
            if (MixCastSdk.CommandLineArgBlockingActivation())
                cantActivate = true;
#endif

            if (IntPtr.Size == 4)
            {
                cantActivate = true;
                Debug.LogWarning("MixCast is only compatible with 64 bit applications");
            }

            if (cantActivate)
            {
                enabled = false;
                yield break;
            }

            if (reparentToSceneRootOnStart)
                transform.parent = null;

            if (transform.parent == null)
                DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;

            Instance = this;

            SetExperienceInfo();

            gameObject.AddComponent<BlueprintReality.SharedTextures.SharedTexturePlugin>();
            if (FindObjectOfType<Interprocess.MixCastInteropPlugin>() == null)
                gameObject.AddComponent<Interprocess.MixCastInteropPlugin>();

            GameObject thriftRoot = new GameObject("Thrift") { hideFlags = HideFlags.HideInHierarchy };
            thriftRoot.transform.SetParent(transform);
            UnityThriftBase.GroupTransform = thriftRoot.transform;

            InitializeThriftConnections();

#if MIXCAST_HDRP
            gameObject.AddComponent<MixCastHdrpDefaultCustomPassVolume>();
#endif

            VirtualTrackedObjectManager = new SdkCustomTrackedObjectMediator();

            if (FindObjectOfType<ExpCameraSpawner>() == null)
            {
                GameObject camerasObj = new GameObject("Cameras");
                camerasObj.transform.parent = transform;
                camerasObj.transform.localPosition = Vector3.zero;
                camerasObj.transform.localRotation = Quaternion.identity;
                camerasObj.transform.localScale = Vector3.one;
                camerasObj.AddComponent<ExpCameraSpawner>();
            }
            if (FindObjectOfType<ExpVideoInputSpawner>() == null)
            {
                GameObject videoInputObj = new GameObject("VideoInputs");
                videoInputObj.transform.parent = transform;
                videoInputObj.transform.localPosition = Vector3.zero;
                videoInputObj.transform.localRotation = Quaternion.identity;
                videoInputObj.transform.localScale = Vector3.one;
                videoInputObj.AddComponent<ExpVideoInputSpawner>();
            }
            if (FindObjectOfType<ExpViewfinderSpawner>() == null)
            {
                GameObject camerasObj = new GameObject("Viewfinders");
                camerasObj.transform.parent = transform;
                camerasObj.transform.localPosition = Vector3.zero;
                camerasObj.transform.localRotation = Quaternion.identity;
                camerasObj.transform.localScale = Vector3.one;
                camerasObj.AddComponent<ExpViewfinderSpawner>();
            }

            MixCastSdk.OnActiveChanged += HandleActiveChanged;
            XrPlatformInfo.OnPlatformLoaded += HandleXrPlatformChanged;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded += HandleSceneLoaded;

            Initialized = true;

            yield return null;

            ClientConnection.TryNotifySdkStarted(MixCastSdkData.ExperienceInfo);
        }

        private void SetExperienceInfo()
        {
            MixCastSdkData.ExperienceInfo.MixcastVersion = MixCastSdk.VERSION_STRING;

            MixCastSdkData.ExperienceInfo.ExperienceExePath = UnityInfo.GetExePath();
            MixCastSdkData.ExperienceInfo.MainWindowHandle = (long)UnityInfo.GetMainWindowHandle();
            MixCastSdkData.ExperienceInfo.MainProcessId = UnityInfo.GetProcessId();

            MixCastSdkData.ExperienceInfo.ProjectId = MixCastSdkData.ProjectSettings.ProjectID;
            MixCastSdkData.ExperienceInfo.ExperienceTitle = Application.productName;
            MixCastSdkData.ExperienceInfo.OrganizationName = Application.companyName;

            MixCastSdkData.ExperienceInfo.EngineVersion = Application.unityVersion;
            MixCastSdkData.ExperienceInfo.GraphicsType = UnityInfo.GetGraphicsType();
            MixCastSdkData.ExperienceInfo.TrackingType = XrPlatformInfo.TrackingSource;

            MixCastSdkData.ExperienceInfo.AlphaIsPremultiplied = MixCastSdkData.ProjectSettings.usingPremultipliedAlpha;
            MixCastSdkData.ExperienceInfo.ColorSpaceIsLinear = QualitySettings.desiredColorSpace == ColorSpace.Linear;

            MixCastSdkData.ExperienceInfo.CanRenderOpaqueBg = MixCastSdkData.ProjectSettings.canRenderOpaqueBg;
            MixCastSdkData.ExperienceInfo.CanRenderTransparentBg = MixCastSdkData.ProjectSettings.canRenderTransparentBg;

            MixCastSdkData.ExperienceInfo.CanRenderSeparateOpaque = !MixCastSdkData.ProjectSettings.doesntOutputGoodDepth;
        }

        private void OnDisable()
        {
            if (!Initialized)
                return;

            MixCastSdk.Active = false;

            MixCastSdk.OnActiveChanged -= HandleActiveChanged;
            XrPlatformInfo.OnPlatformLoaded -= HandleXrPlatformChanged;

            UnityEngine.SceneManagement.SceneManager.sceneLoaded -= HandleSceneLoaded;

            CancelInvoke("VerifyServiceConnection");

            VirtualTrackedObjectManager = null;

#if MIXCAST_HDRP
            Destroy(gameObject.GetComponent<MixCastHdrpDefaultCustomPassVolume>());
#endif

            if (ClientConnection != null)
                ClientConnection.TryNotifySdkStopped();

            Instance = null;
        }

        private void Update()
        {
            if (!Initialized)
                return;

            if (lostService)
            {
                lostService = false;
                MixCastSdk.Active = false;
            }

            VirtualTrackedObjectManager.Update();

#if MIXCAST_LWRP || MIXCAST_URP || MIXCAST_HDRP
            if (MixCastSdk.Active && RenderPipelineManager.currentPipeline != lastRenderPipeline)
                HandleRenderPipelineChanged();
#endif
        }

        void HandleActiveChanged()
        {
            if (MixCastSdk.Active)
            {
                serviceCheck = new Client.ServiceMutexCheck(() => { lostService = true; });
                ClientProc = System.Diagnostics.Process.GetProcessById((int)Service_SDK_Handler.ClientProcId);
            }
            else
            {
                ClientProc = null;
                serviceCheck.Dispose();
                serviceCheck = null;

                ExpCameraBehaviour.DisposeAllCachedQueues();
                ExpFrameSender.DisposeAllCachedQueues();

#if MIXCAST_LWRP || MIXCAST_URP_V1 || MIXCAST_URP_V2 || MIXCAST_URP_V3 || MIXCAST_URP_V4
                MixCastUrpRendererFeature.UnregisterAllRenderers();
#endif

#if MIXCAST_LWRP || MIXCAST_URP || MIXCAST_HDRP
                lastRenderPipeline = null;
#endif
            }
        }

        void HandleXrPlatformChanged()
        {
            MixCastSdkData.ExperienceInfo.TrackingType = XrPlatformInfo.TrackingSource;

            if (MixCastSdk.Active)
                ClientConnection.TryNotifySdkChanged(MixCastSdkData.ExperienceInfo);
        }
#if MIXCAST_LWRP || MIXCAST_URP || MIXCAST_HDRP
        void HandleRenderPipelineChanged()
        {
#if MIXCAST_LWRP || MIXCAST_URP_V1 || MIXCAST_URP_V2 || MIXCAST_URP_V3 || MIXCAST_URP_V4
            MixCastUrpRendererFeature.UnregisterAllRenderers();
#endif
            lastRenderPipeline = RenderPipelineManager.currentPipeline;

            RenderPipelineAsset renderPipelineAsset = null;
#if MIXCAST_LWRP
            renderPipelineAsset = GraphicsSettings.renderPipelineAsset;
#else
            if (QualitySettings.renderPipeline != null)
                renderPipelineAsset = QualitySettings.renderPipeline;
            else
                renderPipelineAsset = GraphicsSettings.currentRenderPipeline;
#endif

            if (renderPipelineAsset == null)
                return;

#if MIXCAST_LWRP
            LightweightRenderPipelineAsset lwrPipelineAsset = renderPipelineAsset as LightweightRenderPipelineAsset;
            if (lwrPipelineAsset != null)
            {
                Type classType = lwrPipelineAsset.GetType();
                FieldInfo rendererDataField = classType.GetField("m_RendererData", BindingFlags.Instance | BindingFlags.NonPublic);

                ForwardRendererData rendererData = rendererDataField.GetValue(lwrPipelineAsset) as ForwardRendererData;
                if (rendererData != null)
                    MixCastUrpRendererFeature.RegisterRenderer(rendererData);
            }
#endif
#if MIXCAST_URP_V1 || MIXCAST_URP_V2 || MIXCAST_URP_V3 || MIXCAST_URP_V4
            UniversalRenderPipelineAsset uniPipelineAsset = renderPipelineAsset as UniversalRenderPipelineAsset;
            if (uniPipelineAsset != null)
            {
                Type classType = uniPipelineAsset.GetType();
                FieldInfo rendererDataListField = classType.GetField("m_RendererDataList",
                    BindingFlags.Instance | BindingFlags.NonPublic);

                IReadOnlyList<ScriptableRendererData> rendererDatas = rendererDataListField.GetValue(uniPipelineAsset) as IReadOnlyList<ScriptableRendererData>;
                for (int i = 0; i < rendererDatas.Count; i++)
                {
#if MIXCAST_URP_V4
                    UniversalRendererData rendererData = rendererDatas[i] as UniversalRendererData;
#else
                    ForwardRendererData rendererData = rendererDatas[i] as ForwardRendererData;
#endif
                    if (rendererData != null)
                        MixCastUrpRendererFeature.RegisterRenderer(rendererData);
                }
            }
#endif
#if MIXCAST_HDRP
            if (renderPipelineAsset is HDRenderPipelineAsset hdPipelineAsset)
            {
                if (hdPipelineAsset.currentPlatformRenderPipelineSettings.colorBufferFormat == RenderPipelineSettings.ColorBufferFormat.R11G11B10)
                    Debug.LogError(string.Format("The MixCast SDK requires that the HDRP color buffer format includes an alpha channel but {0} uses R11G11B10", renderPipelineAsset.name));

                if (!hdPipelineAsset.currentPlatformRenderPipelineSettings.supportCustomPass)
                    Debug.LogError(string.Format("The MixCast SDK requires Custom Passes enabled for HDRP support, but {0} has it disabled. MixCast won't operate correctly until this is resolved", renderPipelineAsset.name));
            }
#endif
        }
#endif

        private void HandleSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
        {
            MixCastSdk.SendCustomEvent("sceneLoaded(" + scene.name + ")");
        }

        private void InitializeThriftConnections()
        {
            ClientConnection = UnityThriftMixCastClient.Get<SDK_Service.Client>(
                "SDK_SERVICE",
                ServerPriority.High,
                ServerType.ThreadPool,
                TransportType.NamedPipe,
                ProtocolType.Binary,
                5000,
                500
                );

            Service_SDK_Handler handler = UnityThriftMixCastServer.Get<Service_SDK_Handler>(
                "SERVICE_SDK",
                ServerPriority.High,
                ServerType.ThreadPool,
                TransportType.NamedPipe,
                ProtocolType.Binary,
                5000,
                500
                );
            handler.MarkOnewayMessageAsImportant("UpdateCameraMetadata");
            handler.MarkOnewayMessageAsImportant("UpdateVideoInputMetadata");
            handler.MarkOnewayMessageAsImportant("UpdateViewfinderMetadata");
            handler.MaxOnewayMsgQueueAllowed = 18000;

            BlueprintReality.SharedTextures.SharedTexturePlugin.manualThriftAddress = "SHAREDTEXTURECOMMUNICATION";
            UnityThriftMixCastClient.Get<BlueprintReality.Thrift.SharedTextures.SharedTextureCommunication.Client>(
                "SHAREDTEXTURECOMMUNICATION",
                ServerPriority.High,
                ServerType.ThreadPool,
                TransportType.NamedPipe,
                ProtocolType.Binary,
                5000,
                500
                );
        }
#endif
    }
}
