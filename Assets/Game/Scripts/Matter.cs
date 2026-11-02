using UnityEngine;

namespace Game
{
    /// <summary>What the mind can shape things from. It makes nothing from nothing.</summary>
    public enum Matter { Stone, Ice, Metal, Light }

    /// <summary>What each matter is like, in the words the game and the model use.</summary>
    public static class Matters
    {
        public static readonly Matter[] All = { Matter.Stone, Matter.Ice, Matter.Metal, Matter.Light };

        /// <summary>The cubic metres one unit of it makes: metal is drawn thin, light is spun finer still.</summary>
        public static float VolumePerUnit(Matter m) => m switch
        {
            Matter.Metal => 0.8f,
            Matter.Light => 1.2f,
            _ => 0.45f,
        };

        /// <summary>Kilograms per cubic metre, for the pieces that fall and roll.</summary>
        public static float Density(Matter m) => m switch
        {
            Matter.Stone => 2600f,
            Matter.Ice => 900f,
            Matter.Metal => 3000f,
            _ => 30f,
        };

        public static string Word(Matter m) => m switch
        {
            Matter.Stone => "stone",
            Matter.Ice => "ice",
            Matter.Metal => "metal",
            _ => "light",
        };

        public static string Id(Matter m) => m.ToString().ToUpperInvariant();

        public static bool TryParse(string id, out Matter matter)
        {
            foreach (var m in All)
            {
                if (Id(m) != id) continue;
                matter = m;
                return true;
            }
            matter = Matter.Stone;
            return false;
        }

        /// <summary>Units needed for a piece of this size.</summary>
        public static int Units(Matter m, float cubicMetres) => Mathf.Max(1, Mathf.CeilToInt(cubicMetres / VolumePerUnit(m) - 0.05f));
    }
}
