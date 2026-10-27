using Dalamud.Interface.Textures;

namespace Dalamud.FindAnything;

public static class Utils {
    public static ISharedImmediateTexture GetIcon(int iconId) {
        return GetIcon(iconId < 1 ? 0u : (uint)iconId);
    }

    public static ISharedImmediateTexture GetIcon(uint iconId) {
        try {
            return Service.TextureProvider.GetFromGameIcon(new GameIconLookup {
                HiRes = false,
                IconId = iconId,
            });
        } catch {
            return Service.TextureProvider.GetFromGameIcon(default);
        }
    }
}
