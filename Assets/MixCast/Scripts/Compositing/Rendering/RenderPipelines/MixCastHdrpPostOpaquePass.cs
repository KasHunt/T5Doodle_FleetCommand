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

#if MIXCAST_HDRP
using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;
#if UNITY_EDITOR
using UnityEditor;
using UnityEditor.Rendering.HighDefinition;
#endif

namespace BlueprintReality.MixCast
{
#if UNITY_EDITOR
    [CustomPassDrawer(typeof(MixCastHdrpPostOpaquePass))]
    public class MixCastHdrpPostOpaquePassDrawer : CustomPassDrawer
    {
        protected override PassUIFlag commonPassUIFlags => PassUIFlag.Name;
    }
#endif

    public class MixCastHdrpPostOpaquePass : CustomPass
    {
        private Material opaqueGrabMat;
        private Material applyDepthCutoffMat;

        public MixCastHdrpPostOpaquePass()
        {
            targetColorBuffer = TargetBuffer.Camera;
            targetDepthBuffer = TargetBuffer.Camera;
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            base.Setup(renderContext, cmd);
            opaqueGrabMat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/BPR/HDRP/OpaqueGrab"));
            applyDepthCutoffMat = CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/BPR/HDRP/ApplyDepthCutoff"));
        }
        protected override void Cleanup()
        {
            CoreUtils.Destroy(opaqueGrabMat);
            CoreUtils.Destroy(applyDepthCutoffMat);
            base.Cleanup();
        }

        protected override void Execute(CustomPassContext ctx)
		{
            ExpCameraBehaviour cam = ExpCameraBehaviour.CurrentlyRendering;
            if (cam == null)
                return;

            bool separateOpaque = cam.LatestFrameInfo.HasCamFlag(ExpCamFlagBit.SeparateOpaque);
            switch (cam.CurrentRenderMode)
			{
                case ExpCameraBehaviour.RenderMode.FullRender:
                    if (separateOpaque)
					{
                        CopyOpaqueGrabMat(cam.TransferOpaqueMat, opaqueGrabMat);
                        ctx.cmd.Blit(Texture2D.blackTexture, cam.OpaqueLayerTex, opaqueGrabMat);
                    }
                    break;

                case ExpCameraBehaviour.RenderMode.Foreground:
                    CopyApplyDepthMat(cam.ApplyCutoffMat, applyDepthCutoffMat);
                    CoreUtils.DrawFullScreen(ctx.cmd, applyDepthCutoffMat);
                    break;
			}
            if (separateOpaque)
                CoreUtils.SetRenderTarget(ctx.cmd, ctx.cameraColorBuffer, ClearFlag.Color);
        }

        void CopyOpaqueGrabMat(Material src, Material dst)
        {
            if (src.IsKeywordEnabled("CONVERT_TO_LINEAR"))
                dst.EnableKeyword("CONVERT_TO_LINEAR");
            else
                dst.DisableKeyword("CONVERT_TO_LINEAR");
        }
        void CopyApplyDepthMat(Material src, Material dst)
        {
            dst.SetFloat("_MaxDist", src.GetFloat("_MaxDist"));
            dst.SetFloat("_PlayerScale", src.GetFloat("_PlayerScale"));
            dst.SetTexture("_DepthTex", src.GetTexture("_DepthTex"));
		}
    }
}
#endif
