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
using System.Threading;
using UnityEngine;

namespace BlueprintReality.MixCast
{
    public class UnityThreadUtility : MonoBehaviour
    {
        private static UnityThreadUtility instance;
        private static bool gotDestroyed = false;

        public static void EnsureExists()
        {
            if (instance == null && !gotDestroyed)
            {
                GameObject obj = new GameObject("MainThreadRunner")
                {
                    hideFlags = HideFlags.HideAndDontSave,
                };
                DontDestroyOnLoad(obj);

                instance = obj.AddComponent<UnityThreadUtility>();
            }
        }

        public static void RunOnMainThread(Action action)
        {
            EnsureExists();
            if (Thread.CurrentThread == instance.mainThread)
                action();
            else
                instance.EnqueueAction(action);
        }

        protected Thread mainThread;
        private List<Action> actions = new List<Action>();

        private List<Action> tempActionList = new List<Action>();

        protected void EnqueueAction(Action action)
        {
            lock (actions)
                actions.Add(action);
        }

        private void Awake()
        {
            mainThread = Thread.CurrentThread;
        }

        private void Update()
        {
            lock (actions)
            {
                for (int i = 0; i < actions.Count; i++)
                    tempActionList.Add(actions[i]);
                actions.Clear();
            }

            for (int i = 0; i < tempActionList.Count; i++)
                tempActionList[i].Invoke();

            tempActionList.Clear();
        }
        private void OnDestroy()
        {
            gotDestroyed = true;
        }
    }
}
