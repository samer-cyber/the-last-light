using System;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// The opening's notes. The hand notes down every dot and dash the orb flashes; the player writes
    /// what they think it says and presses Enter. The director judges it, and the page strikes it out
    /// or underlines it. A sticky note with the Morse alphabet turns up when the director says.
    /// </summary>
    public class Notepad : PageBase
    {
        [SerializeField] MorseEmitter emitter;
        [SerializeField] int rowsKept = 3;
        [SerializeField] int guessesKept = 4;

        /// <summary>The player wrote something and pressed Enter.</summary>
        public event Action<string> Guessed;

        VisualElement log, guesses, key, row, lastGuess;

        protected override string Placeholder => "what is it saying?";
        protected override string Footer => "enter  write it down          tab  put it away";

        protected override void Start()
        {
            base.Start();
            emitter.SymbolEnded += OnSymbol;
            emitter.StepStarted += OnStep;
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (!emitter) return;
            emitter.SymbolEnded -= OnSymbol;
            emitter.StepStarted -= OnStep;
        }

        protected override void Compose(VisualElement page)
        {
            page.Add(Words("day 0", "notepad__date"));
            page.Add(Words("it flashes:", "notepad__note"));
            log = Element("notepad__log");
            page.Add(log);
            page.Add(Words("it says:", "notepad__note"));
            guesses = Element("notepad__guesses");
            page.Add(guesses);
        }

        protected override void Decorate(VisualElement sheet)
        {
            key = BuildKey();
            sheet.Add(key);
        }

        protected override void Written(string text)
        {
            // The line is drawn on a holder around the words: a label measures its own text and cannot hold children.
            lastGuess = Element("notepad__guess");
            lastGuess.Add(Words(text, "notepad__guess-text"));
            guesses.Add(lastGuess);
            while (guesses.childCount > guessesKept) guesses.RemoveAt(0);
            Guessed?.Invoke(text);
        }

        /// <summary>Strikes out the last thing written.</summary>
        public void StrikeLast() => Stroke(lastGuess, "strike");

        /// <summary>Underlines the last thing written.</summary>
        public void UnderlineLast() => Stroke(lastGuess, "underline");

        /// <summary>Sticks the Morse alphabet to the page.</summary>
        public void ShowKey()
        {
            key.AddToClassList("sticky--shown");
            if (IsOpen) Play(openClip);
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
    }
}
