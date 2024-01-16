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

using BlueprintReality.MixCast.Data;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast.Cameras
{
    //Sets the alpha of a CanvasGroup to control the visibility of a group of UI elements
    [RequireComponent(typeof(CanvasGroup))]
    public class SetCanvasVisibleForMixCast : SetVisibilityForMixCast
    {
#if UNITY_STANDALONE_WIN
        private CanvasGroup target;
        private float originalAlpha;

        protected override void OnEnable()
        {
            if (target == null)
            {
                target = GetComponent<CanvasGroup>();
                originalAlpha = target.alpha;
            }
            base.OnEnable();
        }

        protected override void SetVisibility(bool visible)
        {
            target.alpha = visible ? originalAlpha : 0;
        }
#endif
    }
}
