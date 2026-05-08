using Aprillz.MewUI.Controls;
using System.Runtime.InteropServices;

namespace Resty.Gui.Views;

/// <summary>
/// 视图层共用工具：P/Invoke + 菜单弹出辅助。
/// </summary>
internal static class ViewHelpers
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public MOUSEINPUT mi;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MOUSEINPUT
    {
        public int dx, dy, mouseData;
        public uint dwFlags, time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_MOUSE         = 0;
    private const uint MOUSEEVENTF_RIGHTDOWN = 0x0008;
    private const uint MOUSEEVENTF_RIGHTUP   = 0x0010;

    /// <summary>
    /// 把 ContextMenu 绑到按钮，并通过模拟右键点击触发弹出。
    /// </summary>
    internal static void PopupMenu(Button btn, ContextMenu menu)
    {
        btn.ContextMenu(menu);
        try
        {
            var inputs = new INPUT[]
            {
                new() { type = INPUT_MOUSE, mi = new() { dwFlags = MOUSEEVENTF_RIGHTDOWN } },
                new() { type = INPUT_MOUSE, mi = new() { dwFlags = MOUSEEVENTF_RIGHTUP   } },
            };
            SendInput((uint)inputs.Length, inputs, Marshal.SizeOf<INPUT>());
        }
        catch { }
    }
}
