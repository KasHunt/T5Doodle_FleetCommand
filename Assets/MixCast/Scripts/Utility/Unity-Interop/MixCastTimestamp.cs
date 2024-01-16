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
using System.Runtime.InteropServices;
using UnityEngine;

namespace BlueprintReality.MixCast
{
    public static partial class MixCastTimestamp
    {
        public const ulong TicksPerSecond = 1000 * 10000;

        const ulong TicksPerDay = 24L * 60 * 60 * TicksPerSecond;

        public static ulong Get() { return GetMixCastTimestamp(); }

        public static string GetSMPTE(ulong frameIndex, float framerate, bool dropFrameFormat = true)
        {
            if (framerate <= 0)
                return "";

            bool shouldDrop = dropFrameFormat &&
                (Mathf.Approximately(framerate, 29.97f) || Mathf.Approximately(framerate, 59.94f));

            ulong naiveFramerate = (ulong)Mathf.RoundToInt(framerate);

            if (shouldDrop)
                frameIndex += CalculateDropFrameOffset(frameIndex, framerate);

            ulong framePart = frameIndex % naiveFramerate;
            ulong secondsPart = (frameIndex / naiveFramerate) % 60;
            ulong minutesPart = (frameIndex / (naiveFramerate * 60)) % 60;
            ulong hoursPart = frameIndex / (naiveFramerate * 60 * 60);

            return string.Format(shouldDrop ? "{0}:{1:00}:{2:00};{3:00}" : "{0}:{1:00}:{2:00}:{3:00}",
                hoursPart, minutesPart, secondsPart, framePart); //assumes framerate is less than 100
        }

        public static ulong CalculateDropFrameOffset(ulong ndfFrameIndex, float framerate)
		{
            ulong dropCountPerMinute = Mathf.Approximately(framerate, 29.97f) ? 2U : 4U;    //skip 2x the frames for 59.94
            ulong d = ndfFrameIndex / (17982 * dropCountPerMinute / 2);
            ulong m = ndfFrameIndex % (17982 * dropCountPerMinute / 2);
            if (m < dropCountPerMinute) m += dropCountPerMinute;
            return dropCountPerMinute * (9*d + (m - dropCountPerMinute) / (1798 * dropCountPerMinute / 2));
		}

        public static ulong GetTimeElapsedToday(ulong timestamp)
        {
            return timestamp % TicksPerDay;
        }
        public static ulong GetTimeElapsedBeforeToday(ulong timestamp)
        {
            return timestamp - GetTimeElapsedToday(timestamp);
        }

        public static void CalculateFramesFromElapsedTime(ulong elapsedTime, float framerate, out ulong frameCount, out ulong timeRemainder)
        {
            double preciseFramerate = FrameTimer.GetPreciseFramerate(framerate);
            frameCount = (ulong)(preciseFramerate * elapsedTime / TicksPerSecond);
            timeRemainder = elapsedTime - (ulong)((double)frameCount * TicksPerSecond / preciseFramerate);
        }

        [DllImport(Interprocess.MixCastInteropPlugin.DllName)]
        private static extern ulong GetMixCastTimestamp();
    }
}
