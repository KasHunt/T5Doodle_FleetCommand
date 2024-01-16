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
    class SharedQueueConsumer_Funcs
    {
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern int CreateSharedQueueConsumer([MarshalAs(UnmanagedType.LPWStr)] string id, out int capacity);
        [DllImport(MixCastInteropPlugin.DllName)]
        public static extern void DisposeSharedQueueConsumer(int index);
    }

    public class SharedQueueConsumer<T> : IDisposable where T : class
    {
        const string ElementIdFormat = "{0}[{1}]";

        public string ID { get; protected set; }

        private int index;
        public bool FoundProducer { get { return index != -1; } }

        private List<SharedQueueReader<T>> elements = new List<SharedQueueReader<T>>();
        public int Capacity { get { return elements.Count; } }
        public int ConsumedCount { get; protected set; }
        public int CurrentSlot
        {
            get
            {
                return ConsumedCount % elements.Count;
            }
        }
        SharedQueueReader<T> CurrentElement
        {
            get
            {
                return elements[CurrentSlot];
            }
        }

        public SharedQueueConsumer(string id)
		{
            ID = id;

            TryConnect();
		}
        bool TryConnect()
		{
            int capacity;
            index = SharedQueueConsumer_Funcs.CreateSharedQueueConsumer(ID, out capacity);
            if (index != -1)
            {
                for (int i = 0; i < capacity; i++)
                    elements.Add(new SharedQueueReader<T>(string.Format(ElementIdFormat, ID, i)));
            }
            return index != -1;
        }
        public void Dispose()
		{
            if (FoundProducer)
            {
                for (int i = elements.Count - 1; i >= 0; i--)
                    elements[i].Dispose();
                elements.Clear();

                SharedQueueConsumer_Funcs.DisposeSharedQueueConsumer(index);
            }
		}

        public bool WaitUntilNextFilled(int timeout = 0)
		{
            if (!FoundProducer && !TryConnect())
                return false;   //producer not found

            return CurrentElement.WaitUntilFilled(timeout);
		}
        public void Read(ref T data)
        {
            CurrentElement.Read(ref data);
        }

        public void MarkEmptied()
		{
            CurrentElement.MarkEmptied();
            ConsumedCount++;
        }
        public void InsertEmptiedFence()
		{
            CurrentElement.InsertEmptiedFence();
            ConsumedCount++;
        }
    }
}
