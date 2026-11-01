using System.Collections.Generic;
using UnityEngine.UIElements;
using FontAsset = UnityEngine.TextCore.Text.FontAsset;

namespace Game
{
    /// <summary>
    /// Fonts taken from the operating system while the game runs, so that none has to be shipped
    /// with it. Each list is tried in order; null means the theme's own font is used.
    /// </summary>
    public static class OsFonts
    {
        static readonly Dictionary<string, FontAsset> Found = new Dictionary<string, FontAsset>();

        public static FontAsset Handwriting() => First("Ink Free", "Segoe Print", "Bradley Hand", "Noteworthy", "Chalkboard SE");

        public static FontAsset Interface() => First("Segoe UI|Light", "Segoe UI", "Helvetica Neue|Light", "Helvetica Neue", "Arial");

        public static void Apply(VisualElement element, FontAsset font)
        {
            if (font != null) element.style.unityFontDefinition = new StyleFontDefinition(FontDefinition.FromSDFFont(font));
        }

        /// <summary>The first font that exists, each written "Family" or "Family|Style".</summary>
        static FontAsset First(params string[] candidates)
        {
            foreach (string candidate in candidates)
            {
                if (!Found.TryGetValue(candidate, out var font))
                {
                    string[] parts = candidate.Split('|');
                    try
                    {
                        font = FontAsset.CreateFontAsset(parts[0], parts.Length > 1 ? parts[1] : "Regular");
                    }
                    catch (System.Exception)
                    {
                        font = null;
                    }
                    Found[candidate] = font;
                }
                if (font != null) return font;
            }
            return null;
        }
    }
}
