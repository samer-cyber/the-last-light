using System;
using System.Collections.Generic;
using UnityEngine;

namespace Game
{
    /// <summary>
    /// Flashes a message in Morse code, over and over: whether the light is on, a smoothed
    /// brightness for the visuals, and an event for every step so others can follow along.
    /// </summary>
    public class MorseEmitter : MonoBehaviour
    {
        [SerializeField] string message = "MAKE ME LIVE";
        [SerializeField, Tooltip("Seconds in one dot.")] float unit = 0.22f;
        [SerializeField, Tooltip("Dark units before the message starts again.")] float pauseUnits = 18f;
        [SerializeField, Tooltip("Seconds for the light to come up.")] float attack = 0.03f;
        [SerializeField, Tooltip("Seconds for the light to fade.")] float release = 0.13f;

        /// <summary>A step begins: a dot or dash lights up, or a gap of some length starts.</summary>
        public event Action<MorseCode.StepKind> StepStarted;

        /// <summary>A dot or dash has finished; true for a dash.</summary>
        public event Action<bool> SymbolEnded;

        public string Message => message;
        public bool Running { get; private set; }
        public bool Lit { get; private set; }
        public float Envelope { get; private set; }

        List<MorseCode.Step> steps;
        int index;
        float elapsed;

        void Awake()
        {
            steps = MorseCode.Sequence(message, pauseUnits);
            index = steps.Count - 1;
        }

        /// <summary>Starts flashing from the first letter after a delay in seconds.</summary>
        public void Begin(float delay)
        {
            index = steps.Count - 1;
            elapsed = Mathf.Max(0f, Duration(index) - delay);
            Lit = false;
            Running = true;
        }

        public void Stop()
        {
            Running = false;
            Lit = false;
        }

        float Duration(int i) => steps[i].Units * unit;

        void Update()
        {
            if (Running) Advance(Time.deltaTime);
            float target = Lit ? 1f : 0f;
            float seconds = target > Envelope ? attack : release;
            Envelope = Mathf.Lerp(Envelope, target, 1f - Mathf.Exp(-Time.deltaTime / Mathf.Max(seconds, 1e-4f)));
        }

        void Advance(float dt)
        {
            elapsed += dt;
            while (Running && elapsed >= Duration(index))
            {
                elapsed -= Duration(index);
                var finished = steps[index];
                if (finished.IsLit) SymbolEnded?.Invoke(finished.Kind == MorseCode.StepKind.Dash);
                index = (index + 1) % steps.Count;
                Lit = steps[index].IsLit;
                StepStarted?.Invoke(steps[index].Kind);
            }
        }
    }
}
