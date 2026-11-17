// VicInputCompat v1.0 —— 新旧输入系统兼容层。
//
// 背景：
//   Unity 2019.3 起提供两套输入系统：
//     - 旧输入：UnityEngine.Input（由 Player Settings 的 Active Input Handling = "Input Manager (Old) / Both" 启用）
//     - 新输入：com.unity.inputsystem（Active Input Handling = "Input System Package (New) / Both"）
//   当项目 Active Input Handling = "Input System Package (New)" 时，直接调用
//   UnityEngine.Input.* 会抛：
//     InvalidOperationException: You are trying to read Input using the UnityEngine.Input class,
//     but you have switched active Input handling to Input System package in Player Settings.
//   Editor 侧若在 EditorApplication.update 这类每帧回调里调用，会每帧刷屏报错。
//
// 设计：
//   1) 优先使用旧输入（行为与原来完全一致，且"Both"模式下也只有旧输入才有 Input Manager 轴映射）。
//   2) 旧输入抛 InvalidOperationException → 说明项目是纯新输入模式 → 永久降级，改用新输入系统。
//      通过反射访问新输入系统，避免本包对 com.unity.inputsystem 的硬依赖（未装该包的项目不受影响）。
//   3) 反射信息全部缓存（Type / PropertyInfo / MethodInfo），运行期每帧只做属性读取，不做类型查找。
//   4) 旧输入抛 ArgumentException（Input Manager 未配置该轴）时，不降级，只当本次无数据。
//
// 对外 API：
//   - GetKeyDown(KeyCode)：按键"本帧按下"，兼容新旧输入
//   - GetVerticalAxis()：垂直轴输入（等价旧 Input.GetAxis("Vertical")），兼容新旧输入
//   - LegacyInputUsable / NewInputSystemAvailable：当前可用的输入后端（供排查用）
//
// 使用方：
//   - Editor/VicTools/RoadScrollEditorHelper.cs（ESC 快捷键，EditorApplication.update 每帧调用）
//   - Runtime/Scripts/RoadScroll.cs（手柄/键盘垂直轴调速，Update 每帧调用）

using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;

namespace Vic.Runtime
{
    /// 输入系统兼容层：屏蔽新旧输入系统的差异，避免在纯新输入（Input System Package）项目中
    /// 调用 <c>UnityEngine.Input.*</c> 抛 InvalidOperationException。
    public static class VicInputCompat
    {
        // ====================================================================
        // 状态
        // ====================================================================

        // 旧输入（UnityEngine.Input）是否可用。
        // 纯新输入模式下第一次调用会抛 InvalidOperationException，捕获后永久置 false，
        // 避免每帧重复抛异常（异常构造 + 堆栈回溯开销很大，且会在 Console 刷屏）。
        private static bool s_legacyInputUsable = true;

        // 新输入系统反射缓存（只需解析一次）
        private static bool s_newInputResolved;
        private static bool s_newInputAvailable;
        private static Type s_keyboardType;
        private static Type s_gamepadType;
        private static PropertyInfo s_keyboardCurrentProp;
        private static PropertyInfo s_gamepadCurrentProp;

        // 键盘控件属性缓存：控件名 → PropertyInfo（避免每帧反射查找）
        private static readonly Dictionary<string, PropertyInfo> s_keyboardControlProps =
            new Dictionary<string, PropertyInfo>(32);

        // 控件"按下"状态属性缓存：控件类型名 → PropertyInfo
        private static readonly Dictionary<string, PropertyInfo> s_buttonStateProps =
            new Dictionary<string, PropertyInfo>(8);

        // Vector2 读取方法缓存（手柄摇杆）
        private static MethodInfo s_readValueMethod;

        // ====================================================================
        // 对外 API
        // ====================================================================

        /// 旧输入系统（UnityEngine.Input）当前是否可用。
        /// 纯新输入项目下为 false。
        public static bool LegacyInputUsable => s_legacyInputUsable;

        /// 新输入系统（com.unity.inputsystem）是否已安装且可访问。
        public static bool NewInputSystemAvailable
        {
            get
            {
                EnsureNewInputResolved();
                return s_newInputAvailable;
            }
        }

        /// 读取按键的"本帧按下"状态，兼容新旧两套输入系统。
        /// <param name="key">按键</param>
        /// <returns>true：本帧按下</returns>
        public static bool GetKeyDown(KeyCode key)
        {
            // 1) 旧输入优先（"Input Manager (Old)" / "Both" 模式）
            if (TryLegacyKeyDown(key, out bool pressed))
                return pressed;

            // 2) 降级到新输入系统（纯 "Input System Package (New)" 模式）
            return TryNewInputKeyDown(key);
        }

