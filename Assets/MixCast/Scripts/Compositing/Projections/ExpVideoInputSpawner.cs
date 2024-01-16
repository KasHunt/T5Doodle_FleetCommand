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
using BlueprintReality.MixCast.Data;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast.Viewfinders
{
    public class ExpVideoInputSpawner : MonoBehaviour
    {
        protected const string DEFAULT_PREFAB_RES_PATH = "Prefabs/MixCast VideoInput";

        public static ExpVideoInputSpawner Instance { get; protected set; }

        public GameObject videoInputPrefab;

        public List<IdentifierContext> VideoInputInstances { get; protected set; }

        //Caching collections for memory management
        List<string> createVIs = new List<string>();
        List<IdentifierContext> destroyVIs = new List<IdentifierContext>();

        void Awake()
        {
            VideoInputInstances = new List<IdentifierContext>();
        }

        private void OnEnable()
        {
            if (Instance != null)
            { 
                DestroyImmediate(gameObject);
                return;
            }

            if(videoInputPrefab == null )
                videoInputPrefab = Resources.Load<GameObject>(DEFAULT_PREFAB_RES_PATH);

            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance != this) { return; }

            DestroyViewfinders();

            Instance = null;
        }

        private void Update()
        {
            for (int i = 0; i < MixCastSdkData.VideoInputs.Count; i++)
                createVIs.Add(MixCastSdkData.VideoInputs[i].Identifier);
            for (int i = 0; i < VideoInputInstances.Count; i++)
                destroyVIs.Add(VideoInputInstances[i]);
            for (int i = 0; i < VideoInputInstances.Count; i++)
            {
                string viId = VideoInputInstances[i].Identifier;
                for (int j = createVIs.Count - 1; j >= 0; j--)
                    if (createVIs[j] == viId)
                        createVIs.RemoveAt(j);
            }
            for (int i = 0; i < MixCastSdkData.VideoInputs.Count; i++)
            {
                for (int j = destroyVIs.Count - 1; j >= 0; j--)
                    if (destroyVIs[j].Identifier == MixCastSdkData.VideoInputs[i].Identifier)
                        destroyVIs.RemoveAt(j);
            }

            for (int i = 0; i < destroyVIs.Count; i++)
            {
                VideoInputInstances.Remove(destroyVIs[i]);
                Destroy(destroyVIs[i].gameObject);
            }

            for (int i = 0; i < createVIs.Count; i++)
            {
                bool wasPrefabActive = videoInputPrefab.gameObject.activeSelf;
                videoInputPrefab.gameObject.SetActive(false);

                GameObject instanceObj = Instantiate(videoInputPrefab, transform, false) as GameObject;
                instanceObj.name = videoInputPrefab.name;

                IdentifierContext instance = instanceObj.GetComponent<IdentifierContext>();
                instance.Identifier = createVIs[i];

                VideoInputInstances.Add(instance);

                videoInputPrefab.gameObject.SetActive(wasPrefabActive);
            }

            bool activateVideoInputs = MixCastSdk.Active;
            for (int i = 0; i < VideoInputInstances.Count; i++)
            {
                if (VideoInputInstances[i].gameObject.activeSelf != activateVideoInputs)
                {
                    VideoInputInstances[i].gameObject.SetActive(activateVideoInputs);
                }
            }

            destroyVIs.Clear();
            createVIs.Clear();
        }

        void DestroyViewfinders()
        {
            for (int i = 0; i < VideoInputInstances.Count; i++)
            {
                Destroy(VideoInputInstances[i].gameObject);
            }

            VideoInputInstances.Clear();
        }
    }
}
#endif
