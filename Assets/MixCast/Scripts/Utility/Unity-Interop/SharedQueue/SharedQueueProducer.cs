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

namespace BlueprintReality.MixCast.Interprocess
{
    class SharedQueueProducer_Funcs
    {
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern int CreateSharedQueueProducer([MarshalAs(UnmanagedType.LPWStr)] string id, int capacity);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern void DisposeSharedQueueProducer(int index);
    }

    public class SharedQueueProducer<T> : IDisposable where T : class
    {
        const string ElementIdFormat = "{0}[{1}]";

        private int index;

        private List<SharedQueueWriter<T>> elements = new List<SharedQueueWriter<T>>();
        public int Capacity
		{
            get
			{
                return elements.Count;
			}
		}

        public int ProducedCount { get; protected set; }
        public int CurrentSlot
        {
            get
            {
                return ProducedCount % elements.Count;
            }
        }
        SharedQueueWriter<T> CurrentElement
        {
            get
            {
                return elements[CurrentSlot];
            }
        }

        public SharedQueueProducer(string id, int capacity)
        {
            for (int i = 0; i < capacity; i++)
                elements.Add(new SharedQueueWriter<T>(string.Format(ElementIdFormat, id, i)));
            index = SharedQueueProducer_Funcs.CreateSharedQueueProducer(id, capacity);
        }
        public void Dispose()
        {
            SharedQueueProducer_Funcs.DisposeSharedQueueProducer(index);
            for (int i = elements.Count - 1; i >= 0; i--)
                elements[i].Dispose();
            elements.Clear();
        }

        public bool WaitUntilNextEmptied(int timeout = 0)
        {
            return CurrentElement.WaitUntilEmptied(timeout);
        }
        public void Write(T data, bool requiresGpuOp)
        {
            CurrentElement.Write(data, requiresGpuOp);
            ProducedCount++;
        }
    }
}
