using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// Writing to the mind: Tab opens one line low on the screen, the keys write on it, Enter sends it,
    /// Tab or Escape puts it away. Keys come straight from the keyboard (Keyboard.onTextInput), as on
    /// the opening's notes, and only once every key held when it opened is let go, so a key held to
    /// walk does not type itself.
    /// </summary>
    public class SayLine : MonoBehaviour
    {
        [SerializeField] UIDocument document;
        [SerializeField] StyleSheet style;
        [SerializeField] Traveller traveller;
        [SerializeField] FollowCamera follow;
        [SerializeField] AudioSource sound;
        [SerializeField] AudioClip[] keyClips;
        [SerializeField] int longest = 110;

        public event Action<string> Said;
        /// <summary>What the empty line says.</summary>
        public string Placeholder { get; set; } = "tell it what to make";
        public bool IsOpen { get; private set; }
        public bool Available { get; set; } = true;

        VisualElement box, caret;
        Label text;
        string typed = "";
        bool armed;
        float openedAt, backspaceHeld, nextRepeat;
        Keyboard listening;
        readonly System.Random random = new System.Random(9);

        void Start() => Build();

        void OnDestroy()
        {
            if (listening != null) listening.onTextInput -= OnCharacter;
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
            if (keys == null) return;
            if (keys.tabKey.wasPressedThisFrame)
            {
                if (IsOpen) Close();
                else Open();
            }
            else if (IsOpen && keys.escapeKey.wasPressedThisFrame) Close();
            if (!IsOpen) return;
            if (!armed && Time.unscaledTime - openedAt > 0.15f && !keys.anyKey.isPressed) armed = true;
            if (!armed) return;
            caret.style.opacity = Mathf.Repeat(Time.unscaledTime - openedAt, 1f) < 0.55f ? 1f : 0f;
            if (keys.enterKey.wasPressedThisFrame || keys.numpadEnterKey.wasPressedThisFrame) Submit();
            if (keys.backspaceKey.wasPressedThisFrame)
            {
                Erase();
                backspaceHeld = 0f;
                nextRepeat = 0.4f;
            }
            else if (keys.backspaceKey.isPressed)
            {
                backspaceHeld += Time.unscaledDeltaTime;
                if (backspaceHeld >= nextRepeat)
                {
                    Erase();
                    nextRepeat += 0.05f;
                }
            }
        }

        public void Open()
        {
            if (IsOpen || !Available) return;
            IsOpen = true;
            armed = false;
            openedAt = Time.unscaledTime;
            typed = "";
            Show();
            box.AddToClassList("say--open");
            traveller.Typing = true;
            follow.Typing = true;
        }

        public void Close()
        {
            if (!IsOpen) return;
            IsOpen = false;
            box.RemoveFromClassList("say--open");
            traveller.Typing = false;
            follow.Typing = false;
        }

        /// <summary>Sends what is written, as Enter does.</summary>
        public void Submit()
        {
            string line = typed.Trim();
            if (line.Length == 0) return;
            typed = "";
            Show();
            Close();
            Said?.Invoke(line);
        }

        /// <summary>Writes letters as if typed (tests use this).</summary>
        public void Write(string letters)
        {
            foreach (char c in letters) Add(c);
        }

        void OnCharacter(char c)
        {
            if (IsOpen && armed) Add(c);
        }

        void Add(char c)
        {
            if (char.IsControl(c) || typed.Length >= longest) return;
            typed += c;
            if (!char.IsWhiteSpace(c) && sound && keyClips != null && keyClips.Length > 0) sound.PlayOneShot(keyClips[random.Next(keyClips.Length)], 0.35f);
            Show();
        }

        void Erase()
        {
            if (typed.Length == 0) return;
            typed = typed.Substring(0, typed.Length - 1);
            Show();
        }

        void Show()
        {
            bool empty = typed.Length == 0;
            text.text = empty ? Placeholder : typed;
            text.EnableInClassList("say__text--empty", empty);
            if (empty) caret.SendToBack();
            else caret.BringToFront();
        }

        void Build()
        {
            var root = document.rootVisualElement;
            if (style) root.styleSheets.Add(style);
            box = new VisualElement { pickingMode = PickingMode.Ignore };
            box.AddToClassList("say");
            var line = new VisualElement { pickingMode = PickingMode.Ignore };
            line.AddToClassList("say__line");
            text = new Label { pickingMode = PickingMode.Ignore };
            text.AddToClassList("say__text");
            caret = new VisualElement { pickingMode = PickingMode.Ignore };
            caret.AddToClassList("say__caret");
            line.Add(text);
            line.Add(caret);
            box.Add(line);
            var keys = new Label("enter  send          tab  put away          hold q  speak") { pickingMode = PickingMode.Ignore };
            keys.AddToClassList("say__keys");
            box.Add(keys);
            OsFonts.Apply(box, OsFonts.Interface());
            root.Add(box);
            Show();
        }
    }
}
