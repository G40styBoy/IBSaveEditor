using SaveDumper.UnrealPackageManager.Crypto;

namespace SaveDumper.Serializer;

/// <summary>
/// Takes crunched Json data and converts it into serialized data readable for BasicLoadObject
/// This gets written out into a binary file
/// </summary>
class DataSerializer : IDisposable
{
    private const string DEFAULT_NAME = "UnencryptedSave0";
    private const string EXTENSION = ".bin";

    private readonly List<UProperty> crunchedData;
    private BinaryWriter binWriter;
    private FileStream stream;
    private readonly UPropertyDataHelper uhelper;
    private readonly string outputPath;
    private UnrealPackage UPK;

    public DataSerializer(UnrealPackage UPK, List<UProperty> crunchedData)
    {
        this.UPK = UPK;
        this.crunchedData = crunchedData;
        string fileName;
        uhelper = new UPropertyDataHelper();

        // if (UPK.packageData.bisEncrypted)
        //     fileName = $"{UPK.packageData.packageName} - MODDED{EXTENSION}";
        // else
        fileName = $@"{UPK.packageName}{EXTENSION}";

        outputPath = Path.Combine(FilePaths.OutputDir, fileName);

        stream = new FileStream(outputPath, FileMode.Create, FileAccess.ReadWrite);
        binWriter = new BinaryWriter(stream);
    }

    internal bool SerializeAndOutputData()
    {
        try
        {
            SerializePackageHeader();
            foreach (var uProperty in crunchedData)
            {
                uhelper.SerializeMetadata(ref binWriter, uProperty);
                uProperty.SerializeValue(binWriter);
            }
            uhelper.SerializeString(ref binWriter, "None");
            binWriter.Flush();

            if (UPK.isEncrypted)
                PackageCrypto.EncryptPackage(ref stream, UPK);

            return true;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(ex.Message);
        }
    }

    private void SerializePackageHeader()
    {
        // even encrypted files have header data stored before encryption
        // add here so the file can be read correctly when loading a save
        if (UPK.isEncrypted)
        {
            switch (UPK.game)
            {
                case Game.IB1:
                    binWriter.Write(PackageCrypto.NO_MAGIC);
                    break;
                case Game.IB2 or Game.VOTE:
                    binWriter.Write(0);
                    binWriter.Write(PackageCrypto.NO_MAGIC);
                    break;
                default:
                    throw new InvalidDataException("Package is encrypted but its game type for header population isnt supported.");
            }
            return;
        }

        // write out unencrypted header info
        binWriter.Write(UPK.saveVersion);
        binWriter.Write(UPK.saveMagic);        
    }

    private string GetNextSaveFileNameForOuput(string fileName, string fileExtension)
    {
        string[] saveFiles = Directory.GetFiles(FilePaths.OutputDir, $"*{fileExtension}");
        if (saveFiles.Length == 0)
            return $"{fileName}0{fileExtension}";

        int maxIndex = -1;

        foreach (var file in saveFiles)
        {
            string nameWithoutExt = Path.GetFileNameWithoutExtension(file);

            var match = System.Text.RegularExpressions.Regex.Match(nameWithoutExt, @"(\d+)$");
            if (match.Success && int.TryParse(match.Value, out int index))
            {
                if (index > maxIndex)
                    maxIndex = index;
            }
        }

        int nextIndex = maxIndex + 1;
        string newFileName = System.Text.RegularExpressions.Regex.Replace(
            fileName,
            @"\d+$",
            nextIndex.ToString()
        );

        if (!System.Text.RegularExpressions.Regex.IsMatch(fileName, @"\d+$"))
            newFileName = $"{fileName}{nextIndex}";

        return newFileName + fileExtension;
    }

    public void Dispose()
    {
        binWriter?.Dispose();
        stream?.Dispose();
    }
}
