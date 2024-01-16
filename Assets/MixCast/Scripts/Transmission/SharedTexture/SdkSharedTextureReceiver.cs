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
using UnityEngine;
using System.Collections;
using System;
using BlueprintReality.SharedTextures;

namespace BlueprintReality.MixCast
{
    [Serializable]
    public class SdkSharedTextureReceiver : SharedTextureReceiver
    {
        public static void ApplyOverrideToCreateFunc()
        {
            SharedTextureReceiver.OverrideCreateFunc((string texId) =>
            {
                return new SdkSharedTextureReceiver(texId);
            });
        }

        private SdkSharedTextureReceiver(string texId)
            : base(texId)
        {
            Service_SDK_Handler.OnExternalTextureUpdated += HandleRemoteTextureUpdate;
        }
        public override void Dispose()
        {
            Service_SDK_Handler.OnExternalTextureUpdated -= HandleRemoteTextureUpdate;
            base.Dispose();
        }
    }
}
#endif
