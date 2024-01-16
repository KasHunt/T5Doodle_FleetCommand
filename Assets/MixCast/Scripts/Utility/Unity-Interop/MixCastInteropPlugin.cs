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

using AOT;
using System;
using System.Runtime.InteropServices;
using UnityEngine;

namespace BlueprintReality.MixCast.Interprocess
{
    public class MixCastInteropPlugin : MonoBehaviour
    {
        public const string DllName = "MixCast-Unity-Interop";

        public const string QueueDescFilenameFormat = "MixCast-Queue({0})-Desc";
        public const string SurfaceDescFilenameFormat = "MixCast-Tex({0})-Desc";

        public static event Action pluginUpdate;

        IntPtr checkQueueWriteFencesFunc;
        IntPtr checkQueueReadFencesFunc;

        [RuntimeInitializeOnLoadMethod]
        static void Init()
        {
            RegisterDebugCallback(OnDebugCallback);
        }


        void Awake()
        {
            checkQueueWriteFencesFunc = SharedQueueWriter_Funcs.GetFunc_UpdateSharedQueueWriterFences();
            checkQueueReadFencesFunc = SharedQueueReader_Funcs.GetFunc_UpdateSharedQueueReaderFences();
            UnityThreadUtility.EnsureExists();
        }

        private void Update()
        {
            if (pluginUpdate != null)
                pluginUpdate.Invoke();

            GL.IssuePluginEvent(checkQueueWriteFencesFunc, 0);
            GL.IssuePluginEvent(checkQueueReadFencesFunc, 0);
        }
        private void LateUpdate()
        {
            Update();
        }

        //------------------------------------------------------------------------------------------------
        enum Style { Normal, Warning, Error }

        public delegate void DebugCallback(IntPtr request, int color, int size);

        [DllImport(DllName, CallingConvention = CallingConvention.Cdecl)]
        static extern void RegisterDebugCallback(DebugCallback cb);

        [MonoPInvokeCallback(typeof(DebugCallback))]
        public static void OnDebugCallback(IntPtr request, int styleVal, int size)
        {
            //Ptr to string
            string str = Marshal.PtrToStringAnsi(request, size);

            switch ((Style)styleVal)
            {
                case Style.Normal:
                    Debug.Log(str);
                    break;
                case Style.Warning:
                    Debug.LogWarning(str);
                    break;
                case Style.Error:
                    Debug.LogError(str);
                    break;
            }
        }
    }
}
