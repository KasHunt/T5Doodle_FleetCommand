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
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast
{
    /// <summary>
    /// The component responsible for driving the creation of frames by MixCast cameras
    /// </summary>
    public class ExpCameraScheduler : MonoBehaviour
    {
        private const float TimeSmoothingFactor = 0.75f;

        public static event System.Action OnBeforeRender;
        public static double AverageMsPerFrame { get; protected set; }

        protected Transform lastParent;

        private WaitForEndOfFrame waitForEndOfFrame = new WaitForEndOfFrame();

        private Coroutine renderUsedCamerasRoutine;


        private void OnEnable()
        {
            renderUsedCamerasRoutine = StartCoroutine(RenderLoop());
        }
        private void OnDisable()
        {
            StopCoroutine(renderUsedCamerasRoutine);
        }

        IEnumerator RenderLoop()
        {
            yield return waitForEndOfFrame;

            double frameStartTime = (double)MixCastTimestamp.Get() / MixCastTimestamp.TicksPerSecond;
            while (isActiveAndEnabled)
            {
                if (MixCastSdk.Active)
                {
                    if (OnBeforeRender != null)
                        OnBeforeRender();

                    for (int i = 0; i < ExpCameraBehaviour.ActiveCameras.Count; i++)
                        ExpCameraBehaviour.ActiveCameras[i].RenderIfNeeded();
                }

                yield return waitForEndOfFrame;

                double frameEndTime = (double)MixCastTimestamp.Get() / MixCastTimestamp.TicksPerSecond;
                double frameTime = (frameEndTime - frameStartTime) * 1000;

                AverageMsPerFrame = TimeSmoothingFactor * AverageMsPerFrame + (1f - TimeSmoothingFactor) * frameTime;
                frameStartTime = frameEndTime;
            }
        }
    }
}
#endif
