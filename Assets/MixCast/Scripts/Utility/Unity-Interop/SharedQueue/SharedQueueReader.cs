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
    public class SharedQueueReader_Funcs
    {
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern int CreateSharedQueueReader([MarshalAs(UnmanagedType.LPWStr)] string id, int dataSize);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern int DisposeSharedQueueReader(int index);

        [DllImport(MixCastInteropPlugin.DllName)]
        [return: MarshalAs(UnmanagedType.U1)]
        public static extern bool WaitUntilSharedQueueReaderFilled(int index, int timeout);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern void ReadSharedQueueData(int readerIndex, IntPtr data);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern void MarkSharedQueueReaderEmptied(int index);

        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern IntPtr GetFunc_InsertSharedQueueReaderFence();
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern IntPtr GetFunc_UpdateSharedQueueReaderFences();
    }

    public class SharedQueueReader<T> : IDisposable where T : class
    {
        private static IntPtr insertFenceFunc;

        private int dataSize;

        private int index;

        public SharedQueueReader(string id)
        {
            if (insertFenceFunc == IntPtr.Zero)
                insertFenceFunc = SharedQueueReader_Funcs.GetFunc_InsertSharedQueueReaderFence();

            dataSize = Marshal.SizeOf(typeof(T));

            index = SharedQueueReader_Funcs.CreateSharedQueueReader(id, dataSize);

            MarkEmptied();
        }
        public void Dispose()
        {
            SharedQueueReader_Funcs.DisposeSharedQueueReader(index);
        }

        public bool WaitUntilFilled(int timeout = -1)
        {
            return SharedQueueReader_Funcs.WaitUntilSharedQueueReaderFilled(index, timeout);
        }
        public void Read(ref T data)
        {
            IntPtr tempMem = Marshal.AllocHGlobal(dataSize);
            try
            {
                SharedQueueReader_Funcs.ReadSharedQueueData(index, tempMem);
                Marshal.PtrToStructure(tempMem, data);
            }
            finally
            {
                Marshal.FreeHGlobal(tempMem);
            }
        }

        public void MarkEmptied()
        {
            SharedQueueReader_Funcs.MarkSharedQueueReaderEmptied(index);
        }
        public void InsertEmptiedFence()
        {
            GL.IssuePluginEvent(insertFenceFunc, index);
        }
    }
}
