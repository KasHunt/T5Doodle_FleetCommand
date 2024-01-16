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
    public abstract class EventTriggerVolume : MonoBehaviour
    {
        public string onEnterEventId = "";
        public string onExitEventId = "";

        public bool countOnEnterWhenEnabled = true;
        public bool countOnExitWhenDisabled = true;

#if UNITY_STANDALONE_WIN
        private Camera lastCam;

        private bool freshStart;
        private bool wasContainedLastFrame;

        protected virtual void OnEnable()
        {
            freshStart = true;
            wasContainedLastFrame = false;
        }
        protected virtual void OnDisable()
        {
            if (!MixCastSdk.Active)
                return;

            if (wasContainedLastFrame && countOnExitWhenDisabled)
                FireEvent(onExitEventId);
        }

        protected virtual void Update()
        {
            if (!MixCastSdk.Active)
            {
                freshStart = true;
                return;
            }

            bool volumeContainsPoint;
            if (!TryCalculateVolumeContainsPoint(out volumeContainsPoint))
            {
                if (wasContainedLastFrame && countOnExitWhenDisabled)
                {
                    FireEvent(onExitEventId);
                }
                freshStart = true;
                return;
            }

            if (volumeContainsPoint)
            {
                if ((freshStart && countOnEnterWhenEnabled) || (!freshStart && !wasContainedLastFrame))
                    FireEvent(onEnterEventId);
            }
            else if (!freshStart && wasContainedLastFrame)
                FireEvent(onExitEventId);

            freshStart = false;
            wasContainedLastFrame = volumeContainsPoint;
        }


        protected bool TryCalculateVolumeContainsPoint(out bool volumeContainsPoint)
        {
            Vector3 point;
            if (TryGetPoint(out point))
            {
                volumeContainsPoint = CalculateVolumeContainsPoint(point);
                return true;
            }
            else
            {
                volumeContainsPoint = false;
                return false;
            }
        }
        protected bool TryGetPoint(out Vector3 point)
        {
            if (lastCam == null)
                lastCam = UnityInfo.FindPrimaryUserCamera();

            if (lastCam == null)
            {
                point = Vector3.zero;
                return false;
            }
            else
            {
                point = lastCam.transform.position;
                return true;
            }
        }

        void FireEvent(string eventId)
        {
            if (!string.IsNullOrEmpty(eventId))
                MixCastSdk.SendCustomEvent(eventId);
        }

        protected abstract bool CalculateVolumeContainsPoint(Vector3 point);
#endif
    }
}
