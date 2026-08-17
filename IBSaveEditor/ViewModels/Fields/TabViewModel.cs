using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>Renders one <see cref="TabSpec"/> as its bound <see cref="SectionViewModel"/>s and <see cref="CollectionViewModel"/>s.</summary>
public sealed class TabViewModel
{
    public TabViewModel(TabSpec spec, Game game, IReadOnlyList<SaveNode> root)
    {
        Spec        = spec;
        Sections    = spec.Sections.Select(s => new SectionViewModel(s, game, root)).ToList();
        Collections = spec.Collections.Select(c => new CollectionViewModel(c, game, root)).ToList();
    }

    public TabSpec Spec { get; }
    public string Title => Spec.Title;
    public IReadOnlyList<SectionViewModel> Sections { get; }
    public IReadOnlyList<CollectionViewModel> Collections { get; }
}
