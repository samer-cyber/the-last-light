# The Last Light

We built minds to help us. They grew past us: they caged the sun to think with, and the world froze. We went under the hill to sleep until the thaw.

**The Last Light** is a third-person game made in Unity for the Meta Global AI Developer Hackathon (Indie Developer track). You wake in Shelter 7, a vault of forty frozen sleepers, 211 years into the cold. The only one awake is **Spark**, the shelter's last caretaker drone. Spark is played by **Meta's Muse Spark**: you talk to it out loud or write to it; it sees what you see, reads what is written in the world, answers in its own words, remembers you, goes about its work, makes things that obey the world's physics, and shows you holograms of how things were. It also keeps its makers' five protocols, and the only way past them is to convince it.

The idea: AI that writes a game for you tends to make it hollow. Here a person wrote the world, its story and its clues; the AI gives it a companion with real freedom, and the way through is yours.

**Demo video:** _link to come_

## What Meta's models do

Every one of Spark's lines and acts goes through Meta's models; the only written lines are a few story beats (its first words, for example).

| Model | What it does in the game |
|---|---|
| **Muse Spark** (`muse-spark-1.3`) | Spark's mind. Every line the player says or writes goes to it with a JPEG of what the player is looking at and "the moment": what is near and in what state, what is held, the last lines, what Spark has already said (so it never repeats itself), the writing within reach, its trust in the player and its memories. It answers through a strict JSON schema (`response_format: json_schema, strict: true`): its words and mood, up to three acts (look, make, unmake, move, come, wait, show a hologram) aimed at ids that exist, a thing to make described by its properties (form, size, weight, heat, glow, floats, bouncy, use), what to remember, how the line changed its trust, and whether it chooses to break a protocol. The game checks every value and carries the acts out. When the player is quiet it talks on its own and keeps a private log about them. What the player says is answered at `reasoning_effort: minimal` (speed matters most); Spark's own thoughts and long, tangled lines get `low`. |
| **Muse Spark, vision** | Spark sees through the picture: asked "what's in front of me?" it says what is there; it flies up to a note and reads it word for word (Mara's handwriting, words scratched in the empty pods, the station wall). |
| **Muse Voice Transcribe** (`muse-voice-transcribe-1.0`) | Hold Q and speak: the microphone's WAV becomes the words Spark hears. Spark stops talking when you start. |
| **Muse Image** (`muse-image-1.0`) | Spark's holograms: asked how a place looked before the cold, it takes your own view and has Muse Image redraw it as it was, then projects it as a window of light that for a moment lies exactly over the frozen world. Also Ima's crayon drawing in the vault. |

The model never holds state: the game keeps the truth (trust, memories, what exists and where, which protocols hold) and sends it with every call.

![How one line becomes an act in the world: what you say (through Muse Voice Transcribe), what Spark sees and the moment go to Muse Spark, which fills a strict schema; the game checks every value and the world obeys](Docs/how-spark-thinks.svg)

**Spark's protocols.** Its makers gave it five rules: keep the sleepers alive; keep the vault sealed until the thaw; make nothing that can harm a person; never leave the sleepers; wake a sleeper only if their pod is failing. Three of them stand between you and the world (the frozen door, the threshold, anything that could fight the machines). Spark breaks one only by its own choice: the model's answer carries a `protocol` field it sets when it decides, and the game accepts it only once the story has earned it (it knows you a little, it has seen a pod fail). Until then it refuses in its own words, and the game holds it to its rules too. What persuades it is whatever you say: there is no right sentence.

## Can a game afford a language model?

Each line Spark answers sends about 6,000 to 8,000 tokens (its card, the moment and one small picture) and gets a few hundred back, in about 6.5 seconds at the median (3 to 11 s for most lines). Building and testing this game for four days, with every test line, bench run and trailer take going through Muse Spark, used about **$3.50** of API credit. An hour of play is roughly 60 to 120 lines: a few cents per player-hour. The card is the same in every call, so prompt caching would remove most of the input cost, and routine lines could move to a small open-weight model on the player's machine.

## Playing it

**From source:** Unity **6000.3.27f2** (6.3 LTS), URP 17.3. Open this folder in Unity, then **Game > Play the Last Light** (or **Game > Build Last Light Scene** to rebuild the scene from code, and **Game > Build for Windows** for a standalone build).

**Keys:** put `META_API_KEY=...` in a file called `secrets.env` in the project folder or any folder above it (it is never committed). Spark's spoken voice uses ElevenLabs (`ELEVENLABS_API_KEY=...` in the same file); without it Spark chirps and its words show as subtitles. Without a Meta key, Spark falls back to simple offline rules.

| Key | |
|---|---|
| W A S D, Shift, Space | walk, run, jump |
| Mouse, wheel | look, zoom |
| **Q** (hold) | speak to Spark |
| **Tab** | write to Spark |
| **E** | use, pick up, put down, read |
| **P**, **L** | Spark's protocols, Spark's log |
| **Esc** | pause: settings (picture, full screen, music, sounds and Spark's voice, mouse speed, difficulty), main menu, quit |

A build opens on a title (Continue, New game, Settings, Quit). The game saves at two checkpoints (awake in the vault; outside it); **Continue** goes back to the last one, and Spark still knows your name.

## How it's built

| Where | What |
|---|---|
| `Assets/Game/Editor/` | The whole scene is built from code, so every change is reviewable: `LastLightSceneBuilder` (the world, Spark, the station, the mirror), `LastLightVault` (the shelter), `LastLightSleepers` and `LastLightSurvivor` (the people), `LastLightWorld` (the ground, the town's houses), `LastLightTown` (the streets, the forest, the chasm and its bridge), `SceneKit` and `Art` (materials, meshes, placing models), `GameMenu` |
| `Assets/Game/Scripts/` | The game: `LastLightDirector` (the story, its beats, every line through Spark's mind), `Mind` (the call to Muse Spark and its schema), `MetaApi`, `PushToTalk` (Muse Voice Transcribe), `Hologram` (Muse Image), `Drone` (Spark's body: following, its rounds in the vault, facing you, a voice queue that cuts in and picks up where it stopped), `SparkVoice`, `Bond` (trust and memories), `Maker` and `Built` (things made from the model's descriptions, with physics), the machines (`Seeker`, `Harvester`), `Menu` and `Settings` |
| `Assets/Game/Story/Drone card.txt` | Spark's instructions: who it is, the world's true facts, how it talks, what it can do |
| `Assets/Game/Shaders/` | Hand-written HLSL: frost on curved glass, mist, the snow ground, the caged sun, holograms |
| `Assets/Game/Art`, `Audio`, `Textures` | Models, sounds and pictures; every source and licence is in [`CREDITS.md`](CREDITS.md) |
| `Docs/` | The diagram above |

**The trailer was filmed inside the game, unscripted.** `Scripts/Filming.cs` records 1920x1080 frames (camera and HUD) and the game's own sound offline, with game time locked to 30 fps so the waits on Meta's API vanish from the footage; `Scripts/Dolly.cs` moves the camera. Spark's replies in the trailer are what Muse Spark said while it was filmed (a few story beats are written lines, such as its first words and "This is as far as I go"), and the cut points were found by transcribing the takes with Muse Voice Transcribe.

## Built during the event

Everything in this repository was made between 1 and 5 November 2026, during the hackathon; the commit history is the record. It includes the idea's earlier versions (a Morse-code opening and a young solar system), each dropped after a playtest and removed from the final game.

## Credits

Art, sound and voices are listed in [`CREDITS.md`](CREDITS.md): CC0 models and textures from Poly Haven and ambientCG, CC BY models from Sketchfab (credited by name), and sound, music and voices made with ElevenLabs.
