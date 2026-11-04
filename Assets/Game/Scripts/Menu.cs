using System;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

namespace Game
{
    /// <summary>
    /// The game's menu, drawn over the HUD: at the start (in a build) the title with Continue and New game; in play,
    /// Esc pauses into Resume, Settings, Main menu and Quit. Settings: picture quality, full screen, the four sound
    /// levels, the mouse, and how hard the cold and the machines are. It makes itself in any scene with the director.
    /// </summary>
    public class Menu : MonoBehaviour
    {
        static bool booted;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (!booted)
            {
                booted = true;
                SceneManager.sceneLoaded += (_, __) => Make();
            }
            Make();
        }

        static void Make()
        {
            if (!FindFirstObjectByType<LastLightDirector>() || FindFirstObjectByType<Menu>()) return;
            new GameObject("Menu").AddComponent<Menu>();
        }

        /// <summary>Whether the menu is up (the game is paused under it).</summary>
        public static bool IsOpen { get; private set; }

        static readonly Color Ink = new Color(0.93f, 0.91f, 0.87f);
        static readonly Color Dim = new Color(0.62f, 0.64f, 0.68f);
        static readonly Color Amber = new Color(1f, 0.72f, 0.38f);

        LastLightDirector director;
        FollowCamera follow;
        SayLine say;
        Hud hud;
        VisualElement overlay;
        bool title, sayWasOpen, readingWas;
        float keptScale = 1f;

        void Start()
        {
            director = FindFirstObjectByType<LastLightDirector>();
            follow = FindFirstObjectByType<FollowCamera>();
            say = FindFirstObjectByType<SayLine>();
            hud = FindFirstObjectByType<Hud>();
            Settings.Apply();
            AudioChannel.TagAll();
            InvokeRepeating(nameof(Retag), 3f, 3f);
            UIDocument document = null;
            foreach (var d in FindObjectsByType<UIDocument>(FindObjectsSortMode.None))
                if (d.panelSettings && !d.panelSettings.targetTexture)
                {
                    document = d;
                    break;
                }
            if (!document) return;
            overlay = new VisualElement { name = "menu", pickingMode = PickingMode.Position };
            overlay.style.position = Position.Absolute;
            overlay.style.left = overlay.style.top = overlay.style.right = overlay.style.bottom = 0;
            overlay.style.alignItems = Align.Center;
            overlay.style.justifyContent = Justify.Center;
            overlay.style.display = DisplayStyle.None;
            document.rootVisualElement.Add(overlay);
            // In a build the game opens on its title; in the editor it goes straight in (tests and filming drive it).
            if (!Application.isEditor || PlayerPrefs.GetInt("menu in editor", 0) == 1) Show(true);
        }

        void Retag() => AudioChannel.TagAll();

