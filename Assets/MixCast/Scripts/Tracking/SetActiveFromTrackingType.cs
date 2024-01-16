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

using UnityEngine;
using System.Collections;

namespace BlueprintReality.MixCast
{
    public class SetActiveFromTrackingType : MonoBehaviour
    {
        public Shared.TrackingSource[] matchSources = new Shared.TrackingSource[0];

        public GameObject[] activateIfMatch = new GameObject[0];
        public GameObject[] deactivateIfMatch = new GameObject[0];

        void OnEnable()
        {
            XrPlatformInfo.OnPlatformLoaded += UpdateState;
            UpdateState();
        }
        void OnDisable()
        {
            XrPlatformInfo.OnPlatformLoaded -= UpdateState;
        }

        void UpdateState()
        {
            Shared.TrackingSource src = XrPlatformInfo.TrackingSource;
            bool match = false;
            for (int i = 0; i < matchSources.Length; i++)
                match = src == matchSources[i];

            for (int i = 0; i < activateIfMatch.Length; i++)
                activateIfMatch[i].SetActive(match);
            for (int i = 0; i < deactivateIfMatch.Length; i++)
                deactivateIfMatch[i].SetActive(!match);
        }
    }
}
