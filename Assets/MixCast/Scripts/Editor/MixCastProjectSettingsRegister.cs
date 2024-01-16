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

#if UNITY_2018_3_OR_NEWER
using UnityEditor;
using BlueprintReality.MixCast.Data;

namespace BlueprintReality.MixCast
{
    public static class MixCastProjectSettingsRegister
    {
        static SerializedObject serializedObj;

        [SettingsProvider]
        public static SettingsProvider CreateSettingsProvider()
        {
            var provider = new SettingsProvider("Project/MixCast", SettingsScope.Project)
            {
                activateHandler = (searchContext, rootElement) =>
                {
                    serializedObj = new SerializedObject(MixCastSdkData.ProjectSettings);
                },
                deactivateHandler = () =>
                {
                    serializedObj = null;
                },
                guiHandler = searchContext => MixCastProjectSettingsInspector.DrawInspector(serializedObj, false),
            };

            return provider;
        }
    }
}
#endif
