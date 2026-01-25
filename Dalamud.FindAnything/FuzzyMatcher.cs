#define BORDER_MATCHING

using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;

namespace Dalamud.FindAnything;

public readonly ref struct FuzzyMatcher {
    private static readonly (int, int)[] EmptySegArray = [];

    private readonly string needleString;
    private readonly ReadOnlySpan<char> needleSpan;
    private readonly int needleFinalPosition;
    private readonly (int start, int end)[] needleSegments;
    private readonly int needleSegmentsLength;
    private readonly MatchMode mode;

    public FuzzyMatcher(string term, MatchMode matchMode) {
        needleString = term;
        needleSpan = needleString.AsSpan();
        needleFinalPosition = needleSpan.Length - 1;
        mode = matchMode;

        switch (matchMode) {
            case MatchMode.FuzzyParts:
                needleSegments = FindNeedleSegments(needleSpan);
                needleSegmentsLength = needleSegments.Length;
                if (needleSegmentsLength < 2) {
                    // Single segment, so use Fuzzy mode
                    needleSegmentsLength = 0;
                    mode = MatchMode.Fuzzy;
                }

                break;
            case MatchMode.Fuzzy:
            case MatchMode.Simple:
                needleSegments = EmptySegArray;
                needleSegmentsLength = 0;
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(matchMode), matchMode, null);
        }
    }

    private static (int start, int end)[] FindNeedleSegments(ReadOnlySpan<char> span) {
        if (span.IndexOfAny(' ', '\u3000') == -1)
            return EmptySegArray; // No spaces found, return EmptySegArray to skip allocation and fall back to Fuzzy

        var segments = new List<(int, int)>();
        var wordStart = -1;

        for (var i = 0; i < span.Length; i++) {
            if (span[i] is not ' ' and not '\u3000') {
                if (wordStart < 0)
                    wordStart = i;
            } else if (wordStart >= 0) {
                segments.Add((wordStart, i - 1));
                wordStart = -1;
            }
        }

        if (wordStart >= 0)
            segments.Add((wordStart, span.Length - 1));

        return segments.ToArray();
    }

    public int Matches(string value) {
        if (needleFinalPosition == -1)
            return 0; // Needle is empty

        if (value.Length == 0)
            return 0; // Haystack is empty

        if (mode == MatchMode.Simple)
            return value.Contains(needleString) ? 1 : 0;

        var haystack = value.AsSpan();

        if (mode == MatchMode.Fuzzy)
            return GetRawScore(haystack, 0, needleFinalPosition);

        if (mode == MatchMode.FuzzyParts) {
            var total = 0;
            for (var i = 0; i < needleSegmentsLength; i++) {
                var (start, end) = needleSegments[i];
                var cur = GetRawScore(haystack, start, end);
                if (cur == 0)
                    return 0;

                total += cur;
            }

            return total;
        }

        throw new Exception($"Invalid match mode: {mode}");
    }

    public int MatchesAny(params string[] values) {
        var max = 0;
        for (var i = 0; i < values.Length; i++) {
            var cur = Matches(values[i]);
            if (cur > max)
                max = cur;
        }

        return max;
    }

    private int GetRawScore(ReadOnlySpan<char> haystack, int needleStart, int needleEnd) {
        var needleSize = needleEnd - needleStart + 1;
        if (needleSize > haystack.Length)
            return 0; // Needle is bigger than haystack, full match is impossible

        var (startPos, gaps, consecutive, borderMatches, endPos) = FindForward(haystack, needleStart, needleEnd);
        if (startPos < 0)
            return 0; // No forward match found, reverse match is impossible

        var score = CalculateRawScore(needleSize, startPos, gaps, consecutive, borderMatches);
        if (gaps == 0)
            return score; // Reverse score cannot beat forward score with no gaps

        (startPos, gaps, consecutive, borderMatches) = FindReverse(haystack, endPos, needleStart, needleEnd);
        var revScore = CalculateRawScore(needleSize, startPos, gaps, consecutive, borderMatches);

        return int.Max(score, revScore);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CalculateRawScore(int needleSize, int startPos, int gaps, int consecutive, int borderMatches) {
        var score = 100
                    + needleSize * 3
                    + borderMatches * 3
                    + consecutive * 5
                    - startPos
                    - gaps * 2;
        if (startPos == 0)
            score += 5;
        return score < 1 ? 1 : score;
    }

    private (int startPos, int gaps, int consecutive, int borderMatches, int haystackIndex) FindForward(
        ReadOnlySpan<char> haystack, int needleStart, int needleEnd) {
        var needle = needleSpan;
        var firstPos = haystack.IndexOf(needle[needleStart]);
        if (firstPos < 0)
            return (-1, 0, 0, 0, 0);

        var gaps = 0;
        var consecutive = 0;
        var borderMatches = 0;

#if BORDER_MATCHING
        if (firstPos > 0) {
            if (!char.IsLetterOrDigit(haystack[firstPos - 1])) {
                borderMatches++;
            }
        }
#endif

        var lastPos = firstPos;
        for (var needleIndex = needleStart + 1; needleIndex <= needleEnd; needleIndex++) {
            var relPos = haystack[(lastPos + 1)..].IndexOf(needle[needleIndex]);
            if (relPos < 0)
                return (-1, 0, 0, 0, 0);

            var pos = lastPos + 1 + relPos;
            var gap = pos - lastPos - 1;
            if (gap == 0) {
                consecutive++;
            } else {
                gaps += gap;
            }

#if BORDER_MATCHING
            if (pos > 0) {
                if (!char.IsLetterOrDigit(haystack[pos - 1])) {
                    borderMatches++;
                }
            }
#endif

            lastPos = pos;
        }

        return (firstPos, gaps, consecutive, borderMatches, lastPos);
    }

    private (int startPos, int gaps, int consecutive, int borderMatches) FindReverse(ReadOnlySpan<char> haystack,
        int haystackLastMatchIndex, int needleStart, int needleEnd) {
        var needle = needleSpan;

        var gaps = 0;
        var consecutive = 0;
        var borderMatches = 0;

#if BORDER_MATCHING
        if (haystackLastMatchIndex > 0) {
            if (!char.IsLetterOrDigit(haystack[haystackLastMatchIndex - 1])) {
                borderMatches++;
            }
        }
#endif

        var lastPos = haystackLastMatchIndex;
        for (var needleIndex = needleEnd - 1; needleIndex >= needleStart; needleIndex--) {
            var pos = haystack[..lastPos].LastIndexOf(needle[needleIndex]);
            if (pos < 0)
                return (-1, 0, 0, 0);

            var gap = lastPos - pos - 1;
            if (gap == 0) {
                consecutive++;
            } else {
                gaps += gap;
            }

#if BORDER_MATCHING
            if (pos > 0) {
                if (!char.IsLetterOrDigit(haystack[pos - 1])) {
                    borderMatches++;
                }
            }
#endif

            lastPos = pos;
        }

        return (lastPos, gaps, consecutive, borderMatches);
    }
}

public enum MatchMode {
    Simple,
    Fuzzy,
    FuzzyParts
}
