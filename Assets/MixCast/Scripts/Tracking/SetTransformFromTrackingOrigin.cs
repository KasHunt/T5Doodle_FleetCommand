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
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
#if MIXCAST_XRMAN
using UnityEngine.XR.Management;
#endif

#if UNITY_2017_2_OR_NEWER
using UnityEngine.XR;
using UnityEngine.Experimental.Rendering;
using UnityXrTracking = UnityEngine.XR.InputTracking;
using Node = UnityEngine.XR.XRNode;
using NodeState = UnityEngine.XR.XRNodeState;
using UnityEngine.Rendering;
#if !UNITY_2020_1_OR_NEWER
using XrBoundary = UnityEngine.Experimental.XR.Boundary;
#endif
#else
using UnityEngine.VR;
using UnityXrTracking = UnityEngine.VR.InputTracking;
using Node = UnityEngine.VR.VRNode;
#if UNITY_2017_1_OR_NEWER
using XrBoundary = UnityEngine.Experimental.VR.Boundary;
using NodeState = UnityEngine.VR.VRNodeState;
#endif
#endif
#if MIXCAST_STEAMVR
using Valve.VR;
#endif

using BlueprintReality.MixCast.Shared;

namespace BlueprintReality.MixCast.Tracking
{
    public class SetTransformFromTrackingOrigin : MonoBehaviour
    {
        private int lastRenderedFrameCount = -1;

        private Camera hmdCamera;

#if MIXCAST_STEAMVR
        private HmdQuad_t steamVrPlayArea;
        private HmdQuad_t[] steamVrBoundaryGeo;
#endif
#if MIXCAST_OCULUS
        private OVRPlugin.TrackingOrigin cachedOvrOrigin;
#endif

#if UNITY_2017_1_OR_NEWER
        protected List<NodeState> trackingNodeStates = new List<NodeState>();
        protected List<Vector3> boundaryGeo = new List<Vector3>();

        protected List<Vector3> playRectGeo = new List<Vector3>();
#endif

#if MIXCAST_STEAMVR || UNITY_2017_1_OR_NEWER
        private Vector3 cachedPlayRectPos;
        private Quaternion cachedPlayRectRot;
#endif

#if MIXCAST_XRMAN
        private XRInputSubsystem xrInput;
#endif
        private Vector3 trackingToPlayAreaPos = Vector3.zero;
        private Quaternion trackingToPlayAreaRot = Quaternion.identity;


        private void OnEnable()
        {
            MixCastSdk.OnActiveChanged += HandleMixCastActiveChanged;
            RegisterTrackingChangedFunc();
            RegisterUpdateFunc();
            HandlePlatformLoaded();
        }
        void RegisterTrackingChangedFunc()
        {
            XrPlatformInfo.OnPlatformLoaded += HandlePlatformLoaded;
            Service_SDK_Handler.OnTrackingSpaceChanged += HandleTrackingSpaceChanged;
#if MIXCAST_OCULUS
            OVRManager.display.RecenteredPose += HandleTrackingSpaceChanged;
#endif
        }
        void RegisterUpdateFunc()
        {
            Camera.onPreRender += ApplyPoses;
#if UNITY_2019_3_OR_NEWER
            RenderPipelineManager.beginFrameRendering += ApplyPoses_Pipeline;
#elif UNITY_2018_1_OR_NEWER
            UnityEngine.Experimental.Rendering.RenderPipeline.beginCameraRendering += ApplyPoses;
#endif
#if MIXCAST_URP_V1 || MIXCAST_URP_V2 || MIXCAST_URP_V3 || MIXCAST_URP_V4
            MixCastUrpRendererFeature.BeforeCameraRender += ApplyPoses;
#endif
        }
        private void OnDisable()
        {
#if MIXCAST_XRMAN
            if (xrInput != null)
            {
                xrInput.boundaryChanged -= HandleBoundaryChanged;
                xrInput = null;
            }
#endif
            UnregisterUpdateFunc();
            UnregisterTrackingChangedFunc();
            MixCastSdk.OnActiveChanged -= HandleMixCastActiveChanged;
        }
        void UnregisterTrackingChangedFunc()
        {
#if MIXCAST_OCULUS
            OVRManager.display.RecenteredPose -= HandleTrackingSpaceChanged;
#endif
            XrPlatformInfo.OnPlatformLoaded -= HandlePlatformLoaded;
            Service_SDK_Handler.OnTrackingSpaceChanged -= HandleTrackingSpaceChanged;
        }
        void UnregisterUpdateFunc()
        {
#if UNITY_2019_3_OR_NEWER
            RenderPipelineManager.beginFrameRendering -= ApplyPoses_Pipeline;
#elif UNITY_2018_1_OR_NEWER
            UnityEngine.Experimental.Rendering.RenderPipeline.beginCameraRendering -= ApplyPoses;
#endif
#if MIXCAST_URP_V1 || MIXCAST_URP_V2 || MIXCAST_URP_V3 || MIXCAST_URP_V4
            MixCastUrpRendererFeature.BeforeCameraRender -= ApplyPoses;
#endif
            Camera.onPreRender -= ApplyPoses;
        }

