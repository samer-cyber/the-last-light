using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// The notes in the young solar system. Each thing the player asks, spoken or written, is noted in
    /// their own words with the answer the sky gave: 1, 0, or a shooting star. A line whose words
    /// changed the world is underlined, one that failed is struck through, each with a few words in the
    /// margin. Hints come as thoughts between the lines. At the end the page becomes "how it lives":
    /// what met each need, in the player's words.
    /// </summary>
    public class Journal : PageBase
    {
        [SerializeField] int linesKept = 13;

        /// <summary>The player wrote something and pressed Enter.</summary>
        public event Action<string> Asked;

        /// <summary>A line of the notes: what was asked, and its mark once the sky answers.</summary>
        public class Entry
        {
            public string Text { get; internal set; }
            internal VisualElement holder, side, mark;
        }

        VisualElement lines;

        protected override string Placeholder => "ask it something";
        protected override string Footer => "enter  ask          tab  put it away          hold q  speak";
        protected override int MaxLength => 90;

        protected override void Compose(VisualElement page)
        {
            page.Add(Words("4.5 billion years ago", "notepad__date"));
            page.Add(Words("i asked it:", "notepad__note"));
            lines = Element("journal__lines");
            page.Add(lines);
        }

        protected override void Written(string text) => Asked?.Invoke(text);

        /// <summary>Notes what the player asked, waiting for its answer.</summary>
        public Entry Note(string words)
        {
            var entry = Row(words);
            entry.mark.Add(Words("…", "journal__pending"));
            return entry;
        }

        // A line of the notes: the words (sized to them, so a stroke covers only them), the margin
        // beside them, and the mark at the end.
        Entry Row(string words)
        {
            var entry = new Entry { Text = words };
            var row = Element("journal__entry");
            entry.holder = Element("journal__holder");
            entry.holder.Add(Words(words, "journal__words"));
            entry.side = Element("journal__side");
            entry.mark = Element("journal__mark");
            row.Add(entry.holder);
            row.Add(entry.side);
            row.Add(entry.mark);
            Add(row);
            return entry;
        }

        /// <summary>Writes the sky's answer beside a line.</summary>
        public void Mark(Entry entry, Sign sign)
        {
            entry.mark.Clear();
            VisualElement shown;
            if (sign == Sign.Unknowable)
            {
                shown = Element("journal__star");
                shown.Add(Element("journal__star-tail"));
                shown.Add(Element("journal__star-head"));
            }
            else shown = Words(sign == Sign.Yes ? "1" : "0", "journal__digit");
            // Drawn on the new sign, not on its box: the box keeps its size, so it would never be
            // laid out again and the sign would stay invisible.
            shown.AddToClassList("journal__sign");
            entry.mark.Add(shown);
            Draw(shown, "journal__sign--fresh");
            Play(writeClip);
        }

        /// <summary>The sky showed something rather than answering: what it showed goes in the margin.</summary>
        public void Pointed(Entry entry, string what)
        {
            entry.mark.Clear();
            entry.side.Add(Words("it showed " + what, "journal__margin"));
            Play(writeClip);
        }

        /// <summary>A few words in the margin of a line, with no line drawn.</summary>
        public void Margin(Entry entry, string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            entry.side.Add(Words(text, "journal__margin"));
        }

        /// <summary>A line that got no answer (no connection, or it took too long): ask again.</summary>
        public void Unanswered(Entry entry)
        {
            entry.mark.Clear();
            entry.mark.Add(Words("?", "journal__pending"));
        }

        /// <summary>Underlines a line whose words changed the world, with a few words in the margin.</summary>
        public void Underline(Entry entry, string margin) => Stroke(entry, "underline", margin);

        /// <summary>Strikes through a line that did not work, with what happened in the margin.</summary>
        public void Strike(Entry entry, string margin) => Stroke(entry, "strike", margin);

        /// <summary>A thought in the player's hand, between the lines.</summary>
        public void Thought(string text)
        {
            Add(Words(text, "journal__thought"));
            Play(writeClip);
        }

        /// <summary>The last page: what met each need, in the player's own words, and what did not work.</summary>
        public void Summary(IEnumerable<(string need, string words)> met, IEnumerable<(string words, string what)> failed)
        {
            lines.Clear();
            Add(Words("how it lives:", "journal__heading"));
            foreach (var (need, words) in met)
            {
                var row = Element("journal__entry");
                row.Add(Words(need, "journal__need"));
                var holder = Element("journal__holder");
                holder.Add(Words("“" + words + "”", "journal__words"));
                row.Add(holder);
                Add(row);
            }
            bool first = true;
            foreach (var (words, what) in failed)
            {
                if (first) Add(Words("and before that:", "journal__heading"));
                first = false;
                Strike(Row(words), what);
            }
        }

        void Add(VisualElement line)
        {
            lines.Add(line);
            while (lines.childCount > linesKept) lines.RemoveAt(0);
            Draw(line, "journal__entry--fresh", () => PencilTo(line, 1f, true));
        }

        void Stroke(Entry entry, string kind, string margin)
        {
            var line = Element(kind);
            line.style.rotate = new Rotate(Angle.Degrees(Wobble(1.5f)));
            entry.holder.Add(line);
            Margin(entry, margin);
            Draw(line, "line--fresh");
            Play(writeClip);
        }
    }
}
