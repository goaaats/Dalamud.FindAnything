﻿using Dalamud.Utility.Signatures;
using FFXIVClientStructs.FFXIV.Client.UI;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using System;

namespace Dalamud.FindAnything;

public class Interop
{
    private static Interop? instance = null;
    public static Interop Instance => instance ??= new Interop();

    private delegate byte UseMcGuffinDelegate(IntPtr module, uint id);

    [Signature("48 89 5C 24 ?? 57 48 83 EC 40 80 3D ?? ?? ?? ?? ??")]
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
