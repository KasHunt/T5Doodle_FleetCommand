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

#if MIXCAST_HDRP
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.HighDefinition;

namespace BlueprintReality.MixCast
{
	[System.Serializable]
	public class MixCastHdrpDefaultCustomPassVolume : MonoBehaviour
    {
		private CustomPassVolume postOpaqueVol, prePostVol;

		private void OnEnable()
		{
			postOpaqueVol = gameObject.AddComponent<CustomPassVolume>();
			postOpaqueVol.injectionPoint = CustomPassInjectionPoint.BeforeTransparent;
			postOpaqueVol.isGlobal = true;
			postOpaqueVol.priority = -999;
			postOpaqueVol.customPasses.Add(new MixCastHdrpPostOpaquePass() { name = "MixCast PostOpaque Pass" });

			prePostVol = gameObject.AddComponent<CustomPassVolume>();
			prePostVol.injectionPoint = CustomPassInjectionPoint.BeforePostProcess;
			prePostVol.isGlobal = true;
			prePostVol.priority = -999;
			prePostVol.customPasses.Add(new MixCastHdrpGrabCleanPass() { name = "MixCast GrabClean Pass" });
		}
		private void OnDisable()
		{
			Destroy(postOpaqueVol);
			postOpaqueVol = null;
			Destroy(prePostVol);
			prePostVol = null;
		}
	}
}
#endif
