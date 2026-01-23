using Dalamud.Game;
using Dalamud.Game.Text.Sanitizer;
using Lumina.Text.ReadOnly;
using System.Runtime.CompilerServices;

namespace Dalamud.FindAnything;

public class Normalizer {
    private readonly Sanitizer sanitizer;
    private readonly bool normalizeKana;

    public Normalizer() {
        var lang = Service.ClientState.ClientLanguage;
        sanitizer = new Sanitizer(lang);
        normalizeKana = lang == ClientLanguage.Japanese;
    }

    private Normalizer(Sanitizer sanitizer, bool hasKana) {
        this.sanitizer = sanitizer;
        normalizeKana = hasKana;
    }

    public Normalizer WithKana(bool hasKana) {
        return new Normalizer(sanitizer, hasKana);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Searchable(ReadOnlySeString input) {
        return Searchable(input.ToText());
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string Searchable(string input) {
        return sanitizer.Sanitize(StripPunctuation(input).Downcase(normalizeKana));
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string SearchableAscii(ReadOnlySeString input) {
        return input.ToText().ToLowerInvariant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string SearchableAscii(string input) {
        return input.ToLowerInvariant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string StripPunctuation(string str) {
        return str.Replace("'", string.Empty);
    }
}