        /// 读取垂直轴输入，等价旧输入的 <c>Input.GetAxis("Vertical")</c>，兼容新旧两套输入系统。
        /// <returns>[-1, 1] 区间的轴值；无可用输入时返回 0</returns>
        public static float GetVerticalAxis()
        {
            // 1) 旧输入优先（保留 Input Manager 中用户自定义的轴映射）
            if (TryLegacyAxis("Vertical", out float axis))
                return axis;

            // 2) 降级到新输入系统：手柄左摇杆 Y → 键盘 W/S、↑/↓
            return GetNewInputVerticalAxis();
        }

        // ====================================================================
        // 旧输入（UnityEngine.Input）
        // ====================================================================

        private static bool TryLegacyKeyDown(KeyCode key, out bool pressed)
        {
            pressed = false;
            if (!s_legacyInputUsable) return false;

            try
            {
                pressed = Input.GetKeyDown(key);
                return true;
            }
            catch (InvalidOperationException)
            {
                // 项目 Active Input Handling = "Input System Package (New)"，旧输入彻底不可用
                s_legacyInputUsable = false;
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VicTools] 读取旧输入按键状态失败：{e.Message}");
                return false;
            }
        }

        private static bool TryLegacyAxis(string axisName, out float axis)
        {
            axis = 0f;
            if (!s_legacyInputUsable) return false;

            try
            {
                axis = Input.GetAxis(axisName);
                return true;
            }
            catch (InvalidOperationException)
            {
                // 纯新输入模式：永久降级
                s_legacyInputUsable = false;
                return false;
            }
            catch (ArgumentException)
            {
                // Input Manager 未配置该轴：旧输入本身仍可用，只是本次没有数据
                return false;
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[VicTools] 读取旧输入轴 '{axisName}' 失败：{e.Message}");
                return false;
            }
        }

        // ====================================================================
        // 新输入系统（com.unity.inputsystem，反射访问，无硬依赖）
        // ====================================================================

        private static void EnsureNewInputResolved()
        {
            if (s_newInputResolved) return;
            s_newInputResolved = true;

            s_keyboardType = FindInputSystemType("UnityEngine.InputSystem.Keyboard");
            s_gamepadType = FindInputSystemType("UnityEngine.InputSystem.Gamepad");
            if (s_keyboardType == null && s_gamepadType == null) return;

            s_newInputAvailable = true;

            if (s_keyboardType != null)
            {
                s_keyboardCurrentProp = s_keyboardType.GetProperty(
                    "current", BindingFlags.Public | BindingFlags.Static);
            }

            if (s_gamepadType != null)
            {
                s_gamepadCurrentProp = s_gamepadType.GetProperty(
                    "current", BindingFlags.Public | BindingFlags.Static);
            }
        }

        private static Type FindInputSystemType(string fullName)
        {
            // Input System 包名 com.unity.inputsystem → 程序集名 Unity.InputSystem
            Type type = Type.GetType(fullName + ", Unity.InputSystem");
            if (type != null) return type;

            // 兜底：遍历已加载程序集（不同 Unity 版本 / 程序集拆分方式差异）
            try
            {
                Assembly[] assemblies = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < assemblies.Length; i++)
                {
                    type = assemblies[i].GetType(fullName);
                    if (type != null) return type;
                }
            }
            catch (Exception)
            {
                // 反射失败视为不支持新输入系统
            }

