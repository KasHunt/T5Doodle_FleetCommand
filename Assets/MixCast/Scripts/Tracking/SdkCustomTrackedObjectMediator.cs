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
using BlueprintReality.MixCast.Experience;
using BlueprintReality.MixCast.Shared;
using BlueprintReality.MixCast.Thrift;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast
{
    public class SdkCustomTrackedObjectMediator
    {
        private List<TrackedObject> transmittedList = new List<TrackedObject>();

        public void Update()
        {
            bool writeAll = CustomTrackedObjectBehaviour.ActiveCustomTrackedObjects.Count != transmittedList.Count;
            for (int i = 0; i < transmittedList.Count && !writeAll; i++)
                writeAll |= transmittedList[i].Identifier != CustomTrackedObjectBehaviour.ActiveCustomTrackedObjects[i].objectIdentifier;

            if (writeAll)
            {
                TrackedObject.ResizeList(transmittedList, CustomTrackedObjectBehaviour.ActiveCustomTrackedObjects.Count);

                for (int i = 0; i < CustomTrackedObjectBehaviour.ActiveCustomTrackedObjects.Count; i++)
                {
                    transmittedList[i].Source = TrackingSource.EXPERIENCE;
                    CustomTrackedObjectBehaviour.ActiveCustomTrackedObjects[i].CopyToTransmittedInfo(transmittedList[i], false);
                }
            }
            else
            {
                bool hasNewData = false;
                for (int i = 0; i < CustomTrackedObjectBehaviour.ActiveCustomTrackedObjects.Count; i++)
                    hasNewData |= CustomTrackedObjectBehaviour.ActiveCustomTrackedObjects[i].CopyToTransmittedInfo(transmittedList[i], true);

                if (!hasNewData)
                    return;
            }

            SDK_Service.Client client = UnityThriftMixCastClient.Get<SDK_Service.Client>();
            if (client != null && client.TryUpdateExperienceTrackedObjectMetadata(transmittedList))
            {
                //Clear dirty flags
                for (int i = 0; i < transmittedList.Count; i++)
                    transmittedList[i].ClearDirtyFlags();
            }
        }
    }
}
#endif
