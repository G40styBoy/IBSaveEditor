using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>
/// Renders a single manifest tab against an already-loaded save's node tree.
/// <para>
/// Deliberately single-tab for now: multi-tab selection and game-driven tab
/// visibility across a whole <see cref="GameManifest"/> is Phase 6's job, once
/// there's more than one tab worth switching between. This is the seam Phase 6
/// builds on, not a preview of it.
/// </para>
/// </summary>
public sealed class TabHostViewModel
{
    public TabHostViewModel(TabSpec tab, Game game, IReadOnlyList<SaveNode> root) =>
        ActiveTab = new TabViewModel(tab, game, root);

    public TabViewModel ActiveTab { get; }
}
