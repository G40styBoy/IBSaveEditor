/// <summary>
/// Takes unencrypted serialized data and mutates it into digestable data.
/// </summary>
public class Deserializer
{
    private List<UProperty> propertyCollection = new();
    private const int MAX_STATIC_ARRAY_ELEMENTS = 2000; 
    private const int UNINITIALIZED_VALUE = -1; 
    
    public List<UProperty> DeserializePackage(UnrealPackage upk)
    {
        while (!upk.IsEndFile())
        {
            long startPos = upk.GetStreamPosition();
            try
            {
                UProperty? tag = ConstructTag(upk);

                if (tag is null)
                    break;

                propertyCollection.Add(tag);
            }
            catch (Exception ex)
            {
                throw new InvalidDataException($"Failed to deserialize package at stream position {startPos}.", ex);
            }
        }
        return propertyCollection;
    }

    private UProperty? ConstructTag(UnrealPackage upk, bool allowStaticArrayDetection = true)
    {
        try
        {
            // immediatly checking if we pull in "None"
            var tag = new TagContainer();
            tag.name = upk.DeserializeString();
            if (tag.name is UType.NONE)               
                return null;
            
            // null possibility is fully accounted for, and in some cases expected for logic
            if (UArrayRegistry.TryGet(upk.info.game, tag.name, out var metadata))
                tag.arrayInfo = metadata!;
            else
                tag.arrayInfo = null; 
            
            if (allowStaticArrayDetection && tag.arrayInfo != null && tag.arrayInfo.arrayType is ArrayType.Static)
            {
                upk.RevertStreamPosition(tag.name);
                PopulateUPropertyMetadata(ref tag, UType.ARRAY_PROPERTY, UNINITIALIZED_VALUE, UNINITIALIZED_VALUE);
                tag.type = UType.ARRAY_PROPERTY;
                tag.arrayEntryCount = 0;
            }
            else
            {
                PopulateUPropertyMetadata(ref tag, upk.DeserializeString(), upk.DeserializeInt(), upk.DeserializeInt());
                if (tag.type is UType.ARRAY_PROPERTY)
                    tag.arrayEntryCount = upk.DeserializeInt();
            }
            return ConstructUProperty(upk, tag);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException("Failed to construct tag.", ex);
        }  
    }

    /// <summary>
    /// Populate the main metadata for a property based on what's read.
    /// </summary>
    private void PopulateUPropertyMetadata(ref TagContainer tag, string type, int size, int arrayIndex)
    {
        tag.type = type;
        tag.arrayIndex = arrayIndex;
        tag.size = size;
    }

    private UProperty ConstructUProperty(UnrealPackage upk, TagContainer tag)
    {
        return tag.type switch
        {
            UType.INT_PROPERTY => new UIntProperty(upk, tag),
            UType.FLOAT_PROPERTY => new UFloatProperty(upk, tag),
            UType.BOOL_PROPERTY => new UBoolProperty(upk, tag),
            UType.BYTE_PROPERTY => UByteProperty.InstantiateProperty(upk, tag),
            UType.STR_PROPERTY => new UStringProperty(upk, tag),
            UType.NAME_PROPERTY => new UNameProperty(upk, tag),
            UType.STRUCT_PROPERTY => CreateStructProperty(upk, tag),
            UType.ARRAY_PROPERTY => CreateArrayProperty(upk, tag),
            _ => throw new NotSupportedException($"Unsupported property type: {tag.type}")
        };
    }

    private UStructProperty CreateStructProperty(UnrealPackage upk, TagContainer tag)
    {
        // store the alternate name for the struct somewhere. This does not get used!
        tag.alternateName = upk.DeserializeString();   
        LoopTagConstructor(upk, out List<UProperty> elements);
        return new UStructProperty(tag, tag.alternateName, elements);
    }

    private UProperty CreateArrayProperty(UnrealPackage upk, TagContainer tag)
    {
        if (tag.arrayInfo.arrayType is ArrayType.Static)
            return BuildArrayProperty(upk, tag);

        return tag.arrayInfo.valueType switch
        {
            PropertyType.IntProperty => BuildArrayProperty(upk, tag, upk => upk.DeserializeInt()),
            PropertyType.FloatProperty => BuildArrayProperty(upk, tag, upk => upk.DeserializeFloat()),
            PropertyType.StrProperty or PropertyType.NameProperty => BuildArrayProperty(upk, tag, upk => upk.DeserializeString()),
            PropertyType.StructProperty => BuildArrayProperty(upk, tag, _ => ReadStructElement(upk)),
            _ => throw new NotSupportedException($"Unsupported array type: {tag.arrayInfo.valueType}")
        };
    }

    private UArrayProperty<T> BuildArrayProperty<T>(UnrealPackage upk, TagContainer tag, Func<UnrealPackage, T> reader) where T : notnull
    {
        var elements = new List<T>();

        for (int i = 0; i < tag.arrayEntryCount; i++)
            elements.Add(reader(upk));

        return new UArrayProperty<T>(tag, elements);
    }

    private UArrayProperty<UProperty> BuildArrayProperty(UnrealPackage upk, TagContainer tag)
    {
        var elements = new List<UProperty>();
        int loopCount = 0;
        while (true)
        {
            if (loopCount > MAX_STATIC_ARRAY_ELEMENTS)
                throw new Exception("Infinite loop detected while deserializing static array.");

            var nextname = upk.PeekString();
            if (nextname != tag.name)
                break;

            elements.Add(ConstructTag(upk, allowStaticArrayDetection: false));
            tag.arrayEntryCount++;
            loopCount++;
        }

        return new UArrayProperty<UProperty>(tag, elements);
    }

    private List<UProperty> ReadStructElement(UnrealPackage upk)
    {
        LoopTagConstructor(upk, out List<UProperty> elements);
        return elements;
    }

    private void LoopTagConstructor(UnrealPackage upk, out List<UProperty> elements)
    {
        UProperty tag;
        elements = new List<UProperty>();

        while ((tag = ConstructTag(upk)) != null)
            elements.Add(tag);
    }
}
