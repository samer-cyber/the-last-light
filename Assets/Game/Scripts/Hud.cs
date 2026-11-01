using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// Everything drawn over the world: the black it opens from, short hints at the bottom, the
    /// white of the flash and the words after it. Keys in hints are written in brackets: "[Tab] take notes".
    /// </summary>
    public class Hud : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] StyleSheet style;

        VisualElement layer, hint, white, subtitle, black;
        Label title;

        public float Black
        {
            set
            {
                Build();
                black.style.opacity = value;
            }
        }

        public float White
        {
            set
            {
                Build();
                white.style.opacity = value;
            }
        }

        void Start() => Build();

        public void ShowHint(string text)
        {
            Build();
            Fill(hint, text);
            hint.AddToClassList("hud__hint--shown");
        }

        public void HideHint()
        {
            Build();
            hint.RemoveFromClassList("hud__hint--shown");
        }

        public void ShowTitle(string text)
        {
            Build();
            title.text = text;
            title.AddToClassList("hud__title--shown");
        }

        public void ShowSubtitle(string text)
        {
            Build();
            Fill(subtitle, text);
            subtitle.AddToClassList("hud__subtitle--shown");
        }

        void Build()
        {
            if (layer != null) return;
            var root = document.rootVisualElement;
            if (style) root.styleSheets.Add(style);
            layer = Element("hud");
            OsFonts.Apply(layer, OsFonts.Interface());
            hint = Element("hud__hint");
            white = Element("hud__white");
            title = new Label { pickingMode = PickingMode.Ignore };
            title.AddToClassList("hud__title");
            layer.Add(title);
            subtitle = Element("hud__subtitle");
            black = Element("hud__black");
            root.Add(layer);
        }

        VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            if (layer == null) return element;
            layer.Add(element);
            return element;
        }

        /// <summary>Lays out words and key caps; a run of three spaces separates groups.</summary>
        static void Fill(VisualElement line, string text)
        {
            line.Clear();
            var parts = Parts(text);
            for (int i = 0; i < parts.Count; i++)
            {
                var label = new Label(parts[i].text) { pickingMode = PickingMode.Ignore };
                label.AddToClassList(parts[i].isKey ? "hud__key" : "hud__words");
                if (!parts[i].isKey && i == parts.Count - 1) label.AddToClassList("hud__words--last");
                line.Add(label);
            }
        }

        static List<(string text, bool isKey)> Parts(string text)
        {
            var parts = new List<(string, bool)>();
            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '[')
                {
                    int end = text.IndexOf(']', i);
                    if (end < 0) end = text.Length;
                    parts.Add((text.Substring(i + 1, Mathf.Max(0, end - i - 1)), true));
                    i = end + 1;
                }
                else
                {
                    int next = text.IndexOf('[', i);
                    if (next < 0) next = text.Length;
                    string words = text.Substring(i, next - i).Trim();
                    if (words.Length > 0) parts.Add((words, false));
                    i = next;
                }
            }
            return parts;
        }
    }
}
