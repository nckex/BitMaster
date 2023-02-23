using BitMaster;

namespace SampleSerialization
{
    [BitSerializable<ulong>]
    public readonly partial struct SampleStruct
    {
        [Bit(Length = 32)] public int Field_One { get; init; }
        [Bit] public bool Field_Two { get; init; }
        [Bit(Length = 16)] public ushort Field_Three { get; init; }
    }
}
