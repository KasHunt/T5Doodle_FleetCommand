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
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.Rendering;

namespace BlueprintReality.MixCast.Interprocess
{
    public class OwnedUnitySharedTexture : IDisposable
    {
        private IntPtr updateFunc;

        private bool ntHandleMode;
        private GraphicsDeviceType gfxType;

        private int texIndex;

        private Texture sourceTex;
        public Texture SourceTex
        {
            get
            {
                return sourceTex;
            }
            set
            {
                if (sourceTex != value)
                {
                    Texture oldTex = SourceTex;
                    sourceTex = value;

                    if (oldTex.width == sourceTex.width && oldTex.height == sourceTex.height)
                    {
                        switch (gfxType)
                        {
                            case GraphicsDeviceType.Direct3D11:
                                UpdateOwnedSharedTextureSource_D3D11(texIndex, sourceTex.GetNativeTexturePtr());
                                break;
                            case GraphicsDeviceType.Direct3D12:
                                UpdateOwnedSharedTextureSource_D3D12(texIndex, sourceTex.GetNativeTexturePtr());
                                break;
                        }
                    }
                    else
                    {
                        DisposeOwnedSharedTexture(texIndex);
                        switch (gfxType)
                        {
                            case GraphicsDeviceType.Direct3D11:
                                isReady = false;
                                handle = IntPtr.Zero;   //fields may be set asynchronously
                                format = 0;
                                texIndex = CreateOwnedSharedTexture_D3D11(sourceTex.GetNativeTexturePtr(), IsTextureLinear(sourceTex), ntHandleMode);
                                isReady = CheckOwnedSharedTextureReady_D3D11(texIndex, out handle, out format);
                                break;
                            case GraphicsDeviceType.Direct3D12:
                                texIndex = CreateOwnedSharedTexture_D3D12(sourceTex.GetNativeTexturePtr(), IsTextureLinear(sourceTex), out handle, out format);
                                isReady = true;
                                break;
                        }
                    }
                }
            }
        }

        private bool isReady = false;

        private IntPtr handle;
        public IntPtr Handle { get { return handle; } }

        private uint format;
        public uint Format { get { return format; } }

        public OwnedUnitySharedTexture(Texture srcTex, IntPtr srcTexPtr, bool ntHandles)
        {
            sourceTex = srcTex;

            ntHandleMode = ntHandles;
            gfxType = SystemInfo.graphicsDeviceType;

            updateFunc = GetUpdateOwnedSharedTextureFunc();

            switch (gfxType)
            {
                case GraphicsDeviceType.Direct3D11:
                    texIndex = CreateOwnedSharedTexture_D3D11(srcTexPtr, IsTextureLinear(sourceTex), ntHandles);
                    isReady = CheckOwnedSharedTextureReady_D3D11(texIndex, out handle, out format);
                    break;
                case GraphicsDeviceType.Direct3D12:
                    texIndex = CreateOwnedSharedTexture_D3D12(srcTexPtr, IsTextureLinear(sourceTex), out handle, out format);
                    isReady = true;
                    break;
            }
        }
        public void Dispose()
        {
            if (texIndex != -1)
            {
                DisposeOwnedSharedTexture(texIndex);
                texIndex = -1;
            }
            sourceTex = null;
        }

        public void IssueUpdate()
        {
            if (!isReady)
                isReady = CheckOwnedSharedTextureReady_D3D11(texIndex, out handle, out format);
            GL.IssuePluginEvent(updateFunc, texIndex);
        }
        public void IssueUpdate(Texture newSrcTex)
        {
            SourceTex = newSrcTex;
            IssueUpdate();
        }

        public static bool IsTextureLinear(Texture tex)
        {
            if (tex == null)
                return false;

            RenderTexture rt = tex as RenderTexture;
            if (rt != null)
                return !rt.sRGB;

#if UNITY_2019_1_OR_NEWER
            switch (tex.graphicsFormat)
			{
                case UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8A8_SRGB:
                case UnityEngine.Experimental.Rendering.GraphicsFormat.B8G8R8_SRGB:
                case UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SRGB:
                case UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8_SRGB:
                    return false;
				default:
                    return true;
			}
#else
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    return IsTextureLinear_D3D11(tex.GetNativeTexturePtr());
                case GraphicsDeviceType.Direct3D12:
                    return IsTextureLinear_D3D12(tex.GetNativeTexturePtr());
                default:
                    return true;
            }
#endif
        }


        [DllImport(MixCastInteropPlugin.DllName)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool IsTextureLinear_D3D11(IntPtr srcTex);
        [DllImport(MixCastInteropPlugin.DllName)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool IsTextureLinear_D3D12(IntPtr srcTex);

        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern int CreateOwnedSharedTexture_D3D11(IntPtr srcTex, [MarshalAs(UnmanagedType.U1)] bool linearColor, [MarshalAs(UnmanagedType.U1)] bool ntHandles);
        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern int CreateOwnedSharedTexture_D3D12(IntPtr srcTex, [MarshalAs(UnmanagedType.U1)] bool linearColor, out IntPtr texHandle, out uint texFormat);
        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern int DisposeOwnedSharedTexture(int index);

        [DllImport(MixCastInteropPlugin.DllName)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool CheckOwnedSharedTextureReady_D3D11(int textureIndex, out IntPtr texHandle, out uint texFormat);

        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern IntPtr UpdateOwnedSharedTextureSource_D3D11(int textureIndex, IntPtr srcTex);
        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern IntPtr UpdateOwnedSharedTextureSource_D3D12(int textureIndex, IntPtr srcTex);

        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern IntPtr GetUpdateOwnedSharedTextureFunc();
    }
}
