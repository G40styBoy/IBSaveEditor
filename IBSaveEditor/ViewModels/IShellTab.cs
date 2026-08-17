namespace IBSaveEditor.ViewModels;

/// <summary>
/// Common surface for anything the main shell's TabControl can host as a tab: a
/// manifest-driven <c>Fields.TabViewModel</c> or the raw-tree <see cref="AdvancedTreeViewModel"/>
/// escape hatch. Exists so the shell's tab header template can bind <c>Title</c> against
/// a single compiled type instead of two unrelated ones.
/// </summary>
public interface IShellTab
{
    string Title { get; }
}
