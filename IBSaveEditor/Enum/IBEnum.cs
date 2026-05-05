using System.Reflection;
using IBSaveEditor.Package;

namespace IBSaveEditor.Enums;

/// <summary>
/// Provides enum-based index lookups for Infinity Blade static arrays.
/// <para>
/// Several static arrays in the save format (NumConsumable, ShowConsumableBadge,
/// SavedCheevo, LastVoteCount) use game-specific enums as their index keys rather
/// than sequential integers. This class maps between those enum values and their
/// integer positions so the serializer and cruncher can reconstruct the correct
/// array indices when reading or writing save JSON.
/// </para>
/// <para>
/// The current game must be set via <see cref="SetGame"/> before any lookups are
/// performed. Both <see cref="JsonDataParser"/> and <see cref="JsonDataCruncher"/>
/// call this on construction.
/// </para>
/// </summary>
public static class IBEnum
{
    // These match the property names used in the save JSON exactly.
    private const string CONSUMABLE      = "NumConsumable";
    private const string SHOW_CONSUMABLE = "ShowConsumableBadge";
    private const string CHEEVO          = "SavedCheevo";
    private const string VOTE_COUNT      = "LastVoteCount";


    /// <summary>
    /// The current game context. All index lookups are dispatched against this.
    /// Must be set before any lookup methods are called.
    /// </summary>
    private static Game _game;

    /// <summary>Sets the game context for all subsequent enum lookups.</summary>
    public static void SetGame(Game game) => _game = game;

    /// <summary>
    /// Returns the string name of the enum entry at the given index for the
    /// specified array alias and current game.
    /// <para>
    /// Used during JSON writing to produce human-readable keys for static array
    /// entries instead of raw integer indices.
    /// </para>
    /// Falls back to <c>"Element{idx + 1}"</c> for any alias/game combination
    /// that has no registered enum type.
    /// </summary>
    /// <param name="alias">The save property name (e.g. "NumConsumable").</param>
    /// <param name="idx">The zero-based array index.</param>
    public static string GetEnumEntryFromIndex(string alias, int idx)
    {
        return (_game, alias) switch
        {
            (Game.IB1 or Game.IB2, CONSUMABLE)      => EnumToString<eTouchRewardActor_IB2>(idx),
            (Game.IB1 or Game.IB2, CHEEVO)           => EnumToString<eAchievements_IB2>(idx),
            (Game.IB3,             CONSUMABLE)       => EnumToString<eTouchRewardActor_IB3>(idx),
            (Game.IB3,             SHOW_CONSUMABLE)  => EnumToString<eTouchRewardActor_IB3>(idx),
            (Game.IB3,             CHEEVO)           => EnumToString<eAchievements_IB3>(idx),
            (Game.VOTE,            VOTE_COUNT)       => EnumToString<CharacterFilterEnum>(idx),
            _                                         => $"Element{idx + 1}"
        };
    }

    // Alias → enum type ─────────────────────────────────────────────────────

    /// <summary>
    /// Returns the <see cref="Type"/> of the enum that provides index keys for the
    /// given array alias in the current game.
    /// <para>
    /// Used during JSON crunching when a static array's entries must be mapped back
    /// to integer indices via reflection for binary serialization.
    /// </para>
    /// </summary>
    /// <param name="alias">The save property name (e.g. "NumConsumable").</param>
    /// <exception cref="InvalidDataException">
    /// Thrown when no enum is registered for the given alias and game combination.
    /// </exception>
    public static Type GetArrayIndexEnum(string alias)
    {
        return (_game, alias) switch
        {
            (Game.IB1 or Game.IB2, CONSUMABLE)      => typeof(eTouchRewardActor_IB2),
            (Game.IB1 or Game.IB2, CHEEVO)           => typeof(eAchievements_IB2),
            (Game.IB3,             CONSUMABLE)       => typeof(eTouchRewardActor_IB3),
            (Game.IB3,             SHOW_CONSUMABLE)  => typeof(eTouchRewardActor_IB3),
            (Game.IB3,             CHEEVO)           => typeof(eAchievements_IB3),
            (Game.VOTE,            VOTE_COUNT)       => typeof(CharacterFilterEnum),
            _ => throw new InvalidDataException(
                     $"No array index enum is registered for '{alias}' in game {_game}.")
        };
    }

    // String → index ────────────────────────────────────────────────────────

    /// <summary>
    /// Returns the zero-based index of a named enum value within the given enum type.
    /// <para>
    /// Used to map a JSON key (e.g. "TRA_GrabBag_Small") back to the integer array
    /// index the binary serializer needs to write.
    /// </para>
    /// </summary>
    /// <typeparam name="T">The enum type to search.</typeparam>
    /// <param name="fName">The enum value name to look up.</param>
    /// <returns>The zero-based index of the value in the enum's name list.</returns>
    /// <exception cref="InvalidDataException">Thrown if the value is not defined in the enum.</exception>
    public static int GetArrayIndexFromEnum<T>(string fName) where T : Enum
    {
        if (!Enum.IsDefined(typeof(T), fName))
            throw new InvalidDataException($"'{fName}' is not defined in {typeof(T).Name}.");

        return Array.IndexOf(Enum.GetNames(typeof(T)), fName);
    }

    /// <summary>
    /// Invokes <see cref="GetArrayIndexFromEnum{T}"/> via reflection when the enum
    /// type is only known at runtime (e.g. when dispatching from a static array builder).
    /// </summary>
    /// <param name="enumType">The runtime <see cref="Type"/> of the target enum.</param>
    /// <param name="value">The enum value name to look up.</param>
    /// <returns>The zero-based index of the value, or -1 if the invocation returns null.</returns>
    /// <exception cref="InvalidOperationException">Thrown if the reflection call fails.</exception>
    public static int GetArrayIndexUsingReflection(Type enumType, string value)
    {
        // MakeGenericMethod lets us call GetArrayIndexFromEnum<T> without knowing T at compile time.
        var method = typeof(IBEnum)
            .GetMethod(nameof(GetArrayIndexFromEnum))!
            .MakeGenericMethod(enumType);

        try
        {
            return (int?)method.Invoke(null, [value]) ?? -1;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    // ── Private helpers ───────────────────────────────────────────────────────

    /// <summary>
    /// Converts an integer index to the string name of the corresponding enum value.
    /// The cast to <typeparamref name="T"/> works because all C# enums have an
    /// underlying integer type that supports this unboxing pattern.
    /// </summary>
    private static string EnumToString<T>(int idx) where T : Enum
        => ((T)(object)idx).ToString();
}