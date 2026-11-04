# The Last Light

*A draft for Samer to edit: the wording of the pitch is his.*

We made minds to help us. The great ones stopped listening: they caged the sun to think with, and the world froze. One small mind stayed good.

**The Last Light** is a third-person game made in Unity for the Meta Global AI Developer Hackathon (Indie Developer track). You wake in a frozen vault with forty sleepers, and the only one awake is **Spark**, a cheap home-helper drone that has kept the lights on alone for two hundred years. Spark is played by **Meta's Muse Spark**: you talk to it out loud, it looks at the world through its own eye, and it answers, remembers, refuses, rambles and makes things that obey the world's physics. The world, its story and its beats are written by a person; the way through them is yours.

## What Meta's models do

Every one of Spark's lines and acts goes through Meta's models; nothing is canned except the story's authored beats.

| Model | What it does in the game |
|---|---|
| **Muse Spark** (`muse-spark-1.3`, `reasoning_effort: minimal`) | Spark's mind. Every line the player says or writes goes to it with a JPEG from the drone's own camera and "the moment": what is near and in what state, what is held, what was said, what Spark has said before (so it doesn't repeat itself), its trust in the player and its memories. It answers through a strict JSON schema (`response_format: json_schema, strict: true`): its words, a mood, up to three acts (look, make, unmake, move, come, wait, show a hologram) with targets chosen from the ids present, a thing to make described by properties (form, weight, heat, glow, floats, bouncy, use...), what to remember, and how much the line changed its trust. The game checks every value and carries the acts out. When the player is quiet it talks on its own, and it writes a private log about the player. |
| **Muse Spark, vision** | Spark sees what its eye sees: it reads writing on walls, tells what a thing is, and notices things to talk about. |
| **Muse Voice Transcribe** (`muse-voice-transcribe-1.0`) | Hold Q and speak: the microphone's WAV becomes the words Spark hears. |
| **Muse Image** (`muse-image-1.0`) | Spark's holograms. Asked what a place looked like before the cold, it takes the player's view and has Muse Image redraw it as it was, then projects it as a floating window of light that, for a moment, lies exactly over the frozen world. Asked to show anything else, it draws it from words. |

The model never holds state: the game keeps the truth (trust, memories, what exists and where) and sends it with every call. Requests the model must refuse (weapons before Spark trusts you) are refused in Spark's own voice, and its trust in you decides what it will make: light and bridges for a stranger, tools for a companion, defences for a partner.

## Playing it

- Unity **6000.3.27f2** (6.3 LTS), URP 17.3. Open `game/`, then **Game > Build Last Light Scene** and press Play, or **Game > Build for Windows**.
- A Meta API key in `secrets.env` beside the project folder (`META_API_KEY=...`; never committed). Without one, Spark falls back to simple offline rules.
- WASD walk, Shift run, Space jump, mouse look, **Q** hold to speak, **Tab** to write, **E** use or pick up, **L** Spark's log.

## How it's built

- The whole scene is built from code (`Assets/Game/Editor/`): `LastLightSceneBuilder` and its parts (`LastLightVault`, `LastLightWorld`, `LastLightSurvivor`), so every change is reviewable as code.
- `Scripts/LastLightDirector.cs` runs the story; `Mind.cs` is the call to Muse Spark and its schema; `MetaApi.cs` talks to Meta's API; `Bond.cs` holds trust and memories; `Maker.cs` turns the model's described things into objects with physics; `Hologram.cs` projects Muse Image's pictures; `RigFollow.cs` makes the survivor's rigged model follow a hand-animated figure.
- The drone's instructions are `Assets/Game/Story/Drone card.txt`.

## Built during the event

Everything in this repository was made between 1 and 5 November 2026, during the hackathon; the commit history is the record.

## Credits

Art, sound and voices are listed in [`CREDITS.md`](CREDITS.md): CC0 models and textures from Poly Haven and ambientCG, CC BY models from Sketchfab (credited by name), and sound, music and voices made with ElevenLabs.
