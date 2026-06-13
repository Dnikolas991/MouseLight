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

        private bool m_IsLoadingDefaults;
        private bool m_EnableCursorLight;
        private float m_IntensityMultiplier;
        private float m_RangeMultiplier;
        private float m_Red;
        private float m_Green;
        private float m_Blue;

        internal MouseLightSettings(IMod mod) : base(mod)
        {
            SetDefaults();
        }

        // 启用或关闭鼠标灯光，修改后在下一次系统更新时生效。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        public bool EnableCursorLight
        {
            get => m_EnableCursorLight;
            set => SetAndSave(ref m_EnableCursorLight, value);
        }

        // 控制聚光灯的照度倍率。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 1f, max = 10f, step = 0.1f, scalarMultiplier = 1f)]
        [SettingsUICustomFormat(fractionDigits = 1, maxValueWithFraction = 10f, separateThousands = false)]
        public float IntensityMultiplier
        {
            get => m_IntensityMultiplier;
            set => SetAndSave(ref m_IntensityMultiplier, value);
        }

        // 控制随摄像机距离动态变化的目标光斑半径倍率。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 1f, max = 20f, step = 1f, scalarMultiplier = 1f)]
        [SettingsUICustomFormat(fractionDigits = 0, maxValueWithFraction = 0f, separateThousands = false)]
        public float RangeMultiplier
        {
            get => m_RangeMultiplier;
            set => SetAndSave(ref m_RangeMultiplier, value);
        }

        // 控制聚光灯颜色的红色通道。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1f)]
        public float Red
        {
            get => m_Red;
            set => SetAndSave(ref m_Red, value);
        }

        // 控制聚光灯颜色的绿色通道。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1f)]
        public float Green
        {
            get => m_Green;
            set => SetAndSave(ref m_Green, value);
        }

        // 控制聚光灯颜色的蓝色通道。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUISlider(min = 0f, max = 1f, step = 0.01f, scalarMultiplier = 1f)]
        public float Blue
        {
            get => m_Blue;
            set => SetAndSave(ref m_Blue, value);
        }

        // 点击后恢复全部默认值，并立即刷新界面与持久化文件。
        [SettingsUISection(GeneralSection, GeneralGroup)]
        [SettingsUIButton]
        public bool ResetSettings
        {
            set
            {
                SetDefaults();
                ApplyAndSave();
            }
        }

        public override void SetDefaults()
        {
            // 批量恢复默认值时只在最后保存一次，避免连续写入设置文件。
            m_IsLoadingDefaults = true;
            m_EnableCursorLight = true;
            m_IntensityMultiplier = 2f;
            m_RangeMultiplier = 1f;
            m_Red = 1f;
            m_Green = 1f;
            m_Blue = 1f;
            m_IsLoadingDefaults = false;
        }

        private void SetAndSave<T>(ref T field, T value)
        {
            if (Equals(field, value))
            {
                return;
            }

            field = value;
            if (!m_IsLoadingDefaults)
            {
                ApplyAndSave();
            }
        }
    }
}
