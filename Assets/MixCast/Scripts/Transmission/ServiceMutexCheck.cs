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

using BlueprintReality.MixCast;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using UnityEngine;

namespace BlueprintReality.MixCast.Client
{
    public class ServiceMutexCheck : IDisposable
    {
        Thread waitThread;

        Action lostService;

        public ServiceMutexCheck(Action lostService)
        {
            this.lostService = lostService;

            waitThread = new Thread(WaitForServiceCloseInThread);
            waitThread.Start();
        }

        public void Dispose()
        {
            EndWaitForService();
            waitThread.Join();
        }

        void WaitForServiceCloseInThread()
        {
            if (WaitForServiceClose())
            {
                if (lostService != null)
                    lostService.Invoke();
            }
        }

        [DllImport(Interprocess.MixCastInteropPlugin.DllName)]
        [return: MarshalAs(UnmanagedType.U1)]
        private static extern bool WaitForServiceClose();

        [DllImport(Interprocess.MixCastInteropPlugin.DllName)]
        private static extern void EndWaitForService();
    }
}
