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
#if MIXCAST_LWRP || MIXCAST_URP_V1 || MIXCAST_URP_V2 || MIXCAST_URP_V3 || MIXCAST_URP_V4
using BlueprintReality.MixCast.Data;
using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
#if MIXCAST_LWRP
using UnityEngine.Rendering.LWRP;
#elif MIXCAST_URP
using UnityEngine.Rendering.Universal;
#endif

#if MIXCAST_LWRP
using PipelineRendererData = UnityEngine.Rendering.LWRP.ForwardRendererData;
#elif MIXCAST_URP_V1 || MIXCAST_URP_V2 || MIXCAST_URP_V3
using PipelineRendererData = UnityEngine.Rendering.Universal.ForwardRendererData;
#elif MIXCAST_URP_V4
using PipelineRendererData = UnityEngine.Rendering.Universal.UniversalRendererData;
#endif


namespace BlueprintReality.MixCast
{
    public class MixCastUrpRendererFeature : ScriptableRendererFeature
    {
        private class CameraPasses
        {
            public MixCastUrpRenderPasses.AfterOpaquePass AfterOpaque { get; private set; }
            public MixCastUrpRenderPasses.SaveAlphaPass GrabBgAlpha { get; private set; }
            public MixCastUrpRenderPasses.ApplyCutoffPass Cutoff { get; private set; }
            public MixCastUrpRenderPasses.SaveAlphaPass GrabFgAlpha { get; private set; }

            public CameraPasses(ExpCameraBehaviour cam)
            {
                AfterOpaque = new MixCastUrpRenderPasses.AfterOpaquePass(cam);
                GrabBgAlpha = new MixCastUrpRenderPasses.SaveAlphaPass(cam);
                Cutoff = new MixCastUrpRenderPasses.ApplyCutoffPass(cam);
                GrabFgAlpha = new MixCastUrpRenderPasses.SaveAlphaPass(cam);
            }
        }

        private static MixCastUrpRendererFeature instance;
        private static MixCastUrpRendererFeature Instance
        {
            get
            {
                if (instance == null)
                {
                    instance = ScriptableObject.CreateInstance<MixCastUrpRendererFeature>();
                    instance.name = "MixCast Renderer Features";
                }
                return instance;
            }
        }

        private static List<PipelineRendererData> registeredWithRenderers = new List<PipelineRendererData>();
        public static void RegisterRenderer(PipelineRendererData renderer)
        {
            if (!registeredWithRenderers.Contains(renderer))
            {
                renderer.rendererFeatures.Add(Instance);
                MarkDataDirty(renderer);
            }
            registeredWithRenderers.Add(renderer);
        }
        public static void UnregisterRenderer(PipelineRendererData renderer)
        {
            registeredWithRenderers.RemoveAt(registeredWithRenderers.LastIndexOf(renderer));
            if (!registeredWithRenderers.Contains(renderer))
            {
                renderer.rendererFeatures.Remove(Instance);
                MarkDataDirty(renderer);
            }
            if (registeredWithRenderers.Count == 0)
                Destroy(Instance);
        }
        private static void MarkDataDirty(PipelineRendererData renderer)
        {
#if MIXCAST_URP
            renderer.SetDirty();
#else
            PropertyInfo dirtyProp = renderer.GetType().GetProperty("isInvalidated", BindingFlags.Instance | BindingFlags.NonPublic);
            dirtyProp.SetValue(renderer, true);
#endif
        }
        public static void UnregisterAllRenderers()
        {
            for (int i = registeredWithRenderers.Count - 1; i >= 0; i--)
                UnregisterRenderer(registeredWithRenderers[i]);
        }

        public static event Action<Camera> BeforeCameraRender;

        private Dictionary<ExpCameraBehaviour, CameraPasses> passes = new Dictionary<ExpCameraBehaviour, CameraPasses>();

        public override void Create()
        {

        }
#if UNITY_2022_1_OR_NEWER
        public override void SetupRenderPasses(ScriptableRenderer renderer, in RenderingData renderingData)
        {
            base.SetupRenderPasses(renderer, renderingData);
            EnqueueRenderPasses(renderer, renderingData.cameraData.camera);
        }
#endif
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
#if !UNITY_2022_1_OR_NEWER
            EnqueueRenderPasses(renderer, renderingData.cameraData.camera);
#endif
        }

        void EnqueueRenderPasses(ScriptableRenderer renderer, Camera camera)
        {
            BeforeCameraRender?.Invoke(camera);

            if (ExpCameraBehaviour.CurrentlyRendering == null)
                return;

            if (!passes.TryGetValue(ExpCameraBehaviour.CurrentlyRendering, out CameraPasses camPasses))
                passes.Add(ExpCameraBehaviour.CurrentlyRendering, camPasses = new CameraPasses(ExpCameraBehaviour.CurrentlyRendering));

            switch (ExpCameraBehaviour.CurrentlyRendering.CurrentRenderMode)
            {
                case ExpCameraBehaviour.RenderMode.FullRender:
                    if (ExpCameraBehaviour.CurrentlyRendering.LatestFrameInfo.HasCamFlag(ExpCamFlagBit.SeparateOpaque))
                        camPasses.AfterOpaque.Enqueue(renderer);
                    if (MixCastSdkData.ProjectSettings.grabUnfilteredAlpha && ExpCameraBehaviour.CurrentlyRendering.LatestFrameInfo.HasCamFlag(ExpCamFlagBit.Translucent))
                        camPasses.GrabBgAlpha.Enqueue(renderer);
                    break;

                case ExpCameraBehaviour.RenderMode.Foreground:
                    renderer.EnqueuePass(camPasses.Cutoff);
                    if (MixCastSdkData.ProjectSettings.grabUnfilteredAlpha)
                        camPasses.GrabFgAlpha.Enqueue(renderer);
                    break;
            }
        }
    }
}
#endif
#endif
