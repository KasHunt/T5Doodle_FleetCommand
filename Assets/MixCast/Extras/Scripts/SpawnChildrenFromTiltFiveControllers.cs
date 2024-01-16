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

using System.Collections;
using System.Collections.Generic;

#if MIXCAST_TILTFIVE
using TiltFive;
#endif

namespace BlueprintReality.MixCast
{
    public class SpawnChildrenFromTiltFiveControllers : UnityEngine.MonoBehaviour
    {
#if MIXCAST_TILTFIVE && UNITY_STANDALONE_WIN
        private class PlayerControllers
        {
            public UnityEngine.GameObject rightController, leftController;
        }

        public UnityEngine.GameObject controllerPrefab;

        private List<PlayerControllers> spawnedControllers = new List<PlayerControllers>();

        private void OnEnable()
        {
            ExpCameraScheduler.OnBeforeRender += UpdateSpawned;
        }
        private void OnDisable()
        {
            ExpCameraScheduler.OnBeforeRender -= UpdateSpawned;
        }

        void UpdateSpawned()
        {
            uint playerCount = GetSupportedPlayerCount();
			while ( spawnedControllers.Count < playerCount )
            {
                PlayerControllers newPlayerControllers = new PlayerControllers();

                newPlayerControllers.rightController = Instantiate(controllerPrefab);
                newPlayerControllers.rightController.transform.parent = transform;
                newPlayerControllers.rightController.name = string.Format(controllerPrefab.name, spawnedControllers.Count, "Right");
                SetPoseFromTiltFiveController rightPoser = newPlayerControllers.rightController.GetComponent<SetPoseFromTiltFiveController>();
                if (rightPoser != null)
                {
#if MIXCAST_TILTFIVE_V2
                    rightPoser.playerIndex = (PlayerIndex)(spawnedControllers.Count + 1);
                    rightPoser.controller = ControllerIndex.Right;
#else
                    rightPoser.controller = ControllerIndex.Primary;
#endif
                }

				newPlayerControllers.leftController = Instantiate(controllerPrefab);
				newPlayerControllers.leftController.transform.parent = transform;
                newPlayerControllers.leftController.name = string.Format(controllerPrefab.name, spawnedControllers.Count, "Left");
                SetPoseFromTiltFiveController leftPoser = newPlayerControllers.leftController.GetComponent<SetPoseFromTiltFiveController>();
                if (leftPoser != null)
				{
#if MIXCAST_TILTFIVE_V2
					leftPoser.playerIndex = (PlayerIndex)(spawnedControllers.Count + 1);
                    leftPoser.controller = ControllerIndex.Left;
#else
                    leftPoser.controller = ControllerIndex.Secondary;
#endif
                }

				spawnedControllers.Add(newPlayerControllers);
                newPlayerControllers.leftController.SetActive(true);
                newPlayerControllers.rightController.SetActive(true);
            }
            while (spawnedControllers.Count > playerCount)
            {
                PlayerControllers destroyControllers = spawnedControllers[spawnedControllers.Count - 1];
                spawnedControllers.RemoveAt(spawnedControllers.Count - 1);

                Destroy(destroyControllers.rightController);
                Destroy(destroyControllers.leftController);
            }
        }

        uint GetSupportedPlayerCount()
        {
            if (XrPlatformInfo.TrackingSource != Shared.TrackingSource.TILT_FIVE)
                return 0;

#if MIXCAST_TILTFIVE_V2
            ISceneInfo sceneInfo = TiltFiveSingletonHelper.GetISceneInfo();
            return sceneInfo.GetSupportedPlayerCount();
#else
            if (TiltFiveManager.Instance != null)
                return 1;
            else
                return 0;
#endif
		}
#endif
                }
}
