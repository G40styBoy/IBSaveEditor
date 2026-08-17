using IBSaveEditor.Manifest;
using IBSaveEditor.Models;
using IBSaveEditor.Package;

namespace IBSaveEditor.ViewModels.Fields;

/// <summary>Renders one <see cref="SectionSpec"/> as its list of bound <see cref="FieldViewModel"/>s.</summary>
public sealed class SectionViewModel
{
    public SectionViewModel(SectionSpec spec, Game game, IReadOnlyList<SaveNode> root, Action? onEdited = null)
    {
        Spec   = spec;
        Fields = spec.Fields.Select(f => new FieldViewModel(f, game, root, onEdited)).ToList();
    }

    public SectionSpec Spec { get; }
    public string Title => Spec.Title;
    public IReadOnlyList<FieldViewModel> Fields { get; }
}
