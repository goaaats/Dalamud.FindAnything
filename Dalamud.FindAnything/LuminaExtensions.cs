using Lumina.Text.Payloads;
using Lumina.Text.ReadOnly;
using System;
using System.Runtime.CompilerServices;
using System.Text;
using System.Diagnostics.CodeAnalysis;

namespace Dalamud.FindAnything;

public static class LuminaExtensions
{
    /// <summary>
    /// Modified version of Lumina's ExtractText which renders a ReadOnlySeString in a way that's suited to display and
    /// text matching, by avoiding unsearchable characters and characters that don't render correctly.
    /// </summary>
    /// <param name="str">The source ReadOnlySeString</param>
    /// <returns>A plain string</returns>
    [MethodImpl(MethodImplOptions.AggressiveOptimization)]
    [SuppressMessage("ReSharper", "SwitchStatementMissingSomeEnumCasesNoDefault")] // Just skip them
    public static string ToText(this ReadOnlySeString str)
    {
        var span = str.AsSpan();
        var len = 0;
        foreach (var v in span) {
            switch (v.Type) {
                case ReadOnlySePayloadType.Text:
                    len += Encoding.UTF8.GetCharCount(v.Body);
                    break;
                case ReadOnlySePayloadType.Macro:
                {
                    // Skip rendering NonBreakingSpace and SoftHyphen entirely
                    switch (v.MacroCode) {
                        case MacroCode.NewLine:
                        case MacroCode.Hyphen:
                            len += 1;
                            break;
                    }
                    break;
                }
            }
        }

        var buf = new char[len];
        var bufspan = buf.AsSpan();
        foreach (var v in span) {
            switch (v.Type) {
                case ReadOnlySePayloadType.Text:
                    bufspan = bufspan[Encoding.UTF8.GetChars(v.Body, bufspan) ..];
                    break;
                case ReadOnlySePayloadType.Macro:
                {
                    switch (v.MacroCode) {
                        case MacroCode.NewLine:
                            bufspan[0] = ' ';
                            bufspan = bufspan[1..];
                            break;

                        case MacroCode.Hyphen:
                            bufspan[0] = '-';
                            bufspan = bufspan[1..];
                            break;
                    }

                    break;
                }
            }
        }

        return new string(buf);
    }
}