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
    public class ExpViewfinderSpawner : MonoBehaviour
    {
        protected const string DEFAULT_PREFAB_RES_PATH = "Prefabs/MixCast Viewfinder";

        public static ExpViewfinderSpawner Instance { get; protected set; }

        public GameObject viewfinderPrefab;

        public List<IdentifierContext> ViewfinderInstances { get; protected set; }

        //Caching collections for memory management
        List<string> createVFs = new List<string>();
        List<IdentifierContext> destroyVFs = new List<IdentifierContext>();

        void Awake()
        {
            ViewfinderInstances = new List<IdentifierContext>();
        }

        private void OnEnable()
        {
            if (Instance != null)
            { 
                DestroyImmediate(gameObject);
                return;
            }

            if(viewfinderPrefab == null )
                viewfinderPrefab = Resources.Load<GameObject>(DEFAULT_PREFAB_RES_PATH);

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
            for (int i = 0; i < MixCastSdkData.Viewfinders.Count; i++)
                createVFs.Add(MixCastSdkData.Viewfinders[i].Identifier);
            for (int i = 0; i < ViewfinderInstances.Count; i++)
                destroyVFs.Add(ViewfinderInstances[i]);
            for (int i = 0; i < ViewfinderInstances.Count; i++)
            {
                string camId = ViewfinderInstances[i].Identifier;
                for (int j = createVFs.Count - 1; j >= 0; j--)
                    if (createVFs[j] == camId)
                        createVFs.RemoveAt(j);
            }
            for (int i = 0; i < MixCastSdkData.Viewfinders.Count; i++)
            {
                for (int j = destroyVFs.Count - 1; j >= 0; j--)
                    if (destroyVFs[j].Identifier == MixCastSdkData.Viewfinders[i].Identifier)
                        destroyVFs.RemoveAt(j);
            }

            for (int i = 0; i < destroyVFs.Count; i++)
            {
                ViewfinderInstances.Remove(destroyVFs[i]);
                Destroy(destroyVFs[i].gameObject);
            }

            for (int i = 0; i < createVFs.Count; i++)
            {
                bool wasPrefabActive = viewfinderPrefab.gameObject.activeSelf;
                viewfinderPrefab.gameObject.SetActive(false);

                GameObject instanceObj = Instantiate(viewfinderPrefab, transform, false) as GameObject;
                instanceObj.name = viewfinderPrefab.name;

                IdentifierContext instance = instanceObj.GetComponent<IdentifierContext>();
                instance.Identifier = createVFs[i];

                ViewfinderInstances.Add(instance);

                viewfinderPrefab.gameObject.SetActive(wasPrefabActive);
            }

            bool activateViewfinders = MixCastSdk.Active;
            for (int i = 0; i < ViewfinderInstances.Count; i++)
            {
                if (ViewfinderInstances[i].gameObject.activeSelf != activateViewfinders)
                {
                    ViewfinderInstances[i].gameObject.SetActive(activateViewfinders);
                }
            }

            destroyVFs.Clear();
            createVFs.Clear();
        }

        void DestroyViewfinders()
        {
            for (int i = 0; i < ViewfinderInstances.Count; i++)
            {
                Destroy(ViewfinderInstances[i].gameObject);
            }

            ViewfinderInstances.Clear();
        }
    }
}
#endif
