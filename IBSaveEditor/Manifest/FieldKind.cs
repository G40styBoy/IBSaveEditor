namespace IBSaveEditor.Manifest;

/// <summary>
/// The editing widget a <see cref="FieldSpec"/> renders as. Drives control
/// selection in the tab host (Phase 3+) independently of the underlying
/// save-data type: e.g. <see cref="Money"/> and <see cref="StatPoint"/> both
/// edit an int, but read differently to a player picking a field.
/// </summary>
public enum FieldKind
{
    Number,
    Money,
    StatPoint,
    Toggle,
    Text,
    ItemRef,
    GemRef,
    EnumChoice,
    Counter,
}
