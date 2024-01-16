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
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Assertions;

namespace BlueprintReality.Unity
{
    /// <summary>
    /// A set of utilities to deal with texture formats.
    /// </summary>
    public static class TextureFormatUtilities
    {
        static Dictionary<int, RenderTextureFormat> s_FormatAliasMap;
        static Dictionary<int, bool> s_SupportedRenderTextureFormats;
        static Dictionary<int, bool> s_SupportedTextureFormats;

        static TextureFormatUtilities()
        {
            s_FormatAliasMap = new Dictionary<int, RenderTextureFormat>
            {
                { (int)TextureFormat.Alpha8, RenderTextureFormat.ARGB32 },
                { (int)TextureFormat.ARGB4444, RenderTextureFormat.ARGB4444 },
                { (int)TextureFormat.RGB24, RenderTextureFormat.ARGB32 },
                { (int)TextureFormat.RGBA32, RenderTextureFormat.ARGB32 },
                { (int)TextureFormat.ARGB32, RenderTextureFormat.ARGB32 },
                { (int)TextureFormat.RGB565, RenderTextureFormat.RGB565 },
                { (int)TextureFormat.R16, RenderTextureFormat.RHalf },
                { (int)TextureFormat.DXT1, RenderTextureFormat.ARGB32 },
                { (int)TextureFormat.DXT5, RenderTextureFormat.ARGB32 },
                { (int)TextureFormat.RGBA4444, RenderTextureFormat.ARGB4444 },
                { (int)TextureFormat.BGRA32, RenderTextureFormat.ARGB32 },
                { (int)TextureFormat.RHalf, RenderTextureFormat.RHalf },
                { (int)TextureFormat.RGHalf, RenderTextureFormat.RGHalf },
                { (int)TextureFormat.RGBAHalf, RenderTextureFormat.ARGBHalf },
                { (int)TextureFormat.RFloat, RenderTextureFormat.RFloat },
                { (int)TextureFormat.RGFloat, RenderTextureFormat.RGFloat },
                { (int)TextureFormat.RGBAFloat, RenderTextureFormat.ARGBFloat },
            };

            // TODO: refactor the next two scopes in a generic function once we have support for enum constraints on generics
            // In 2018.1 SystemInfo.SupportsRenderTextureFormat() generates garbage so we need to
            // cache its calls to avoid that...
            {
                s_SupportedRenderTextureFormats = new Dictionary<int, bool>();
                var values = Enum.GetValues(typeof(RenderTextureFormat));

                foreach (var format in values)
                {
                    if ((int)format < 0) // Safe guard, negative values are deprecated stuff
                        continue;

                    if (IsObsolete(format))
                        continue;

                    bool supported = SystemInfo.SupportsRenderTextureFormat((RenderTextureFormat)format);
                    s_SupportedRenderTextureFormats[(int)format] = supported;
                }
            }

            // Same for TextureFormat
            {
                s_SupportedTextureFormats = new Dictionary<int, bool>();
                var values = Enum.GetValues(typeof(TextureFormat));

                foreach (var format in values)
                {
                    if ((int)format < 0) // Crashes the runtime otherwise (!)
                        continue;

                    if (IsObsolete(format))
                        continue;

                    bool supported = SystemInfo.SupportsTextureFormat((TextureFormat)format);
                    s_SupportedTextureFormats[(int)format] = supported;
                }
            }
        }

        static bool IsObsolete(object value)
        {
            var fieldInfo = value.GetType().GetField(value.ToString());
            var attributes = (ObsoleteAttribute[])fieldInfo.GetCustomAttributes(typeof(ObsoleteAttribute), false);
            return attributes != null && attributes.Length > 0;
        }

        /// <summary>
        /// Returns a <see cref="RenderTextureFormat"/> compatible with the given texture's format.
        /// </summary>
        /// <param name="texture">A texture to get a compatible format from</param>
        /// <returns>A compatible render texture format</returns>
        public static RenderTextureFormat GetUncompressedRenderTextureFormat(this Texture texture)
        {
            Assert.IsNotNull(texture);

            if (texture is RenderTexture)
                return (texture as RenderTexture).format;

            if (texture is Texture2D)
            {
                var inFormat = ((Texture2D)texture).format;
                RenderTextureFormat outFormat;

                if (!s_FormatAliasMap.TryGetValue((int)inFormat, out outFormat))
                    throw new NotSupportedException("Texture format not supported");

                return outFormat;
            }

            return RenderTextureFormat.Default;
        }

        internal static bool IsSupported(this RenderTextureFormat format)
        {
            bool supported;
            s_SupportedRenderTextureFormats.TryGetValue((int)format, out supported);
            return supported;
        }

        internal static bool IsSupported(this TextureFormat format)
        {
            bool supported;
            s_SupportedTextureFormats.TryGetValue((int)format, out supported);
            return supported;
        }
    }
}
