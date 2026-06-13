using System.Collections.Generic;
using Colossal;

namespace MouseLight
{
    internal sealed class MouseLightLocale : IDictionarySource
    {
        private readonly IReadOnlyDictionary<string, string> m_Entries;

        private MouseLightLocale(IReadOnlyDictionary<string, string> entries)
        {
            m_Entries = entries;
        }

        // 英语是默认语言，也是其他语言缺失时的文本基准。
        internal static MouseLightLocale CreateEnglish(MouseLightSettings settings)
        {
            return new MouseLightLocale(CreateEntries(
                settings,
                "Mouse Light",
                "General",
                "Cursor light",
                "Enable cursor light",
                "Illuminate the game world position pointed to by the mouse in dark environments.",
                "Light intensity multiplier",
                "Controls the illumination intensity multiplier of the cursor spotlight.",
                "Range multiplier",
                "Controls how much the target illumination radius changes with camera distance.",
                "Red channel",
                "Controls the red component of the spotlight color.",
                "Green channel",
                "Controls the green component of the spotlight color.",
                "Blue channel",
                "Controls the blue component of the spotlight color.",
                "Reset settings",
                "Restore all Mouse Light settings to their defaults."));
        }

        // 简体中文设置文本。
        internal static MouseLightLocale CreateChinese(MouseLightSettings settings)
        {
            return new MouseLightLocale(CreateEntries(
                settings,
                "鼠标灯光",
                "常规",
                "光标照明",
                "启用光标照明",
                "在较暗环境中照亮鼠标指向的游戏世界位置。",
                "灯光亮度倍率",
                "控制鼠标聚光灯的光照强度倍率。",
                "范围倍率",
                "控制目标照明半径随摄像机距离变化的幅度。",
                "红色通道",
                "控制聚光灯颜色的红色分量。",
                "绿色通道",
                "控制聚光灯颜色的绿色分量。",
                "蓝色通道",
                "控制聚光灯颜色的蓝色分量。",
                "重置设置",
                "将鼠标灯光的全部设置恢复为默认值。"));
        }

        // 德语设置文本。
        internal static MouseLightLocale CreateGerman(MouseLightSettings settings)
        {
            return new MouseLightLocale(CreateEntries(
                settings,
                "Mauslicht",
                "Allgemein",
                "Cursor-Beleuchtung",
                "Cursor-Beleuchtung aktivieren",
                "Beleuchtet in dunkler Umgebung die vom Mauszeiger markierte Position in der Spielwelt.",
                "Lichtstärke-Multiplikator",
                "Steuert den Multiplikator der Beleuchtungsstärke des Maus-Spotlights.",
                "Reichweitenmultiplikator",
                "Steuert, wie stark sich der Zielbeleuchtungsradius mit der Kameradistanz verändert.",
                "Rotkanal",
                "Steuert den Rotanteil der Spotlight-Farbe.",
                "Grünkanal",
                "Steuert den Grünanteil der Spotlight-Farbe.",
                "Blaukanal",
                "Steuert den Blauanteil der Spotlight-Farbe.",
                "Einstellungen zurücksetzen",
                "Setzt alle Einstellungen von Mouse Light auf die Standardwerte zurück."));
        }

        private static IReadOnlyDictionary<string, string> CreateEntries(
            MouseLightSettings settings,
            string title,
            string tab,
            string group,
            string enabledLabel,
            string enabledDescription,
            string intensityLabel,
            string intensityDescription,
            string rangeLabel,
            string rangeDescription,
            string redLabel,
            string redDescription,
            string greenLabel,
            string greenDescription,
            string blueLabel,
            string blueDescription,
            string resetLabel,
            string resetDescription)
        {
            // 所有语言共用 ModSetting 生成的稳定本地化键。
            return new Dictionary<string, string>
            {
                { settings.GetSettingsLocaleID(), title },
                { settings.GetOptionTabLocaleID(MouseLightSettings.GeneralSection), tab },
                { settings.GetOptionGroupLocaleID(MouseLightSettings.GeneralGroup), group },
                { settings.GetOptionLabelLocaleID(nameof(MouseLightSettings.EnableCursorLight)), enabledLabel },
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.EnableCursorLight)), enabledDescription },
                { settings.GetOptionLabelLocaleID(nameof(MouseLightSettings.IntensityMultiplier)), intensityLabel },
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.IntensityMultiplier)), intensityDescription },
                { settings.GetOptionLabelLocaleID(nameof(MouseLightSettings.RangeMultiplier)), rangeLabel },
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.RangeMultiplier)), rangeDescription },
                { settings.GetOptionLabelLocaleID(nameof(MouseLightSettings.Red)), redLabel },
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.Red)), redDescription },
                { settings.GetOptionLabelLocaleID(nameof(MouseLightSettings.Green)), greenLabel },
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.Green)), greenDescription },
                { settings.GetOptionLabelLocaleID(nameof(MouseLightSettings.Blue)), blueLabel },
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.Blue)), blueDescription },
                { settings.GetOptionLabelLocaleID(nameof(MouseLightSettings.ResetSettings)), resetLabel },
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.ResetSettings)), resetDescription }
            };
        }

        public IEnumerable<KeyValuePair<string, string>> ReadEntries(
            IList<IDictionaryEntryError> errors,
            Dictionary<string, int> indexCounts)
        {
            return m_Entries;
        }

        public void Unload()
        {
        }
    }
}
