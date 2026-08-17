using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>
/// Backs the toolbar's "Preview Tabs" window: renders every tab in a game's
/// manifest against an already-loaded save, with real tab switching via
/// Avalonia's own <c>TabControl</c>.
/// <para>
/// Deliberately separate from <see cref="TabHostViewModel"/>, which is
/// single-tab by design - the seam Phase 6 builds the real in-shell tab host
/// on. This is a throwaway preview surface for looking at manifest-driven
/// fields against a real save before that shell exists, not a preview of
/// Phase 6 itself.
/// </para>
/// </summary>
public sealed class ManifestPreviewViewModel
{
    public ManifestPreviewViewModel(GameManifest manifest, IReadOnlyList<SaveNode> root)
    {
        Game = manifest.Game;
        Tabs = manifest.Tabs.Select(t => new TabViewModel(t, manifest.Game, root)).ToList();
    }

    public Game Game { get; }
    public IReadOnlyList<TabViewModel> Tabs { get; }
}
