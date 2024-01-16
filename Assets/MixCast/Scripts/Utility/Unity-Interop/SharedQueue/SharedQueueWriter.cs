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
    public class SharedQueueWriter_Funcs
    {
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern int CreateSharedQueueWriter([MarshalAs(UnmanagedType.LPWStr)] string id, int dataSize);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern int DisposeSharedQueueWriter(int writerIndex);

        [DllImport(MixCastInteropPlugin.DllName)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool WaitUntilSharedQueueWriterEmptied(int writerIndex, int timeout);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern void WriteSharedQueueData(int writerIndex, IntPtr data);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern void MarkSharedQueueWriterFilled(int writerIndex);

        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern IntPtr GetFunc_InsertSharedQueueWriterFence();
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern IntPtr GetFunc_UpdateSharedQueueWriterFences();
    }

    public class SharedQueueWriter<T> : IDisposable where T : class
    {
        private static IntPtr insertFenceFunc;

        private int dataSize;

        private int index;

        public SharedQueueWriter(string id)
        {
            if (insertFenceFunc == IntPtr.Zero)
                insertFenceFunc = SharedQueueWriter_Funcs.GetFunc_InsertSharedQueueWriterFence();

            dataSize = Marshal.SizeOf(typeof(T));
            index = SharedQueueWriter_Funcs.CreateSharedQueueWriter(id, dataSize);
        }
        public void Dispose()
        {
            SharedQueueWriter_Funcs.DisposeSharedQueueWriter(index);
        }

        public bool WaitUntilEmptied(int timeout = -1)
        {
            return SharedQueueWriter_Funcs.WaitUntilSharedQueueWriterEmptied(index, timeout);
        }
        public void Write(T data, bool requiresGpuOp)
        {
            IntPtr tempMem = Marshal.AllocHGlobal(dataSize);
            try
            {
                Marshal.StructureToPtr(data, tempMem, false);
                SharedQueueWriter_Funcs.WriteSharedQueueData(index, tempMem);
            }
            finally
            {
                Marshal.FreeHGlobal(tempMem);
            }

            if (!requiresGpuOp)
                MarkFilled();
            else
                InsertFilledFence();
        }

        void MarkFilled()
        {
            SharedQueueWriter_Funcs.MarkSharedQueueWriterFilled(index);
        }
        void InsertFilledFence()
        {
            GL.IssuePluginEvent(insertFenceFunc, index);
        }
    }
}