            return null;
        }

        private static bool TryNewInputKeyDown(KeyCode key)
        {
            EnsureNewInputResolved();
            if (!s_newInputAvailable || s_keyboardCurrentProp == null) return false;

            object keyboard = GetKeyboardDevice();
            if (keyboard == null) return false;

            string controlName = MapKeyCodeToControlName(key);
            if (controlName == null) return false;

            object control = GetKeyboardControl(keyboard, controlName);
            if (control == null) return false;

            object pressed = GetControlState(control, "wasPressedThisFrame");
            return pressed is bool b && b;
        }

        private static float GetNewInputVerticalAxis()
        {
            EnsureNewInputResolved();
            if (!s_newInputAvailable) return 0f;

            // 1) 手柄左摇杆 Y 优先
            float stickY = GetGamepadLeftStickY();
            if (Mathf.Abs(stickY) > 0.01f)
                return Mathf.Clamp(stickY, -1f, 1f);

            // 2) 键盘 W/S、↑/↓
            object keyboard = GetKeyboardDevice();
            if (keyboard == null) return 0f;

            bool up = IsKeyPressed(keyboard, "wKey") || IsKeyPressed(keyboard, "upArrowKey");
            bool down = IsKeyPressed(keyboard, "sKey") || IsKeyPressed(keyboard, "downArrowKey");
            return (up ? 1f : 0f) - (down ? 1f : 0f);
        }

        private static object GetKeyboardDevice()
        {
            if (s_keyboardCurrentProp == null) return null;
            try
            {
                // 无键盘设备时 current 为 null
                return s_keyboardCurrentProp.GetValue(null);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static object GetKeyboardControl(object keyboard, string controlName)
        {
            if (!s_keyboardControlProps.TryGetValue(controlName, out PropertyInfo prop))
            {
                prop = s_keyboardType.GetProperty(
                    controlName, BindingFlags.Public | BindingFlags.Instance);
                s_keyboardControlProps[controlName] = prop;
            }

            if (prop == null) return null;

            try
            {
                return prop.GetValue(keyboard);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsKeyPressed(object keyboard, string controlName)
        {
            object control = GetKeyboardControl(keyboard, controlName);
            if (control == null) return false;

            object pressed = GetControlState(control, "isPressed");
            return pressed is bool b && b;
        }

        private static object GetControlState(object control, string statePropertyName)
        {
            // 缓存键必须带上属性名：同一控件类型上会取不同属性
            // （KeyControl 的 "isPressed" 与 "wasPressedThisFrame"），只按类型名缓存会串键
            string cacheKey = control.GetType().Name + "." + statePropertyName;
            if (!s_buttonStateProps.TryGetValue(cacheKey, out PropertyInfo prop))
            {
                prop = control.GetType().GetProperty(
                    statePropertyName, BindingFlags.Public | BindingFlags.Instance);
                s_buttonStateProps[cacheKey] = prop;
            }

            if (prop == null) return null;

            try
            {
                return prop.GetValue(control);
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static float GetGamepadLeftStickY()
        {
            if (s_gamepadCurrentProp == null || s_gamepadType == null) return 0f;

            object gamepad;
            try
            {
                gamepad = s_gamepadCurrentProp.GetValue(null);
            }
            catch (Exception)
            {
                return 0f;
            }
            if (gamepad == null) return 0f;

            PropertyInfo stickProp = s_gamepadType.GetProperty(
                "leftStick", BindingFlags.Public | BindingFlags.Instance);
            if (stickProp == null) return 0f;

            object stick;
            try
            {
                stick = stickProp.GetValue(gamepad);
            }
            catch (Exception)
            {
                return 0f;
            }
            if (stick == null) return 0f;

            // 优先取 Vector2Control.y，其次回退 ReadValue()
            object y = GetControlState(stick, "y");
            if (y is float fy) return fy;

            try
            {
                if (s_readValueMethod == null ||
                    s_readValueMethod.DeclaringType == null ||
                    !s_readValueMethod.DeclaringType.IsInstanceOfType(stick))
                {
                    s_readValueMethod = stick.GetType().GetMethod(
                        "ReadValue", BindingFlags.Public | BindingFlags.Instance,
                        null, Type.EmptyTypes, null);
                }

                if (s_readValueMethod == null) return 0f;

                object value = s_readValueMethod.Invoke(stick, null);
                return value is Vector2 vec ? vec.y : 0f;
            }
            catch (Exception)
            {
                return 0f;
            }
        }

        /// KeyCode → 新输入系统 Keyboard 控件属性名映射。
        /// 未覆盖的按键返回 null（表示不支持）。
        private static string MapKeyCodeToControlName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.Escape: return "escapeKey";
                case KeyCode.Return:
                case KeyCode.KeypadEnter: return "enterKey";
                case KeyCode.Space: return "spaceKey";
                case KeyCode.Tab: return "tabKey";
                case KeyCode.Backspace: return "backspaceKey";
                case KeyCode.Delete: return "deleteKey";
                case KeyCode.UpArrow: return "upArrowKey";
                case KeyCode.DownArrow: return "downArrowKey";
                case KeyCode.LeftArrow: return "leftArrowKey";
                case KeyCode.RightArrow: return "rightArrowKey";
                case KeyCode.LeftShift: return "leftShiftKey";
                case KeyCode.RightShift: return "rightShiftKey";
                case KeyCode.LeftControl: return "leftCtrlKey";
                case KeyCode.RightControl: return "rightCtrlKey";
                case KeyCode.LeftAlt: return "leftAltKey";
                case KeyCode.RightAlt: return "rightAltKey";
                case KeyCode.LeftCommand: return "leftMetaKey";
                case KeyCode.RightCommand: return "rightMetaKey";
            }

            // A~Z（KeyCode 枚举值连续：'A'=97 起）
            if (key >= KeyCode.A && key <= KeyCode.Z)
            {
                char c = (char)('A' + (key - KeyCode.A));
                return char.ToLowerInvariant(c) + "Key";
            }

            // 数字 0~9（KeyCode.Alpha0='0' 起连续）
            if (key >= KeyCode.Alpha0 && key <= KeyCode.Alpha9)
                return "digit" + (key - KeyCode.Alpha0) + "Key";

            // F1~F12（连续）
            if (key >= KeyCode.F1 && key <= KeyCode.F12)
                return "f" + (key - KeyCode.F1 + 1) + "Key";

            return null;
        }
    }
}
