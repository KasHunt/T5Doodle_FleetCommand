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

using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BlueprintReality.SharedTextures
{
    public class SharedTexturePlugin : MonoBehaviour
    {
        private static bool created = false;
        public static void EnsureExists()
        {
            if (!created)
            {
                new GameObject("SharedPlugin")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                }.AddComponent<SharedTexturePlugin>();
            }
        }

        public static string manualThriftAddress = null;

        private void Awake()
        {
            DontDestroyOnLoad(gameObject);
            created = true;
        }
    }
}
