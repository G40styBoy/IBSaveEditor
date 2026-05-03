using System.Security.Cryptography;
namespace IBSaveEditor.Package;

class PackageCrypto
{
    // Credits to hox for the keys and the idea to initialize the keys in UTF8. Thanks!
    private readonly static byte[] IB1AES = "NoBwPWDkRqFMTaHeVCJkXmLSZoNoBIPm"u8.ToArray();
    private readonly static byte[] IB2AES = "|FK}S];v]!!cw@E4l-gMXa9yDPvRfF*B"u8.ToArray();
    private readonly static byte[] IB3AES = "6nHmjd:hbWNf=9|UO2:?;K0y+gZL-jP5"u8.ToArray();
    private readonly static byte[] VOTEAES = "DKksEKHkldF#(WDJ#FMS7jla5f(@J12|"u8.ToArray();

    private const int BLOCK_SIZE = 16;

    public static bool TryDecryptHalfBlock(Game game, Stream stream)
    {
        byte[] buffer = new byte[BLOCK_SIZE];
        stream.ReadExactly(buffer, 0, BLOCK_SIZE);
        using var aes = ConstructPackageAES(game);
        using var transformer = aes.CreateDecryptor();

        return IsHalfBlockUnencrypted(transformer.TransformFinalBlock(buffer, 0, BLOCK_SIZE));
    }

    /// <summary>
    /// gets the first 8 bytes of a block to see if its encrypted or not
    /// </summary>
    /// <param name="block">block of bytes to check encryption</param>
    /// <returns>if block is encrypted</returns>
    private static bool IsHalfBlockUnencrypted(byte[] block)
        => BitConverter.ToUInt32(block, block.Length - 12) is PackageConstants.NO_MAGIC;

    public static byte[] GetPackageAESKey(Game game)
    {
        return game switch
        {
            Game.IB1 => IB1AES,
            Game.IB2 => IB2AES,
            Game.IB3 => IB3AES,
            Game.VOTE => VOTEAES,
            _ => throw new NotImplementedException($"AES Key for {game} not supported!")
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

    public static byte[] DecryptPackage(UnrealPackage upk)
    {
        Aes aes = ConstructPackageAES(upk.info.game);
        using (ICryptoTransform decryptor = aes.CreateDecryptor())
        {
            int srcOffset = sizeof(int);

            // skip over save version and encryption magic for IB1 package
            if (upk.info.game is Game.IB1)
                srcOffset *= 2;

            byte[] streamBytes = upk.GetStreamBytes();

            // get the encrypted package's bytes and skip X amount over
            byte[] encryptedData = new byte[streamBytes.Length - srcOffset];
            Array.ConstrainedCopy(streamBytes, srcOffset, encryptedData, 0, encryptedData.Length); 
            return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
        }

    }

    /// <summary>
    /// Encrypts a stream depending on the package type, and appends the necessary header info.
    /// </summary>
    public static void EncryptPackage(ref Stream stream, PackageInfo info)
    {
        stream.Position = 0;
        Aes aes = ConstructPackageAES(info.game);
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
            // then copy header data into the new array
            int headerSize = 0;
            if (info.game is Game.IB1)
                headerSize = sizeof(uint) * 2;
            else
                headerSize = sizeof(uint);

            byte[] finalData = new byte[headerSize + encryptedData.Length];
            int offset = 4;
            if (info.game is Game.IB1)
            {
                Array.Copy(BitConverter.GetBytes(info.saveVersion), 0, finalData, 0, sizeof(uint));
                Array.Copy(BitConverter.GetBytes(info.saveMagic), 0, finalData, sizeof(int), sizeof(uint));
                offset *= 2;
            }
            else
            {
                uint packageConstant;
                if (info.game is Game.IB2 or Game.VOTE)
                    packageConstant = PackageConstants.IB2_SAVE_MAGIC;
                else
                    packageConstant = PackageConstants.IB3_SAVE_MAGIC;
                Array.Copy(BitConverter.GetBytes(packageConstant), 0, finalData, 0, sizeof(uint));
            }

            Array.Copy(encryptedData, 0, finalData, offset, encryptedData.Length);
            stream.Position = 0;
            stream.Write(finalData, 0, finalData.Length);
        }
    }
}