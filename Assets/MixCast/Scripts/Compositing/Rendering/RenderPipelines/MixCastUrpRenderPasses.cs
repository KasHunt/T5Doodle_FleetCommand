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
using UnityEngine;
using UnityEngine.Rendering;
#if MIXCAST_LWRP
using UnityEngine.Rendering.LWRP;
#elif MIXCAST_URP
using UnityEngine.Rendering.Universal;
#endif

namespace BlueprintReality.MixCast
{
    public static class MixCastUrpRenderPasses
    {
        public class AfterOpaquePass : ScriptableRenderPass
        {
            ExpCameraBehaviour cam;
#if UNITY_2022_1_OR_NEWER
            RTHandle colorTarget;
#else
            RenderTargetIdentifier colorTarget;
#endif
            public AfterOpaquePass(ExpCameraBehaviour cam)
            {
                this.cam = cam;
                renderPassEvent = RenderPassEvent.BeforeRenderingTransparents;
            }
            public void Enqueue(ScriptableRenderer renderer)
            {
#if UNITY_2022_1_OR_NEWER
                colorTarget = renderer.cameraColorTargetHandle;
#else
                colorTarget = renderer.cameraColorTarget;
#endif
                renderer.EnqueuePass(this);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get();
                cmd.Blit(colorTarget, cam.OpaqueLayerTex, cam.TransferOpaqueMat);
                cmd.Blit(Texture2D.blackTexture, colorTarget, cam.ResetForTransparentMat);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public class ApplyCutoffPass : ScriptableRenderPass
        {
            ExpCameraBehaviour cam;
            public ApplyCutoffPass(ExpCameraBehaviour cam)
            {
                this.cam = cam;
#if MIXCAST_LWRP || MIXCAST_URP_V1 || MIXCAST_URP_V2
                renderPassEvent = RenderPassEvent.BeforeRenderingPrepasses;
#else
                renderPassEvent = RenderPassEvent.BeforeRenderingPrePasses;
#endif
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                Camera camera = renderingData.cameraData.camera;

                var cmd = CommandBufferPool.Get();
                cmd.SetViewProjectionMatrices(Matrix4x4.identity, Matrix4x4.identity);
                cmd.DrawMesh(RenderingUtils.fullscreenMesh, Matrix4x4.identity, cam.ApplyCutoffMat);
                cmd.SetViewProjectionMatrices(camera.worldToCameraMatrix, camera.projectionMatrix);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }

        public class SaveAlphaPass : ScriptableRenderPass
        {
            ExpCameraBehaviour cam;
#if UNITY_2022_1_OR_NEWER
            RTHandle colorTarget;
#else
            RenderTargetIdentifier colorTarget;
#endif
            public SaveAlphaPass(ExpCameraBehaviour cam)
            {
                this.cam = cam;
                renderPassEvent = RenderPassEvent.AfterRenderingTransparents;
            }
            public void Enqueue(ScriptableRenderer renderer)
            {
#if UNITY_2022_1_OR_NEWER
                colorTarget = renderer.cameraColorTargetHandle;
#else
                colorTarget = renderer.cameraColorTarget;
#endif
                renderer.EnqueuePass(this);
            }
            public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
            {
                var cmd = CommandBufferPool.Get();
                cmd.Blit(colorTarget, cam.CleanColorTarget, cam.TransferAlphaMat);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
    }
}
#endif
#endif
