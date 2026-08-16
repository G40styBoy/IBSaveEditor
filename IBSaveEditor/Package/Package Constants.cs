namespace IBSaveEditor.Package;

public class PackageConstants
{
    public const int SAVE_FILE_VERSION_IB3 = 5;
    public const int SAVE_FILE_VERSION_PC  = 4;

    public const uint IB1_SAVE_MAGIC = 3235830701u;
    public const uint IB2_SAVE_MAGIC = 709824353u;
    public const uint IB3_SAVE_MAGIC = 541812089u;
    public const uint NO_MAGIC = 4294967295u;
}

public enum Game
{
    IB1,
    IB2,
    IB3,
    VOTE
}

