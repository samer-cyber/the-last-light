using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// A page of the player's notes. Tab brings up a sheet of ruled paper on screen (or, with a
    /// <see cref="HeldNotebook"/>, raises the notebook in the player's hands, its page drawn into a
    /// texture and the pencil following the writing). While it is up the keys write on its last line,
    /// and Enter hands the line to the page. Each scene's page decides what else is written on it.
    /// </summary>
    public abstract class PageBase : MonoBehaviour
    {
        /// <summary>Spacing of the ruled lines, in panel pixels; every row of writing is this tall.</summary>
        protected const float Rule = 36f;

        [SerializeField, Tooltip("Draws the page: the screen's panel, or one drawn into the held notebook's texture.")] protected UIDocument document;
        [SerializeField, Tooltip("Optional: a notebook in the player's hands that shows the page.")] protected HeldNotebook held;
        [SerializeField] protected StyleSheet style;
        [SerializeField] protected Texture2D paper;
        [SerializeField] protected FloatingPlayer player;
        [SerializeField] protected AudioSource sound;
        [SerializeField] protected AudioClip openClip;
        [SerializeField] protected AudioClip closeClip;
        [SerializeField] protected AudioClip writeClip;
        [SerializeField, Tooltip("Short pencil strokes, one played per letter.")] protected AudioClip[] strokeClips;

        public bool IsOpen { get; private set; }
        public bool HasBeenOpened { get; private set; }
        public float SecondsOpen { get; private set; }
        /// <summary>False while a scripted moment should keep the page away.</summary>
        public bool Available { get; set; } = true;

        protected VisualElement sheet, page;
        protected readonly System.Random hand = new System.Random(11);
        /// <summary>When a key was last typed: the hand does not wander off while someone writes.</summary>
        protected float lastKeyAt = -10f;

        VisualElement answerLine, caret;
        Label answer;
        // What is being written. Keys come straight from the keyboard (Keyboard.onTextInput), not
        // through UI Toolkit's focus, which a panel drawn into a texture does not reliably get.
        string typed = "";
        float backspaceHeld, nextRepeat;
        // Typing is taken only once the page is up and every key held when it opened is let go,
        // so a key held to drift (W, A, S, D) does not type itself onto the page.
        bool armed;
        float openedAt;
        Keyboard listening;

        /// <summary>What the empty last line says.</summary>
        protected abstract string Placeholder { get; }
        /// <summary>The small print at the foot of the page.</summary>
        protected abstract string Footer { get; }
        /// <summary>The longest line that can be written.</summary>
        protected virtual int MaxLength => 40;
        /// <summary>Writes what the page holds above its last line.</summary>
        protected abstract void Compose(VisualElement page);
        /// <summary>Adds anything that lies on top of the sheet, such as a sticky note.</summary>
        protected virtual void Decorate(VisualElement sheet) { }
        /// <summary>The player wrote a line and pressed Enter.</summary>
        protected abstract void Written(string text);

        protected virtual void Start() => Build();

        protected virtual void OnDestroy()
        {
            if (listening != null) listening.onTextInput -= OnCharacter;
        }

        protected virtual void Update()
        {
            var keys = Keyboard.current;
            if (keys != listening)
            {
                if (listening != null) listening.onTextInput -= OnCharacter;
                listening = keys;
                if (listening != null) listening.onTextInput += OnCharacter;
            }
            if (keys != null)
            {
                if (keys.tabKey.wasPressedThisFrame)
                {
                    if (IsOpen) Close();
                    else Open();
                }
                else if (IsOpen && keys.escapeKey.wasPressedThisFrame) Close();
            }
            if (!IsOpen) return;
            SecondsOpen += Time.deltaTime;
            if (!armed && Time.time - openedAt > RaiseSeconds && (keys == null || !keys.anyKey.isPressed))
            {
                armed = true;
                typed = "";
                ShowTyped();
            }
            if (!armed || keys == null) return;
            caret.style.opacity = Mathf.Repeat(Time.time - openedAt, 1f) < 0.55f ? 1f : 0f;
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) Submit();
            if (keys.backspaceKey.wasPressedThisFrame)
            {
                Erase();
                backspaceHeld = 0f;
                nextRepeat = 0.4f;
            }
            else if (keys.backspaceKey.isPressed)
            {
                backspaceHeld += Time.deltaTime;
                if (backspaceHeld >= nextRepeat)
                {
                    Erase();
                    nextRepeat += 0.05f;
                }
            }
        }

        /// <summary>Writes letters on the last line, as if typed (tests and the voice can use this).</summary>
        public void Write(string text)
        {
            foreach (char c in text) Add(c);
        }

        /// <summary>Hands the last line to the page, as Enter does.</summary>
        public void Submit()
        {
            string text = typed.Trim();
            if (text.Length == 0 || !Available) return;
            typed = "";
            ShowTyped();
            Play(writeClip);
            Written(text);
        }

        void OnCharacter(char c)
        {
            if (IsOpen && armed) Add(c);
        }

        void Add(char c)
        {
            if (char.IsControl(c) || typed.Length >= MaxLength) return;
            typed += c;
            lastKeyAt = Time.time;
            if (!char.IsWhiteSpace(c) && strokeClips != null && strokeClips.Length > 0) Play(strokeClips[hand.Next(strokeClips.Length)]);
            ShowTyped();
        }

        void Erase()
        {
            if (typed.Length == 0) return;
            typed = typed.Substring(0, typed.Length - 1);
            ShowTyped();
        }

        void ShowTyped()
        {
            bool empty = typed.Length == 0;
            // A long line keeps its end in view.
            answer.text = empty ? Placeholder : typed.Length > 38 ? "…" + typed.Substring(typed.Length - 37) : typed;
            answer.EnableInClassList("notepad__answer-text--placeholder", empty);
            if (empty) caret.SendToBack();
            else caret.BringToFront();
            // Once laid out, the pencil goes to the end of the line.
            answer.schedule.Execute(() => PencilTo(answer, empty ? 0f : 1f, !empty));
        }

        float RaiseSeconds => held ? held.RaiseSeconds : 0.45f;

        public void Open()
        {
            if (IsOpen || !Available) return;
            IsOpen = HasBeenOpened = true;
            armed = false;
            openedAt = Time.time;
            if (held) held.Raise();
            else sheet.AddToClassList("notepad--open");
            player.Writing = true;
            Play(openClip);
            PencilTo(answerLine, 0f, false);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            armed = false;
            if (held) held.Lower();
            else sheet.RemoveFromClassList("notepad--open");
            caret.style.opacity = 0f;
            player.Writing = false;
            Play(closeClip);
        }

        void Build()
        {
            var root = document.rootVisualElement;
            if (style) root.styleSheets.Add(style);

            sheet = Element("notepad");
            if (held) sheet.AddToClassList("notepad--held");
            sheet.Add(Element("notepad__shadow"));
            page = Element("notepad__page");
            if (paper) page.style.backgroundImage = paper;
            page.generateVisualContent += DrawRules;
            sheet.Add(page);
            OsFonts.Apply(sheet, OsFonts.Handwriting());

            Compose(page);
            answerLine = Element("notepad__answer");
            answer = Words("", "notepad__answer-text");
            caret = Element("notepad__caret");
            caret.style.opacity = 0f;
            answerLine.Add(answer);
            answerLine.Add(caret);
            page.Add(answerLine);
            ShowTyped();
            page.Add(Words(Footer, "notepad__footer"));

            Decorate(sheet);
            root.Add(sheet);
            sheet.SendToBack();
        }

        /// <summary>
        /// Sends the pencil of a held notebook to an element on the page: across it by "along" (0 left
        /// edge, 1 right edge) plus an offset in page pixels, at its lower edge where the writing sits.
        /// </summary>
        protected void PencilTo(VisualElement target, float along, bool writing, float offset = 0f)
        {
            if (!held || target == null) return;
            var root = document.rootVisualElement.layout;
            if (root.width <= 0f || root.height <= 0f) return;
            var bounds = target.worldBound;
            float x = Mathf.Lerp(bounds.xMin, bounds.xMax, along) + offset;
            float y = bounds.yMax - 6f;
            held.WriteAt(new Vector2(x / root.width, 1f - y / root.height), writing);
        }

        void DrawRules(MeshGenerationContext context)
        {
            float width = page.layout.width, height = page.layout.height;
            var painter = context.painter2D;
            painter.lineWidth = 1.3f;
            painter.strokeColor = new Color(0.33f, 0.52f, 0.78f, 0.35f);
            painter.BeginPath();
            for (float y = 2f * Rule; y < height - 4f; y += Rule)
            {
                painter.MoveTo(new Vector2(0f, y));
                painter.LineTo(new Vector2(width, y));
            }
            painter.Stroke();
            painter.strokeColor = new Color(0.86f, 0.32f, 0.32f, 0.45f);
            painter.BeginPath();
            painter.MoveTo(new Vector2(62f, 0f));
            painter.LineTo(new Vector2(62f, height));
            painter.Stroke();
        }

        /// <summary>
        /// Starts an element in a state class and drops it once it is laid out, so that its
        /// transition plays like a stroke of the pen; then calls back, the element now in place.
        /// </summary>
        protected static void Draw(VisualElement element, string startClass, Action placed = null)
        {
            element.AddToClassList(startClass);
            element.RegisterCallback<GeometryChangedEvent>(Drawn);

            void Drawn(GeometryChangedEvent _)
            {
                element.UnregisterCallback<GeometryChangedEvent>(Drawn);
                element.RemoveFromClassList(startClass);
                placed?.Invoke();
            }
        }

        protected static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        protected static Label Words(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        protected float Wobble(float amount) => (float)(hand.NextDouble() * 2.0 - 1.0) * amount;

        protected void Play(AudioClip clip)
        {
            if (sound && clip) sound.PlayOneShot(clip);
        }
    }
}
