internal static class PackageConstants
{
    // Credits to hox for the keys and the idea to initialize the keys in UTF8. Thanks!
    public readonly static byte[] IB1AES = "NoBwPWDkRqFMTaHeVCJkXmLSZoNoBIPm"u8.ToArray();
    public readonly static byte[] IB2AES = "|FK}S];v]!!cw@E4l-gMXa9yDPvRfF*B"u8.ToArray();
    public readonly static byte[] VOTEAES = "DKksEKHkldF#(WDJ#FMS7jla5f(@J12|"u8.ToArray();

    public const int SAVE_FILE_VERSION_IB3 = 5;
    public const int SAVE_FILE_VERSION_PC  = 4;

    public const uint IB2_SAVE_MAGIC = 709824353u;
    public const uint IB1_SAVE_MAGIC = 3235830701u;
    public const uint NO_MAGIC = 4294967295u;
}

