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
    public class EventTriggerSphereVolume : EventTriggerVolume
    {
        public float radius = 0.5f;

#if UNITY_STANDALONE_WIN
        protected override bool CalculateVolumeContainsPoint(Vector3 point)
        {
            Vector3 localPoint = transform.InverseTransformPoint(point);
            return localPoint.sqrMagnitude < radius * radius;
        }

        private void OnDrawGizmosSelected()
        {
            Color rgb = Color.red;
            Color transparent = rgb;
            transparent.a = 0.15f;

            Gizmos.matrix = transform.localToWorldMatrix;


            Gizmos.color = transparent;
            Gizmos.DrawSphere(Vector3.zero, radius);
            Gizmos.color = rgb;
            Gizmos.DrawWireSphere(Vector3.zero, radius);
        }
#endif
    }
}
