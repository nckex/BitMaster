using BitMaster;

namespace SampleSerialization
{
    [BitSerializable<byte>]
    public readonly partial struct SampleBooleanStruct
    {
        [Bit] public bool Field1 { get; init; }
        [Bit] public bool Field2 { get; init; }
        [Bit] public bool Field3 { get; init; }
        [Bit] public bool Field4 { get; init; }
    }
}
