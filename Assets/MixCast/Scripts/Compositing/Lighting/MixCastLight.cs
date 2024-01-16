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
    [RequireComponent(typeof(Light))]
    public class MixCastLight : MonoBehaviour
    {
        public static List<MixCastLight> Active { get; protected set; }
        static MixCastLight()
		{
            Active = new List<MixCastLight>();
		}

        public Light Light { get; protected set; }
		private void Awake()
		{
            Light = GetComponent<Light>();
		}

		private void OnEnable()
		{
			if (Light.type != LightType.Point && Light.type != LightType.Directional && Light.type != LightType.Spot)
			{
				Debug.LogError("MixCast doesn't yet support lights of type: " + Light.type);
				return;
			}
			Active.Add(this);
		}
		private void OnDisable()
		{
			Active.Remove(this);
		}
	}
}
