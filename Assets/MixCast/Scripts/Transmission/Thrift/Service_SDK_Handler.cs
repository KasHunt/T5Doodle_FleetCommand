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
using BlueprintReality.MixCast.Data;
using BlueprintReality.MixCast.Experience;
using BlueprintReality.Thrift.SharedTextures;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Scripting;

namespace BlueprintReality.MixCast
{
    [Preserve]
    public class Service_SDK_Handler : Service_SDK.Handler
    {
        public static long ClientProcId { get; protected set; }

        public static event System.Action<string, SharedTex> OnExternalTextureUpdated;
        public static event System.Action OnCamerasUpdated;

        public static event System.Action OnTrackingSpaceChanged;
        public static event System.Action OnResetWorldOrientation;
        public static event System.Action<float> OnModifyWorldOrientation;

        public static event System.Action<string> OnCommandReceived;

        public Service_SDK_Handler() { }

        [Preserve]
        public override void SetActivationState(bool active, long clientProcId)
        {
            //Debug.Log("SetActivationState(" + active + ")");
            ClientProcId = active ? clientProcId : 0;
            MixCastSdk.Active = active;
        }

        [Preserve]
        public override void NotifyServiceStarted()
        {
            MixCastSdkBehaviour.Instance.ClientConnection.TryNotifySdkStarted(MixCastSdkData.ExperienceInfo);
        }

        [Preserve]
        public override void UpdateCameraMetadata(List<Shared.VirtualCamera> cameras)
        {
            for (int i = 0; i < cameras.Count; i++)
            {
                Shared.VirtualCamera prevData = null;
                if (cameras[i].__isset.identifier)
                    prevData = MixCastSdkData.GetCameraWithId(cameras[i].Identifier);   //match by identifier if possible
                if (prevData == null && i < MixCastSdkData.Cameras.Count)
                    prevData = MixCastSdkData.Cameras[i];                                   //otherwise match by index, assuming any element swap/list resize would provide IDs again
                if (!cameras[i].__isset.identifier && prevData == null)
                {
                    Debug.LogError("Incomplete camera list!");
                    continue;
                }
                TransferCameraData(prevData, cameras[i]);
            }
            MixCastSdkData.Cameras.Clear();
            MixCastSdkData.Cameras.AddRange(cameras);

            if (OnCamerasUpdated != null)
                OnCamerasUpdated.Invoke();
        }
        static void TransferCameraData(Shared.VirtualCamera prevData, Shared.VirtualCamera newData)
        {
            if (!newData.__isset.identifier && prevData.__isset.identifier)
                newData.Identifier = prevData.Identifier;

            if (!newData.__isset.fieldOfView && prevData.__isset.fieldOfView)
                newData.FieldOfView = prevData.FieldOfView;
            if (!newData.__isset.currentPosition && prevData.__isset.currentPosition)
                newData.CurrentPosition = prevData.CurrentPosition;
            if (!newData.__isset.currentRotation && prevData.__isset.currentRotation)
                newData.CurrentRotation = prevData.CurrentRotation;

            if (!newData.__isset.renderResolutionWidth && prevData.__isset.renderResolutionWidth)
                newData.RenderResolutionWidth = prevData.RenderResolutionWidth;
            if (!newData.__isset.renderResolutionHeight && prevData.__isset.renderResolutionHeight)
                newData.RenderResolutionHeight = prevData.RenderResolutionHeight;
            if (!newData.__isset.renderFramerate && prevData.__isset.renderFramerate)
                newData.RenderFramerate = prevData.RenderFramerate;

            if (!newData.__isset.usesFullRender && prevData.__isset.usesFullRender)
                newData.UsesFullRender = prevData.UsesFullRender;
            if (!newData.__isset.usesForeground && prevData.__isset.usesForeground)
                newData.UsesForeground = prevData.UsesForeground;
            if (!newData.__isset.isFirstPerson && prevData.__isset.isFirstPerson)
                newData.IsFirstPerson = prevData.IsFirstPerson;
            if (!newData.__isset.isBackgroundTranslucent && prevData.__isset.isBackgroundTranslucent)
                newData.IsBackgroundTranslucent = prevData.IsBackgroundTranslucent;

            if (!newData.__isset.autoSnapshotEnabled && prevData.__isset.autoSnapshotEnabled)
                newData.AutoSnapshotEnabled = prevData.AutoSnapshotEnabled;
            if (!newData.__isset.videoRecordingEnabled && prevData.__isset.videoRecordingEnabled)
                newData.VideoRecordingEnabled = prevData.VideoRecordingEnabled;
            if (!newData.__isset.videoStreamingEnabled && prevData.__isset.videoStreamingEnabled)
                newData.VideoStreamingEnabled = prevData.VideoStreamingEnabled;
        }
        [Preserve]
        public override void UpdateVideoInputMetadata(List<Shared.VideoInput> videoInputs)
        {
            for( int i = 0; i < videoInputs.Count; i++ )
            {
                Shared.VideoInput prevData = null;
                if (videoInputs[i].__isset.identifier)
                    prevData = MixCastSdkData.GetVideoInputWithId(videoInputs[i].Identifier);   //match by identifier if possible
                if(prevData == null && i < MixCastSdkData.VideoInputs.Count)
                    prevData = MixCastSdkData.VideoInputs[i];                                   //otherwise match by index, assuming any element swap/list resize would provide IDs again
                if (!videoInputs[i].__isset.identifier && prevData == null)
                {
                    Debug.LogError("Incomplete video input list!");
                    continue;
                }
                TransferVideoInputData(prevData, videoInputs[i]);
            }
            MixCastSdkData.VideoInputs.Clear();
            MixCastSdkData.VideoInputs.AddRange(videoInputs);
        }
        static void TransferVideoInputData(Shared.VideoInput prevData, Shared.VideoInput newData)
        {
            if (!newData.__isset.identifier && prevData.__isset.identifier)
                newData.Identifier = prevData.Identifier;

            if (!newData.__isset.fieldOfView && prevData.__isset.fieldOfView)
                newData.FieldOfView = prevData.FieldOfView;
            if (!newData.__isset.currentPosition && prevData.__isset.currentPosition)
                newData.CurrentPosition = prevData.CurrentPosition;
            if (!newData.__isset.currentRotation && prevData.__isset.currentRotation)
                newData.CurrentRotation = prevData.CurrentRotation;

            if (!newData.__isset.projectToUser && prevData.__isset.projectToUser)
                newData.ProjectToUser = prevData.ProjectToUser;
            if (!newData.__isset.projectToCameras && prevData.__isset.projectToCameras)
                newData.ProjectToCameras = prevData.ProjectToCameras;
        }

