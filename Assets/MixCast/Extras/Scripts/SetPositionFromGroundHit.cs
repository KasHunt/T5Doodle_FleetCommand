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

namespace BlueprintReality.MixCast
{
    [ExecuteInEditMode]
    public class SetPositionFromGroundHit : MonoBehaviour
    {
        public Transform startTransform;
        public Vector3 direction = Vector3.down;
        public LayerMask layers;
        public float backupDistance = 0;
        public float maxDistance = 10;

        public GameObject activeOnHit;

        private void OnEnable()
        {
            if (startTransform == null)
                startTransform = transform.parent;

            LateUpdate();
        }

        private void LateUpdate()
        {
            if (startTransform == null)
                return;

            RaycastHit hitInfo;
            if (UnityEngine.Physics.Raycast(startTransform.position - backupDistance * direction, direction, out hitInfo, maxDistance, layers, QueryTriggerInteraction.UseGlobal))
            {
                transform.position = hitInfo.point;
                transform.up = hitInfo.normal;

                if( activeOnHit != null )
                    activeOnHit.SetActive(true);
            }
            else
                if (activeOnHit != null)
                    activeOnHit.SetActive(false);
        }
    }
}
