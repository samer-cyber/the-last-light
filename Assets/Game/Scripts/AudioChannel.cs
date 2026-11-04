using UnityEngine;

namespace Game
{
    /// <summary>
    /// Which of the mixer's channels a sound belongs to (music, sounds, Spark's voice). It turns the source's own output
    /// down by the channel's level as the audio is made, so it never fights the fades the game sets on the source.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class AudioChannel : MonoBehaviour
    {
        public Settings.Channel channel = Settings.Channel.Sounds;
        volatile float gain = 1f;

        void Update() => gain = Settings.Gain(channel);

        void OnAudioFilterRead(float[] data, int channels)
        {
            float g = gain;
            if (g > 0.999f) return;
            for (int i = 0; i < data.Length; i++) data[i] *= g;
        }

        /// <summary>Gives every sound in the scene its channel, by what it is called.</summary>
        public static void TagAll()
        {
            foreach (var source in Object.FindObjectsByType<AudioSource>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (source.GetComponent<AudioChannel>()) continue;
                string name = source.gameObject.name;
                var channel = name.Contains("Music") ? Settings.Channel.Music
                    : name == "Voice" || name == "Memory" ? Settings.Channel.Voice
                    : Settings.Channel.Sounds;
                var tag = source.gameObject.AddComponent<AudioChannel>();
                tag.channel = channel;
                tag.gain = Settings.Gain(channel);
            }
        }
    }
}
