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
using BlueprintReality.MixCast.Interprocess;
using BlueprintReality.MixCast.Thrift;
using BlueprintReality.Thrift.SharedTextures;
using BlueprintReality.Unity;
using System;
using System.Collections;
using UnityEngine;

namespace BlueprintReality.SharedTextures
{
    [Serializable]
    public class SharedTextureReceiver : IDisposable
    {
        public static Func<string, SharedTextureReceiver> Create { get; private set; }
        static SharedTextureReceiver()
        {
            Create = (string texId) =>
            {
                return new SharedTextureReceiver(texId);
            };
        }
        public static void OverrideCreateFunc(Func<string, SharedTextureReceiver> newFunc)
        {
            Create = newFunc;
        }

        public string TextureId { get; protected set; }

        public bool RequestSucceeded { get; protected set; }

        protected Texture localTex;

        protected WatchedUnitySharedTexture sharedTex;
        protected RenderTexture sharedCopyTex;

        public Texture Texture
        {
            get
            {
                if (localTex != null)
                    return localTex;
                else if (sharedTex != null)
                    return sharedCopyTex;
                else
                    return null;
            }
        }


        protected TextureWrapMode wrapMode = TextureWrapMode.Clamp;
        public TextureWrapMode WrapMode
        {
            get
            {
                return wrapMode;
            }
            set
            {
                wrapMode = value;
                if (Texture != null)
                    Texture.wrapMode = wrapMode;
            }
        }

        protected FilterMode filterMode = FilterMode.Bilinear;
        public FilterMode FilterMode
        {
            get
            {
                return filterMode;
            }
            set
            {
                filterMode = value;
                if (Texture != null)
                    Texture.filterMode = filterMode;
            }
        }

        protected SharedTex textureInfo;

        public event Action OnTextureChanged;

        protected SharedTextureReceiver(string texId)
        {
            TextureId = texId;
            RefreshTextureInfo();

            MixCastInteropPlugin.pluginUpdate += CopyOverTex;
        }
        public virtual void Dispose()
        {
            MixCastInteropPlugin.pluginUpdate -= CopyOverTex;

            if (localTex != null)
            {
                localTex = null;

                if (OnTextureChanged != null)
                    OnTextureChanged.Invoke();
            }
            if (sharedTex != null)
            {
                ClearWatchedSharedTexture();

                if (OnTextureChanged != null)
                    OnTextureChanged.Invoke();
            }
        }

        void CopyOverTex()
        {
            if(sharedTex != null && sharedTex.Texture != null)
            {
                sharedTex.AcquireSync();
                //Graphics.Blit(sharedTex.Texture, sharedCopyTex);
                Graphics.CopyTexture(sharedTex.Texture, sharedCopyTex);
                sharedTex.ReleaseSync();
                //RenderTexture.active = null;
            }
        }

        public virtual void RefreshTextureInfo()
        {
            SharedTextureCommunication.Client client = string.IsNullOrEmpty(SharedTexturePlugin.manualThriftAddress) ?
                UnityThriftMixCastClient.Get<SharedTextureCommunication.Client>() :
                UnityThriftMixCastClient.Get<SharedTextureCommunication.Client>(SharedTexturePlugin.manualThriftAddress);

            RequestSucceeded = client.TrySharedTextureRequest(TextureId, out textureInfo);
            if (RequestSucceeded)
                RefreshWatchedSharedTexture();
        }
        protected void RefreshWatchedSharedTexture()
        {
            if (textureInfo == null)
                return;

            if (sharedTex != null && sharedTex.SrcTexHandle.ToInt64() == textureInfo.Handle)
                return;

            ClearWatchedSharedTexture();

            if (textureInfo.Handle != 0)
            {
                if (textureInfo.ProcId != 0)
                {
                    System.Diagnostics.Process srcProc = System.Diagnostics.Process.GetProcessById((int)textureInfo.ProcId);
                    sharedTex = new WatchedUnitySharedTexture(srcProc.Handle, (IntPtr)textureInfo.Handle);
                }
                else
                    sharedTex = new WatchedUnitySharedTexture((IntPtr)textureInfo.Handle);

                if (sharedTex.Texture != null)
                {
                    sharedTex.Texture.wrapMode = wrapMode;
                    sharedTex.Texture.filterMode = filterMode;

                    sharedCopyTex = new RenderTexture(sharedTex.Texture.width, sharedTex.Texture.height, 0, sharedTex.Texture.GetUncompressedRenderTextureFormat(), RenderTextureReadWrite.Linear)
                    {
                        wrapMode = wrapMode,
                        filterMode = filterMode,
                        useMipMap = false,
                    };

                    CopyOverTex();
                }
            }
            InvokeTexChanged();
        }
        protected void ClearWatchedSharedTexture()
        {
            if (sharedTex == null)
                return;

            sharedTex.Dispose();
            sharedTex = null;

            MonoBehaviour.Destroy(sharedCopyTex);
            sharedCopyTex = null;
        }

        protected void InvokeTexChanged()
        {
            if (OnTextureChanged != null)
                OnTextureChanged.Invoke();
        }

        //Useful for streamlining event driven updates
        public void HandleRemoteTextureUpdate(string evTextureId, SharedTex evTextureInfo)
        {
            if (TextureId != evTextureId || localTex != null)
                return;

            textureInfo = evTextureInfo;
            RefreshWatchedSharedTexture();
            RequestSucceeded = true;
        }
    }
}
#endif
