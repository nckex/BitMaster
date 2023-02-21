using BitMaster;

namespace SampleSerialization
{
    [BitSerializable<ulong>]
    public readonly partial struct SampleExtendedWithoutKeepStruct
    {
        [Bit(Length = 3), BitExtension(Offset = 23, Length = 1)] public byte FirstValue { get; init; }
        [Bit(Length = 20)] public uint SecondValue { get; init; }
    }
}
