using UnityEngine;

namespace SoulArcher
{
    public static class GameFont
    {
        private static Font cached;

        public static Font Get()
        {
            if (cached != null) return cached;
            cached = Resources.Load<Font>("Fonts/Galmuri11-Bold");
            if (cached == null) cached = Resources.Load<Font>("Fonts/Galmuri11");
            if (cached == null) cached = Font.CreateDynamicFontFromOSFont(new[] { "Malgun Gothic", "Arial" }, 16);
            if (cached == null) cached = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            return cached;
        }
    }
}
