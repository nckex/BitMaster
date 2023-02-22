using BitMaster;

namespace SampleSerialization
{
    [BitSerializable<ulong>]
    public readonly partial struct SampleUnionStruct
    {
        [Bit(Offset = 0, Length = 16)] public uint All { get; init; }
        [Bit(Offset = 0, Length = 2)] public uint Half_One_All { get; init; }
        [Bit(Offset = 2, Length = 2)] public uint Half_Two_All { get; init; }
        [Bit(Length = 16)] public uint LastVal { get; init; }
    }
}
