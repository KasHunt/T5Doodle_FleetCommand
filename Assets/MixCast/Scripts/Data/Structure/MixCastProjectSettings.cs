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

#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
#if MIXCAST_LWRP || MIXCAST_URP || MIXCAST_HDRP
using UnityEngine.Rendering;
#endif
#if MIXCAST_LWRP
using UnityEngine.Rendering.LWRP;
#elif MIXCAST_URP
using UnityEngine.Rendering.Universal;
#endif
#if MIXCAST_HDRP
using UnityEngine.Rendering.HighDefinition;
#endif

namespace BlueprintReality.MixCast
{
    public class MixCastProjectSettings : ScriptableObject
#if UNITY_EDITOR
        , ISerializationCallbackReceiver
#endif
    {
        //Generated
        [SerializeField]
        private string projectId;
        public string ProjectID { get { return projectId; } }

        //Pixel Data
        [Tooltip("This signals whether your transparent materials write Premultiplied Alpha to the alpha channel. " + 
			"This results in more accurate compositing of virtual objects in the foreground but requires AlphaBlend shaders to multiply their output color by their alpha and Additive shaders to produce 0 alpha.")]
        [UnityEngine.Serialization.FormerlySerializedAs("usingPMA")]
        public bool usingPremultipliedAlpha = true;
        [Tooltip("If this is set, MixCast will use the alpha channel before it is potentially modified by Image Effects, rather than the final values after rendering completes."
#if MIXCAST_LWRP || MIXCAST_URP
            + "\n - This should be true when URP/LWRP is used with Post Processing on the MixCast camera, but can be set to false if not for a small performance boost."
#endif
#if MIXCAST_HDRP
            + "\n - This setting has no effect when HDRP is used; HDRP will always result in a valid Alpha Channel (assuming you've set the Color Buffer Format setting correctly)."
#endif
            )]
        public bool grabUnfilteredAlpha = true;
        [Tooltip("This tells MixCast if you're applying any post-processing like Depth of Field, Motion Blur, or Bloom that would modify surrounding pixels, causing " +
            "a loss of accuracy in the depth information of the scene.")]
        public bool doesntOutputGoodDepth = false;

        //Customization
        [Tooltip("If this is set, MixCast will render the scene through the provided Camera rather than a default.")]
        public GameObject layerCamPrefab;
        [Tooltip("This signals that the scene can be rendered with an Opaque background representing the virtual environment")]
        public bool canRenderOpaqueBg = true;
        [Tooltip("This describes layers that should be excluded when rendering the scene with an Opaque background")]
        public LayerMask opaqueOnlyBgLayers = 0;
        [Tooltip("This signals that the scene can be rendered with a Transparent background, possibly allowing for Physical video to be drawn below it.")]
        public bool canRenderTransparentBg = true;
        [Tooltip("This describes layers that should be excluded when rendering the scene with a Transparent background")]
        public LayerMask transparentOnlyBgLayers = 0;


        //Quality
        [Tooltip("This signals whether to take the Anti-aliasing parameters you've configured in the Quality section of your Unity Project Settings, or to use custom parameters. Can't be modified at runtime.")]
        public bool overrideQualitySettingsAA = false;
        [Tooltip("The Anti-aliasing factor to use in MixCast scene rendering. Can't be modified at runtime.")]
        public int overrideAntialiasingVal = 0;

        //Effects
        [Tooltip("This value tells MixCast which Light components should apply to the player (using their Culling Mask field)")]
        public string subjectLayerName = "Default";

        private int subjectLayerVal;
        public int SubjectLayer
		{
			get
			{
                if(subjectLayerVal == -1 && !string.IsNullOrEmpty(subjectLayerName))
                    subjectLayerVal = LayerMask.NameToLayer(subjectLayerName);
                return subjectLayerVal;
            }
		}

        [Tooltip("This signals whether to only apply virtual lighting from Lights with the MixCastLight component attached (better performance but manual) or to scan the scene for Lights automatically")]
        public bool specifyLightsManually = false;
        [Tooltip("This value allows you to tweak the strength of the effect that Directional Lights have on the user in Mixed Reality")]
        public float directionalLightPower = 1f;
        [Tooltip("This value allows you to tweak the strength of the effect that Point Lights have on the user in Mixed Reality")]
        public float pointLightPower = 1f;

        //Editor
        [Tooltip("This signals whether MixCast should only be activatable in Standalone builds when the exe is run with the command line argument '-mixcast'. " +
            "If this is unchecked, you can use the command line argument '-nomixcast' to force MixCast off for the experience at runtime.")]
        public bool requireCommandLineArg = false;
        [Tooltip("This signals whether MixCast should be activatable when running the application within the Unity Editor")]
        public bool enableMixCastInEditor = true;
        [Tooltip("This signals whether MixCast should display the user feed in the Scene View when MixCast is active")]
        public bool displaySubjectInScene = false;
        public bool applySdkFlagsAutomatically = true;

        public MixCastProjectSettings() : base()
        {
            ValidateId();
        }
        void ValidateId()
        {
            if (string.IsNullOrEmpty(projectId))
                projectId = System.Guid.NewGuid().ToString();
        }

#if UNITY_EDITOR
        public void OnBeforeSerialize()
        {
            ValidateId();
        }
        public void OnAfterDeserialize()
        {
            ValidateId();
        }
#endif
    }
}
