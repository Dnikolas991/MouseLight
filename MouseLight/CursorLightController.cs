using UnityEngine;
using UnityEngine.Rendering.HighDefinition;

namespace MouseLight
{
    internal sealed class CursorLightController
    {
        //可调节参数的默认值在MouseLightSettings里面
        // 以下参数用于稳定不同镜头距离下的光斑尺寸和照度。
        private const float ReferenceFootprintRadius = 40f;
        private const float ReferenceLumens = 50000f;
        private const float FootprintDistanceScale = 8f;
        private const float MinimumFootprintRadius = 8f;
        private const float RangeBeyondTargetFactor = 1.5f;
        private const float SpotAngle = 90f;

        private static CursorLightController s_Shared;
        private GameObject m_LightObject;
        private Light m_Light;
        private HDAdditionalLightData m_HdLight;
        private Vector3 m_SmoothedPosition;
        private Vector3 m_SmoothedDirection;
        private bool m_HasPosition;

        internal static CursorLightController Shared => s_Shared ?? (s_Shared = new CursorLightController());

        internal bool EnsureCreated()
        {
            if (m_Light != null)
            {
                return true;
            }

            try
            {
                // 全程复用一个聚光灯，避免逐帧创建对象和产生残影。
                m_LightObject = new GameObject("MouseLight.CursorSpotLight");
                Object.DontDestroyOnLoad(m_LightObject);
                m_Light = m_LightObject.AddComponent<Light>();
                m_Light.type = LightType.Spot;
                //默认灯光是白色
                m_Light.color = Color.white;
                m_Light.spotAngle = SpotAngle;
                m_Light.shadows = LightShadows.None;

                // CS2 使用 HDRP 物理光源，聚光灯需要附加 HDAdditionalLightData。
                m_HdLight = m_LightObject.AddComponent<HDAdditionalLightData>();
                m_HdLight.type = HDLightType.Spot;
                m_HdLight.SetSpotAngle(SpotAngle, 1f);
                m_HdLight.SetIntensity(0f, LightUnit.Lumen);
                m_HdLight.EnableShadows(false);
                m_Light.enabled = false;
                return true;
            }
            catch (System.Exception exception)
            {
                Logger.Error($"鼠标聚光灯初始化失败：{exception.Message}");
                Dispose();
                return false;
            }
        }

        internal void Update(
            Vector3 targetPosition,
            Vector3 cameraPosition,
            float nightFactor,
            float intensityMultiplier,
            float rangeMultiplier,
            float red,
            float green,
            float blue)
        {
            if (!EnsureCreated())
            {
                return;
            }

            float factor = Mathf.Clamp01(nightFactor);
            if (factor <= 0.001f)
            {
                Hide();
                return;
            }

            Vector3 cameraToTarget = targetPosition - cameraPosition;
            float distance = cameraToTarget.magnitude;
            if (distance <= 0.01f)
            {
                Hide();
                return;
            }

            float baseIntensity = Mathf.Clamp(intensityMultiplier, 1f, 10f);
            float rangeScale = Mathf.Clamp(rangeMultiplier, 1f, 20f);

            // 先根据镜头距离计算目标光斑半径，系数 8 与范围倍率直接相乘。
            float footprintRadius = Mathf.Max(
                MinimumFootprintRadius,
                Mathf.Sqrt(distance) * FootprintDistanceScale * rangeScale);

            // 90 度聚光灯的半角为 45 度，因此回退距离等于目标光斑半径。
            float halfAngleRadians = SpotAngle * 0.5f * Mathf.Deg2Rad;
            float retreatDistance = footprintRadius / Mathf.Tan(halfAngleRadians);
            float actualRange = retreatDistance + footprintRadius * RangeBeyondTargetFactor;

            // 光源沿摄像机射线从命中点向摄像机回退，并始终照向命中点。
            Vector3 cameraDirection = cameraToTarget / Mathf.Max(1f, distance);
            Vector3 desiredPosition = targetPosition - cameraDirection * retreatDistance;
            Vector3 desiredDirection = (targetPosition - desiredPosition).normalized;

            float smoothing = 1f - Mathf.Exp(-18f * Time.unscaledDeltaTime);
            m_SmoothedPosition = m_HasPosition
                ? Vector3.Lerp(m_SmoothedPosition, desiredPosition, smoothing)
                : desiredPosition;
            m_SmoothedDirection = m_HasPosition
                ? Vector3.Slerp(m_SmoothedDirection, desiredDirection, smoothing)
                : desiredDirection;
            m_HasPosition = true;

            m_LightObject.transform.position = m_SmoothedPosition;
            m_LightObject.transform.rotation = Quaternion.LookRotation(m_SmoothedDirection, Vector3.up);

            // HDRP 按距离平方衰减，因此流明按回退距离平方补偿，使缩放时地面亮度更稳定。
            float distanceCompensation = Mathf.Pow(footprintRadius / ReferenceFootprintRadius, 2f);
            float hdrpIntensity = ReferenceLumens * distanceCompensation * baseIntensity * factor;

            m_Light.range = actualRange;
            Color lightColor = new Color(
                Mathf.Clamp01(red),
                Mathf.Clamp01(green),
                Mathf.Clamp01(blue),
                1f);
            m_Light.color = lightColor;
            m_HdLight.color = lightColor;
            m_HdLight.SetRange(actualRange);
            m_HdLight.SetIntensity(hdrpIntensity, LightUnit.Lumen);
            m_Light.enabled = true;
        }

        internal void Hide()
        {
            if (m_Light != null)
            {
                m_Light.enabled = false;
                m_HdLight?.SetIntensity(0f, LightUnit.Lumen);
            }

            m_HasPosition = false;
        }

        internal static void DisposeShared()
        {
            s_Shared?.Dispose();
            s_Shared = null;
        }

        private void Dispose()
        {
            if (m_LightObject != null)
            {
                Object.Destroy(m_LightObject);
            }

            m_Light = null;
            m_HdLight = null;
            m_LightObject = null;
            m_HasPosition = false;
        }
    }
}
