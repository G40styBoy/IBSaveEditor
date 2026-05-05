using IBSaveEditor.Package;
using IBSaveEditor.UProperties;
using IBSaveEditor.UProperties.UArray;

namespace IBSaveEditor.Services;

/// <summary>
/// Resolves array properties against the static <see cref="UArrayRegistry"/>
/// to determine the type of data each array holds and how it should be displayed
/// in the editor UI.
/// <para>
/// Translates the backend's <see cref="PropertyType"/> enum into UI-friendly
/// type hint strings. Also determines whether an array's items should be
/// "unwrapped" for display : meaning the JSON wrapper struct around each
/// primitive item should be looked through so the user sees the raw value.
/// </para>
/// </summary>
public static class ArrayTypeResolver
{
    /// <summary>
    /// Looks up an array's metadata in the registry for the given game.
    /// </summary>
    /// <returns>The metadata, or <c>null</c> if not registered.</returns>
    public static ArrayMetadata? Resolve(Game game, string arrayName)
    {
        if (UArrayRegistry.TryGet(game, arrayName, out var meta))
            return meta;
        return null;
    }

    /// <summary>
    /// Converts a backend <see cref="PropertyType"/> to the UI type hint string
    /// used for badge classes and editor selection (e.g. "int", "name", "struct").
    /// </summary>
    public static string ToTypeHint(PropertyType propertyType) => propertyType switch
    {
        PropertyType.IntProperty    => "int",
        PropertyType.FloatProperty  => "float",
        PropertyType.BoolProperty   => "bool",
        PropertyType.ByteProperty   => "byte",
        PropertyType.StrProperty    => "string",
        PropertyType.NameProperty   => "name",
        PropertyType.StructProperty => "struct",
        _                            => "string"
    };

    /// <summary>
    /// True when this array's items should be displayed with their JSON wrapper
    /// struct hidden. This applies only to STATIC arrays of non-struct types
    /// (NumConsumable, ShowConsumableBadge, etc.) where the JSON wraps each
    /// primitive in a single-key struct that has no meaning to the user.
    /// <para>
    /// When this returns true, the UI looks through the wrapper to display
    /// the inner primitive directly, but the backing data layer keeps the
    /// wrapper intact so round-tripping to JSON remains correct.
    /// </para>
    /// </summary>
    public static bool ShouldUnwrapForDisplay(ArrayMetadata meta)
        => meta.arrayType == ArrayType.Static
        && meta.valueType != PropertyType.StructProperty;
}
