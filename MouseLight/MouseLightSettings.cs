using Game.Modding;
using Game.Settings;
using Colossal.IO.AssetDatabase;

namespace MouseLight
{
    [FileLocation(nameof(MouseLight))]
    [SettingsUIGroupOrder(GeneralGroup)]
    internal sealed class MouseLightSettings : ModSetting
    {
        internal const string GeneralSection = "General";
        internal const string GeneralGroup = "CursorLight";

        internal MouseLightSettings(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        // 启用或关闭鼠标灯光，修改后在下一次系统更新时生效。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        public bool EnableCursorLight { get; set; }

        // 控制聚光灯的照度倍率。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 1f, max = 10f, step = 0.1f, scalarMultiplier = 1f)]
        [SettingsUICustomFormat(fractionDigits = 1, maxValueWithFraction = 10f, separateThousands = false)]
        public float IntensityMultiplier { get; set; }

        // 控制随摄像机距离动态变化的目标光斑半径倍率。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 1f, max = 20f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUICustomFormat(fractionDigits = 0, maxValueWithFraction = 0f, separateThousands = false)]
        public float RangeMultiplier { get; set; }

        // 控制聚光灯颜色的红色通道。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1f)]
        public float Red { get; set; }

        // 控制聚光灯颜色的绿色通道。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1f)]
        public float Green { get; set; }

        // 控制聚光灯颜色的蓝色通道。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1f)]
        public float Blue { get; set; }

        public override void SetDefaults()
        {
            EnableCursorLight = true;
            IntensityMultiplier = 2f;
            RangeMultiplier = 1f;
            Red = 1f;
            Green = 1f;
            Blue = 1f;
        }
    }
}
