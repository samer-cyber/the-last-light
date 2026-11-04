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
        VisualElement hologram, scanLines, honour, trust, logPanel, logEntries, protocols, protocolRows;
        VisualElement[] trustPips;
        Label title, card, listenWords, said, note, aimWords, aimAction, carry, voiceName, voiceWords, photoCaption, goalTitle, goalStep;
        Label honourTitle, honourLine, trustWords, health;
        IVisualElementScheduledItem saidFade, noteFade, voiceFade, photoFade, honourFade, hurtFade;
        readonly System.Collections.Generic.Dictionary<string, (float at, System.Action act)> later = new System.Collections.Generic.Dictionary<string, (float, System.Action)>();

        /// <summary>Runs something after some game seconds; a newer one under the same key replaces it.</summary>
        void Later(string key, float seconds, System.Action act) => later[key] = (Time.time + seconds, act);

        void Update()
        {
            if (later.Count == 0) return;
            foreach (var key in new System.Collections.Generic.List<string>(later.Keys))
            {
                if (!later.TryGetValue(key, out var item) || Time.time < item.at) continue;
                later.Remove(key);
                item.act();
            }
        }
        VisualElement hurt;
        Texture2D lines;

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
            later.Remove("voice");
            voiceName.text = (name ?? "").ToUpperInvariant();
            voice.EnableInClassList("hud__voice--memory", name == "YOU, LONG AGO");
            int letters = Mathf.Clamp(Mathf.CeilToInt(line.Length * shown), 0, line.Length);
            // The rest is laid out but clear, so the line does not jump as it is typed.
            voiceWords.text = line.Substring(0, letters) + "<alpha=#00>" + line.Substring(letters);
            voice.AddToClassList("hud__voice--shown");
        }

        public void VoiceFade(float seconds)
        {
            Build();
            voiceFade?.Pause();
            Later("voice", seconds, () => voice.RemoveFromClassList("hud__voice--shown"));
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
            Later("photo", seconds, () => photo.RemoveFromClassList("hud__photo--shown"));
        }

        /// <summary>What the survivor is doing: the goal, and the step now. Empty hides it.</summary>
        public void Goal(string title, string step)
        {
            Build();
            goalTitle.text = (title ?? "").ToUpperInvariant();
            goalStep.text = step ?? "";
            goal.EnableInClassList("hud__goal--shown", !string.IsNullOrEmpty(title));
        }

        /// <summary>A picture laid over the whole view (a hologram of the past), at an opacity; null takes it away.</summary>
        public void Hologram(Texture2D picture, float opacity)
        {
            Build();
            if (picture) hologram.style.backgroundImage = new StyleBackground(picture);
            hologram.style.opacity = picture ? opacity : 0f;
            scanLines.style.opacity = picture ? opacity * 0.5f : 0f;
            hologram.style.display = picture && opacity > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
            scanLines.style.display = hologram.style.display;
        }

        /// <summary>Something earned, high in the middle for a few seconds: "SPARK TRUSTS YOU" and what it means.</summary>
        public void Honour(string heading, string line, float seconds = 6f)
        {
            Build();
            honourTitle.text = heading;
            honourLine.text = line;
            honour.AddToClassList("hud__honour--shown");
            honourFade?.Pause();
            Later("honour", seconds, () => honour.RemoveFromClassList("hud__honour--shown"));
        }

        /// <summary>Frost over the view (inside the pod), 0 to 1.</summary>
        public void Frost(float amount)
        {
            Build();
            if (!frostTexture)
            {
                // Frost: thick at the edges, thin in the middle, with a grain of crystals.
                const int size = 256;
                frostTexture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "Frost" };
                var pixels = new Color[size * size];
                for (int y = 0; y < size; y++)
                    for (int x = 0; x < size; x++)
                    {
                        float u = x / (float)size - 0.5f, v = y / (float)size - 0.5f;
                        float edge = Mathf.Clamp01((new Vector2(u * 1.2f, v).magnitude - 0.12f) / 0.4f);
                        float grain = Mathf.PerlinNoise(x * 0.09f, y * 0.09f) * 0.6f + Mathf.PerlinNoise(x * 0.35f, y * 0.35f) * 0.4f;
                        float a = Mathf.Clamp01(edge * 1.1f + grain * 0.35f - 0.1f);
                        pixels[y * size + x] = new Color(0.82f, 0.9f, 1f, a);
                    }
                frostTexture.SetPixels(pixels);
                frostTexture.Apply();
                frost.style.backgroundImage = new StyleBackground(frostTexture);
            }
            frost.style.opacity = amount;
            frost.style.display = amount > 0.001f ? DisplayStyle.Flex : DisplayStyle.None;
        }

        Texture2D frostTexture;
        VisualElement frost;

        /// <summary>A red flash at the edges: the survivor was hit.</summary>
        public void Flash(float strength)
        {
            Build();
            hurt.style.opacity = strength;
            hurtFade?.Pause();
            hurtFade = hurt.schedule.Execute(() => hurt.style.opacity = 0f).StartingIn(120);
        }

        /// <summary>Spark's trust, small under the goal: four marks, as many lit as the tier, and its name.</summary>
        public void Trust(int tier, string words)
        {
            Build();
            trust.AddToClassList("hud__trust--shown");
            for (int i = 0; i < trustPips.Length; i++) trustPips[i].EnableInClassList("hud__trust-pip--on", i <= tier);
            trustWords.text = words;
        }

        /// <summary>How warm the survivor still is (1 full); hidden when full.</summary>
        public void Warmth(float value)
        {
            Build();
            health.style.opacity = value > 0.995f ? 0f : 1f;
            health.style.width = Length.Percent(24f * Mathf.Clamp01(value));
            health.EnableInClassList("hud__warmth--low", value < 0.35f);
        }

        /// <summary>Spark's protocols, top left under its trust: those it broke struck through and lit.</summary>
        public void Protocols(System.Collections.Generic.IEnumerable<(string text, bool broken)> rules, bool open)
        {
            Build();
            protocols.EnableInClassList("hud__protocols--shown", open);
            protocolRows.Clear();
            foreach (var (text, broken) in rules)
            {
                var row = new Label(broken ? "<s>" + text + "</s>   BROKEN" : text) { pickingMode = PickingMode.Ignore, enableRichText = true };
                row.AddToClassList("hud__protocol");
                if (broken) row.AddToClassList("hud__protocol--broken");
                protocolRows.Add(row);
            }
        }

        /// <summary>Spark's log, open on the right, or closed.</summary>
        public void Log(System.Collections.Generic.IEnumerable<(string when, string entry)> entries, bool open)
        {
            Build();
            logPanel.EnableInClassList("hud__log--shown", open);
            if (!open) return;
            logEntries.Clear();
            int count = 0;
            foreach (var (when, entry) in entries)
            {
                var row = Part("hud__log-row");
                var at = new Label(when) { pickingMode = PickingMode.Ignore };
                at.AddToClassList("hud__log-when");
                var words = new Label(entry) { pickingMode = PickingMode.Ignore };
                words.AddToClassList("hud__log-entry");
                row.Add(at);
                row.Add(words);
                logEntries.Add(row);
                count++;
            }
            if (count == 0)
            {
                var empty = new Label("Nothing yet.") { pickingMode = PickingMode.Ignore };
                empty.AddToClassList("hud__log-entry");
                logEntries.Add(empty);
            }
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
            Later("flash " + shown, seconds, () => label.RemoveFromClassList(shown));
        }

        void Build()
        {
            if (layer != null) return;
            var root = document.rootVisualElement;
            if (style) root.styleSheets.Add(style);
            layer = Element("hud");
            OsFonts.Apply(layer, OsFonts.Interface());
            // The hologram of the past lies under everything else.
            hologram = Element("hud__hologram");
            hologram.style.display = DisplayStyle.None;
            lines = new Texture2D(1, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Repeat, name = "Scan Lines" };
            lines.SetPixels(new[] { new Color(0f, 0f, 0f, 0.55f), new Color(0f, 0f, 0f, 0f), new Color(0.6f, 0.95f, 1f, 0.08f), new Color(0f, 0f, 0f, 0f) });
            lines.Apply();
            hurt = Element("hud__hurt");
            frost = Element("hud__frost");
            frost.style.display = DisplayStyle.None;
            scanLines = Element("hud__scanlines");
            scanLines.style.backgroundImage = new StyleBackground(lines);
            scanLines.style.display = DisplayStyle.None;
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
            trust = Element("hud__trust");
            var pips = Part("hud__trust-pips");
            trustPips = new VisualElement[4];
            for (int i = 0; i < 4; i++)
            {
                trustPips[i] = Part("hud__trust-pip");
                pips.Add(trustPips[i]);
            }
            trust.Add(pips);
            trustWords = new Label { pickingMode = PickingMode.Ignore };
            trustWords.AddToClassList("hud__trust-words");
            trust.Add(trustWords);
            health = new Label { pickingMode = PickingMode.Ignore };
            health.AddToClassList("hud__warmth");
            health.style.opacity = 0f;
            layer.Add(health);
            honour = Element("hud__honour");
            honourTitle = new Label { pickingMode = PickingMode.Ignore };
            honourTitle.AddToClassList("hud__honour-title");
            honour.Add(honourTitle);
            honourLine = new Label { pickingMode = PickingMode.Ignore };
            honourLine.AddToClassList("hud__honour-line");
            honour.Add(honourLine);
            protocols = Element("hud__protocols");
            var protocolHead = new Label("SPARK'S PROTOCOLS   [P]") { pickingMode = PickingMode.Ignore };
            protocolHead.AddToClassList("hud__protocols-head");
            protocols.Add(protocolHead);
            protocolRows = Part("hud__protocols-rows");
            protocols.Add(protocolRows);
            logPanel = Element("hud__log");
            var logHead = new Label("SPARK'S LOG") { pickingMode = PickingMode.Ignore };
            logHead.AddToClassList("hud__log-head");
            logPanel.Add(logHead);
            logEntries = Part("hud__log-entries");
            logPanel.Add(logEntries);
            photo = Element("hud__photo");
            photoImage = Part("hud__photo-image");
            photo.Add(photoImage);
            // A viewfinder over the picture: corner marks and a red dot.
            foreach (string corner in new[] { "tl", "tr", "bl", "br" }) photoImage.Add(Part("hud__photo-corner hud__photo-corner--" + corner));
            photoImage.Add(Part("hud__photo-rec"));
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
            // The words stay readable over the white of a memory.
            voice.PlaceInFront(white);
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