        [Preserve]
        public override void UpdateViewfinderMetadata(List<Shared.Viewfinder> viewfinders)
        {
            for (int i = 0; i < viewfinders.Count; i++)
            {
                Shared.Viewfinder prevData = MixCastSdkData.GetViewfinderWithId(viewfinders[i].Identifier);   //match by identifier if possible
                TransferViewfinderData(prevData, viewfinders[i]);
            }
            MixCastSdkData.Viewfinders.Clear();
            MixCastSdkData.Viewfinders.AddRange(viewfinders);
        }
        static void TransferViewfinderData(Shared.Viewfinder prevData, Shared.Viewfinder newData)
        {
            if (!newData.__isset.currentPosition && prevData.__isset.currentPosition)
                newData.CurrentPosition = prevData.CurrentPosition;
            if (!newData.__isset.currentRotation && prevData.__isset.currentRotation)
                newData.CurrentRotation = prevData.CurrentRotation;
            if (!newData.__isset.currentScale && prevData.__isset.currentScale)
                newData.CurrentScale = prevData.CurrentScale;
        }

        [Preserve]
        public override void NotifyExternalTexturesUpdated(List<SharedTexPacket> textureInfo)
        {
            //Debug.Log("MixCast called: NotifyExternalTextureUpdated(" + string.Join(",", textureInfo.ConvertAll(t => t.Id).ToArray()) + ")");
            if (OnExternalTextureUpdated != null)
            {
                for (int i = 0; i < textureInfo.Count; i++)
                    OnExternalTextureUpdated(textureInfo[i].Id, textureInfo[i].Info);
            }
        }

        [Preserve]
        public override void NotifyTrackingSpaceChanged()
        {
            //Debug.Log("MixCast called: NotifyTrackingSpaceChanged()");
            if (OnTrackingSpaceChanged != null)
                OnTrackingSpaceChanged();
        }

        [Preserve]
        public override void ResetWorldOrientation()
        {
            //Debug.Log("MixCast called: ResetWorldOrientation()");
            if (OnResetWorldOrientation != null)
                OnResetWorldOrientation();
        }

        [Preserve]
        public override void ModifyWorldOrientation(double degrees)
        {
            //Debug.Log("MixCast called: ModifyWorldOrientation(" + degrees + ")");
            if (OnModifyWorldOrientation != null)
                OnModifyWorldOrientation((float)degrees);
        }

        [Preserve]
        public override void SendExperienceCommand(string eventId)
        {
            Debug.Log("MixCast called: SendExperienceCommand(" + eventId + ")");
            if (OnCommandReceived != null)
                OnCommandReceived(eventId);
        }

        //Legacy functions left in for backwards compatibility purposes
        [Preserve]
        public override void SendLegacyData(string dataJson) { }
        [Preserve]
        public override void UpdateTrackedObjectMetadata(List<Shared.TrackedObject> trackedObjects) { }
        [Preserve]
        public override void UpdateDesktopMetadata(Shared.Desktop desktop) { }
    }
}
#endif
