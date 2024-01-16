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
using System.Collections.Generic;
using UnityEngine;
#if MIXCAST_STEAMVR
using Valve.VR;
#endif

namespace BlueprintReality.MixCast.Cameras
{
    public class SetControllerVisibleForMixCast : SetRenderersVisibleForMixCast
    {
#if MIXCAST_STEAMVR
        private SteamVR_RenderModel render;

        protected override void OnEnable()
        {
            render = GetComponent<SteamVR_RenderModel>();

            SteamVR_Events.RenderModelLoaded.AddListener(HandleRenderLoaded);

            base.OnEnable();
        }

        private void HandleRenderLoaded(SteamVR_RenderModel curRender, bool success)
        {
            if (render != curRender)
                return;

            if (success)
                targets = new List<Renderer>(GetComponentsInChildren<Renderer>());
            else
                targets = new List<Renderer>();
        }

        protected override void OnDisable()
        {
            SteamVR_Events.RenderModelLoaded.RemoveListener(HandleRenderLoaded);

            base.OnDisable();
        }
#endif
    }
}
#endif
