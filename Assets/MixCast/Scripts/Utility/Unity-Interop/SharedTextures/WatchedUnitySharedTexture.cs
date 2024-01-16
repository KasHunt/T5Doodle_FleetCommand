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
    public class WatchedUnitySharedTexture : IDisposable
    {
        private static IntPtr acquireSyncFunc = IntPtr.Zero, releaseSyncFunc = IntPtr.Zero;

        private int index = -1;

        public IntPtr SrcTexHandle { get; protected set; }
        public Texture2D Texture { get; protected set; }

        private bool isReady;

        public WatchedUnitySharedTexture(IntPtr srcProcHandle, IntPtr remoteTexHandle)
        {
            Initialize(srcProcHandle, remoteTexHandle, true);
        }
        public WatchedUnitySharedTexture(IntPtr remoteTexHandle)
        {
            Initialize(IntPtr.Zero, remoteTexHandle, false);
        }
        void Initialize(IntPtr srcProcHandle, IntPtr remoteTexHandle, bool ntHandleMode)
        {
            if (acquireSyncFunc == IntPtr.Zero)
                acquireSyncFunc = GetWatchedSharedTex_AcquireSyncFunc();
            if (releaseSyncFunc == IntPtr.Zero)
                releaseSyncFunc = GetWatchedSharedTex_ReleaseSyncFunc();

            SrcTexHandle = remoteTexHandle;

            IntPtr texPtr = IntPtr.Zero;
            uint width = 0, height = 0, fmt = 0;
            switch (SystemInfo.graphicsDeviceType)
            {
                case GraphicsDeviceType.Direct3D11:
                    if (ntHandleMode)
                        index = CreateWatchedSharedTex_D3D11_NT(srcProcHandle, remoteTexHandle); 
                    else
                        index = CreateWatchedSharedTex_D3D11(remoteTexHandle);

                    if (index != -1)
                    {
                        isReady = CheckWatchedSharedTexReady_D3D11(index, out texPtr, out width, out height, out fmt);
                        if (isReady)
                        {
                            Texture = Texture2D.CreateExternalTexture((int)width, (int)height,
                                GetUnityFormatFromDirectXFormat(fmt), false, IsFormatLinear(fmt),
                                texPtr);
                        }
                    }
                    break;

                case GraphicsDeviceType.Direct3D12:
                    index = CreateWatchedSharedTex_D3D12(srcProcHandle, remoteTexHandle, out texPtr, out width, out height, out fmt);
                    if (index != -1)
                    {
                        isReady = true;
                        Texture = Texture2D.CreateExternalTexture((int)width, (int)height, 
                            GetUnityFormatFromDirectXFormat(fmt), false, IsFormatLinear(fmt), 
                            texPtr);
                    }
                    break;
            }
        }

        public void Dispose()
        {
            if (Texture != null)
            {
                MonoBehaviour.Destroy(Texture);
            }
            if(index != -1)
            {
                ReleaseWatchedSharedTex(index);
                index = -1;
            }
            SrcTexHandle = IntPtr.Zero;
        }

        //Affects render thread
        public void AcquireSync()
        {
            if (index != -1)
            {
                if (!isReady)
                {
                    IntPtr texPtr;
                    uint width, height, fmt;
                    isReady = CheckWatchedSharedTexReady_D3D11(index, out texPtr, out width, out height, out fmt);
                    if (isReady)
                    {
                        Texture = Texture2D.CreateExternalTexture((int)width, (int)height,
                            GetUnityFormatFromDirectXFormat(fmt), false, IsFormatLinear(fmt),
                            texPtr);
                    }
                }
                GL.IssuePluginEvent(acquireSyncFunc, index);
            }
        }
        public void ReleaseSync()
        {
            if (index != -1)
                GL.IssuePluginEvent(releaseSyncFunc, index);
        }

        public static TextureFormat GetUnityFormatFromDirectXFormat(uint format)
        {
            switch (format)
            {
                case 2:
                    return TextureFormat.RGBAFloat;
                case 10:
                    return TextureFormat.RGBAHalf;
                case 24:
                case 27:
                case 28:
                case 29:
                    return TextureFormat.RGBA32;
                case 41:
                    return TextureFormat.RFloat;
                case 56:
                    return TextureFormat.R16;
                case 65:
                    return TextureFormat.Alpha8;
                case 87:
                case 91:
                    return TextureFormat.BGRA32;

                default:
                    // Add more pixel format support
                    Debug.LogError("Unsupported Pixel Format:" + format + "!!!");
                    return TextureFormat.RGBA32;
            }
        }
        public static bool IsFormatLinear(uint format)
		{
            switch(format)
			{
                case 29:
                case 91:
                    return false;
                default:
                    return true;
			}
		}

        delegate void WatchedSharedTextureReadyCallback(IntPtr sharedTex, uint width, uint height, uint format);

        //D3D11 source and dest
        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern int CreateWatchedSharedTex_D3D11(IntPtr texHandle);

        //D3D12 source and D3D11 dest
        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern int CreateWatchedSharedTex_D3D11_NT(IntPtr srcProcHandle, IntPtr texHandle);

        //D3D12 source and dest
        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern int CreateWatchedSharedTex_D3D12(IntPtr srcProcHandle, IntPtr remoteTexHandle,
            out IntPtr sharedTexPtr, out uint width, out uint height, out uint format);

        [DllImport(MixCastInteropPlugin.DllName)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool CheckWatchedSharedTexReady_D3D11(int textureIndex,
            out IntPtr sharedTexPtr, out uint width, out uint height, out uint format);

        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern void ReleaseWatchedSharedTex(int index);

        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern IntPtr GetWatchedSharedTex_AcquireSyncFunc();
        [DllImport(MixCastInteropPlugin.DllName)]
        private static extern IntPtr GetWatchedSharedTex_ReleaseSyncFunc();
    }
}
