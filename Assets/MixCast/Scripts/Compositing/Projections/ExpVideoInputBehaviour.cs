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
using UnityEngine;
using System.Collections;
using BlueprintReality.MixCast.Shared;
using BlueprintReality.MixCast.Data;
using BlueprintReality.SharedTextures;
using System.Collections.Generic;

namespace BlueprintReality.MixCast.Viewfinders
{
    public class ExpVideoInputBehaviour : MonoBehaviour
    {
        public IdentifierContext videoInputId;

        public Transform positionTransform;
        public Transform rotationTransform;

        public Shader projectionShader;
        public MeshRenderer projectionRenderer;

#if UNITY_2017_3_OR_NEWER
        [Range(0, 4)]
        public int decimation = 0;

        private int lastMeshWidth;
        private int lastMeshHeight;
#endif

        private SharedTextureReceiver colorTex;
        private SharedTextureReceiver depthTex;

        private Material mat;
        private Mesh mesh;

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(videoInputId.Identifier))
            {
                colorTex = SharedTextureReceiver.Create(SharedTexIds.VideoInputs.LatestColor.Get(videoInputId.Identifier));
                depthTex = SharedTextureReceiver.Create(SharedTexIds.VideoInputs.LatestDepth.Get(videoInputId.Identifier));
            }

            mat = new Material(projectionShader);
            
            projectionRenderer.material = mat;

            ExpCameraBehaviour.FrameStarted += HandleFrameStarted;
            ExpCameraBehaviour.FrameEnded += HandleFrameEnded;

#if !UNITY_2017_3_OR_NEWER
            RegenerateMesh(479, 134);   //older versions of Unity have a 65k cap on vertices, this amount is just under that
#endif
        }
        private void OnDisable()
        {
            ExpCameraBehaviour.FrameStarted -= HandleFrameStarted;
            ExpCameraBehaviour.FrameEnded -= HandleFrameEnded;

            if (colorTex != null)
            {
                colorTex.Dispose();
                colorTex = null;
            }
            if (depthTex != null)
            {
                depthTex.Dispose();
                depthTex = null;
            }
        }

        private void LateUpdate()
        {
            Shared.VideoInput videoInput = MixCastSdkData.GetVideoInputWithId(videoInputId.Identifier);
            if (videoInput == null)
                return;

            positionTransform.localPosition = videoInput.CurrentPosition.unity;
            rotationTransform.localRotation = videoInput.CurrentRotation.unity;

            if (!colorTex.RequestSucceeded)
                colorTex.RefreshTextureInfo();
            if (!depthTex.RequestSucceeded)
                depthTex.RefreshTextureInfo();

#if UNITY_2017_3_OR_NEWER
            if (depthTex.Texture != null)
            {
                int newCellsX = Mathf.Max(2, depthTex.Texture.width / (decimation + 1)) - 1;
                int newCellsY = Mathf.Max(2, depthTex.Texture.height / (decimation + 1)) - 1;

                if (newCellsX != lastMeshWidth || newCellsY != lastMeshHeight)
                    RegenerateMesh(newCellsX, newCellsY);
            }
#endif

            bool drawable = colorTex.Texture != null && depthTex.Texture != null;
            projectionRenderer.gameObject.SetActive(drawable);
            if (drawable)
            {
                colorTex.Texture.filterMode = FilterMode.Point;
                mat.SetTexture("_MainTex", colorTex.Texture);
                depthTex.Texture.filterMode = FilterMode.Point;
                mat.SetTexture("_DepthTex", depthTex.Texture);

                mat.SetFloat("_MaxDist", ExpCameraBehaviour.MaxDepthInCutoffTexture);
                float yProj = Mathf.Tan((float)videoInput.FieldOfView * Mathf.Deg2Rad * 0.5f);
                float xProj = yProj * colorTex.Texture.width / colorTex.Texture.height;
                mat.SetVector("_ProjectionExtents", new Vector2(xProj, yProj));
            }
        }

        void HandleFrameStarted(ExpCameraBehaviour cam)
        {
            Shared.VideoInput videoInput = MixCastSdkData.GetVideoInputWithId(videoInputId.Identifier);
            if (videoInput == null)
                return;

            bool displayToCamera = false;
            for (int i = 0; i < videoInput.ProjectToCameras.Count && !displayToCamera; i++)
                displayToCamera |= videoInput.ProjectToCameras[i] == cam.cameraContext.Identifier;

            projectionRenderer.enabled = displayToCamera;
        }
        void HandleFrameEnded(ExpCameraBehaviour cam)
        {
            Shared.VideoInput videoInput = MixCastSdkData.GetVideoInputWithId(videoInputId.Identifier);
            if (videoInput == null)
                return;

            projectionRenderer.enabled = videoInput.ProjectToUser;
        }


        void RegenerateMesh(int cellsX, int cellsY)
        {
            if (mesh == null)
            {
                mesh = new Mesh();
#if UNITY_2017_3_OR_NEWER
                mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
#endif
            }
            else
                mesh.Clear();

            List<Vector3> positions = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<int> indices = new List<int>();

            for (int y = 0; y <= cellsY; y++)
            {
                float v = (float)y / cellsY;
                for (int x = 0; x <= cellsX; x++)
                {
                    float u = (float)x / cellsX;

                    positions.Add(2 * new Vector3(u - 0.5f, v - 0.5f, 0));
                    uvs.Add(new Vector2(u, v));
                }
            }
            for (int y = 0; y < cellsY; y++)
            {
                for (int x = 0; x < cellsX; x++)
                {
                    int bottomLeft = y * (cellsX + 1) + x;
                    int bottomRight = bottomLeft + 1;
                    int topLeft = (y + 1) * (cellsX + 1) + x;
                    int topRight = topLeft + 1;

                    //Alternate how quads are broken into triangles per row
                    if (y % 2 == 0)
                    {
                        indices.Add(bottomLeft);
                        indices.Add(topLeft);
                        indices.Add(topRight);

                        indices.Add(bottomLeft);
                        indices.Add(topRight);
                        indices.Add(bottomRight);
                    }
                    else
                    {
                        indices.Add(bottomRight);
                        indices.Add(bottomLeft);
                        indices.Add(topLeft);

                        indices.Add(bottomRight);
                        indices.Add(topLeft);
                        indices.Add(topRight);
                    }
                }
            }
            mesh.SetVertices(positions);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(indices, 0);
            mesh.bounds = new Bounds(Vector3.forward * 5, Vector3.one * 10); //should have a better bounds check?

            projectionRenderer.GetComponent<MeshFilter>().sharedMesh = mesh;
#if UNITY_2017_3_OR_NEWER
            lastMeshWidth = cellsX;
            lastMeshHeight = cellsY;
#endif
        }
    }
}
#endif
