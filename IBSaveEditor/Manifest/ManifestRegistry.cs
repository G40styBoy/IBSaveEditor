using IBSaveEditor.Package;

namespace IBSaveEditor.Manifest;

/// <summary>
/// Looks up the <see cref="GameManifest"/> for a given <see cref="Game"/>.
/// <para>
/// All four games now have a real manifest (built up incrementally: IB3 first as the
/// Phase 3 vertical slice, then IB1/IB2/VOTE), each corpus-verified via
/// <c>ManifestCorpusHonestyTests</c>. <see cref="EmptyManifest"/> is a defensive
/// fallback for any future <see cref="Game"/> value that gets added before it has one -
/// not currently reachable, but cheaper to keep than to remove and re-add later.
/// </para>
/// </summary>
public static class ManifestRegistry
{
    private static readonly IReadOnlyDictionary<Game, GameManifest> Manifests = BuildRegistry();

    private static Dictionary<Game, GameManifest> BuildRegistry()
    {
        var manifests = Enum.GetValues<Game>().ToDictionary(g => g, EmptyManifest);
        manifests[Game.IB1]  = IB1Manifest.Instance;
        manifests[Game.IB2]  = IB2Manifest.Instance;
        manifests[Game.IB3]  = IB3Manifest.Instance;
        manifests[Game.VOTE] = VOTEManifest.Instance;
        return manifests;
    }

    public static GameManifest Get(Game game) =>
        Manifests.TryGetValue(game, out var manifest)
            ? manifest
            : throw new KeyNotFoundException($"No manifest registered for game '{game}'.");

    public static IEnumerable<GameManifest> All => Manifests.Values;

    private static GameManifest EmptyManifest(Game game) =>
        new() { Game = game, Tabs = Array.Empty<TabSpec>() };
}
