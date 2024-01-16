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
    public static partial class SharedTexIds
    {
        public static partial class Cameras
        {
            public static class ForegroundCutoff
            {
                private const string IdFormat = "{0}_fg_cutoff";
                public static string Get(string cameraId)
                {
                    return string.Format(IdFormat, cameraId);
                }
            }

            public static class ExperienceLayers
            {
                private const string IdFormat = "{0}_exp_layers";
                public static string Get(string cameraId)
                {
                    return string.Format(IdFormat, cameraId);
                }
            }
        }

        public static partial class VideoInputs
        {
            public static class LatestColor
            {
                private const string IdFormat = "{0}_latest_color";
                public static string Get(string videoInputId)
                {
                    return string.Format(IdFormat, videoInputId);
                }
            }
            public static class LatestDepth
            {
                private const string IdFormat = "{0}_latest_depth";
                public static string Get(string videoInputId)
                {
                    return string.Format(IdFormat, videoInputId);
                }
            }
        }

        public static partial class Viewfinders
        {
            public const string ViewfinderViewIdFormat = "{0}_view";
            public static string GetIdForViewfinderView(string viewfinderId)
            {
                return string.Format(ViewfinderViewIdFormat, viewfinderId);
            }
        }
    }
}
