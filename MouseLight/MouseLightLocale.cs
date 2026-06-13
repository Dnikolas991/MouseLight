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
                "Illuminate the world position under the mouse in dark conditions.",
                "Light intensity",
                "Controls the target illumination level of the HDRP cursor spotlight.",
                "Range multiplier",
                "Controls the target illuminated radius as the camera distance changes.",
                "Red",
                "Red channel of the spotlight color.",
                "Green",
                "Green channel of the spotlight color.",
                "Blue",
                "Blue channel of the spotlight color."));
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
                "灯光亮度",
                "控制 HDRP 鼠标聚光灯的目标照度。",
                "范围倍率",
                "控制目标照明半径随摄像机距离变化的幅度。",
                "红色通道",
                "控制聚光灯颜色的红色分量。",
                "绿色通道",
                "控制聚光灯颜色的绿色分量。",
                "蓝色通道",
                "控制聚光灯颜色的蓝色分量。"));
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
                "Beleuchtet bei Dunkelheit die Weltposition unter dem Mauszeiger.",
                "Lichtintensität",
                "Bestimmt die Zielbeleuchtungsstärke des HDRP-Maus-Spotlights.",
                "Reichweitenmultiplikator",
                "Bestimmt den beleuchteten Zielradius bei wechselnder Kameradistanz.",
                "Rot",
                "Roter Farbkanal des Spotlights.",
                "Grün",
                "Grüner Farbkanal des Spotlights.",
                "Blau",
                "Blauer Farbkanal des Spotlights."));
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
            string blueDescription)
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
                { settings.GetOptionDescLocaleID(nameof(MouseLightSettings.Blue)), blueDescription }
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
