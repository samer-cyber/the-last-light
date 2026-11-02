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

        VisualElement layer, hint, white, subtitle, black, listen, listenLevel;
        Label title, listenWords, said, note;
        IVisualElementScheduledItem saidFade, noteFade;

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

        public void HideTitle()
        {
            Build();
            title.RemoveFromClassList("hud__title--shown");
        }

        public void ShowSubtitle(string text)
        {
            Build();
            Fill(subtitle, text);
            subtitle.AddToClassList("hud__subtitle--shown");
        }

        /// <summary>While the player holds the key to speak: a light and how loud their voice is (0 to 1).</summary>
        public void Listening(float level)
        {
            Build();
            listenWords.text = "listening";
            listenLevel.style.width = Length.Percent(Mathf.Clamp01(level) * 100f);
            listen.AddToClassList("hud__listen--shown");
        }

        /// <summary>Between letting go of the key and the answer: a few words where the level was.</summary>
        public void Waiting(string words)
        {
            Build();
            listenWords.text = words;
            listenLevel.style.width = Length.Percent(0f);
            listen.AddToClassList("hud__listen--shown");
        }

        public void StopListening()
        {
            Build();
            listen.RemoveFromClassList("hud__listen--shown");
        }

        /// <summary>The player's own words, as they were heard, for a few seconds.</summary>
        public void Said(string text, float seconds = 5f) => Flash(said, "hud__said--shown", "“" + text + "”", seconds, ref saidFade);

        /// <summary>A short line in the middle of the view, for a few seconds ("millions of years pass").</summary>
        public void Note(string text, float seconds = 4f) => Flash(note, "hud__note--shown", text, seconds, ref noteFade);

        void Flash(Label label, string shown, string text, float seconds, ref IVisualElementScheduledItem fade)
        {
            Build();
            label.text = text;
            label.AddToClassList(shown);
            fade?.Pause();
            fade = label.schedule.Execute(() => label.RemoveFromClassList(shown)).StartingIn((long)(seconds * 1000f));
        }

        void Build()
        {
            if (layer != null) return;
            var root = document.rootVisualElement;
            if (style) root.styleSheets.Add(style);
            layer = Element("hud");
            OsFonts.Apply(layer, OsFonts.Interface());
            hint = Element("hud__hint");
            listen = Element("hud__listen");
            listen.Add(Part("hud__listen-dot"));
            var bar = Part("hud__listen-bar");
            listenLevel = Part("hud__listen-level");
            bar.Add(listenLevel);
            listen.Add(bar);
            listenWords = new Label { pickingMode = PickingMode.Ignore };
            listenWords.AddToClassList("hud__listen-words");
            listen.Add(listenWords);
            said = new Label { pickingMode = PickingMode.Ignore };
            said.AddToClassList("hud__said");
            layer.Add(said);
            note = new Label { pickingMode = PickingMode.Ignore };
            note.AddToClassList("hud__note");
            layer.Add(note);
            white = Element("hud__white");
            title = new Label { pickingMode = PickingMode.Ignore };
            title.AddToClassList("hud__title");
            layer.Add(title);
            subtitle = Element("hud__subtitle");
            black = Element("hud__black");
            root.Add(layer);
        }

        static VisualElement Part(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
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
