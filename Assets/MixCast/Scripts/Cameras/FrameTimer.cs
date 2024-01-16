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

using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast
{
    [Serializable]
    public class FrameTimer
    {
        public bool IsStarted { get; protected set; }

        public ulong LastFrameIndex { get; protected set; }
        public ulong LastUpdateTime { get; protected set; }
        public ulong LastIdealTime { get; protected set; }

        public float FrameRate { get; protected set; }

        ulong startTimestamp;

        public void Start(float frameRate)
        {
            FrameRate = frameRate;

			startTimestamp = MixCastTimestamp.Get();

            LastUpdateTime = startTimestamp;
            LastFrameIndex = 0;
            LastIdealTime = startTimestamp;

            IsStarted = true;
        }

        public void Update()
        {
			LastUpdateTime = MixCastTimestamp.Get();
            LastFrameIndex = GetFrameIndexForTimestamp(LastUpdateTime - startTimestamp, FrameRate);
            LastIdealTime = GetIdealTimestampForFrameIndex(LastFrameIndex, FrameRate) + startTimestamp;
        }

        public static ulong GetFrameIndexForTimestamp(ulong durationTicks, float framerate)
        {
            ulong extraDuration = (ulong)(0.5f * MixCastTimestamp.TicksPerSecond / framerate);
            return (ulong)(GetPreciseFramerate(framerate) * (durationTicks + extraDuration) / MixCastTimestamp.TicksPerSecond);
        }
        public static ulong GetIdealTimestampForFrameIndex(ulong frameIndex, float framerate)
        {
            return (ulong)((double)frameIndex * MixCastTimestamp.TicksPerSecond / GetPreciseFramerate(framerate));
        }

        public static ulong GetMidnightRelativeFrameIndex(ulong timestamp, float framerate)
        {
            const ulong TicksPerDay = 24L * 60 * 60 * 1000 * 10000;
            double ticksPerFrame = (double)MixCastTimestamp.TicksPerSecond / GetPreciseFramerate(framerate);
            return GetFrameIndexForTimestamp((timestamp - (ulong)((double)timestamp % ticksPerFrame)) % TicksPerDay, framerate);
        }

        public static double GetPreciseFramerate(float approxFramerate)
        {
            if (Mathf.Approximately(approxFramerate, 29.97f))
                return 30000.0 / 1001;
            else if (Mathf.Approximately(approxFramerate, 59.94f))
                return 60000.0 / 1001;
            return approxFramerate;
        }
    }
}
