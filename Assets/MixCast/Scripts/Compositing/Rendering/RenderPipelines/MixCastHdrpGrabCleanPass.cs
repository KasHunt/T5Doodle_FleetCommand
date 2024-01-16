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
    [CustomPassDrawer(typeof(MixCastHdrpGrabCleanPass))]
    public class MixCastHdrpGrabCleanPassDrawer : CustomPassDrawer
    {
        protected override PassUIFlag commonPassUIFlags => PassUIFlag.Name;
    }
#endif

    public class MixCastHdrpGrabCleanPass : CustomPass
    {
        public MixCastHdrpGrabCleanPass()
        {
            targetColorBuffer = TargetBuffer.None;
            targetDepthBuffer = TargetBuffer.None;
        }

        protected override void Setup(ScriptableRenderContext renderContext, CommandBuffer cmd)
        {
            base.Setup(renderContext, cmd);
        }
        protected override void Cleanup()
        {
            base.Cleanup();
        }

        protected override void Execute(CustomPassContext ctx)
		{
            ExpCameraBehaviour cam = ExpCameraBehaviour.CurrentlyRendering;
            if (cam == null)
                return;

            var scale = RTHandles.rtHandleProperties.rtHandleScale; 
            ctx.cmd.Blit(ctx.cameraColorBuffer, cam.CleanColorTarget, new Vector2(scale.x, scale.y), Vector2.zero, 0, 0);
        }
    }
}
#endif
