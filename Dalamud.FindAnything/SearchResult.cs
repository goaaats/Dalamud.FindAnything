using Dalamud.Interface.Textures;
using System;
using System.Collections.Generic;

namespace Dalamud.FindAnything;

public interface ISearchResult {
    public string CatName { get; }
    public string Name { get; }
    public ISharedImmediateTexture? Icon { get; }
    public int Score { get; }
    public object Key { get; }
    public bool CloseFinder => true;
    public void Selected();
}

public sealed class SearchResultComparer : IEqualityComparer<ISearchResult> {
    public static SearchResultComparer Instance { get; } = new();

    public bool Equals(ISearchResult? a, ISearchResult? b) {
        if (ReferenceEquals(a, b)) return true;
        return a is not null
               && b is not null
               && a.GetType() == b.GetType()
               && Equals(a.Key, b.Key);
    }

    public int GetHashCode(ISearchResult obj) => HashCode.Combine(obj.GetType(), obj.Key);
}