        void HandleMixCastActiveChanged()
        {
            if (MixCastSdk.Active)
                HandleTrackingSpaceChanged();
        }

        void HandlePlatformLoaded()
        {
#if MIXCAST_XRMAN
            if (xrInput != null)
                xrInput.boundaryChanged -= HandleBoundaryChanged;

            if(XRGeneralSettings.Instance != null && XRGeneralSettings.Instance.Manager != null && XRGeneralSettings.Instance.Manager.activeLoader != null)
            {
                xrInput = XRGeneralSettings.Instance.Manager.activeLoader.GetLoadedSubsystem<XRInputSubsystem>();
                if (xrInput != null)
                    xrInput.boundaryChanged += HandleBoundaryChanged;
            }
#endif

            HandleTrackingSpaceChanged();
        }

#if MIXCAST_XRMAN
        void HandleBoundaryChanged(XRInputSubsystem xrInput)
        {
            HandleTrackingSpaceChanged();
        }
#endif

#if UNITY_2019_3_OR_NEWER
        void ApplyPoses_Pipeline(ScriptableRenderContext ctx, Camera[] cams)
		{
            ApplyPoses(null);
        }
#endif

        void ApplyPoses(Camera cam)
        {
            if (lastRenderedFrameCount == Time.renderedFrameCount)
                return;

            lastRenderedFrameCount = Time.renderedFrameCount;

            UpdateTransform();
        }

        void UpdateTransform()
        {
            if (XrPlatformInfo.TrackingSource == TrackingSource.UNKNOWN)
            {
                ApplyTransformFromFirstRoom();
                return;
            }

#if MIXCAST_TILTFIVE
            if (XrPlatformInfo.TrackingSource == TrackingSource.TILT_FIVE)
            {
#if MIXCAST_TILTFIVE_V2
                TiltFive.ISceneInfo sceneInfo = TiltFive.TiltFiveSingletonHelper.GetISceneInfo();
                
                Pose gameboardPose = sceneInfo.GetGameboardPose();

                transform.position = gameboardPose.position;
				transform.rotation = gameboardPose.rotation;
				transform.localScale = Vector3.one * sceneInfo.GetScaleToUWRLD_UGBD();
#else
                TiltFive.GameBoardSettings boardSettings = TiltFive.TiltFiveManager.Instance.gameBoardSettings;
                TiltFive.ScaleSettings scaleSettings = TiltFive.TiltFiveManager.Instance.scaleSettings;
                
				transform.position = boardSettings.gameBoardCenter;
                transform.rotation = Quaternion.Euler(boardSettings.gameBoardRotation);
                transform.localScale = Vector3.one * scaleSettings.GetScaleToUWRLD_UGBD(boardSettings.gameBoardScale);
#endif
				return;
            }
#endif


            if (hmdCamera == null || !hmdCamera.isActiveAndEnabled)
                hmdCamera = UnityInfo.FindPrimaryUserCamera();

            if (hmdCamera == null)
            {
                ApplyTransformFromFirstRoom();
                return;
            }

            float playerScale = 1;
            if (hmdCamera.transform.parent != null)
                playerScale = hmdCamera.transform.parent.TransformVector(Vector3.forward).magnitude;

            Matrix4x4 hmdToWorld, trackingToHmd, trackingToStanding;
            if (!CalculateHmdToWorldTransform(out hmdToWorld) ||
                !CalculateTrackingToHmdPose(playerScale, out trackingToHmd) ||
                !CalculateSeatedTrackingOffset(out trackingToStanding))
                return;

            if (DetectBoundaryChanged())
                HandleTrackingSpaceChanged();

            Matrix4x4 trackingOrigin = hmdToWorld * trackingToHmd.inverse * trackingToStanding.inverse *
                Matrix4x4.TRS(trackingToPlayAreaPos * playerScale, trackingToPlayAreaRot, Vector3.one);

            transform.position = trackingOrigin.MultiplyPoint(Vector3.zero);
            transform.rotation = Quaternion.LookRotation(trackingOrigin.MultiplyVector(Vector3.forward), trackingOrigin.MultiplyVector(Vector3.up));
            transform.localScale = Vector3.one * playerScale;
        }

