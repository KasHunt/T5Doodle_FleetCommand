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

namespace BlueprintReality.MixCast.Interaction
{
    public class EventTriggerBoxVolume : EventTriggerVolume
    {
        public Vector3 size = Vector3.one;

#if UNITY_STANDALONE_WIN
        protected override bool CalculateVolumeContainsPoint(Vector3 point)
        {
            Vector3 localPoint = transform.InverseTransformPoint(point);

            if (localPoint.x < -size.x * 0.5f || localPoint.x > size.x * 0.5f)
                return false;
            if (localPoint.y < -size.y * 0.5f || localPoint.y > size.y * 0.5f)
                return false;
            if (localPoint.z < -size.z * 0.5f || localPoint.z > size.z * 0.5f)
                return false;
            return true;
        }

        private void OnDrawGizmosSelected()
        {
            Color rgb = Color.red;
            Color transparent = rgb;
            transparent.a = 0.15f;

            Gizmos.matrix = transform.localToWorldMatrix;


            Gizmos.color = transparent;
            Gizmos.DrawCube(Vector3.zero, size);
            Gizmos.color = rgb;
            Gizmos.DrawWireCube(Vector3.zero, size);
        }
#endif
    }
}
