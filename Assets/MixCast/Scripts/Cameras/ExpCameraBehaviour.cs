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
using BlueprintReality.MixCast.Data;
using BlueprintReality.MixCast.Interprocess;
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
#if MIXCAST_LWRP 
using UnityEngine.Rendering.LWRP;
#elif MIXCAST_URP
using UnityEngine.Rendering.Universal;
#endif
#if MIXCAST_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace BlueprintReality.MixCast
{
    public class ExpCameraBehaviour : MonoBehaviour
    {
        private static class ShaderNames
        {
            public const string Blit = "Hidden/BPR/Blit";
            public const string BlitAlpha = "Hidden/BPR/AlphaTransfer";

            public const string GrabOpaqueLayer = "Hidden/BPR/OpaqueGrab";
            public const string ResetAfterOpaqueLayer = "Hidden/BPR/PostOpaqueGrab";

            public const string ApplyDepthCutoff = "Hidden/BPR/ApplyDepthCutoff";
        }

        private class CamComponentInfo
        {
#if MIXCAST_LWRP
            public LWRPAdditionalCameraData urpData;
#elif MIXCAST_URP
            public UniversalAdditionalCameraData urpData;
#endif
#if MIXCAST_HDRP
            public HDAdditionalCameraData hdData;
#endif

            public bool hasGrabAlphaCommand = false;


            public CameraClearFlags clearFlags;
            public Color clearColor;
            public int cullingMask;
        }
        public enum RenderMode
        {
            None, FullRender, Foreground
        }

        public const float MaxDepthInCutoffTexture = 65.525f;

        private const string ForegroundCamName = "Foreground Camera";
        private const string FullRenderCamName = "Full Render Camera";

        private const CameraEvent GrabOpaqueEvent = CameraEvent.AfterSkybox;
        private static readonly CameraEvent[] ApplyCutoffOnEvents =
        {
             CameraEvent.BeforeGBuffer,
             CameraEvent.AfterForwardOpaque,
        };
        private const CameraEvent ResetForTransparentsEvent = CameraEvent.AfterImageEffectsOpaque;

#if MIXCAST_HDRP
        private static readonly FrameSettingsField[] DisabledSettings =
        {
            FrameSettingsField.LensDistortion,
        };
#endif

        private static Texture2D clearTex;
        private static Dictionary<string, SharedQueueConsumer<ExpFrame>> cachedFrameQueues = new Dictionary<string, SharedQueueConsumer<ExpFrame>>();
        public static void DisposeAllCachedQueues()
        {
            foreach (SharedQueueConsumer<ExpFrame> queue in cachedFrameQueues.Values)
                queue.Dispose();
            cachedFrameQueues.Clear();
        }

        public static List<ExpCameraBehaviour> ActiveCameras { get; protected set; }
        public static ExpCameraBehaviour CurrentlyRendering { get; protected set; } //Assigned to the MixCastCamera that is being processed between FrameStarted and FrameEnded

        public static event Action<ExpCameraBehaviour> FrameStarted;
        public static event Action<ExpCameraBehaviour> FrameEnded;

        static ExpCameraBehaviour()
        {
            ActiveCameras = new List<ExpCameraBehaviour>();
        }

        public IdentifierContext cameraContext;

        public Transform positionTransform;
        public Transform rotationTransform;

        public bool notifyWhenFrameDropped = false;

        private RenderTexture fullRenderTarget, foregroundTarget;

        public Material TransferOpaqueMat { get; protected set; }
        public Material ResetForTransparentMat { get; protected set; }
        public Material TransferAlphaMat { get; protected set; }
        public Material ApplyCutoffMat { get; protected set; }

        public RenderTexture OpaqueLayerTex { get; protected set; }
        public IntPtr OpaqueLayerTexPtr { get; protected set; }
        public RenderTexture CleanColorTarget { get; protected set; }

        private CommandBuffer fullRenderPostOpaqueCmd;
        private CommandBuffer foregroundApplyCutoffCmd;
        private CommandBuffer foregroundPostOpaqueCmd;

        private CommandBuffer grabAlphaCommand;
        //private CommandBuffer reinjectOpaqueCommand;

        public RenderTexture LayersTexture { get; protected set; }
        public IntPtr LayersTexturePtr { get; protected set; }
        private Material transferResultsMat;


        private List<Camera> renderCameras = new List<Camera>();

        private Dictionary<Camera, CamComponentInfo> subrendererInfo = new Dictionary<Camera, CamComponentInfo>();

        public RenderMode CurrentRenderMode { get; protected set; }

        private SharedQueueConsumer<ExpFrame> requestedFrames;
        private Dictionary<long, WatchedUnitySharedTexture> externalTextureTable = new Dictionary<long, WatchedUnitySharedTexture>();

        private ExpFrame lastReceivedFrame;
        private bool receivedLastFrameTooEarly;
        private bool renderedDuringLastExpFrame;

        public ExpFrame LatestFrameInfo { get; protected set; }
        public uint RenderedFrameCount { get; protected set; }
        public ExpFrameSender FramePipe { get; protected set; }

        protected void Awake()
        {
            if (clearTex == null)
            {
                clearTex = new Texture2D(2, 2);
                clearTex.SetPixels(new Color[] { Color.clear, Color.clear, Color.clear, Color.clear });
                clearTex.Apply();
            }

            TransferOpaqueMat = new Material(Shader.Find(ShaderNames.GrabOpaqueLayer));
            if (QualitySettings.desiredColorSpace == ColorSpace.Gamma)
                TransferOpaqueMat.EnableKeyword("CONVERT_TO_LINEAR");
            ResetForTransparentMat = new Material(Shader.Find(ShaderNames.ResetAfterOpaqueLayer));
            TransferAlphaMat = new Material(Shader.Find(ShaderNames.BlitAlpha));
            ApplyCutoffMat = new Material(Shader.Find(ShaderNames.ApplyDepthCutoff));
            ApplyCutoffMat.SetFloat("_MaxDist", MaxDepthInCutoffTexture);

            fullRenderPostOpaqueCmd = new CommandBuffer() { name = "MixCast-PostOpaque" };
            foregroundApplyCutoffCmd = new CommandBuffer() { name = "MixCast-ApplyCutoff" };
            foregroundPostOpaqueCmd = new CommandBuffer() { name = "MixCast-PostOpaque" };

            grabAlphaCommand = new CommandBuffer() { name = "Get Correct Alpha" };
            //reinjectOpaqueCommand = new CommandBuffer() { name = "Reinject Opaque Layer" };

            transferResultsMat = new Material(Shader.Find(ShaderNames.Blit));
            if (QualitySettings.desiredColorSpace == ColorSpace.Gamma)
                transferResultsMat.EnableKeyword("CONVERT_TO_LINEAR");

            lastReceivedFrame = new ExpFrame();
            LatestFrameInfo = new ExpFrame();

            SpawnSceneLayerCameras();
        }
        void SpawnSceneLayerCameras()
        {
            if (MixCastSdkData.ProjectSettings.layerCamPrefab != null)
                renderCameras = SpawnLayerCameraFromPrefab();
            else
                renderCameras = SpawnLayerCameraFromScratch("Render Cam");
        }
        List<Camera> SpawnLayerCameraFromPrefab()
        {
            GameObject spawnedCamObj = Instantiate(MixCastSdkData.ProjectSettings.layerCamPrefab);

            spawnedCamObj.transform.SetParent(rotationTransform != null ? rotationTransform : (positionTransform != null ? positionTransform : transform));
            spawnedCamObj.transform.localPosition = Vector3.zero;
            spawnedCamObj.transform.localRotation = Quaternion.identity;
            spawnedCamObj.transform.localScale = Vector3.one;

            List<Camera> camList = new List<Camera>();
            camList.AddRange(spawnedCamObj.GetComponentsInChildren<Camera>(true));
            camList.Sort((x, y) => x.depth.CompareTo(y.depth));

            for (int i = 0; i < camList.Count; i++)
            {
                Camera newCam = camList[i];
                CamComponentInfo newInfo = SetupCameraComponent(newCam);
                subrendererInfo.Add(newCam, newInfo);
            }

            return camList;
        }
        List<Camera> SpawnLayerCameraFromScratch(string rootName)
        {
            GameObject newCamObj = new GameObject(rootName)
            {
                //hideFlags = HideFlags.HideAndDontSave
            };

            newCamObj.transform.SetParent(rotationTransform != null ? rotationTransform : (positionTransform != null ? positionTransform : transform));
            newCamObj.transform.localPosition = Vector3.zero;
            newCamObj.transform.localRotation = Quaternion.identity;
            newCamObj.transform.localScale = Vector3.one;


            Camera newCam = newCamObj.AddComponent<Camera>();
            newCam.depth = 1;

            SetCameraParametersFromMainCamera paramCopier = newCamObj.AddComponent<SetCameraParametersFromMainCamera>();
            paramCopier.clearSettings = true;
            paramCopier.clippingPlanes = true;
            paramCopier.cullingMask = true;
            paramCopier.hdr = true;

            CamComponentInfo newInfo = SetupCameraComponent(newCam);
            subrendererInfo.Add(newCam, newInfo);

            return new List<Camera>() { newCam };
        }

        CamComponentInfo SetupCameraComponent(Camera newCam)
        {
            newCam.stereoTargetEye = StereoTargetEyeMask.None;
            newCam.depthTextureMode = DepthTextureMode.Depth;
            newCam.enabled = false;

            CamComponentInfo newInfo = new CamComponentInfo();

#if MIXCAST_LWRP
            newInfo.urpData = newCam.GetComponent<LWRPAdditionalCameraData>();
            if (newInfo.urpData == null)
                newInfo.urpData = newCam.gameObject.AddComponent<LWRPAdditionalCameraData>();
            newInfo.urpData.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
#elif MIXCAST_URP
            newInfo.urpData = newCam.GetComponent<UniversalAdditionalCameraData>();
            if (newInfo.urpData == null)
                newInfo.urpData = newCam.gameObject.AddComponent<UniversalAdditionalCameraData>();
            newInfo.urpData.requiresDepthOption = CameraOverrideOption.UsePipelineSettings;
    #if MIXCAST_URP_V2 || MIXCAST_URP_V3 || MIXCAST_URP_V4
            newInfo.urpData.allowXRRendering = false;
    #endif
#endif
#if MIXCAST_HDRP
            newInfo.hdData = newCam.GetComponent<HDAdditionalCameraData>();
            if (newInfo.hdData == null)
                newInfo.hdData = newCam.gameObject.AddComponent<HDAdditionalCameraData>();
            newInfo.hdData.xrRendering = false;

            newInfo.hdData.customRenderingSettings = true;
            for (int j = 0; j < DisabledSettings.Length; j++)
            {
                newInfo.hdData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)DisabledSettings[j]] = true;
                newInfo.hdData.renderingPathCustomFrameSettings.SetEnabled(DisabledSettings[j], false);
            }
            newInfo.hdData.renderingPathCustomFrameSettingsOverrideMask.mask[(uint)FrameSettingsField.CustomPass] = true;
            newInfo.hdData.renderingPathCustomFrameSettings.SetEnabled(FrameSettingsField.CustomPass, true); //must have custom passes enabled
#endif

            return newInfo;
        }

        protected void OnEnable()
        {
            if (string.IsNullOrEmpty(cameraContext.Identifier))
                return;

            if(!cachedFrameQueues.TryGetValue(cameraContext.Identifier, out requestedFrames))
                requestedFrames = cachedFrameQueues[cameraContext.Identifier] = new SharedQueueConsumer<ExpFrame>(string.Format("RequestedExpFrames({0})", cameraContext.Identifier));
            FramePipe = new ExpFrameSender(cameraContext.Identifier);

            ActiveCameras.Add(this);
        }
        protected void OnDisable()
        {
            if (!ActiveCameras.Remove(this))
                return;

            foreach (var kvp in externalTextureTable)
                kvp.Value.Dispose();
            externalTextureTable.Clear();

            FramePipe.Dispose();

            ReleaseOutput();
        }

        protected void BuildOutput(int targetWidth, int targetHeight, bool forwardRendering)
        {
            fullRenderTarget = CreateLayerRenderTarget(targetWidth, targetHeight, true, forwardRendering);
            foregroundTarget = CreateLayerRenderTarget(targetWidth, targetHeight, true, forwardRendering);

            RenderTextureFormat sharedTexFmt = SystemInfo.graphicsDeviceType == GraphicsDeviceType.Direct3D12 ?
                RenderTextureFormat.ARGBHalf : RenderTextureFormat.ARGBFloat;   //workaround for DX12 sharing issue
            LayersTexture = new RenderTexture(targetWidth, targetHeight * 2, 0, sharedTexFmt, RenderTextureReadWrite.Linear)
            {
                useMipMap = false,
#if UNITY_5_5_OR_NEWER
                autoGenerateMips = false,
#else
                generateMips = false,
#endif
            };
            LayersTexture.Create();
            LayersTexturePtr = LayersTexture.GetNativeTexturePtr();

            if (MixCastSdkData.ProjectSettings.grabUnfilteredAlpha)
            {
                CleanColorTarget = CreateLayerAlphaTarget(targetWidth, targetHeight);

                grabAlphaCommand.Clear();
                grabAlphaCommand.Blit(BuiltinRenderTextureType.CurrentActive, CleanColorTarget/*, TransferAlphaMat*/);
            }
        }
        RenderTexture CreateLayerRenderTarget(int width, int height, bool depth, bool antialiased)
        {
            return new RenderTexture(width, height, 24, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
            {
                antiAliasing = antialiased ? CalculateAntiAliasingValueForCamera() : 1,
                useMipMap = false,
#if UNITY_5_5_OR_NEWER
                autoGenerateMips = false,
#else
                generateMips = false,
#endif
            };
        }
        private static int CalculateAntiAliasingValueForCamera()
        {
            if (MixCastSdkData.ProjectSettings.overrideQualitySettingsAA)
                return 1 << MixCastSdkData.ProjectSettings.overrideAntialiasingVal;    //{unity-antialiasing-units} === 2^{saved-units}
            else
                return Mathf.Max(QualitySettings.antiAliasing, 1);  //Disabled can equal 0 rather than 1
        }
        RenderTexture CreateLayerAlphaTarget(int width, int height)
        {
            return new RenderTexture(width, height, 0, RenderTextureFormat.ARGBFloat, RenderTextureReadWrite.Linear)
            {
                useMipMap = false,
#if UNITY_5_5_OR_NEWER
                autoGenerateMips = false,
#else
                generateMips = false,
#endif
            };
        }

        protected void ReleaseOutput()
        {
            for (int i = 0; i < renderCameras.Count; i++)
                SetCameraAlphaCommandAttached(renderCameras[i], false);

            if (CleanColorTarget != null)
            {
                CleanColorTarget.Release();
                CleanColorTarget = null;
            }
            if (fullRenderTarget != null)
            {
                fullRenderTarget.Release();
                fullRenderTarget = null;
            }
            if (foregroundTarget != null)
            {
                foregroundTarget.Release();
                foregroundTarget = null;
            }
            AllocateOpaqueTargetIfNeeded(false);

            if (LayersTexture != null)
            {
                LayersTexture.Release();
                LayersTexture = null;
            }
        }

        protected void LateUpdate()
        {
            if (RenderedFrameCount == 0)
                return;

            if (positionTransform != null)
                positionTransform.localPosition = LatestFrameInfo.camPos;
            if (rotationTransform != null)
                rotationTransform.localRotation = LatestFrameInfo.camRot;
        }

        public void RenderIfNeeded()
        {
            ulong curTime = MixCastTimestamp.Get();
            ulong syncWindowEnd = curTime + (ulong)(0.65f * ExpCameraScheduler.AverageMsPerFrame * 10000);

            bool haveFrameToRender = false;

            if (receivedLastFrameTooEarly)
            {
                if (lastReceivedFrame.syncTime <= syncWindowEnd)
                {
                    receivedLastFrameTooEarly = false;
                    LatestFrameInfo.CopyFrom(lastReceivedFrame);
                    ulong syncWindowStart = syncWindowEnd - (ulong)(MixCastTimestamp.TicksPerSecond / LatestFrameInfo.camFramerate);

                    if (LatestFrameInfo.syncTime >= syncWindowStart)
                        haveFrameToRender = true;
                    else
                    {
                        if (notifyWhenFrameDropped)
                            Debug.LogWarning(string.Format("Too late to render held frame {0}, missed by {1:F2}ms",
                                LatestFrameInfo.frameIndex,
                                1000 * (float)(syncWindowStart - LatestFrameInfo.syncTime) / MixCastTimestamp.TicksPerSecond));
                        requestedFrames.MarkEmptied();
                    }
                }
            }

            if (!receivedLastFrameTooEarly)
            {
                while (!haveFrameToRender && requestedFrames.WaitUntilNextFilled(0))
                {
                    requestedFrames.Read(ref lastReceivedFrame);

                    if (lastReceivedFrame.syncTime > syncWindowEnd)
                    {
                        receivedLastFrameTooEarly = true;
                        break;
                    }

                    LatestFrameInfo.CopyFrom(lastReceivedFrame); //keep the last frame's data regardless of rendering
                    ulong syncWindowStart = syncWindowEnd - (ulong)(MixCastTimestamp.TicksPerSecond / LatestFrameInfo.camFramerate);

                    if (LatestFrameInfo.syncTime < syncWindowStart)
                    {
                        if (notifyWhenFrameDropped)
                            Debug.LogWarning(string.Format("Too late to render new frame {0}, missed by {1:F2}ms, was {2} last frame",
                                LatestFrameInfo.frameIndex,
                                1000 * (float)(syncWindowStart - LatestFrameInfo.syncTime) / MixCastTimestamp.TicksPerSecond,
                                renderedDuringLastExpFrame ? "BUSY" : "FREE"));
                        requestedFrames.MarkEmptied();
                        continue;
                    }

                    haveFrameToRender = true;
                }
            }

            if (!haveFrameToRender)
            {
                renderedDuringLastExpFrame = false;
                return;
            }

            Render();
            requestedFrames.InsertEmptiedFence();
            renderedDuringLastExpFrame = true;
        }

        void Render()
        {
            if (LayersTexture != null)
            {
                if (LatestFrameInfo.camWidth != fullRenderTarget.width || LatestFrameInfo.camHeight != fullRenderTarget.height)
                    ReleaseOutput();
            }
            if (LayersTexture == null)
                BuildOutput((int)LatestFrameInfo.camWidth, (int)LatestFrameInfo.camHeight, renderCameras[0].actualRenderingPath == RenderingPath.Forward);

            if (positionTransform != null)
                positionTransform.localPosition = LatestFrameInfo.camPos;
            if (rotationTransform != null)
                rotationTransform.localRotation = LatestFrameInfo.camRot;

            CurrentlyRendering = this;
            if (FrameStarted != null)
                FrameStarted(this);


            bool separateOpaque = LatestFrameInfo.HasCamFlag(ExpCamFlagBit.SeparateOpaque);
            if (LatestFrameInfo.renderFull)
            {
                AllocateOpaqueTargetIfNeeded(separateOpaque);
                RenderBackground(separateOpaque);
                if (separateOpaque)
                    RenderOpaquePostProcessing();
            }

            if (LatestFrameInfo.renderForeground)
                RenderForeground(separateOpaque);

            AtlasLayers(LatestFrameInfo.renderFull, LatestFrameInfo.renderForeground && externalTextureTable[LatestFrameInfo.occlusionTex.ToInt64()].Texture != null);

            if (FrameEnded != null)
                FrameEnded(this);
            CurrentlyRendering = null;

            Graphics.SetRenderTarget(null);

            RenderedFrameCount++;
        }

        void RenderBackground(bool separateOpaque)
        {
            CurrentRenderMode = RenderMode.FullRender;

            for (int i = 0; i < renderCameras.Count; i++)
            {
                renderCameras[i].depthTextureMode = separateOpaque ? DepthTextureMode.Depth : DepthTextureMode.None;
#if MIXCAST_LWRP || MIXCAST_URP
                subrendererInfo[renderCameras[i]].urpData.requiresDepthOption = separateOpaque ? CameraOverrideOption.On : CameraOverrideOption.UsePipelineSettings;
#endif
                if (separateOpaque)
                    renderCameras[i].AddCommandBuffer(GrabOpaqueEvent, fullRenderPostOpaqueCmd);
            }

            RenderCameraStack(fullRenderTarget, LatestFrameInfo.camFoV, 
                LatestFrameInfo.HasCamFlag(ExpCamFlagBit.Translucent), LatestFrameInfo.HasCamFlag(ExpCamFlagBit.Translucent));

            for (int i = 0; i < renderCameras.Count; i++)
            {
                if (separateOpaque)
                    renderCameras[i].RemoveCommandBuffer(GrabOpaqueEvent, fullRenderPostOpaqueCmd);
            }

            CurrentRenderMode = RenderMode.None;
        }

        void RenderForeground(bool separateOpaque)
        {
            CurrentRenderMode = RenderMode.Foreground;

            ApplyCutoffMat.SetFloat("_PlayerScale", transform.TransformVector(Vector3.forward).magnitude);

            WatchedUnitySharedTexture occlusionTex;
            if (externalTextureTable.TryGetValue(LatestFrameInfo.occlusionTex.ToInt64(), out occlusionTex))
            {
                if (occlusionTex.Texture != null)
                {
                    if (occlusionTex.Texture.width != LatestFrameInfo.camWidth || occlusionTex.Texture.height != LatestFrameInfo.camHeight)
                    {
                        occlusionTex.Dispose();
                        occlusionTex = null;
                        externalTextureTable.Remove(LatestFrameInfo.occlusionTex.ToInt64());
                    }
                }
            }
            if (occlusionTex == null)
            {
                occlusionTex = new WatchedUnitySharedTexture(MixCastSdkBehaviour.Instance.ClientProc.Handle, LatestFrameInfo.occlusionTex);
                externalTextureTable.Add(LatestFrameInfo.occlusionTex.ToInt64(), occlusionTex);
            }

            occlusionTex.AcquireSync();
            if (occlusionTex.Texture != null)
            {
                ApplyCutoffMat.SetTexture("_DepthTex", occlusionTex.Texture);
                foregroundApplyCutoffCmd.Blit(Texture2D.whiteTexture, BuiltinRenderTextureType.CameraTarget, ApplyCutoffMat);    //actual depth texture will be applied not as _MainTex
                if (separateOpaque)
                    foregroundPostOpaqueCmd.Blit(Texture2D.blackTexture, BuiltinRenderTextureType.CameraTarget, ResetForTransparentMat);

                for (int i = 0; i < renderCameras.Count; i++)
                {
                    for (int j = 0; j < ApplyCutoffOnEvents.Length; j++)
                        renderCameras[i].AddCommandBuffer(ApplyCutoffOnEvents[j], foregroundApplyCutoffCmd);

                    if (separateOpaque)
                        renderCameras[i].AddCommandBuffer(ResetForTransparentsEvent, foregroundPostOpaqueCmd);
                }

                bool needsAlpha = LatestFrameInfo.renderFull || LatestFrameInfo.HasCamFlag(ExpCamFlagBit.Translucent);
                RenderCameraStack(foregroundTarget, LatestFrameInfo.camFoV,
                    needsAlpha, false);

                for (int i = 0; i < renderCameras.Count; i++)
                {
                    for (int j = 0; j < ApplyCutoffOnEvents.Length; j++)
                        renderCameras[i].RemoveCommandBuffer(ApplyCutoffOnEvents[j], foregroundApplyCutoffCmd);

                    if (separateOpaque)
                        renderCameras[i].RemoveCommandBuffer(ResetForTransparentsEvent, foregroundPostOpaqueCmd);
                }

                foregroundApplyCutoffCmd.Clear();
                foregroundPostOpaqueCmd.Clear();
            }

            occlusionTex.ReleaseSync();
            CurrentRenderMode = RenderMode.None;
        }

        void RenderCameraStack(RenderTexture target, float fieldOfView, bool needsAlpha, bool forceClearBackground)
        {
            RenderTexture.active = target;
            GL.Clear(true, true, Color.clear);
            RenderTexture.active = null;

            bool bgIsTranslucent = LatestFrameInfo.HasCamFlag(ExpCamFlagBit.Translucent);
#if MIXCAST_HDRP
            bool isHdrp = (RenderPipelineManager.currentPipeline is HDRenderPipeline);
#else
            bool isHdrp = false;
#endif
#if MIXCAST_URP
            bool isUrp = (RenderPipelineManager.currentPipeline is UniversalRenderPipeline);
#endif

            float aspect = (float)target.width / target.height;

            //set up attributes of cameras before calling Render on any
            for (int i = 0; i < renderCameras.Count; i++)
            {
                Camera renderCamera = renderCameras[i];
                if (!renderCamera.gameObject.activeInHierarchy)
                    continue;

                CamComponentInfo camInfo = subrendererInfo[renderCamera];
#if MIXCAST_URP
                bool isOverlayCam = isUrp && camInfo.urpData.renderType == CameraRenderType.Overlay;
#else
                bool isOverlayCam = false;
#endif
                renderCamera.enabled = isOverlayCam;
                
                renderCamera.targetTexture = target;
                if (!Mathf.Approximately(renderCamera.aspect, aspect))
                    renderCamera.aspect = aspect;

                if (!Mathf.Approximately(renderCamera.fieldOfView, fieldOfView))
                    renderCamera.fieldOfView = fieldOfView;

                camInfo.cullingMask = renderCamera.cullingMask;
                LayerMask includeMask = bgIsTranslucent ? MixCastSdkData.ProjectSettings.transparentOnlyBgLayers : MixCastSdkData.ProjectSettings.opaqueOnlyBgLayers;
                LayerMask excludeMask = bgIsTranslucent ? MixCastSdkData.ProjectSettings.opaqueOnlyBgLayers : MixCastSdkData.ProjectSettings.transparentOnlyBgLayers;
                renderCamera.cullingMask = (renderCamera.cullingMask | includeMask) & ~excludeMask;

                camInfo.clearFlags = renderCamera.clearFlags;
                camInfo.clearColor = renderCamera.backgroundColor;
                if (forceClearBackground &&
                    (renderCamera.clearFlags == CameraClearFlags.Color || renderCamera.clearFlags == CameraClearFlags.Skybox))
                {
                    renderCamera.clearFlags = CameraClearFlags.Color;
                    renderCamera.backgroundColor = Color.clear;
                }
            }

            //Trigger .Render() on cameras that need it called
            for (int i = 0; i < renderCameras.Count; i++)
            {
                Camera renderCamera = renderCameras[i];
                if (!renderCamera.gameObject.activeInHierarchy || renderCamera.enabled) //.enabled signals that rendering is triggered without needing to call .Render()
                    continue;

                if (MixCastSdkData.ProjectSettings.grabUnfilteredAlpha)
                    SetCameraAlphaCommandAttached(renderCamera, needsAlpha && !isHdrp);

                renderCamera.Render();

                if (MixCastSdkData.ProjectSettings.grabUnfilteredAlpha && needsAlpha && !isHdrp)
                    Graphics.Blit(CleanColorTarget, renderCamera.targetTexture, TransferAlphaMat);
            }

            //Return fields to existing values
            for( int i = 0; i < renderCameras.Count; i++ )
            {
                Camera renderCamera = renderCameras[i];
                if (!renderCamera.gameObject.activeInHierarchy || renderCamera.enabled) //.enabled signals that rendering is triggered without needing to call .Render
                    continue;

                CamComponentInfo camInfo = subrendererInfo[renderCamera];

                renderCameras[i].targetTexture = null;

                renderCamera.cullingMask = camInfo.cullingMask;
                if (forceClearBackground)
                {
                    renderCamera.backgroundColor = subrendererInfo[renderCamera].clearColor;
                    renderCamera.clearFlags = subrendererInfo[renderCamera].clearFlags;
                }
            }
        }

        void RenderOpaquePostProcessing()
        {
            for (int i = 0; i < renderCameras.Count; i++)
            {
                Camera renderCamera = renderCameras[i];
                if (!renderCamera.gameObject.activeInHierarchy)
                    continue;

                renderCamera.targetTexture = OpaqueLayerTex;
                int oldCullingMask = renderCamera.cullingMask;
                renderCamera.cullingMask = 0;
                renderCamera.clearFlags = CameraClearFlags.Nothing;

                renderCamera.depthTextureMode = DepthTextureMode.None;
#if MIXCAST_LWRP || MIXCAST_URP
                subrendererInfo[renderCamera].urpData.requiresDepthOption = CameraOverrideOption.Off;
#endif

                //renderCamera.AddCommandBuffer(GrabOpaqueEvent, reinjectOpaqueCommand);
                renderCamera.Render();
                //renderCamera.RemoveCommandBuffer(GrabOpaqueEvent, reinjectOpaqueCommand);

                renderCamera.backgroundColor = subrendererInfo[renderCamera].clearColor;
                renderCamera.clearFlags = subrendererInfo[renderCamera].clearFlags;
                renderCamera.cullingMask = oldCullingMask;
                renderCamera.targetTexture = null;
            }
        }

        void SetCameraAlphaCommandAttached(Camera cam, bool attach)
        {
            CamComponentInfo camInfo = subrendererInfo[cam];
            if (camInfo.hasGrabAlphaCommand == attach)
                return;

            if (attach)
                cam.AddCommandBuffer(CameraEvent.AfterForwardAlpha, grabAlphaCommand);
            else
                cam.RemoveCommandBuffer(CameraEvent.AfterForwardAlpha, grabAlphaCommand);
            camInfo.hasGrabAlphaCommand = attach;
        }

        void AllocateOpaqueTargetIfNeeded(bool needed)
        {
            if (needed && OpaqueLayerTex == null)
            {
                OpaqueLayerTex = new RenderTexture((int)LatestFrameInfo.camWidth, (int)LatestFrameInfo.camHeight, 0, LayersTexture.format, RenderTextureReadWrite.Linear)
                {
                    useMipMap = false,
#if UNITY_5_5_OR_NEWER
                    autoGenerateMips = false,
#else
                    generateMips = false,
#endif
                };
                OpaqueLayerTex.name = "MixCast Opaque Layer";
                OpaqueLayerTex.Create();
                OpaqueLayerTexPtr = OpaqueLayerTex.GetNativeTexturePtr();

                fullRenderPostOpaqueCmd.Blit(BuiltinRenderTextureType.CameraTarget, OpaqueLayerTex, TransferOpaqueMat);
                fullRenderPostOpaqueCmd.Blit(Texture2D.blackTexture, BuiltinRenderTextureType.CameraTarget, ResetForTransparentMat);

                //reinjectOpaqueCommand.Blit(OpaqueLayerTex, BuiltinRenderTextureType.CameraTarget);
            }
            else if (!needed && OpaqueLayerTex != null)
            {
                OpaqueLayerTex.Release();
                OpaqueLayerTex = null;

                fullRenderPostOpaqueCmd.Clear();
                foregroundPostOpaqueCmd.Clear();

                //reinjectOpaqueCommand.Clear();
            }
        }

        void AtlasLayers(bool haveFullRender, bool haveForeground)
        {
            GL.PushMatrix();

            RenderTexture.active = LayersTexture;
            GL.LoadPixelMatrix(0, 1, 2, 0);

            if (haveFullRender)
                Graphics.DrawTexture(new Rect(0, 0, 1, 1), fullRenderTarget, transferResultsMat);
            if (haveForeground)
                Graphics.DrawTexture(new Rect(0, 1, 1, 1), foregroundTarget, transferResultsMat);

            RenderTexture.active = null;
            GL.PopMatrix();
        }
    }
}
#endif
