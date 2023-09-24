using System;
using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using ImGuiNET;

namespace Dalamud.FindAnything;

// Most of this is stolen from QoLBar
public unsafe class Input
{
    [DllImport("user32.dll", CharSet = CharSet.Auto, ExactSpelling = true)] private static extern IntPtr GetForegroundWindow();
    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)] private static extern int GetWindowThreadProcessId(IntPtr handle, out int processId);
    private static bool IsGameFocused
    {
        get
        {
            var activatedHandle = GetForegroundWindow();
            if (activatedHandle == IntPtr.Zero)
                return false;

            _ = GetWindowThreadProcessId(activatedHandle, out var activeProcId);

            return activeProcId == Environment.ProcessId;
        }
    }

    private static bool IsGameTextInputActive =>
        Framework.Instance()->GetUiModule()->GetRaptureAtkModule()->AtkModule.IsTextInputActive();

    public static bool Disabled => IsGameTextInputActive || !IsGameFocused || ImGui.GetIO().WantCaptureKeyboard;

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool GetKeyboardState(byte[] lpKeyState);
    private static readonly byte[] keyboardState = new byte[256];

    public void Update()
    {
        GetKeyboardState(keyboardState);
    }

    public bool IsDown(VirtualKey key) => (keyboardState[(int)key] & 0x80) != 0;
}