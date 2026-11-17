// EmissionFlicker v1.0 - PBR_Mobile自发光闪烁脚本（脚本挂载到模型上）
// EmissionFlicker v1.1 - 缓存ShaderPropertyID，优化材质获取逻辑，消除硬编码字符串
// EmissionFlicker v1.2 - 修复 _EmissionScale 初始为 0 时无法闪烁、以及编辑态基准被脚本写入值污染导致闪烁衰减的问题
// EmissionFlicker v1.3 - fallbackBaseEmissionScale 支持运行时实时修改（改动后下一帧立即生效，无需重启/重新初始化）
// EmissionFlicker v1.4 - 修复自发光参数0时无法闪烁的问题，支持PBR_Mobile_NEW.shader，添加"基准强度"参数。
using UnityEngine;

namespace Vic.Runtime
{
    /// 自发光闪烁组件 - 运行时自动控制材质的自发光强度闪烁
    [ExecuteAlways]
    public class EmissionFlicker : MonoBehaviour
    {
        [Header("闪烁设置")]
        [Tooltip("是否启用闪烁")]
        public bool enableFlicker = true;

        [Tooltip("闪烁速度")]
        [Range(0.1f, 20f)]
        public float flickerSpeed = 5.0f;

        [Tooltip("最小亮度倍数")]
        [Range(0f, 4f)]
        public float minIntensity = 0.2f;

        [Tooltip("最大亮度倍数")]
        [Range(1f, 5f)]
        public float maxIntensity = 1.5f;

        [Tooltip("闪烁曲线强度（越大闪烁越明显）")]
        [Range(1f, 5f)]
        public float curvePower = 2.0f;

        [Header("目标设置")]
        [Tooltip("目标渲染器（留空则自动获取）")]
        public Renderer targetRenderer;

        [Tooltip("材质索引（如果有多个材质）")]
        public int materialIndex = 0;

        [Header("基准强度")]
        [Tooltip("材质的 _EmissionScale 为 0 时，作为闪烁基准的自发光强度（<=0 表示不回退，保持 0 不闪烁）。运行时修改本参数下一帧立即生效")]
        public float fallbackBaseEmissionScale = 1.0f;

        private static readonly int EmissionScaleID = Shader.PropertyToID("_EmissionScale");

        private Material _material;
        private float _baseEmissionScale;      // 闪烁计算基准
        private float _restoreEmissionScale;  // 关闭组件时写回材质的原始值
        private float _lastAppliedValue;      // 脚本最后一次写入材质的值（用于识别被污染的材质值）
        private bool _hasAppliedValue;
        private float _cachedFallbackBase;    // 上一次使用的备用基准（用于检测参数变化）
        private bool _usingFallbackBase;      // 当前基准是否来自备用基准
        private float _time;
        private bool _initialized;

        private void OnEnable()
        {
            Initialize();
        }

        private void Update()
        {
            // 始终执行：保证 fallbackBaseEmissionScale 在运行时被修改后下一帧立即生效
            ApplyFallbackBaseIfChanged();

            if (!enableFlicker || !_initialized || _material == null)
                return;

            _time += Time.deltaTime;

            float t = Mathf.Sin(_time * flickerSpeed) * 0.5f + 0.5f;
            t = Mathf.Pow(t, curvePower);

            float currentScale = Mathf.Lerp(
                _baseEmissionScale * minIntensity,
                _baseEmissionScale * maxIntensity,
                t);

            _material.SetFloat(EmissionScaleID, currentScale);
            _lastAppliedValue = currentScale;
            _hasAppliedValue = true;
        }

        private void OnDisable()
        {
            if (_initialized && _material != null)
            {
                // 还原材质原始自发光强度，避免把闪烁后的数值留在材质资产上
                _material.SetFloat(EmissionScaleID, _restoreEmissionScale);
            }
            _hasAppliedValue = false;
        }

        private void OnValidate()
        {
            Initialize();
        }

