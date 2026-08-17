using IBSaveEditor.Package;

namespace IBSaveEditor.Enums;

/// <summary>
/// Resolves the authoritative C# enum type (if any) for an <c>EnumNode</c>'s raw
/// <c>EnumType</c> name, per game where the mapping is game-specific.
/// <para>
/// Distinct from <see cref="IBEnum"/>, which maps ARRAY ALIASES ("NumConsumable") to
/// the enum that provides their index keys - a save-array-indexing concern with its
/// own dispatch table. This maps ENUM TYPE NAMES as embedded directly in EnumNode
/// data ("ePlayerCharacterType", "eTouchRewardActor") for the unrelated EnumChoice
/// field control ; the two never share a lookup.
/// </para>
/// <para>
/// Populated from <c>docs/schema/{game}.enum-census.json</c> cross-referenced against
/// <c>Enum/SaveEnums.cs</c> - only types confirmed to actually appear in EnumNodes
/// AND have a matching hand-authored enum are listed. Types with no entry here have
/// no authoritative source in this codebase yet ; EnumChoice falls back to
/// <see cref="ObservedEnumValues"/> for those.
/// </para>
/// </summary>
public static class EnumChoiceSource
{
    public static Type? TryGetAuthoritativeType(Game game, string enumType) => (game, enumType) switch
    {
        (_, "ePlayerCharacterType") => typeof(ePlayerCharacterType),
        (_, "eNewWorldType")        => typeof(eNewWorldType),

        (Game.IB3,             "eTouchRewardActor") => typeof(eTouchRewardActor_IB3),
        (Game.IB1 or Game.IB2, "eTouchRewardActor") => typeof(eTouchRewardActor_IB2),

        _ => null,
    };
}
