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

namespace BlueprintReality.MixCast
{
    public class SetPoseFromTiltFiveController : UnityEngine.MonoBehaviour
    {
#if MIXCAST_TILTFIVE && UNITY_STANDALONE_WIN
#if MIXCAST_TILTFIVE_V2
        public TiltFive.PlayerIndex playerIndex;
#endif
        public TiltFive.ControllerIndex controller;
        public UnityEngine.GameObject gripObject;

        private void OnEnable()
        {
            ExpCameraScheduler.OnBeforeRender += UpdateWand;
        }
        private void OnDisable()
        {
            ExpCameraScheduler.OnBeforeRender -= UpdateWand;
        }

        void UpdateWand()
        {
            if (GetTrackingAvailability())
			{
#if MIXCAST_TILTFIVE_V2
				gripObject.transform.position = TiltFive.Wand.GetPosition(controller, TiltFive.ControllerPosition.Grip, playerIndex);
				gripObject.transform.rotation = TiltFive.Wand.GetRotation(controller, playerIndex);
#else
                gripObject.transform.position = TiltFive.Wand.GetPosition(controller, TiltFive.ControllerPosition.Grip);
                gripObject.transform.rotation = TiltFive.Wand.GetRotation(controller);
#endif

#if MIXCAST_TILTFIVE_V2
				gripObject.transform.localScale = UnityEngine.Vector3.one * TiltFive.TiltFiveSingletonHelper.GetISceneInfo().GetScaleToUWRLD_UGBD();
#else
                TiltFive.GameBoardSettings boardSettings = TiltFive.TiltFiveManager.Instance.gameBoardSettings;
                TiltFive.ScaleSettings scaleSettings = TiltFive.TiltFiveManager.Instance.scaleSettings;

				gripObject.transform.localScale = UnityEngine.Vector3.one * scaleSettings.GetScaleToUWRLD_UGBD(boardSettings.gameBoardScale);
#endif

				gripObject.SetActive(true);
            }
            else
                gripObject.SetActive(false);
        }

        bool GetTrackingAvailability()
		{
            if (!TiltFive.GameBoard.TryGetGameboardType(out var gameboardType) ||
                gameboardType == TiltFive.GameboardType.GameboardType_None)
                return false;
#if MIXCAST_TILTFIVE_V2
            return TiltFive.TiltFiveSingletonHelper.TryGetISceneInfo(out var sceneInfo) && sceneInfo.IsActiveAndEnabled()
                && TiltFive.Player.IsConnected(playerIndex)
                && TiltFive.Wand.TryCheckConnected(out bool isConnected, playerIndex, controller)
                && isConnected;
#else
            return TiltFive.TiltFiveManager.Instance != null
                && TiltFive.Display.GetGlassesAvailability()
                && TiltFive.Input.GetWandAvailability(controller);
#endif
		}
#endif
    }
}
