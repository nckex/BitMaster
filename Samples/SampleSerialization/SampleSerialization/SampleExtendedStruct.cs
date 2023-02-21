using BitMaster;

namespace SampleSerialization
{
    [BitSerializable<ulong>]
    public readonly partial struct SampleExtendedStruct
    {
        [Bit(Length = 12), BitExtension(Offset = 25, Length = 5, KeepExtensionBits = true)] public uint FirstValue { get; init; }
        [Bit(Length = 13)] public ushort SecondValue { get; init; }
    }
}
