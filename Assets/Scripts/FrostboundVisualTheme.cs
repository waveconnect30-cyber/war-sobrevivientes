using UnityEngine;

namespace FrostboundFrontier
{
    public static class FrostboundVisualTheme
    {
        private const string Root = "FrostboundSkin/";
        private static Sprite Load(string key) => Resources.Load<Sprite>(Root + key);
        private static Texture2D Texture(string key) => Load(key)?.texture;

        public static void ApplyButton(GUIStyle style, bool gold = false)
        {
            if (style == null) return;
            Texture2D normal = Texture(gold ? "button_gold" : "button_blue");
            Texture2D active = Texture(gold ? "button_blue" : "button_gold");
            if (normal == null) return;
            style.normal.background = normal;
            style.hover.background = normal;
            style.focused.background = normal;
            style.active.background = active != null ? active : normal;
            style.normal.textColor = style.hover.textColor = style.focused.textColor = Color.white;
            style.active.textColor = Color.white;
            style.border = new RectOffset(22, 22, 18, 18);
            style.padding = new RectOffset(14, 14, 8, 10);
        }

        public static void ApplyPanel(GUIStyle style, bool strip = false)
        {
            if (style == null) return;
            Texture2D texture = Texture(strip ? "panel_strip" : "panel_main");
            if (texture == null) return;
            style.normal.background = texture;
            style.border = new RectOffset(28, 28, 28, 28);
            style.padding = new RectOffset(20, 20, 16, 18);
        }

        public static void DrawIcon(Rect rect, string key, Color tint)
        {
            Sprite sprite = Load("icon_" + key);
            if (sprite == null) return;
            Color previous = GUI.color;
            GUI.color = tint;
            GUI.DrawTexture(rect, sprite.texture, ScaleMode.ScaleToFit, true);
            GUI.color = previous;
        }

        public static Sprite NodeSprite(string tileType, string resourceType)
        {
            if (tileType == "Beast") return Load("icon_temperature");
            if (tileType != "ResourceNode") return null;
            if (resourceType == "Wood") return Load("icon_wood");
            if (resourceType == "Food") return Load("icon_food");
            return Load("icon_coal");
        }
    }
}
