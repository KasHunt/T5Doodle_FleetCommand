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
using BlueprintReality.MixCast.Interprocess;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace BlueprintReality.MixCast
{
    public class ExpFrameSender : IDisposable
    {
        const int QueueCapacity = 10;

		private static Dictionary<string, SharedQueueProducer<ExpFrame>> cachedRenderedFramesQueues = new Dictionary<string, SharedQueueProducer<ExpFrame>>();
		public static void DisposeAllCachedQueues()
        {
			foreach (SharedQueueProducer<ExpFrame> queue in cachedRenderedFramesQueues.Values)
				queue.Dispose();
			cachedRenderedFramesQueues.Clear();
        }

        private string camId;

        private SharedQueueProducer<ExpFrame> renderedExpFrames;
        private OwnedUnitySharedTexture[] queueLayersTextures;
        private OwnedUnitySharedTexture[] queueOpaqueTextures;

        bool nextEmptySeen = false;
        bool firstWriteCompleted = false;
        bool recordingMissedLastFrame;

        private ExpFrame sendFrame = new ExpFrame();

        public ExpFrameSender(string camId)
        {
            this.camId = camId;

			if(!cachedRenderedFramesQueues.TryGetValue(camId, out renderedExpFrames))
				renderedExpFrames = cachedRenderedFramesQueues[camId] = new SharedQueueProducer<ExpFrame>(string.Format("RenderedExpFrames({0})", camId), QueueCapacity);
			queueLayersTextures = new OwnedUnitySharedTexture[QueueCapacity];
            queueOpaqueTextures = new OwnedUnitySharedTexture[QueueCapacity];

            ExpCameraBehaviour.FrameEnded += HandleFrameUpdated;
        }
        public void Dispose()
        {
			ExpCameraBehaviour.FrameEnded -= HandleFrameUpdated;

            DisposeSharedTexs(ref queueLayersTextures);
            DisposeSharedTexs(ref queueOpaqueTextures);
		}
        void DisposeSharedTexs(ref OwnedUnitySharedTexture[] texs)
        {
            for (int i = 0; i < texs.Length; i++)
                if (texs[i] != null)
                    texs[i].Dispose();
            texs = null;
        }

        private void HandleFrameUpdated(ExpCameraBehaviour curCam)
        {
            if (curCam.cameraContext.Identifier != camId)
                return;

            sendFrame.frameIndex = curCam.LatestFrameInfo.frameIndex;
            sendFrame.syncTime = curCam.LatestFrameInfo.syncTime;

            sendFrame.camFramerate = curCam.LatestFrameInfo.camFramerate;
            sendFrame.camWidth = curCam.LatestFrameInfo.camWidth;
            sendFrame.camHeight = curCam.LatestFrameInfo.camHeight;

            sendFrame.camPos = curCam.LatestFrameInfo.camPos;
            sendFrame.camRot = curCam.LatestFrameInfo.camRot;
            sendFrame.camFoV = curCam.LatestFrameInfo.camFoV;

            sendFrame.renderForeground = curCam.LatestFrameInfo.renderForeground;
            sendFrame.renderFull = curCam.LatestFrameInfo.renderFull;

            sendFrame.camFlags = curCam.LatestFrameInfo.camFlags;

            sendFrame.occlusionApproxDepth = curCam.LatestFrameInfo.occlusionApproxDepth;
            sendFrame.occlusionTex = curCam.LatestFrameInfo.occlusionTex;

            if (!nextEmptySeen)
            {
                if (!renderedExpFrames.WaitUntilNextEmptied(0))
                {
                    if (!recordingMissedLastFrame && firstWriteCompleted && curCam.notifyWhenFrameDropped)
                    {
                        UnityEngine.Debug.LogWarning(string.Format("Compositing process fell behind at time {0}",
                            MixCastTimestamp.GetSMPTE(
                                FrameTimer.GetMidnightRelativeFrameIndex(curCam.LatestFrameInfo.syncTime, Mathf.RoundToInt(curCam.LatestFrameInfo.camFramerate)),
                                Mathf.RoundToInt(curCam.LatestFrameInfo.camFramerate))));
                    }
                    recordingMissedLastFrame = true;
                    return;
                }
                nextEmptySeen = true;
            }

            //Copy layers tex to shared location
			OwnedUnitySharedTexture sharedLayersTex = queueLayersTextures[renderedExpFrames.CurrentSlot];
			if (sharedLayersTex == null)
			{
				sharedLayersTex = new OwnedUnitySharedTexture(curCam.LayersTexture, curCam.LayersTexturePtr, true);
				queueLayersTextures[renderedExpFrames.CurrentSlot] = sharedLayersTex;
			}
			sharedLayersTex.IssueUpdate(curCam.LayersTexture);
			if (sharedLayersTex.Handle == IntPtr.Zero)
				return;

            sendFrame.layersTex = sharedLayersTex.Handle;   //Handle is current after updating the shared tex


			if (sendFrame.HasCamFlag(ExpCamFlagBit.SeparateOpaque) && curCam.OpaqueLayerTex != null)
			{
				OwnedUnitySharedTexture sharedOpaqueTex = queueOpaqueTextures[renderedExpFrames.CurrentSlot];
				if (sharedOpaqueTex == null)
				{
					sharedOpaqueTex = new OwnedUnitySharedTexture(curCam.OpaqueLayerTex, curCam.OpaqueLayerTexPtr, true);
					queueOpaqueTextures[renderedExpFrames.CurrentSlot] = sharedOpaqueTex;
				}
				sharedOpaqueTex.IssueUpdate(curCam.OpaqueLayerTex);
				if (sharedOpaqueTex.Handle == IntPtr.Zero)
					return;

				sendFrame.opaqueLayerTex = sharedOpaqueTex.Handle;   //Handle is current after updating the shared tex
			}
			else
				sendFrame.opaqueLayerTex = IntPtr.Zero;

            PopulateLights(ref sendFrame.lights);

			renderedExpFrames.Write(sendFrame, true);
			nextEmptySeen = false;

			if (!firstWriteCompleted)
			{
				firstWriteCompleted = true;
				recordingMissedLastFrame = false;

				if (curCam.notifyWhenFrameDropped)
				{
					UnityEngine.Debug.Log(string.Format("Compositing process started at time {0}",
						MixCastTimestamp.GetSMPTE(
							FrameTimer.GetMidnightRelativeFrameIndex(curCam.LatestFrameInfo.syncTime, Mathf.RoundToInt(curCam.LatestFrameInfo.camFramerate)),
							Mathf.RoundToInt(curCam.LatestFrameInfo.camFramerate))));
				}
			}

			if (recordingMissedLastFrame && curCam.notifyWhenFrameDropped)
			{
				UnityEngine.Debug.Log(string.Format("Compositing process back in business at time {0}",
					MixCastTimestamp.GetSMPTE(
						FrameTimer.GetMidnightRelativeFrameIndex(curCam.LatestFrameInfo.syncTime, Mathf.RoundToInt(curCam.LatestFrameInfo.camFramerate)),
						Mathf.RoundToInt(curCam.LatestFrameInfo.camFramerate))));
			}

			recordingMissedLastFrame = false;
		}

		List<Light> activeLights = new List<Light>();	//cached
		void PopulateLights(ref ExpLights arr)
        {
            Transform trackingRoot = Cameras.ExpCameraSpawner.Instance.transform;

            if (MixCastSdkData.ProjectSettings.specifyLightsManually)
			{
				for (int i = 0; i < MixCastLight.Active.Count; i++)
					activeLights.Add(MixCastLight.Active[i].Light);
			}
			else
			{
				Light[] pointLights = Light.GetLights(LightType.Point, MixCastSdkData.ProjectSettings.SubjectLayer);
				for (int i = 0; i < pointLights.Length; i++)
					activeLights.Add(pointLights[i]);
				Light[] dirLights = Light.GetLights(LightType.Directional, MixCastSdkData.ProjectSettings.SubjectLayer);
				for (int i = 0; i < dirLights.Length; i++)
					activeLights.Add(dirLights[i]);
				Light[] spotLights = Light.GetLights(LightType.Spot, MixCastSdkData.ProjectSettings.SubjectLayer);
				for (int i = 0; i < spotLights.Length; i++)
					activeLights.Add(spotLights[i]);
			}

			int listIndex = 0;
			int arrIndex = 0;
			while( arrIndex < ExpFrame.MaxLights )
			{
                ExpLight nextLight = default(ExpLight);
				if (listIndex >= activeLights.Count)
				{
					arr[arrIndex++] = nextLight;
					continue;
				}

				Light light = activeLights[listIndex++];
				if (light.intensity <= 0)
					continue;

				switch (light.type)
				{
					case LightType.Point:
						nextLight.type = ExpLightType.Point;
						break;
					case LightType.Directional:
                        nextLight.type = ExpLightType.Directional;
						break;
					case LightType.Spot:
                        nextLight.type = ExpLightType.Spot;
						break;
				}

                nextLight.color = new Vector3(light.color.r, light.color.g, light.color.b) * light.intensity;

                nextLight.pos = trackingRoot.InverseTransformPoint(light.transform.position);
                nextLight.forward = trackingRoot.InverseTransformDirection(light.transform.forward).normalized;

                nextLight.range = light.range;
                nextLight.angle = light.spotAngle * Mathf.Deg2Rad * 0.5f;

                arr[arrIndex++] = nextLight;
			}

			activeLights.Clear();
		}
	}
}
#endif
