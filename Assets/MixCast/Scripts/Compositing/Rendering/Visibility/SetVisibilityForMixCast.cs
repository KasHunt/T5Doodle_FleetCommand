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

using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using BlueprintReality.MixCast.Shared;

namespace BlueprintReality.MixCast.Cameras
{
    public abstract class SetVisibilityForMixCast : MonoBehaviour
    {
        public enum MixCastCameraMode
        {
            Always,
            IfAnyConditionMet,
            IfAllConditionsMet
        }

        public enum RenderTypeCondition
        {
            NotUsed,
            Mixed,
            Virtual
        }

        public enum BackgroundTypeCondition
        {
            NotUsed,
            Opaque,
            Transparent
        }

        public enum PerspectiveCondition
        {
            NotUsed,
            First,
            Third
        }

        public bool showForRegularCameras = true;
        public bool showForMixCastCameras = true;
        public MixCastCameraMode mixCastCameraMode = MixCastCameraMode.Always;
        public RenderTypeCondition renderTypeCondition = RenderTypeCondition.NotUsed;
        public BackgroundTypeCondition backgroundTypeCondition = BackgroundTypeCondition.NotUsed;
        public PerspectiveCondition perspectiveCondition = PerspectiveCondition.NotUsed;

#if UNITY_STANDALONE_WIN
        protected virtual void OnEnable()
        {
            ExpCameraBehaviour.FrameStarted += HandleMixCastRenderStarted;
            ExpCameraBehaviour.FrameEnded += HandleMixCastRenderEnded;
            
            SetVisibility(showForRegularCameras);
        }
        protected virtual void OnDisable()
        {
            ExpCameraBehaviour.FrameStarted -= HandleMixCastRenderStarted;
            ExpCameraBehaviour.FrameEnded -= HandleMixCastRenderEnded;

            SetVisibility(true);
        }

        private void HandleMixCastRenderStarted(ExpCameraBehaviour cam)
        {
			bool showForThisMixCastCamera = showForMixCastCameras;
            switch(mixCastCameraMode)
            {
                case MixCastCameraMode.Always:
                    break;
                case MixCastCameraMode.IfAnyConditionMet:
                    if (!HasAnyFlag(cam.LatestFrameInfo))
                        showForThisMixCastCamera = !showForThisMixCastCamera;
                    break;
                case MixCastCameraMode.IfAllConditionsMet:
                    if (!HasAllFlags(cam.LatestFrameInfo))
                        showForThisMixCastCamera = !showForThisMixCastCamera;
                    break;
            }
            SetVisibility(showForThisMixCastCamera);
        }
        private void HandleMixCastRenderEnded(ExpCameraBehaviour cam)
        {
            SetVisibility(showForRegularCameras);
        }

        protected abstract void SetVisibility(bool visible);

        protected bool HasAnyFlag(ExpFrame frame)
        {
            if (renderTypeCondition == RenderTypeCondition.Mixed)
            {
                if (frame.renderForeground)
                    return true;
            }
            else if (renderTypeCondition == RenderTypeCondition.Virtual)
            {
                if (!frame.renderForeground)
                    return true;
            }

            if (backgroundTypeCondition == BackgroundTypeCondition.Opaque)
            {
                if (frame.renderFull && !frame.HasCamFlag(ExpCamFlagBit.Translucent))
                    return true;
            }
            else if (backgroundTypeCondition == BackgroundTypeCondition.Transparent)
            {
                if (!frame.renderFull || frame.HasCamFlag(ExpCamFlagBit.Translucent))
                    return true;
            }

            if ( perspectiveCondition == PerspectiveCondition.First )
            {
                if (frame.HasCamFlag(ExpCamFlagBit.FirstPerson))
                    return true;
            }
            else if( perspectiveCondition == PerspectiveCondition.Third )
            {
                if (!frame.HasCamFlag(ExpCamFlagBit.FirstPerson))
                    return true;
            }

            return false;
        }
        protected bool HasAllFlags(ExpFrame frame)
        {
            if (renderTypeCondition == RenderTypeCondition.Mixed)
            {
                if (!frame.renderForeground)
                    return false;
            }
            else if (renderTypeCondition == RenderTypeCondition.Virtual)
            {
                if (frame.renderForeground)
                    return false;
            }

            if (backgroundTypeCondition == BackgroundTypeCondition.Opaque)
            {
                if (!frame.renderFull || frame.HasCamFlag(ExpCamFlagBit.Translucent))
                    return false;
            }
            else if( backgroundTypeCondition == BackgroundTypeCondition.Transparent)
            {
                if (frame.renderFull && !frame.HasCamFlag(ExpCamFlagBit.Translucent))
                    return false;
            }

            if (perspectiveCondition == PerspectiveCondition.First)
            {
                if (!frame.HasCamFlag(ExpCamFlagBit.FirstPerson))
                    return false;
            }
            else if (perspectiveCondition == PerspectiveCondition.Third)
            {
                if (frame.HasCamFlag(ExpCamFlagBit.FirstPerson))
                    return false;
            }

            return true;
        }
#endif
    }
}
