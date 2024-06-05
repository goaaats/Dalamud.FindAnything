using System.Runtime.InteropServices;
using Dalamud.Game.ClientState.Keys;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using ImGuiNET;

namespace Dalamud.FindAnything;

// Most of this is stolen from QoLBar
public unsafe class Input
{
    private static bool IsGameFocused => !Framework.Instance()->WindowInactive;
    private static bool IsGameTextInputActive => RaptureAtkModule.Instance()->AtkModule.IsTextInputActive();

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