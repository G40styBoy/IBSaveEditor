using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>
/// Renders every tab in a game's manifest against an already-loaded save.
/// This is the production shape of the manifest-driven field editor - the
/// in-shell tab host is built directly on this, not a separate seam.
/// </summary>
public sealed class ManifestTabsViewModel
{
    public ManifestTabsViewModel(GameManifest manifest, IReadOnlyList<SaveNode> root)
    {
        Game = manifest.Game;
        Tabs = manifest.Tabs.Select(t => new TabViewModel(t, manifest.Game, root)).ToList();
    }

    public Game Game { get; }
    public IReadOnlyList<TabViewModel> Tabs { get; }
}
