using Dalamud.Game;
using Dalamud.Game.Text.Sanitizer;
using Lumina.Text.ReadOnly;
using System.Runtime.CompilerServices;

namespace Dalamud.FindAnything;

public class Normalizer
{
    private readonly ClientLanguage lang;
    private readonly Sanitizer sanitizer;
    private readonly bool normalizeKana;

    public Normalizer(ClientLanguage lang) {
        this.lang = lang;
        sanitizer = new Sanitizer(lang);
        normalizeKana = lang == ClientLanguage.Japanese;
    }

    private Normalizer(ClientLanguage lang, Sanitizer sanitizer, bool hasKana) {
        this.lang = lang;
        this.sanitizer = sanitizer;
        normalizeKana = hasKana;
    }

    public Normalizer WithKana(bool hasKana) {
        return new Normalizer(lang, sanitizer, hasKana);
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

    public string SearchableAscii(string input) {
        return input.ToLowerInvariant();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string StripPunctuation(string str) {
        return str.Replace("'", string.Empty);
    }
}