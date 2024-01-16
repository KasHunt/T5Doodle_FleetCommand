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
using System.Collections;
using BlueprintReality.MixCast.Shared;
using BlueprintReality.MixCast.Data;
using BlueprintReality.SharedTextures;

namespace BlueprintReality.MixCast.Viewfinders
{
    public class ExpViewfinderBehaviour : MonoBehaviour
    {
        public IdentifierContext viewfinderContext;

        public Transform positionTransform;
        public Transform rotationTransform;
        public Transform configurableScaleTransform;

        public Renderer displayRenderer;
        private Material displayMat;
        public Transform aspectRatioScaleTransform;
        public string texPropName = "_MainTex";

        private SharedTextureReceiver texReceiver;

        private void Awake()
        {
            displayMat = displayRenderer.material;
        }

        private void OnEnable()
        {
            if (!string.IsNullOrEmpty(viewfinderContext.Identifier))
            {
                texReceiver = SharedTextureReceiver.Create(SharedTexIds.Viewfinders.GetIdForViewfinderView(viewfinderContext.Identifier));
                texReceiver.OnTextureChanged += HandleTextureChange;
                HandleTextureChange();
            }
        }
        private void OnDisable()
        {
            if (texReceiver != null)
            {
                texReceiver.OnTextureChanged -= HandleTextureChange;
                texReceiver.Dispose();
                texReceiver = null;
            }
        }

        void HandleTextureChange()
        {
            displayMat.SetTexture(texPropName, texReceiver != null ? texReceiver.Texture : null);
            displayRenderer.gameObject.SetActive(texReceiver.Texture != null);
        }

        private void LateUpdate()
        {
            Viewfinder viewfinder = MixCastSdkData.GetViewfinderWithId(viewfinderContext.Identifier);
            if (viewfinder == null)
                return;

            if (!texReceiver.RequestSucceeded)
                texReceiver.RefreshTextureInfo();

            if (positionTransform != null)
                positionTransform.localPosition = viewfinder.CurrentPosition.unity;
            if (rotationTransform != null)
                rotationTransform.localRotation = viewfinder.CurrentRotation.unity;

            Vector3 aspectScale = Vector3.one;
            if (texReceiver.Texture != null)
                aspectScale.x = (float)texReceiver.Texture.width / texReceiver.Texture.height;

            if (configurableScaleTransform == aspectRatioScaleTransform)
            {
                configurableScaleTransform.localScale = Vector3.Scale(viewfinder.CurrentScale.unity, aspectScale);
            }
            else
            {
                configurableScaleTransform.localScale = viewfinder.CurrentScale.unity;
                aspectRatioScaleTransform.transform.localScale = aspectScale;
            }
        }
    }
}
#endif
