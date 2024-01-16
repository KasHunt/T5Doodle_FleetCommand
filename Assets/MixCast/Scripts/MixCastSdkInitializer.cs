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
using UnityEngine.SceneManagement;

namespace BlueprintReality.MixCast
{
    //Class that ensures that the MixCast SDK is active at runtime regardless of project setup
    public class MixCastSdkInitializer : MonoBehaviour
    {
        public const float InitializedCheckDelay = 1f;

        public static bool suppressInitialization;

        [RuntimeInitializeOnLoadMethod]
        private static void Initialize()
        {
            GameObject go = new GameObject("MixCast Initializer");
            go.hideFlags = HideFlags.HideAndDontSave;
            go.AddComponent<MixCastSdkInitializer>();
            DontDestroyOnLoad(go);
        }

        //allow time for scenes to load before checking if MixCast SDK Behaviour exists yet
        float timeSinceSceneLoad = 0;

        void Awake()
        {
            SceneManager.sceneLoaded += ResetTimer;
            StartCoroutine(EnsureSdkInitialized());
        }
        IEnumerator EnsureSdkInitialized()
        {
            while (timeSinceSceneLoad < InitializedCheckDelay && MixCastSdkBehaviour.Instance == null)
            {
                timeSinceSceneLoad += Time.deltaTime;
                yield return null;
            }

            if (!suppressInitialization)
                MixCastSdkBehaviour.EnsureExists();
            Destroy(gameObject);
        }
        void OnDestroy()
        {
            SceneManager.sceneLoaded -= ResetTimer;
        }

        void ResetTimer(Scene scene, LoadSceneMode mode) { timeSinceSceneLoad = 0; }
    }
}
#endif
