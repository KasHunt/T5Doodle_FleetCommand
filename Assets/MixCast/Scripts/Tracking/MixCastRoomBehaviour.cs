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
using UnityEngine;

namespace BlueprintReality.MixCast
{
    public class MixCastRoomBehaviour : MonoBehaviour
    {
#if UNITY_STANDALONE_WIN
        public static List<MixCastRoomBehaviour> ActiveRoomBehaviours { get; protected set; }

        public static float GetAverageScale()
        {
            if (ActiveRoomBehaviours.Count == 0)
                return 1;

            float scale = 0;
            for (int i = 0; i < ActiveRoomBehaviours.Count; i++)
                scale += ActiveRoomBehaviours[i].GetScale();
            scale /= ActiveRoomBehaviours.Count;
            return scale;
        }

        protected static float cummulativeRotation = 0;

        static MixCastRoomBehaviour()
        {
            ActiveRoomBehaviours = new List<MixCastRoomBehaviour>();
            Service_SDK_Handler.OnResetWorldOrientation += ResetRotation;
            Service_SDK_Handler.OnModifyWorldOrientation += Rotate;
        }
        public static void Rotate(float degrees)
        {
            cummulativeRotation += degrees;
            for (int i = 0; i < ActiveRoomBehaviours.Count; i++)
                ActiveRoomBehaviours[i].UpdateRotation();
        }
        public static void ResetRotation()
        {
            Rotate(-cummulativeRotation);
        }

        protected virtual void OnEnable()
        {
            ActiveRoomBehaviours.Add(this);
            UpdateRotation();
        }
        protected virtual void OnDisable()
        {
            ActiveRoomBehaviours.Remove(this);
        }
        protected void UpdateRotation()
        {
            transform.localRotation = Quaternion.Euler(0, cummulativeRotation, 0);
        }

        public float GetScale()
        {
            return transform.TransformVector(Vector3.forward).magnitude;
        }
#endif
    }
}