        void OnDestroy()
        {
            if (IsOpen)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
            }
            IsOpen = false;
#if UNITY_EDITOR
            Settings.Restore();
#endif
        }

        void Update()
        {
            if (overlay == null) return;
            var keys = Keyboard.current;
            bool sayOpen = say && say.IsOpen;
            bool reading = hud && hud.Inspecting;
            // Esc closes the line being written or the page being read first; only then does it pause.
            if (keys != null && keys.escapeKey.wasPressedThisFrame && !sayWasOpen && !readingWas)
            {
                if (IsOpen)
                {
                    if (!title) Hide();
                }
                else if (director && !director.Ended) Show(false);
            }
            sayWasOpen = sayOpen;
            readingWas = reading;
        }

        void Show(bool asTitle)
        {
            title = asTitle;
            IsOpen = true;
            keptScale = Time.timeScale > 0f ? Time.timeScale : 1f;
            Time.timeScale = 0f;
            AudioListener.pause = true;
            if (follow)
            {
                follow.Lock(false);
                follow.Typing = true;
            }
            UnityEngine.Cursor.visible = true;
            overlay.style.display = DisplayStyle.Flex;
            overlay.style.backgroundColor = asTitle ? new Color(0.015f, 0.02f, 0.035f, 1f) : new Color(0.015f, 0.02f, 0.035f, 0.82f);
            overlay.BringToFront();
            Main();
        }

        void Hide()
        {
            IsOpen = false;
            overlay.style.display = DisplayStyle.None;
            Time.timeScale = keptScale;
            AudioListener.pause = false;
            if (follow)
            {
                follow.Typing = false;
                follow.Lock(true);
            }
        }

        // ---- Pages ----

        void Main()
        {
            overlay.Clear();
            var box = Column();
            box.Add(Text("THE LAST LIGHT", 70, Ink, 12f));
            box.Add(Text(title ? "A GAME WHERE THE AI IS SOMEONE" : "PAUSED", 18, Dim, 6f));
            Space(box, 40);
            if (title)
            {
                int checkpoint = Settings.Checkpoint;
                if (checkpoint > 0)
                    box.Add(Choice(checkpoint >= 2 ? "Continue   outside the vault" : "Continue   in the vault", Continue));
                box.Add(Choice("New game", NewGame));
            }
            else box.Add(Choice("Resume", Hide));
            box.Add(Choice("Settings", SettingsPage));
            if (!title) box.Add(Choice("Main menu", MainMenu));
            box.Add(Choice("Quit", Quit));
            overlay.Add(box);
        }

        void SettingsPage()
        {
            overlay.Clear();
            var box = Column();
            box.Add(Text("SETTINGS", 40, Ink, 8f));
            Space(box, 14);
            box.Add(Heading("PICTURE"));
            box.Add(Options("Quality", Settings.Qualities, Settings.Quality, i => Settings.Quality = i));
            box.Add(Options("Full screen", new[] { "On", "Off" }, Settings.Fullscreen ? 0 : 1, i => Settings.Fullscreen = i == 0));
            box.Add(Heading("SOUND"));
            box.Add(Level("Master", Settings.Master, v => Settings.Master = v));
            box.Add(Level("Music", Settings.Music, v => Settings.Music = v));
            box.Add(Level("Sounds", Settings.Sounds, v => Settings.Sounds = v));
            box.Add(Level("Spark's voice", Settings.Voice, v => Settings.Voice = v));
            box.Add(Heading("CONTROLS"));
            box.Add(Level("Mouse speed", Settings.Mouse, v => Settings.Mouse = v, 0.25f, 2.5f));
            box.Add(Heading("DIFFICULTY"));
            box.Add(Options("Cold and machines", Settings.Difficulties, Settings.Difficulty, i => Settings.Difficulty = i));
            Space(box, 18);
            box.Add(Choice("Back", Main));
            overlay.Add(box);
        }

        // ---- What the choices do ----

        void Continue()
        {
            int checkpoint = Settings.Checkpoint;
            Hide();
            if (director) director.Continue(checkpoint, Settings.SavedName, Settings.SavedTrust);
        }

        void NewGame()
        {
            Settings.Forget();
            Hide();
        }

        void MainMenu()
        {
            Time.timeScale = 1f;
            AudioListener.pause = false;
            IsOpen = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
        }

        static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // ---- Parts ----

        static VisualElement Column()
        {
            var box = new VisualElement();
            box.style.alignItems = Align.Center;
            box.style.minWidth = 620;
            return box;
        }

        static Label Text(string words, int size, Color colour, float spacing = 0f)
        {
            var label = new Label(words);
            label.style.fontSize = size;
            label.style.color = colour;
            label.style.letterSpacing = spacing;
            label.style.unityTextAlign = TextAnchor.MiddleCenter;
            label.style.marginBottom = 4;
            return label;
        }

        static void Space(VisualElement box, float height)
        {
            var gap = new VisualElement();
            gap.style.height = height;
            box.Add(gap);
        }

        static Label Heading(string words)
        {
            var label = Text(words, 15, Amber, 5f);
            label.style.marginTop = 16;
            label.style.marginBottom = 6;
            return label;
        }

        static Button Choice(string words, Action act)
        {
            var button = new Button(act) { text = words };
            Plain(button, 28);
            button.style.marginTop = button.style.marginBottom = 6;
            return button;
        }

        // A button without the theme's box: just its words.
        static void Bare(Button button, int size)
        {
            button.style.fontSize = size;
            button.style.color = Ink;
            button.style.backgroundColor = Color.clear;
            button.style.borderTopWidth = button.style.borderBottomWidth = button.style.borderLeftWidth = button.style.borderRightWidth = 0;
            button.style.paddingLeft = button.style.paddingRight = 14;
        }

        // Words that warm when the pointer is on them.
        static void Plain(Button button, int size)
        {
            Bare(button, size);
            button.RegisterCallback<PointerEnterEvent>(_ => button.style.color = Amber);
            button.RegisterCallback<PointerLeaveEvent>(_ => button.style.color = Ink);
        }

        static VisualElement Row(string words)
        {
            var row = new VisualElement();
            row.style.flexDirection = FlexDirection.Row;
            row.style.alignItems = Align.Center;
            row.style.width = 620;
            row.style.marginTop = row.style.marginBottom = 3;
            var label = Text(words, 21, Ink);
            label.style.width = 230;
            label.style.unityTextAlign = TextAnchor.MiddleLeft;
            row.Add(label);
            return row;
        }

        // A row of choices, the chosen one amber and underlined.
        static VisualElement Options(string words, string[] names, int chosen, Action<int> pick)
        {
            var row = Row(words);
            var buttons = new Button[names.Length];
            int marked = chosen;
            void Mark()
            {
                for (int i = 0; i < buttons.Length; i++)
                {
                    buttons[i].style.color = i == marked ? Amber : Dim;
                    buttons[i].style.borderBottomWidth = i == marked ? 2 : 0;
                }
            }
            for (int i = 0; i < names.Length; i++)
            {
                int index = i;
                var button = new Button(() =>
                {
                    marked = index;
                    pick(index);
                    Mark();
                }) { text = names[i] };
                Bare(button, 21);
                button.style.borderBottomColor = Amber;
                button.RegisterCallback<PointerEnterEvent>(_ => button.style.color = Ink);
                button.RegisterCallback<PointerLeaveEvent>(_ => Mark());
                buttons[i] = button;
                row.Add(button);
            }
            Mark();
            return row;
        }

        static VisualElement Level(string words, float value, Action<float> set, float low = 0f, float high = 1f)
        {
            var row = Row(words);
            var slider = new Slider(low, high) { value = value };
            slider.style.width = 360;
            slider.RegisterValueChangedCallback(e => set(e.newValue));
            row.Add(slider);
            return row;
        }
    }
}
