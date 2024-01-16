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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace BlueprintReality.MixCast
{

    [InitializeOnLoad]
    public class ShaderTransparencyWizard : EditorWindow
    {
        private const string HEADER_TEXT = "This wizard makes it easy to enable full transparency support for MixCast without impacting rendering for the player, using Pre-Multiplied Alpha. You should only use this wizard if you plan on enabling \"Using PMA\" in MixCast's Project Settings.";
        private const string PROJECT_WIDE_HELP = "This operation will detect transparent shaders in the project, generate updated versions of those shaders, then update all affected materials to use the replacement shaders.\nThis operation needs to be re-run any time your old transparent shaders are modified, new transparent shaders are written, or new materials are created referring to the old shaders.";
        private const string STEP_ONE_HELP = "Choose a folder containing transparent shaders to generate PMA replacements for. The replacement files will be placed next to the originals.";
        private const string STEP_TWO_HELP = "Choose a folder of materials using non-PMA shaders that you've generated replacements for.";
        private const string REVERT_STEP_HELP = "Only use this if you need to revert materials to original shaders";

        [MenuItem("MixCast/Fix Shaders", priority = 5)]
        public static void ShowWindow()
        {
            EditorWindow.GetWindow(typeof(ShaderTransparencyWizard));
        }

        private static string LastSelectedFolder
        {
            get
            {
                return EditorPrefs.GetString(Application.productName + "_MC_ShaderFolder", "");
            }
            set
            {
                EditorPrefs.SetString(Application.productName + "_MC_ShaderFolder", value);
            }
        }


        int mode = 0;
        private void OnGUI()
        {
            EditorGUILayout.LabelField("Shader Transparency Wizard", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(HEADER_TEXT, MessageType.Info);

            EditorGUILayout.Space();

            mode = GUILayout.Toolbar(mode, new string[] { "Auto", "Manual" });
            EditorGUILayout.Space();

            GUILayout.BeginVertical(EditorStyles.helpBox);
            if (mode == 0)
            {
                EditorGUILayout.LabelField("Automatic Project Update", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(PROJECT_WIDE_HELP, MessageType.None);
                GUI.color = Color.Lerp(Color.green, Color.white, 0.5f);
                if (GUILayout.Button("Generate Shaders and Update Materials", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                {
                    coroutines.Add(GenerateShaders(false, () => SwapShaders(false, true)));
                }
                GUI.color = Color.white;
            }
            else
            {
                EditorGUILayout.LabelField("Step 1", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(STEP_ONE_HELP, MessageType.None);
                GUI.color = Color.Lerp(Color.green, Color.white, 0.5f);
                if (GUILayout.Button("1 - Generate PMA Shaders", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                {
                    coroutines.Add(GenerateShaders(true, null));
                }
                GUI.color = Color.white;

                EditorGUILayout.Space();

                EditorGUILayout.LabelField("Step 2", EditorStyles.boldLabel);
                EditorGUILayout.HelpBox(STEP_TWO_HELP, MessageType.None);
                //GUILayout.BeginHorizontal();
                GUI.color = Color.Lerp(Color.green, Color.white, 0.5f);
                if (GUILayout.Button("2 - Update Materials", GUILayout.Height(EditorGUIUtility.singleLineHeight * 2)))
                {
                    SwapShaders(true, true);
                }
                GUI.color = Color.white;

                EditorGUILayout.HelpBox(REVERT_STEP_HELP, MessageType.None);
                GUI.color = Color.Lerp(Color.red, Color.white, 0.5f);
                if (GUILayout.Button("Revert Materials"))
                {
                    SwapShaders(true, false);
                }
                GUI.color = Color.white;
            }
            EditorGUILayout.EndVertical();
        }

        IEnumerator GenerateShaders(bool displayPopup, System.Action onComplete)
        {
            string shaderFolder = LastSelectedFolder;
            if (string.IsNullOrEmpty(shaderFolder))
                shaderFolder = Application.dataPath;

            if (displayPopup)
                shaderFolder = EditorUtility.OpenFolderPanel("Shader Folder", shaderFolder, "");
            else
                shaderFolder = Application.dataPath;

            if (string.IsNullOrEmpty(shaderFolder))
                yield break;

            if (!Directory.Exists(shaderFolder))
                yield break;
            if (!shaderFolder.StartsWith(Application.dataPath))
            {
                Debug.LogError("Didn't select a folder in the project!");
                yield break;
            }
            LastSelectedFolder = shaderFolder;

            EditorUtility.DisplayProgressBar("Generating Shaders...", "", 0);

            for (int i = 0; i < 3; i++)
                yield return null;

            List<string> modifiedShaders = new List<string>();
            string[] shaderPaths = Directory.GetFiles(shaderFolder, "*.shader", SearchOption.AllDirectories);
            for (int i = 0; i < shaderPaths.Length; i++)
            {
                if (shaderPaths[i].EndsWith(" PMA.shader"))
                    continue;

                bool upgraded = false;
                string[] shaderContents = File.ReadAllLines(shaderPaths[i]);
                string shaderName = GetShaderName(shaderContents);
                if (shaderName.StartsWith("MixCast/") || shaderName.StartsWith("Hidden/MixCast"))
                    continue;

                for (int j = 0; j < shaderContents.Length; j++)
                {
                    string oldContents = shaderContents[j];

                    shaderContents[j] = CorrectBlend(shaderContents[j]);
                    upgraded |= shaderContents[j] != oldContents;

                    shaderContents[j] = CorrectShaderName(shaderContents[j]);
                    shaderContents[j] = CorrectBlendOp(shaderContents[j]);
                    shaderContents[j] = CorrectColorMask(shaderContents[j]);

                    bool inFrag = false;
                    for (int k = 0; k < j; k++)
                    {
                        if (!shaderContents[k].TrimStart().StartsWith("#pragma") && shaderContents[k].Contains("frag"))
                            inFrag = true;
                    }
                    if (inFrag)
                        shaderContents[j] = CorrectFinalColor(shaderContents[j]);
                }

                if (upgraded)
                {
                    string newShaderPath = shaderPaths[i].Replace(".shader", " PMA.shader");
                    File.WriteAllLines(newShaderPath, shaderContents);
                    modifiedShaders.Add(newShaderPath.Substring(Application.dataPath.Length + 1));
                }

                yield return null;
            }

            AssetDatabase.Refresh();

            StringWriter stringWriter = new StringWriter();
            stringWriter.WriteLine(string.Format("Generated {0} shaders", modifiedShaders.Count));
            for (int i = 0; i < modifiedShaders.Count; i++)
                stringWriter.WriteLine("    " + modifiedShaders[i]);
            Debug.Log(stringWriter.ToString());

            EditorUtility.ClearProgressBar();

            if (onComplete != null)
                onComplete();
        }

        string CorrectBlend(string shaderLine)
        {
            if (shaderLine.TrimStart().StartsWith("Blend "))
            {
                int opStartIndex = shaderLine.IndexOf("Blend ") + "Blend ".Length;
                int commentStartIndex = shaderLine.IndexOf("//");
                string blendPrefix = shaderLine.Substring(0, opStartIndex);
                string blendOp;
                if (commentStartIndex == -1)
                    blendOp = shaderLine.Substring(opStartIndex).Trim();
                else
                    blendOp = shaderLine.Substring(opStartIndex, commentStartIndex - opStartIndex - 1).Trim();
                string blendSuffix = "";
                if (commentStartIndex != -1)
                    blendSuffix = shaderLine.Substring(commentStartIndex);

                switch (blendOp)
                {
                    case "One One":
                        return blendPrefix + "One One, Zero One" + blendSuffix;

                    case "SrcAlpha One":
                    case "SrcAlpha One, Zero One":
                        return blendPrefix + "One One, Zero One" + blendSuffix;

                    case "SrcAlpha OneMinusSrcAlpha":
                        return blendPrefix + "One OneMinusSrcAlpha, OneMinusDstAlpha One" + blendSuffix;

                    case "One SrcAlpha":
                        return blendPrefix + "One SrcAlpha, Zero One";
                }
            }
            return shaderLine;
        }
        string CorrectShaderName(string shaderLine)
        {
            if (shaderLine.TrimStart().StartsWith("Shader "))
            {
                int firstQuoteIndex = shaderLine.IndexOf('\"');
                int lastQuoteIndex = shaderLine.LastIndexOf('\"');
                string oldName = shaderLine.Substring(firstQuoteIndex + 1, lastQuoteIndex - firstQuoteIndex - 1);
                string newName = oldName + " PMA";

                string namePrefix = shaderLine.Substring(0, firstQuoteIndex + 1);
                string nameSuffix = shaderLine.Substring(lastQuoteIndex);
                return namePrefix + newName + nameSuffix;
            }
            return shaderLine;
        }
        string GetShaderName(string[] lines)
        {
            for (int i = 0; i < lines.Length; i++)
            {
                string shaderLine = lines[i];
                if (shaderLine.TrimStart().StartsWith("Shader "))
                {
                    int firstQuoteIndex = shaderLine.IndexOf('\"');
                    int lastQuoteIndex = shaderLine.LastIndexOf('\"');
                    return shaderLine.Substring(firstQuoteIndex + 1, lastQuoteIndex - firstQuoteIndex - 1);
                }
            }
            return "";
        }
        string CorrectBlendOp(string shaderLine)
        {
            if (shaderLine.TrimStart().StartsWith("BlendOp "))
            {
                return "//" + shaderLine;
            }
            return shaderLine;
        }
        string CorrectColorMask(string shaderLine)
        {
            if (shaderLine.TrimStart().StartsWith("ColorMask "))
            {
                int opStartIndex = shaderLine.IndexOf("ColorMask ") + "ColorMask ".Length;
                string blendPrefix = shaderLine.Substring(0, opStartIndex);
                return blendPrefix + "RGBA";
            }
            return shaderLine;
        }
        string CorrectFinalColor(string shaderLine)
        {
            if (shaderLine.TrimStart().StartsWith("return "))
            {
                int opStartIndex = shaderLine.IndexOf("return ") + "return ".Length;
                int opEndIndex = shaderLine.LastIndexOf(';');
                string opPrefix = shaderLine.Substring(0, shaderLine.IndexOf("return "));
                string opString = shaderLine.Substring(opStartIndex, opEndIndex - opStartIndex);
                string assignOp = "float4 finalFinalColor = " + opString + "; ";
                string multOp = "finalFinalColor.rgb *= finalFinalColor.a; ";
                string returnOp = "return finalFinalColor;";
                return opPrefix + assignOp + "\n"
                    + opPrefix + multOp + "\n"
                    + opPrefix + returnOp;
            }
            return shaderLine;
        }


        void SwapShaders(bool displayPopup, bool toPMA = true)
        {
            string materialFolder = LastSelectedFolder;
            if (string.IsNullOrEmpty(materialFolder))
                materialFolder = Application.dataPath;

            if (displayPopup)
                materialFolder = EditorUtility.OpenFolderPanel("Material Folder", materialFolder, "");
            else
                materialFolder = Application.dataPath;

            if (string.IsNullOrEmpty(materialFolder))
                return;

            if (!Directory.Exists(materialFolder))
                return;
            LastSelectedFolder = materialFolder;


            string pathInAssets = LastSelectedFolder.Substring(LastSelectedFolder.IndexOf("/Assets") + 1);
            string[] guids = AssetDatabase.FindAssets("t:Material", pathInAssets.Length > 0 ? new string[] { pathInAssets } : null);

            Dictionary<Material, Shader> swappingMaterials = new Dictionary<Material, Shader>();
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat != null)
                {
                    if (toPMA)
                    {
                        Shader replace = Shader.Find(mat.shader.name + " PMA");
                        if (replace != null)
                        {
                            swappingMaterials[mat] = replace;
                        }
                    }
                    else
                    {
                        if (mat.shader.name.EndsWith(" PMA"))
                        {
                            Shader replace = Shader.Find(mat.shader.name.Substring(0, mat.shader.name.Length - " PMA".Length));
                            if (replace != null)
                            {
                                swappingMaterials[mat] = replace;
                            }
                        }
                    }
                }
            }
            Undo.RecordObjects(new List<Material>(swappingMaterials.Keys).ToArray(), "Swap Shaders");
            foreach (Material mat in swappingMaterials.Keys)
            {
                mat.shader = swappingMaterials[mat];
                EditorUtility.SetDirty(mat);
            }
            AssetDatabase.Refresh();

            StringWriter stringWriter = new StringWriter();
            stringWriter.WriteLine(string.Format("Updated shader reference on {0} material(s)", swappingMaterials.Count));
            foreach (Material mat in swappingMaterials.Keys)
                stringWriter.WriteLine("    " + AssetDatabase.GetAssetPath(mat).Substring("Assets/".Length));
            Debug.Log(stringWriter.ToString());
        }


        static ShaderTransparencyWizard()
        {
            EditorApplication.update += Update;
        }

        //Run coroutines
        static List<IEnumerator> coroutines = new List<IEnumerator>();

        private static void Update()
        {
            for (int i = coroutines.Count - 1; i >= 0; i--)
                if (!coroutines[i].MoveNext())
                    coroutines.RemoveAt(i);
        }
    }
}
