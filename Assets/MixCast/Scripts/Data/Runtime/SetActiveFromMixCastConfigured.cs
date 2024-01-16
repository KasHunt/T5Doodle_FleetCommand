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

namespace BlueprintReality.MixCast
{
    public class SetActiveFromMixCastConfigured : MonoBehaviour
    {
        public List<GameObject> on = new List<GameObject>();
        public List<GameObject> off = new List<GameObject>();

        private bool lastState = false;

        private void OnEnable()
        {
            ApplyState(CalculateState());
        }
        private void Update()
        {
            bool newState = CalculateState();
            if (newState != lastState)
                ApplyState(newState);
        }

        bool CalculateState()
        {
            return MixCastSdkData.Cameras.Count > 0;
        }
        void ApplyState(bool state)
        {
            for (int i = 0; i < on.Count; i++)
                on[i].SetActive(state);
            for (int i = 0; i < off.Count; i++)
                off[i].SetActive(!state);

            lastState = state;
        }
    }
}
#endif
