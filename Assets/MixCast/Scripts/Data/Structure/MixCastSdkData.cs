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

using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast.Data
{
    public static class MixCastSdkData
    {
        private const string PROJECT_SETTINGS_FILENAME = "MixCast_ProjectSettings";

        //Build-time
        private static MixCastProjectSettings projectSettings;
        public static MixCastProjectSettings ProjectSettings
        {
            get
            {
                if (projectSettings == null)
                    projectSettings = Resources.Load<MixCastProjectSettings>(PROJECT_SETTINGS_FILENAME);
                return projectSettings;
            }
        }

        //Runtime
        private static Shared.ExperienceMetadata experienceInfo;
        public static Shared.ExperienceMetadata ExperienceInfo
        {
            get
            {
                if (experienceInfo == null)
                    experienceInfo = new Shared.ExperienceMetadata();
                return experienceInfo;
            }
            set
            {
                experienceInfo = value;
            }
        }

        private static List<Shared.Viewfinder> viewfinders;
        public static List<Shared.Viewfinder> Viewfinders
        {
            get
            {
                if (viewfinders == null)
                    viewfinders = new List<Shared.Viewfinder>();
                return viewfinders;
            }
        }
        public static Shared.Viewfinder GetViewfinderWithId(string id)
        {
            for (int i = 0; i < Viewfinders.Count; i++)
                if (Viewfinders[i].Identifier == id)
                    return Viewfinders[i];
            return null;
        }

        private static List<Shared.VirtualCamera> cameras;
        public static List<Shared.VirtualCamera> Cameras
		{
            get
			{
                if (cameras == null)
                    cameras = new List<Shared.VirtualCamera>();
                return cameras;
			}
		}
        public static Shared.VirtualCamera GetCameraWithId(string id)
        {
            for (int i = 0; i < Cameras.Count; i++)
                if (Cameras[i].Identifier == id)
                    return Cameras[i];
            return null;
        }

        private static List<Shared.VideoInput> videoInputs;
        public static List<Shared.VideoInput> VideoInputs
        {
            get
            {
                if (videoInputs == null)
                    videoInputs = new List<Shared.VideoInput>();
                return videoInputs;
            }
        }
        public static Shared.VideoInput GetVideoInputWithId(string id)
        {
            for (int i = 0; i < VideoInputs.Count; i++)
                if (VideoInputs[i].Identifier == id)
                    return VideoInputs[i];
            return null;
        }
    }
}
