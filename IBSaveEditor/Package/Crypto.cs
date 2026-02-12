using System.Security.Cryptography;
namespace SaveDumper.UnrealPackageManager.Crypto;

class PackageCrypto
{

    // Credits to hox for the keys and the idea to initialize the keys in UTF8. Thanks!
    private readonly static byte[] IB1AES = "NoBwPWDkRqFMTaHeVCJkXmLSZoNoBIPm"u8.ToArray();
    private readonly static byte[] IB2AES = "|FK}S];v]!!cw@E4l-gMXa9yDPvRfF*B"u8.ToArray();
    private readonly static byte[] VOTEAES = "DKksEKHkldF#(WDJ#FMS7jla5f(@J12|"u8.ToArray();
    public const int SAVE_FILE_VERSION_IB3 = 5;
    public const int SAVE_FILE_VERSION_PC = 4;
    public const uint IB2_SAVE_MAGIC = 709824353u;
    public const uint IB1_SAVE_MAGIC = 3235830701u;
    public const uint NO_MAGIC = 4294967295u;
    private const int BLOCK_SIZE = 16;

    public static bool TryDecryptHalfBlock(Game game, Stream stream)
    {
        byte[] buffer = new byte[BLOCK_SIZE];
        stream.Read(buffer, 0, BLOCK_SIZE);
        using var aes = ConstructPackageAES(game);
        using var transformer = aes.CreateDecryptor();

        return IsHalfBlockUnencrypted(transformer.TransformFinalBlock(buffer, 0, BLOCK_SIZE));
    }

    /// <summary>
    /// gets the first 8 bytes of a block to see if its encrypted or not
    /// </summary>
    /// <param name="block"></param>
    /// <returns></returns>
    private static bool IsHalfBlockUnencrypted(byte[] block)
    {
        uint first = BitConverter.ToUInt32(block, 0);
        uint last = BitConverter.ToUInt32(block, block.Length - 12);

        // check here to see if our expected decrypted header values are present
        return first is NO_MAGIC or 0 || last is NO_MAGIC;
    }

    /// <summary>
    /// Checks the first 8 bytes to determine the package's encryption state
    /// </summary>
    /// <returns>Returns if the package is encrypted or not</returns>
    public static bool IsPackageEncrypted(UnrealPackage UPK) =>
         !(UPK.saveVersion == SAVE_FILE_VERSION_IB3 || UPK.saveVersion == SAVE_FILE_VERSION_PC)
         || UPK.saveMagic != NO_MAGIC;

    public static byte[] GetPackageAESKey(Game game)
    {
        return game switch
        {
            Game.IB1 => IB1AES,
            Game.IB2 => IB2AES,
            Game.VOTE => VOTEAES,
            _ => []
        };
    }

    /// <summary>
    /// Takes a package type and constructs an Aes class for crypto depending on the package.
    /// </summary>
    /// <param name="game">Package to setup the Aes class upon</param>
    /// <returns>Constructed Aes class with all info required to decrypt or encrypt the package</returns>
    public static Aes ConstructPackageAES(Game game)
    {
        Aes aes = Aes.Create();
        aes.Key = GetPackageAESKey(game);
        aes.Padding = PaddingMode.Zeros;
        aes.Mode = game == Game.IB1 ? CipherMode.CBC : CipherMode.ECB;

        // For IB1's IV we can simply use a block of empty bytes
        if (game is Game.IB1)
            aes.IV = new byte[BLOCK_SIZE];

        return aes;
    }

    public static byte[] DecryptPackage(UnrealPackage UPK)
    {
        Aes aes = ConstructPackageAES(UPK.game);
        using (ICryptoTransform decryptor = aes.CreateDecryptor())
        {
            int srcOffset = sizeof(int);

            // skip over save version and encryption magic for IB1 package
            if (UPK.game is Game.IB1)
                srcOffset *= 2;

            byte[] streamBytes = UPK.GetStreamBytes();

            // get the encrypted package's bytes and skip X amount over
            byte[] encryptedData = new byte[streamBytes.Length - srcOffset];
            Array.ConstrainedCopy(streamBytes, srcOffset, encryptedData, 0, encryptedData.Length); 
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }

    }

    /// <summary>
    /// Encrypts a stream depending on the package type, and appends the necessary header info.
    /// </summary>
    /// <param name="stream"></param>
    /// <param name="data"></param>
    /// <exception cref="InvalidDataException"></exception>
    public static void EncryptPackage(ref FileStream stream, UnrealPackage UPK)
    {
        stream.Position = 0;
        Aes aes = ConstructPackageAES(UPK.game);
        using (ICryptoTransform encryptor = aes.CreateEncryptor())
        {
            byte[] decryptedData;
            using (var memStream = new MemoryStream())
            {
                stream.CopyTo(memStream);
                decryptedData = memStream.ToArray();
            }

            byte[] encryptedData = encryptor.TransformFinalBlock(decryptedData, 0, decryptedData.Length);

            // calculate header size here to append correct amount of data
            int headerSize = 0;
            if (UPK.game is Game.IB1)
                headerSize = sizeof(uint) * 2;
            else if (UPK.game is Game.IB2 or Game.VOTE)
                headerSize = sizeof(uint);
            else
                throw new InvalidDataException("Package is encrypted but isn't supported for appending header info.");

            byte[] finalData = new byte[headerSize + encryptedData.Length];

            // copy header data into the new array first
            int offset = 4;
            if (UPK.game is Game.IB1)
            {
                Array.Copy(BitConverter.GetBytes(UPK.saveVersion), 0, finalData, 0, sizeof(uint));
                Array.Copy(BitConverter.GetBytes(UPK.saveMagic), 0, finalData, sizeof(int), sizeof(uint));
                offset *= 2;
            }
            else if (UPK.game is Game.IB2 or Game.VOTE)
                Array.Copy(BitConverter.GetBytes(IB2_SAVE_MAGIC), 0, finalData, 0, sizeof(uint));

            Array.Copy(encryptedData, 0, finalData, offset, encryptedData.Length);
            stream.Position = 0;
            stream.Write(finalData, 0, finalData.Length);
        }
    }
}