        /// 重新以材质当前值为基准（忽略脚本自己写入的波形值）
        public void ResetBaseIntensity()
        {
            if (!_initialized || _material == null)
                return;

            float current = _material.GetFloat(EmissionScaleID);
            if (!IsPollutedValue(current))
            {
                _restoreEmissionScale = current;
                ResolveBase(current);
            }
            _time = 0f;
        }

        /// 设置备用基准强度（运行时可直接调用，立即生效）
        public void SetFallbackBaseEmissionScale(float value)
        {
            fallbackBaseEmissionScale = value;
            ApplyFallbackBaseIfChanged();
        }

        private void Initialize()
        {
            _initialized = false;

            if (targetRenderer == null)
                targetRenderer = GetComponent<Renderer>();

            if (targetRenderer == null)
            {
                Debug.LogWarning($"EmissionFlicker: 未找到 Renderer 组件在 {gameObject.name}");
                return;
            }

            _material = GetTargetMaterial();
            if (_material == null)
            {
                Debug.LogWarning($"EmissionFlicker: 未找到材质索引 {materialIndex} 在 {gameObject.name}");
                return;
            }

            if (!_material.HasProperty(EmissionScaleID))
            {
                Debug.LogWarning($"EmissionFlicker: 材质 {_material.name} 没有 _EmissionScale 属性");
                return;
            }

            float current = _material.GetFloat(EmissionScaleID);

            // 编辑态（ExecuteAlways + sharedMaterials）下材质里的值可能是脚本自己写入的结果，
            // 这种值不能当作新基准，否则基准会被波形逐次拉低直到 0，闪烁随之消失
            if (!IsPollutedValue(current))
            {
                _restoreEmissionScale = current;
                ResolveBase(current);
            }
            else
            {
                // 材质值是脚本自己写入的污染值：保持原基准，但备用基准的修改仍需实时生效
                ApplyFallbackBase(true);
            }

            _cachedFallbackBase = fallbackBaseEmissionScale;
            _time = 0f;
            _initialized = true;
        }

        /// 判断材质当前值是否为脚本自己写入的波形值（污染值）
        private bool IsPollutedValue(float materialValue)
        {
            return _hasAppliedValue && Mathf.Approximately(materialValue, _lastAppliedValue);
        }

        /// 计算闪烁基准：材质原始值为 0 时回退到备用基准，保证闪烁可见
        private void ResolveBase(float materialValue)
        {
            if (materialValue > 0.0001f)
            {
                // 材质本身有强度时，以材质值为基准，备用基准不参与
                _baseEmissionScale = materialValue;
                _usingFallbackBase = false;
            }
            else if (fallbackBaseEmissionScale > 0.0001f)
            {
                _baseEmissionScale = fallbackBaseEmissionScale;
                _usingFallbackBase = true;
            }
            else
            {
                _baseEmissionScale = 0f;
                _usingFallbackBase = false;
            }
        }

        /// 备用基准参数变化时实时更新基准（force 为 true 时忽略变化检测强制应用）
        private void ApplyFallbackBaseIfChanged(bool force = false)
        {
            if (!force && Mathf.Approximately(_cachedFallbackBase, fallbackBaseEmissionScale))
                return;

            _cachedFallbackBase = fallbackBaseEmissionScale;
            ApplyFallbackBase(true);
        }

        /// 仅当当前基准来自备用基准时，才把备用基准应用到闪烁基准上
        private void ApplyFallbackBase(bool force)
        {
            if (!_usingFallbackBase)
                return;

            _baseEmissionScale = fallbackBaseEmissionScale > 0.0001f ? fallbackBaseEmissionScale : 0f;
        }

        private Material GetTargetMaterial()
        {
            var materials = Application.isPlaying
                ? targetRenderer.materials
                : targetRenderer.sharedMaterials;

            return materialIndex < materials.Length ? materials[materialIndex] : null;
        }
    }
}
