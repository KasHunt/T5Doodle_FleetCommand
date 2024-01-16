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
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast {
	public class SetActiveFromCameraCount : MonoBehaviour {
        public int minCount = 0;
        public int maxCount = 100;

        public List<GameObject> activeInRange = new List<GameObject>();
        public List<GameObject> inactiveInRange = new List<GameObject>();

        private bool lastState;

        private void OnEnable()
        {
            SetNewState(CalculateNewState());
        }
        private void Update()
        {
            bool newState = CalculateNewState();
            if (newState != lastState)
                SetNewState(newState);
        }

        bool CalculateNewState()
        {
            return MixCastSdkData.Cameras.Count >= minCount && MixCastSdkData.Cameras.Count <= maxCount;
        }
        void SetNewState(bool newState)
        {
            lastState = newState;
            activeInRange.ForEach(g => g.SetActive(newState));
            inactiveInRange.ForEach(g => g.SetActive(!newState));
        }
    }
}
#endif