        void ApplyTransformFromFirstRoom()
        {
            if (MixCastRoomBehaviour.ActiveRoomBehaviours.Count > 0)
            {
                transform.position = MixCastRoomBehaviour.ActiveRoomBehaviours[0].transform.position;
                transform.rotation = MixCastRoomBehaviour.ActiveRoomBehaviours[0].transform.rotation;
                transform.localScale = Vector3.one * MixCastRoomBehaviour.ActiveRoomBehaviours[0].transform.TransformVector(Vector3.forward).magnitude;
            }
        }

        bool CalculateHmdToWorldTransform(out Matrix4x4 hmdToWorld)
        {
            hmdToWorld = Matrix4x4.identity;
            if (hmdCamera == null || !hmdCamera.isActiveAndEnabled)
                return false;

            hmdToWorld = hmdCamera.transform.localToWorldMatrix;
            return true;
        }

        bool CalculateTrackingToHmdPose(float playerScale, out Matrix4x4 trans)
        {
            trans = Matrix4x4.identity;

#if UNITY_2019_1_OR_NEWER
            InputTracking.GetNodeStates(trackingNodeStates);

            Vector3 hmdLocalPos = Vector3.zero;
            Quaternion hmdLocalRot = Quaternion.identity;
            for (int i = 0; i < trackingNodeStates.Count; i++)
            {
                if (trackingNodeStates[i].nodeType == Node.CenterEye)
                {
                    if (!trackingNodeStates[i].TryGetPosition(out hmdLocalPos) ||
                        !trackingNodeStates[i].TryGetRotation(out hmdLocalRot))
                        return false;
                }
            }
#else
            Vector3 hmdLocalPos = UnityXrTracking.GetLocalPosition(Node.CenterEye);
            Quaternion hmdLocalRot = UnityXrTracking.GetLocalRotation(Node.CenterEye);
#endif

            trans = Matrix4x4.TRS(hmdLocalPos * playerScale, hmdLocalRot, Vector3.one * playerScale);
            return true;
        }

        bool CalculateSeatedTrackingOffset(out Matrix4x4 trans)
        {
            trans = Matrix4x4.identity;

            switch (XrPlatformInfo.TrackingSource)
            {
                default:
                    return true;

                case TrackingSource.STEAMVR:
#if MIXCAST_STEAMVR
                    if (OpenVR.System != null && OpenVR.Compositor != null &&
                        OpenVR.Compositor.GetTrackingSpace() == ETrackingUniverseOrigin.TrackingUniverseSeated)
                    {
                        HmdMatrix34_t seatedOffset = OpenVR.System.GetSeatedZeroPoseToStandingAbsoluteTrackingPose();
                        SteamVR_Utils.RigidTransform seatedTransform = new SteamVR_Utils.RigidTransform(seatedOffset);
                        trans = Matrix4x4.TRS(seatedTransform.pos, seatedTransform.rot, Vector3.one);
                    }
#endif
                    return true;
            }
        }


