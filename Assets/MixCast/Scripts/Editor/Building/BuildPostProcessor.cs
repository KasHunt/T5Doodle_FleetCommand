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

#if UNITY_STANDALONE_WIN
using UnityEngine;
using UnityEditor;
using UnityEditor.Callbacks;

namespace BlueprintReality.MixCast
{
    public class BuildPostProcessor
    {
        public const string VERSION_FILENAME = "version.txt";
        public const string LEGAL_SRC_FILENAME = "mixcast_legal.txt";
        public const string LEGAL_DST_FILENAME = "legal.txt";

        [PostProcessBuild(99)]
        public static void OnPostprocessBuild(BuildTarget target, string pathToBuiltProject)
        {
            if (target != BuildTarget.StandaloneWindows64)
                return;

            string exeName = System.IO.Path.GetFileNameWithoutExtension(pathToBuiltProject);
            string buildFolder = System.IO.Path.GetDirectoryName(pathToBuiltProject);
            string dataFolder = System.IO.Path.Combine(buildFolder, exeName + "_Data");
            string streamingAssetsFolder = System.IO.Path.Combine(dataFolder, "StreamingAssets");
            if (!System.IO.Directory.Exists(streamingAssetsFolder))
                System.IO.Directory.CreateDirectory(streamingAssetsFolder);
            string mixcastFolder = System.IO.Path.Combine(streamingAssetsFolder, "MixCast");
            if (!System.IO.Directory.Exists(mixcastFolder))
                System.IO.Directory.CreateDirectory(mixcastFolder);

            CreateVersionTxt(mixcastFolder);
            CopyLegalTxt(mixcastFolder);
        }

        static void CreateVersionTxt(string mixcastFolder)
        {
            System.IO.File.WriteAllText(System.IO.Path.Combine(mixcastFolder, VERSION_FILENAME), MixCastSdk.VERSION_STRING);
        }

        static void CopyLegalTxt(string mixcastFolder)
        {
            string[] srcFile = System.IO.Directory.GetFiles(Application.dataPath, LEGAL_SRC_FILENAME, System.IO.SearchOption.AllDirectories);
            if (srcFile.Length > 0)
                System.IO.File.Copy(srcFile[0], System.IO.Path.Combine(mixcastFolder, LEGAL_DST_FILENAME), true);
        }
    }
}
#endif
