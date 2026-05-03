using System.Reflection;
using IBSaveEditor.Package;

namespace IBSaveEditor.Enums;
/// <summary>
/// Class designed to aid in dealing with Infinity Blade Enums
/// </summary>
public static class IBEnum
{
    private const string CONSUMABLE = "NumConsumable";
    private const string SHOW_CONSUMABLE = "ShowConsumableBadge";
    private const string CHEEVO = "SavedCheevo";
    private const string VOTE_COUNT = "LastVoteCount";
    private static Game game;

    public static string GetEnumEntryFromIndex(string alias, int idx)
    {
        return (game, alias) switch
        {
            (Game.IB2 or Game.IB1, CONSUMABLE) => EnumToString<eTouchRewardActor_IB2>(idx),
            (Game.IB2 or Game.IB1, CHEEVO) => EnumToString<eAchievements_IB2>(idx),
            (Game.IB3, CONSUMABLE) => EnumToString<eTouchRewardActor_IB3>(idx),
            (Game.IB3, SHOW_CONSUMABLE) => EnumToString<eTouchRewardActor_IB3>(idx),
            (Game.IB3, CHEEVO) => EnumToString<eAchievements_IB3>(idx),
            (Game.VOTE, VOTE_COUNT) => EnumToString<CharacterFilterEnum>(idx),
            _ => $"Element{idx + 1}"
        };
    }
     
    public static Type GetArrayIndexEnum(string alias)
    {
        return (game, alias) switch
        {
            (Game.IB2 or Game.IB1, CONSUMABLE) => typeof(eTouchRewardActor_IB2),
            (Game.IB2 or Game.IB1, CHEEVO) => typeof(eAchievements_IB2),
            (Game.IB3, CONSUMABLE) => typeof(eTouchRewardActor_IB3),
            (Game.IB3, SHOW_CONSUMABLE) => typeof(eTouchRewardActor_IB3),
            (Game.IB3, CHEEVO) => typeof(eAchievements_IB3),
            (Game.VOTE, VOTE_COUNT) => typeof(CharacterFilterEnum),
            _ => throw new InvalidDataException("")
        };
    }

    private static string EnumToString<T>(int idx) where T : Enum => ((T)(object)idx).ToString();

    /// <summary>
    /// Associates a string t from the generic enum passed.
    /// </summary>
    /// <returns>Index position of the enum value, or -1 if not found</returns>
    public static int GetArrayIndexFromEnum<T>(string fName) where T : Enum
    {
        if (Enum.IsDefined(typeof(T), fName))
        {
            var enumNames = Enum.GetNames(typeof(T));
            return Array.IndexOf(enumNames, fName);
        }

        throw new InvalidDataException($"{fName} not found inside of {typeof(T)}");
    }

    public static int GetArrayIndexUsingReflection(Type enumType, string value)
    {
        MethodInfo method = typeof(IBEnum).GetMethod(nameof(GetArrayIndexFromEnum))!;
        MethodInfo genericMethod = method.MakeGenericMethod(enumType);
        try
        {
            return (int?)genericMethod.Invoke(null, new[] { value }) ?? -1;
        }
        catch (Exception exception)
        {
            throw new InvalidOperationException(exception.Message);
        }
    }

    public static void SetGame(Game _game) => game = _game;
}