        bool DetectBoundaryChanged()
        {
            switch (XrPlatformInfo.TrackingSource)
            {
                default:
                    return false;

                case TrackingSource.OCULUS:
#if MIXCAST_OCULUS
                    if (OVRManager.instance != null)
                        return cachedOvrOrigin != OVRPlugin.GetTrackingOriginType();
#endif
#if UNITY_2017_1_OR_NEWER && !UNITY_2020_1_OR_NEWER
                    if (XrBoundary.TryGetGeometry(playRectGeo, XrBoundary.Type.PlayArea))
                    {
                        Vector3 newPos = Vector3.Lerp(playRectGeo[0], playRectGeo[2], 0.5f);
                        if (Vector3.SqrMagnitude(newPos - cachedPlayRectPos) > 0.01f * 0.01f)
                            return true;
                        Quaternion newRot = Quaternion.LookRotation(playRectGeo[1] - playRectGeo[2]);
                        if (Quaternion.Angle(newRot, cachedPlayRectRot) > 0.5f)
                            return true;
                    }
#endif
                    return false;

                case TrackingSource.STEAMVR:
#if UNITY_2017_1_OR_NEWER && !UNITY_2020_1_OR_NEWER
                    if (XrBoundary.TryGetGeometry(playRectGeo, XrBoundary.Type.PlayArea))
                    {
                        Vector3 newPos = Vector3.Lerp(playRectGeo[0], playRectGeo[2], 0.5f);
                        if (Vector3.SqrMagnitude(newPos - cachedPlayRectPos) > 0.01f * 0.01f)
                            return true;
                        Quaternion newRot = Quaternion.LookRotation(playRectGeo[1] - playRectGeo[2]);
                        if (Quaternion.Angle(newRot, cachedPlayRectRot) > 0.5f)
                            return true;
                    }
#endif

#if MIXCAST_STEAMVR
                    if (OpenVR.Chaperone != null && OpenVR.Chaperone.GetPlayAreaRect(ref steamVrPlayArea))
                    {
                        Vector3 newPos = Vector3.Lerp(SteamVrToUnityVector(steamVrPlayArea.vCorners0), SteamVrToUnityVector(steamVrPlayArea.vCorners2), 0.5f);
                        if (Vector3.SqrMagnitude(newPos - cachedPlayRectPos) > 0.01f * 0.01f)
                            return true;
                        Quaternion newRot = Quaternion.LookRotation(SteamVrToUnityVector(steamVrPlayArea.vCorners1) - SteamVrToUnityVector(steamVrPlayArea.vCorners2));
                        if (Quaternion.Angle(newRot, cachedPlayRectRot) > 0.5f)
                            return true;
                    }
#endif

                    return false;
            }
        }
        void HandleTrackingSpaceChanged()
        {
            Matrix4x4 newTrans;
            if (CalculatePlayAreaOffset(out newTrans))
            {
                trackingToPlayAreaPos = newTrans.GetColumn(3);
                trackingToPlayAreaRot = Quaternion.LookRotation(
                    newTrans.GetColumn(2),
                    newTrans.GetColumn(1)
                );
            }
        }
        bool CalculatePlayAreaOffset(out Matrix4x4 trans)
        {
            trans = Matrix4x4.identity;
            if (XrPlatformInfo.TrackingSource != TrackingSource.STEAMVR &&
                XrPlatformInfo.TrackingSource != TrackingSource.OCULUS)
                return true;

#if MIXCAST_OCULUS
            if (OVRManager.boundary != null && OVRManager.boundary.GetConfigured())
            {
                if (boundaryGeo == null)
                    boundaryGeo = new List<Vector3>();
                else
                    boundaryGeo.Clear();
                boundaryGeo.AddRange(OVRManager.boundary.GetGeometry(OVRBoundary.BoundaryType.OuterBoundary));

                if (boundaryGeo.Count > 0)
                {
                    Vector3 center = Vector3.zero;
                    for (int i = 0; i < boundaryGeo.Count; i++)
                        center += boundaryGeo[i];
                    center /= boundaryGeo.Count;

                    trans = Matrix4x4.TRS(center, Quaternion.LookRotation(boundaryGeo[0] - center), Vector3.one);

                    cachedOvrOrigin = OVRPlugin.GetTrackingOriginType();

                    return true;
                }
            }
#endif

#if MIXCAST_XRMAN
            if (xrInput != null && xrInput.TryGetBoundaryPoints(boundaryGeo) && boundaryGeo.Count > 0)
            {
                Vector3 center = Vector3.zero;
                for (int i = 0; i < boundaryGeo.Count; i++)
                    center += boundaryGeo[i];
                center /= boundaryGeo.Count;

                Vector3 flattenedCenter = center;
                flattenedCenter.y = 0;

                trans = Matrix4x4.TRS(flattenedCenter, Quaternion.LookRotation(boundaryGeo[0] - center), Vector3.one);
                return true;
            }
#endif


#if UNITY_2017_1_OR_NEWER && !UNITY_2020_1_OR_NEWER
            if (XrBoundary.configured &&
                XrBoundary.TryGetGeometry(boundaryGeo, XrBoundary.Type.TrackedArea) &&
                boundaryGeo.Count > 0)
            {
#if !UNITY_2018_2_OR_NEWER  //try UNITY_2018_3_OR_NEWER or UNITY_2018_4_OR_NEWER if not compiling
                for (int i = 0; i < boundaryGeo.Count; i++)
                    boundaryGeo[i] = new Vector3(boundaryGeo[i].x, boundaryGeo[i].y, -boundaryGeo[i].z);
#endif

                Vector3 center = Vector3.zero;
                for (int i = 0; i < boundaryGeo.Count; i++)
                    center += boundaryGeo[i];
                center /= boundaryGeo.Count;

                Vector3 flattenedCenter = center;
                flattenedCenter.y = 0;

                trans = Matrix4x4.TRS(flattenedCenter, Quaternion.LookRotation(boundaryGeo[0] - center), Vector3.one);

                //cache play rect to allow for faster change checks
                if (XrBoundary.TryGetGeometry(playRectGeo, XrBoundary.Type.PlayArea))
                {
                    cachedPlayRectPos = Vector3.Lerp(playRectGeo[0], playRectGeo[2], 0.5f);
                    cachedPlayRectRot = Quaternion.LookRotation(playRectGeo[1] - playRectGeo[2]);
                }

                return true;
            }
#endif

#if MIXCAST_STEAMVR
            if (XrPlatformInfo.TrackingSource == TrackingSource.STEAMVR && OpenVR.ChaperoneSetup != null &&
                OpenVR.ChaperoneSetup.GetLiveCollisionBoundsInfo(out steamVrBoundaryGeo) && steamVrBoundaryGeo.Length > 0)
            {
                Vector3 center = Vector3.zero;
                for (int i = 0; i < steamVrBoundaryGeo.Length; i++)
                    center += SteamVrToUnityVector(steamVrBoundaryGeo[i].vCorners0);
                center /= steamVrBoundaryGeo.Length;

                Vector3 flattenedCenter = center;
                flattenedCenter.y = 0;

                trans = Matrix4x4.TRS(flattenedCenter, Quaternion.LookRotation(SteamVrToUnityVector(steamVrBoundaryGeo[0].vCorners0) - center), Vector3.one);

                //cache play rect to allow for faster change checks
                if (OpenVR.Chaperone != null && OpenVR.Chaperone.GetPlayAreaRect(ref steamVrPlayArea))
                {
                    cachedPlayRectPos = Vector3.Lerp(SteamVrToUnityVector(steamVrPlayArea.vCorners0), SteamVrToUnityVector(steamVrPlayArea.vCorners2), 0.5f);
                    cachedPlayRectRot = Quaternion.LookRotation(SteamVrToUnityVector(steamVrPlayArea.vCorners1) - SteamVrToUnityVector(steamVrPlayArea.vCorners2));
                }

                return true;
            }
#endif

            return false;
        }

#if MIXCAST_STEAMVR
        public static Vector3 SteamVrToUnityVector(HmdVector3_t vec)
        {
            return new Vector3(vec.v0, vec.v1, -vec.v2);
        }
#endif
    }
}
#endif
