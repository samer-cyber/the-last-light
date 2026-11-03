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

        VisualElement layer, hint, white, subtitle, black, listen, listenLevel, aim, aimDot, voice, photo, photoImage, goal;
        Label title, card, listenWords, said, note, aimWords, aimAction, carry, voiceName, voiceWords, photoCaption, goalTitle, goalStep;
        IVisualElementScheduledItem saidFade, noteFade, voiceFade, photoFade;

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

        /// <summary>White words on the black, before the world fades in.</summary>
        public void ShowCard(string text)
        {
            Build();
            card.text = text;
            card.AddToClassList("hud__card--shown");
        }

        public void HideCard()
        {
            Build();
            card.RemoveFromClassList("hud__card--shown");
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

        /// <summary>
        /// What the centre of the view rests on: a small dot, its name under it, and what a key does
        /// with it ("[E] gather"). Null hides the words and leaves the dot.
        /// </summary>
        public void Aim(string name, string action)
        {
            Build();
            aim.style.display = DisplayStyle.Flex;
            aimWords.text = name ?? "";
            aimAction.text = action ?? "";
            aimDot.EnableInClassList("hud__aim-dot--on", name != null);
        }

        public void HideAim()
        {
            Build();
            aim.style.display = DisplayStyle.None;
        }

        /// <summary>What the mind holds, small, low on the left ("stone 4   light 3").</summary>
        public void Carry(string text)
        {
            Build();
            carry.text = text;
        }

        /// <summary>
        /// The drone's words under the view, its name before them, typed out as far as
        /// <paramref name="shown"/> (0 to 1) while it chirps them.
        /// </summary>
        public void Voice(string name, string line, float shown)
        {
            Build();
            voiceFade?.Pause();
            voiceName.text = (name ?? "").ToUpperInvariant();
            int letters = Mathf.Clamp(Mathf.CeilToInt(line.Length * shown), 0, line.Length);
            // The rest is laid out but clear, so the line does not jump as it is typed.
            voiceWords.text = line.Substring(0, letters) + "<alpha=#00>" + line.Substring(letters);
            voice.AddToClassList("hud__voice--shown");
        }

        public void VoiceFade(float seconds)
        {
            Build();
            voiceFade?.Pause();
            voiceFade = voice.schedule.Execute(() => voice.RemoveFromClassList("hud__voice--shown")).StartingIn((long)(seconds * 1000f));
        }

        /// <summary>A picture the drone's eye took, framed in the corner, with a few words under it.</summary>
        public void Photo(Texture2D picture, string caption, float seconds = 9f)
        {
            Build();
            photoImage.style.backgroundImage = new StyleBackground(picture);
            photoImage.MarkDirtyRepaint();
            photoCaption.text = caption ?? "";
            photo.RemoveFromClassList("hud__photo--shown");
            photo.AddToClassList("hud__photo--flash");
            photo.schedule.Execute(() =>
            {
                photo.RemoveFromClassList("hud__photo--flash");
                photo.AddToClassList("hud__photo--shown");
            }).StartingIn(120);
            photoFade?.Pause();
            photoFade = photo.schedule.Execute(() => photo.RemoveFromClassList("hud__photo--shown")).StartingIn((long)(seconds * 1000f));
        }

        /// <summary>What the survivor is doing: the goal, and the step now. Empty hides it.</summary>
        public void Goal(string title, string step)
        {
            Build();
            goalTitle.text = (title ?? "").ToUpperInvariant();
            goalStep.text = step ?? "";
            goal.EnableInClassList("hud__goal--shown", !string.IsNullOrEmpty(title));
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
            aim = Element("hud__aim");
            aim.style.display = DisplayStyle.None;
            aimDot = Part("hud__aim-dot");
            aim.Add(aimDot);
            aimWords = new Label { pickingMode = PickingMode.Ignore };
            aimWords.AddToClassList("hud__aim-words");
            aim.Add(aimWords);
            aimAction = new Label { pickingMode = PickingMode.Ignore };
            aimAction.AddToClassList("hud__aim-action");
            aim.Add(aimAction);
            carry = new Label { pickingMode = PickingMode.Ignore };
            carry.AddToClassList("hud__carry");
            layer.Add(carry);
            goal = Element("hud__goal");
            goalTitle = new Label { pickingMode = PickingMode.Ignore };
            goalTitle.AddToClassList("hud__goal-title");
            goal.Add(goalTitle);
            goalStep = new Label { pickingMode = PickingMode.Ignore };
            goalStep.AddToClassList("hud__goal-step");
            goal.Add(goalStep);
            photo = Element("hud__photo");
            photoImage = Part("hud__photo-image");
            photo.Add(photoImage);
            photoCaption = new Label { pickingMode = PickingMode.Ignore };
            photoCaption.AddToClassList("hud__photo-caption");
            photo.Add(photoCaption);
            voice = Element("hud__voice");
            voiceName = new Label { pickingMode = PickingMode.Ignore };
            voiceName.AddToClassList("hud__voice-name");
            voice.Add(voiceName);
            voiceWords = new Label { pickingMode = PickingMode.Ignore, enableRichText = true };
            voiceWords.AddToClassList("hud__voice-words");
            voice.Add(voiceWords);
            white = Element("hud__white");
            title = new Label { pickingMode = PickingMode.Ignore };
            title.AddToClassList("hud__title");
            layer.Add(title);
            subtitle = Element("hud__subtitle");
            black = Element("hud__black");
            card = new Label { pickingMode = PickingMode.Ignore };
            card.AddToClassList("hud__card");
            layer.Add(card);
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
