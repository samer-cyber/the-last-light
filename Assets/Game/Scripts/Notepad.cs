using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// The page of the player's notebook. Tab raises the notebook (<see cref="HeldNotebook"/>); the
    /// hand notes down every dot and dash the orb flashes; the player writes what they think it says
    /// and presses Enter. The director judges it, and the page strikes it out or underlines it. The
    /// page is drawn into a texture shown on the notebook, and the pencil follows the writing.
    /// </summary>
    public class Notepad : MonoBehaviour
    {
        /// <summary>Spacing of the ruled lines, in panel pixels; every row of writing is this tall.</summary>
        const float Rule = 36f;

        [SerializeField, Tooltip("Draws into the page's render texture.")] UIDocument document;
        [SerializeField] HeldNotebook held;
        [SerializeField] StyleSheet style;
        [SerializeField] Texture2D paper;
        [SerializeField] MorseEmitter emitter;
        [SerializeField] FloatingPlayer player;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip openClip;
        [SerializeField] AudioClip closeClip;
        [SerializeField] AudioClip writeClip;
        [SerializeField, Tooltip("Short pencil strokes, one played per letter.")] AudioClip[] strokeClips;
        [SerializeField] int rowsKept = 3;
        [SerializeField] int guessesKept = 4;

        /// <summary>The player wrote something and pressed Enter.</summary>
        public event Action<string> Guessed;

        public bool IsOpen { get; private set; }
        public bool HasBeenOpened { get; private set; }
        public float SecondsOpen { get; private set; }
        /// <summary>False while a scripted moment should keep the notepad away.</summary>
        public bool Available { get; set; } = true;

        VisualElement sheet, page, log, guesses, key, row, lastGuess, answerLine, caret;
        Label answer;
        // What is being written. Keys come straight from the keyboard (Keyboard.onTextInput), not
        // through UI Toolkit's focus, because the page is a panel drawn into a texture.
        string typed = "";
        float backspaceHeld, nextRepeat, lastKeyAt = -10f;
        // Typing is taken only once the page is up and every key held when it opened is let go,
        // so a key held to drift (W, A, S, D) does not type itself onto the page.
        bool armed;
        float openedAt;
        readonly System.Random hand = new System.Random(11);

        Keyboard listening;

        void Start()
        {
            Build();
            emitter.SymbolEnded += OnSymbol;
            emitter.StepStarted += OnStep;
        }

        void OnDestroy()
        {
            if (listening != null) listening.onTextInput -= OnCharacter;
            if (!emitter) return;
            emitter.SymbolEnded -= OnSymbol;
            emitter.StepStarted -= OnStep;
        }

        void Update()
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

        /// <summary>Writes letters on the answer line, as if typed (the voice can use this too).</summary>
        public void Write(string text)
        {
            foreach (char c in text) Add(c);
        }

        /// <summary>Writes the answer line down as a guess.</summary>
        public void Submit()
        {
            string text = typed.Trim();
            if (text.Length == 0 || !Available) return;
            typed = "";
            ShowTyped();
            // The line is drawn on a holder around the words: a label measures its own text and cannot hold children.
            lastGuess = Element("notepad__guess");
            lastGuess.Add(Words(text, "notepad__guess-text"));
            guesses.Add(lastGuess);
            while (guesses.childCount > guessesKept) guesses.RemoveAt(0);
            Play(writeClip);
            Guessed?.Invoke(text);
        }

        void OnCharacter(char c)
        {
            if (IsOpen && armed) Add(c);
        }

        void Add(char c)
        {
            if (char.IsControl(c) || typed.Length >= 40) return;
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
            answer.text = empty ? "what is it saying?" : typed;
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
            caret.style.opacity = 0f;
            player.Writing = false;
            Play(closeClip);
        }

        /// <summary>Strikes out the last thing written.</summary>
        public void StrikeLast() => Stroke(lastGuess, "strike");

        /// <summary>Underlines the last thing written.</summary>
        public void UnderlineLast() => Stroke(lastGuess, "underline");

        /// <summary>Sticks the Morse alphabet beside the page.</summary>
        public void ShowKey()
        {
            key.AddToClassList("sticky--shown");
            if (IsOpen) Play(openClip);
        }

        void Build()
        {
            var root = document.rootVisualElement;
            if (style) root.styleSheets.Add(style);

            sheet = Element("notepad");
            sheet.Add(Element("notepad__shadow"));
            page = Element("notepad__page");
            if (paper) page.style.backgroundImage = paper;
            page.generateVisualContent += DrawRules;
            sheet.Add(page);
            OsFonts.Apply(sheet, OsFonts.Handwriting());

            page.Add(Words("day 0", "notepad__date"));
            page.Add(Words("it flashes:", "notepad__note"));
            log = Element("notepad__log");
            page.Add(log);
            page.Add(Words("it says:", "notepad__note"));
            guesses = Element("notepad__guesses");
            page.Add(guesses);

            answerLine = Element("notepad__answer");
            answer = Words("", "notepad__answer-text");
            caret = Element("notepad__caret");
            caret.style.opacity = 0f;
            answerLine.Add(answer);
            answerLine.Add(caret);
            page.Add(answerLine);
            ShowTyped();
            page.Add(Words("enter  write it down          tab  put it away", "notepad__footer"));

            key = BuildKey();
            sheet.Add(key);
            root.Add(sheet);
            sheet.SendToBack();
        }

        VisualElement BuildKey()
        {
            var note = Element("sticky");
            note.Add(Words("morse", "sticky__title"));
            var grid = Element("sticky__grid");
            // Two columns, A to M down the left and N to Z down the right.
            for (int i = 0; i < 13; i++)
            {
                foreach (char letter in new[] { MorseCode.Alphabet[i], MorseCode.Alphabet[i + 13] })
                {
                    var cell = Element("sticky__cell");
                    cell.Add(Words(letter.ToString(), "sticky__letter"));
                    foreach (char symbol in MorseCode.Encode(letter)) cell.Add(Element(symbol == '-' ? "key-dash" : "key-dot"));
                    grid.Add(cell);
                }
            }
            note.Add(grid);
            return note;
        }

        void OnSymbol(bool dash)
        {
            if (row == null) NewRow();
            var cell = Element("ink-cell");
            var mark = Element(dash ? "ink-dash" : "ink-dot");
            // A hand never makes two marks alike.
            mark.style.rotate = new Rotate(Angle.Degrees(Wobble(dash ? 6f : 0f)));
            mark.style.translate = new Translate(0f, Wobble(1.5f));
            if (dash) mark.style.width = 17f + Wobble(2.5f);
            cell.Add(mark);
            row.Add(cell);
            // The hand notes the flash, unless it is busy writing an answer.
            Draw(mark, "ink--fresh", () => { if (Time.time - lastKeyAt > 1.5f) PencilTo(cell, 1f, IsOpen); });
        }

        void OnStep(MorseCode.StepKind kind)
        {
            if (row == null) return;
            if (kind == MorseCode.StepKind.LetterGap) row.Add(Element("ink-gap"));
            else if (kind == MorseCode.StepKind.WordGap) row.Add(Words("/", "ink-slash"));
            else if (kind == MorseCode.StepKind.Pause) row = null;
        }

        void NewRow()
        {
            row = Element("notepad__row");
            log.Add(row);
            while (log.childCount > rowsKept) log.RemoveAt(0);
        }

        void Stroke(VisualElement target, string kind)
        {
            if (target == null) return;
            var line = Element(kind);
            line.style.rotate = new Rotate(Angle.Degrees(Wobble(2.5f)));
            target.Add(line);
            Draw(line, "line--fresh", () => PencilTo(target, 1f, true));
        }

        /// <summary>
        /// Sends the pencil to an element on the page: across it by "along" (0 left edge, 1 right edge)
        /// plus an offset in page pixels, at its lower edge where the writing sits.
        /// </summary>
        void PencilTo(VisualElement target, float along, bool writing, float offset = 0f)
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
        static void Draw(VisualElement element, string startClass, Action placed = null)
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

        static VisualElement Element(string className)
        {
            var element = new VisualElement { pickingMode = PickingMode.Ignore };
            element.AddToClassList(className);
            return element;
        }

        static Label Words(string text, string className)
        {
            var label = new Label(text) { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }

        float Wobble(float amount) => (float)(hand.NextDouble() * 2.0 - 1.0) * amount;

        void Play(AudioClip clip)
        {
            if (sound && clip) sound.PlayOneShot(clip);
        }
    }
}
