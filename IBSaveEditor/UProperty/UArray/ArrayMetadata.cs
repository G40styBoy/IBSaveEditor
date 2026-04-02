namespace IBSaveEditor.UProperties.UArray;

/// <summary>
/// Metadata of a UProperty array.
/// </summary>
public record ArrayMetadata
{
    public ArrayName arrayName;
    public AlternateName alternateName;
    public PropertyType valueType;
    public ArrayType arrayType;

    public ArrayMetadata(ArrayName arrayName, AlternateName alternateName, PropertyType valueType, ArrayType arrayType)
    {
        this.arrayName = arrayName;
        this.alternateName = alternateName;
        this.valueType = valueType;
        this.arrayType = arrayType;
    }
}