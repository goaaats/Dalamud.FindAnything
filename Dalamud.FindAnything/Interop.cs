using System;
using System.Runtime.InteropServices;
using System.Text;
using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;

namespace Dalamud.FindAnything;

public class Interop
{
    private static Interop? instance = null;
    public static Interop Instance => instance ??= new Interop();

    private delegate byte UseMcGuffinDelegate(IntPtr module, uint id);

    [Signature("E8 ?? ?? ?? ?? EB 0C 48 8B 07")]
    private readonly UseMcGuffinDelegate? useMcGuffin = null!;

    public Interop()
    {
        FindAnythingPlugin.GameInteropProvider.InitializeFromAttributes(this);
    }

    public unsafe void UseMgGuffin(uint mcGuffinId)
    {
        if (useMcGuffin == null) {
            throw new InvalidOperationException("Could not find signature for using collection item");
        }

        var module = (IntPtr)UIModule.Instance()->GetAgentModule()->GetAgentByInternalId(AgentId.McGuffin);
        useMcGuffin(module, mcGuffinId);
    }
}
