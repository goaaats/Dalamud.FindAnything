using System;

namespace Dalamud.FindAnything;

public static class StringExtensions
{
    extension(string source)
    {
        public string Downcase(bool normalizeKana = false)
        {
            if (normalizeKana)
            {
                var span = source.AsSpan();
                for (var t = 0; t < span.Length; t++)
                {
                    if (span[t] is >= '\u3041' and <= '\u3096') // hiragana range
                    {
                        var buffer = new char[span.Length];
                        for (var i = 0; i < span.Length; i++)
                        {
                            var c = span[i];
                            if (c is >= '\u3041' and <= '\u3096')
                            {
                                buffer[i] = (char)(c + 0x60); // convert to katakana
                            }
                            else
                            {
                                buffer[i] = char.ToLowerInvariant(c);
                            }
                        }

                        return new string(buffer);
                    }
                }
            }

            return source.ToLowerInvariant();
        }

        public bool ContainsKana()
        {
            var span = source.AsSpan();
            for (var t = 0; t < span.Length; t++)
            {
                if (span[t] is >= '\u3041' and <= '\u3096' or >= '\u30a1' and <= '\u30f6')
                {
                    return true;
                }
            }

            return false;
        }
    }
}
