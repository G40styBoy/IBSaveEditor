using System.Security.Cryptography;
using IBSaveEditor.Wrappers;

namespace IBSaveEditor.Package;

/// <summary>
/// Handles all AES encryption and decryption operations for Unreal save packages.
/// </summary>
internal static class PackageCrypto
{
    // Credits to Hox for the keys and the idea to initialize them in UTF8. Thanks!!
    private static readonly byte[] IB1AES  = "NoBwPWDkRqFMTaHeVCJkXmLSZoNoBIPm"u8.ToArray();
    private static readonly byte[] IB2AES  = "|FK}S];v]!!cw@E4l-gMXa9yDPvRfF*B"u8.ToArray();
    private static readonly byte[] IB3AES  = "6nHmjd:hbWNf=9|UO2:?;K0y+gZL-jP5"u8.ToArray();
    private static readonly byte[] VOTEAES = "DKksEKHkldF#(WDJ#FMS7jla5f(@J12|"u8.ToArray();

    private const int BLOCK_SIZE = 16;

    /// <summary>
    /// Attempts to decrypt the first block of a stream to determine if the package
    /// is encrypted under the given game's AES key.
    /// </summary>
    /// <returns>True if the decrypted block matches the expected unencrypted signature.</returns>
    public static bool TryDecryptHalfBlock(Game game, UnrealStream stream)
    {
        var buffer = new byte[BLOCK_SIZE];
        stream.BaseStream.ReadExactly(buffer, 0, BLOCK_SIZE);

        using var aes         = ConstructPackageAES(game);
        using var transformer = aes.CreateDecryptor();
        var decrypted         = transformer.TransformFinalBlock(buffer, 0, BLOCK_SIZE);

        return IsHalfBlockUnencrypted(decrypted);
    }

    /// <summary>
    /// Decrypts the full contents of a package's stream.
    /// </summary>
    /// <returns>The decrypted package bytes, excluding the save header.</returns>
    public static byte[] DecryptPackage(UnrealPackage upk)
    {
        using var aes         = ConstructPackageAES(upk.info.game);
        using var decryptor   = aes.CreateDecryptor();

        // IB1 has an extra 4-byte magic value in the header, so skip 8 bytes instead of 4
        int srcOffset    = upk.info.game is Game.IB1 ? sizeof(int) * 2 : sizeof(int);
        var streamBytes  = upk.Stream.GetStreamBytes();

        var encryptedData = new byte[streamBytes.Length - srcOffset];
        Array.ConstrainedCopy(streamBytes, srcOffset, encryptedData, 0, encryptedData.Length);

        return decryptor.TransformFinalBlock(encryptedData, 0, encryptedData.Length);
    }

    /// <summary>
    /// Checks the first 8 bytes of a decrypted block to verify it is unencrypted.
    /// </summary>
    private static bool IsHalfBlockUnencrypted(byte[] block)
        => BitConverter.ToUInt32(block, block.Length - 12) is PackageConstants.NO_MAGIC;

    /// <summary>
    /// Encrypts a stream in place and prepends the correct game-specific header.
    /// </summary>
    public static void EncryptPackage(ref UnrealStream stream, PackageInfo info)
    {
        // Read the raw decrypted data from the stream
        stream.Position = 0;
        using var memStream = new MemoryStream();
        stream.BaseStream.CopyTo(memStream);
        var decryptedData = memStream.ToArray();

        // Encrypt
        using var aes       = ConstructPackageAES(info.game);
        using var encryptor = aes.CreateEncryptor();
        var encryptedData   = encryptor.TransformFinalBlock(decryptedData, 0, decryptedData.Length);

        // Build the final output: header + encrypted data
        var finalData = BuildEncryptedOutput(info, encryptedData);

        stream.Position = 0;
        stream.BaseStream.Write(finalData, 0, finalData.Length);
    }

    /// <summary>
    /// Constructs the full output buffer by prepending the correct header to the encrypted data.
    /// IB1 has a two-field header (save version + magic). All others have a single magic field.
    /// </summary>
    private static byte[] BuildEncryptedOutput(PackageInfo info, byte[] encryptedData)
    {
        int headerSize = info.game is Game.IB1 ? sizeof(uint) * 2 : sizeof(uint);
        var finalData  = new byte[headerSize + encryptedData.Length];

        if (info.game is Game.IB1)
        {
            Array.Copy(BitConverter.GetBytes(info.saveVersion), 0, finalData, 0,           sizeof(uint));
            Array.Copy(BitConverter.GetBytes(info.saveMagic),   0, finalData, sizeof(uint), sizeof(uint));
        }
        else
        {
            // IB2 and VOTE share the same save magic; IB3 has its own
            uint magic = info.game is Game.IB2 or Game.VOTE
                ? PackageConstants.IB2_SAVE_MAGIC
                : PackageConstants.IB3_SAVE_MAGIC;

            Array.Copy(BitConverter.GetBytes(magic), 0, finalData, 0, sizeof(uint));
        }

        int offset = info.game is Game.IB1 ? sizeof(uint) * 2 : sizeof(uint);
        Array.Copy(encryptedData, 0, finalData, offset, encryptedData.Length);

        return finalData;
    }

    /// <summary>
    /// Returns the AES key for the given game.
    /// </summary>
    public static byte[] GetPackageAESKey(Game game) => game switch
    {
        Game.IB1  => IB1AES,
        Game.IB2  => IB2AES,
        Game.IB3  => IB3AES,
        Game.VOTE => VOTEAES,
        _         => throw new NotImplementedException($"AES key for {game} is not supported.")
    };

    /// <summary>
    /// Constructs a configured <see cref="Aes"/> instance for the given game.
    /// IB1 uses CBC mode with a zero IV; all others use ECB mode.
    /// </summary>
    public static Aes ConstructPackageAES(Game game)
    {
        var aes     = Aes.Create();
        aes.Key     = GetPackageAESKey(game);
        aes.Padding = PaddingMode.Zeros;
        aes.Mode    = game is Game.IB1 ? CipherMode.CBC : CipherMode.ECB;

        // IB1 uses CBC: an explicit IV is required. A zeroed block is correct here.
        if (game is Game.IB1)
            aes.IV = new byte[BLOCK_SIZE];

        return aes;
    }
}