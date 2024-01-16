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

namespace BlueprintReality.MixCast.Cameras
{
    public class ExpCameraSpawner : MonoBehaviour
    {
        protected const string DEFAULT_PREFAB_RES_PATH = "Prefabs/MixCast Camera";

        public static ExpCameraSpawner Instance { get; protected set; }

        public GameObject cameraPrefab;

        public List<IdentifierContext> CameraInstances { get; protected set; }

        protected Transform lastParent;

        private Camera lastMainCam;
        private RenderingPath lastRenderingPath;

        //Caching collections for memory management
        List<string> createCams = new List<string>();
        List<IdentifierContext> destroyCams = new List<IdentifierContext>();

        void Awake()
        {
            CameraInstances = new List<IdentifierContext>();
        }

        private void OnEnable()
        {
            if (transform.parent != null)
                lastParent = transform.parent;

            if (Instance != null)
            {
                Instance.lastParent = lastParent;
                DestroyImmediate(gameObject);
                return;
            }

            if (GetComponent<ExpCameraScheduler>() == null)
                gameObject.AddComponent<ExpCameraScheduler>();

            if( cameraPrefab == null )
                cameraPrefab = Resources.Load<GameObject>(DEFAULT_PREFAB_RES_PATH);

            Instance = this;
        }

        private void OnDisable()
        {
            if (Instance != this) { return; }

            DestroyCameras();

            Instance = null;
        }

        private void Update()
        {
            if (MixCastRoomBehaviour.ActiveRoomBehaviours.Count == 0 && lastParent != null && lastParent.GetComponent<MixCastRoomBehaviour>() == null)
                lastParent.gameObject.AddComponent<MixCastRoomBehaviour>();

            if (lastMainCam != null && !lastMainCam.isActiveAndEnabled)
                lastMainCam = null;
            if (lastMainCam == null)
                lastMainCam = UnityInfo.FindPrimaryUserCamera();

            for (int i = 0; i < MixCastSdkData.Cameras.Count; i++)
                createCams.Add(MixCastSdkData.Cameras[i].Identifier);
            for (int i = 0; i < CameraInstances.Count; i++)
                destroyCams.Add(CameraInstances[i]);

            if (!ShouldRebuildCameras())
            {
                for (int i = 0; i < CameraInstances.Count; i++)
                {
                    string camId = CameraInstances[i].Identifier;
                    for (int j = createCams.Count - 1; j >= 0; j--)
                        if (createCams[j] == camId)
                            createCams.RemoveAt(j);
                }
                for (int i = 0; i < MixCastSdkData.Cameras.Count; i++)
                {
                    for (int j = destroyCams.Count - 1; j >= 0; j--)
                        if (destroyCams[j].Identifier == MixCastSdkData.Cameras[i].Identifier)
                            destroyCams.RemoveAt(j);
                }
            }

            for (int i = 0; i < destroyCams.Count; i++)
            {
                CameraInstances.Remove(destroyCams[i]);
                Destroy(destroyCams[i].gameObject);
            }

            for (int i = 0; i < createCams.Count; i++)
            {
                bool wasPrefabActive = cameraPrefab.gameObject.activeSelf;
                cameraPrefab.gameObject.SetActive(false);

                GameObject instanceObj = Instantiate(cameraPrefab, transform, false) as GameObject;
                instanceObj.name = cameraPrefab.name;

                IdentifierContext instance = instanceObj.GetComponent<IdentifierContext>();
                instance.Identifier = createCams[i];

                CameraInstances.Add(instance);

                cameraPrefab.gameObject.SetActive(wasPrefabActive);
            }

            bool activateCameras = MixCastSdk.Active;
            for (int i = 0; i < CameraInstances.Count; i++)
            {
                if (CameraInstances[i].gameObject.activeSelf != activateCameras)
                {
                    CameraInstances[i].gameObject.SetActive(activateCameras);
                }
            }

            destroyCams.Clear();
            createCams.Clear();

            if (lastMainCam != null)
				lastRenderingPath = lastMainCam.actualRenderingPath;
        }

        void DestroyCameras()
        {
            for (int i = 0; i < CameraInstances.Count; i++)
            {
                Destroy(CameraInstances[i].gameObject);
            }

            CameraInstances.Clear();
        }

        bool ShouldRebuildCameras()
        {
            return lastMainCam != null && lastMainCam.actualRenderingPath != lastRenderingPath;
        }
    }
}
#endif
