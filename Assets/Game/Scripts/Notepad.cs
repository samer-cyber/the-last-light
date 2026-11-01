using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// The player's notepad, a sheet of ruled paper that Tab brings up. The hand notes down every
    /// dot and dash the orb flashes; the player writes what they think it says and presses Enter.
    /// The director judges it and the notepad strikes it out or underlines it.
    /// </summary>
    public class Notepad : MonoBehaviour
    {
        /// <summary>Spacing of the ruled lines, in panel pixels; every row of writing is this tall.</summary>
        const float Rule = 36f;

        [SerializeField] UIDocument document;
        [SerializeField] StyleSheet style;
        [SerializeField] Texture2D paper;
        [SerializeField] MorseEmitter emitter;
        [SerializeField] FloatingPlayer player;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip openClip;
        [SerializeField] AudioClip closeClip;
        [SerializeField] AudioClip writeClip;
        [SerializeField] int rowsKept = 3;
        [SerializeField] int guessesKept = 4;

        /// <summary>The player wrote something and pressed Enter.</summary>
        public event Action<string> Guessed;

        public bool IsOpen { get; private set; }
        public bool HasBeenOpened { get; private set; }
        public float SecondsOpen { get; private set; }
        /// <summary>False while a scripted moment should keep the notepad away.</summary>
        public bool Available { get; set; } = true;

        VisualElement sheet, page, log, guesses, key, row, lastGuess;
        TextField answer;
        // Typing is taken only once the page is up and every key held when it opened is let go,
        // so a key held to drift (W, A, S, D) does not type itself onto the page.
        bool armed;
        float openedAt;
        readonly System.Random hand = new System.Random(11);

        void Start()
        {
            Build();
            emitter.SymbolEnded += OnSymbol;
            emitter.StepStarted += OnStep;
        }

        void OnDestroy()
        {
            if (!emitter) return;
            emitter.SymbolEnded -= OnSymbol;
            emitter.StepStarted -= OnStep;
        }

        void Update()
        {
            var keys = Keyboard.current;
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
                answer.SetValueWithoutNotify("");
                answer.Focus();
            }
        }

        /// <summary>How long the page takes to come up (Notepad.uss, .notepad translate).</summary>
        const float RaiseSeconds = 0.45f;

        public void Open()
        {
            if (IsOpen || !Available) return;
            IsOpen = HasBeenOpened = true;
            armed = false;
            openedAt = Time.time;
            sheet.AddToClassList("notepad--open");
            player.Writing = true;
            Play(openClip);
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            armed = false;
            sheet.RemoveFromClassList("notepad--open");
            answer.Blur();
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

            answer = new TextField { maxLength = 48 };
            answer.AddToClassList("notepad__answer");
            answer.textEdition.placeholder = "what is it saying?";
            answer.RegisterCallback<KeyDownEvent>(OnAnswerKey, TrickleDown.TrickleDown);
            page.Add(answer);
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
            Draw(mark, "ink--fresh");
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

        void OnAnswerKey(KeyDownEvent evt)
        {
            if (!armed)
            {
                evt.StopImmediatePropagation();
                return;
            }
            if (evt.keyCode != KeyCode.Return && evt.keyCode != KeyCode.KeypadEnter) return;
            evt.StopPropagation();
            string text = answer.value.Trim();
            if (text.Length == 0 || !Available) return;
            answer.SetValueWithoutNotify("");
            // The line is drawn on a holder around the words: a label measures its own text and cannot hold children.
            lastGuess = Element("notepad__guess");
            lastGuess.Add(Words(text, "notepad__guess-text"));
            guesses.Add(lastGuess);
            while (guesses.childCount > guessesKept) guesses.RemoveAt(0);
            Play(writeClip);
            Guessed?.Invoke(text);
        }

        void Stroke(VisualElement target, string kind)
        {
            if (target == null) return;
            var line = Element(kind);
            line.style.rotate = new Rotate(Angle.Degrees(Wobble(2.5f)));
            target.Add(line);
            Draw(line, "line--fresh");
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
        /// transition plays like a stroke of the pen.
        /// </summary>
        static void Draw(VisualElement element, string startClass)
        {
            element.AddToClassList(startClass);
            element.RegisterCallback<GeometryChangedEvent>(Drawn);

            void Drawn(GeometryChangedEvent _)
            {
                element.UnregisterCallback<GeometryChangedEvent>(Drawn);
                element.RemoveFromClassList(startClass);
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
