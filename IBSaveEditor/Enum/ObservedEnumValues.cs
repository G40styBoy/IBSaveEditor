using IBSaveEditor.Package;

namespace IBSaveEditor.Enums;

/// <summary>
/// Observed-only candidate lists for EnumNode types <see cref="EnumChoiceSource"/> has
/// no authoritative C# enum for. Transcribed by hand from
/// <c>docs/schema/{game}.enum-census.json</c> - i.e. from what the local save corpus
/// happened to contain - NOT from the game's own definitions.
/// <para>
/// Unlike <c>SaveEnums.cs</c>, these are NOT guaranteed complete: a type with 51 legal
/// UnrealScript values might only have shown 8 of them across the saves this was built
/// from. A value outside this list still displays correctly (EnumChoice always shows
/// the field's real current value, inserting it into the candidate list if the census
/// didn't cover it) - it just wasn't offered as a *choice* until someone re-ran the
/// census against a bigger corpus, found the field in the game's own ini/config, or
/// promoted it to <see cref="EnumChoiceSource"/> once a matching C# enum exists.
/// </para>
/// </summary>
public static class ObservedEnumValues
{
    // From docs/schema/IB3.enum-census.json (25 saves). "None" here is a real observed
    // enum value (Unreal's own default/unset token), not a sentinel to filter out.
    private static readonly Dictionary<string, string[]> IB3 = new(StringComparer.Ordinal)
    {
        ["ArenaDifficultyLevel"] = new[] { "ADL_Easy", "None" },
        ["BossMapVariation"]     = new[] { "BMV_CustomMap", "BMV_VariationA", "BMV_VariationB", "BMV_VariationC", "BMV_VariationD", "BMV_VariationE" },
        ["ItemEnhancementType"]  = new[] { "IET_GemCooker", "IET_PotionCauldron" },
        ["QuestActionEnum"]      = new[] { "QAE_CompleteQuest", "QAE_HideoutSubLevels" },
        ["QuestEventEnum"]       = new[] { "QEE_QuestStart" },
        ["ShowQuestType"]        = new[] { "SQT_ClashMobSolo" },
        ["SwordItemState"]       = new[] { "SIS_ItemNormal" },
        ["eBossAttackClass"]     = new[]
        {
            "BAC_06ftF_Claw", "BAC_06ftF_Sword", "BAC_06ft_2H", "BAC_06ft_2W", "BAC_06ft_SnS",
            "BAC_08ft_2S", "BAC_10ft_Claw", "BAC_10ft_SnS", "BAC_12ftF_Staff", "BAC_12ft_2SB",
            "BAC_12ft_Staff", "BAC_15ft_2H", "BAC_15ft_Log", "BAC_Random",
        },
    };

    public static IReadOnlyList<string>? TryGet(Game game, string enumType) => game switch
    {
        Game.IB3 => IB3.TryGetValue(enumType, out var values) ? values : null,
        _        => null,
    };
}
