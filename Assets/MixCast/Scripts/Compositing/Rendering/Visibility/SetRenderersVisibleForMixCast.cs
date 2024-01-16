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
    public class SetRenderersVisibleForMixCast : SetVisibilityForMixCast
    {
        [Header("Targets")]
        public List<Renderer> targets = new List<Renderer>();

#if UNITY_STANDALONE_WIN
        protected override void OnEnable()
        {
            if (targets.Count == 0)
                GetComponentsInChildren<Renderer>(targets);
            base.OnEnable();
        }

        protected override void SetVisibility(bool visible)
        {
            for (int i = 0; i < targets.Count; i++)
                if (targets[i] != null)
                    targets[i].enabled = visible;
        }

        private void Reset()
        {
            targets.Clear();
            GetComponentsInChildren<Renderer>(targets);
        }
#endif
    }
}
