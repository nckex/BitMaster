namespace BitMaster
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class BitExtensionAttribute : Attribute
    {
        public required int Offset { get; init; }
        public required int Length { get; init; }
        public bool KeepExtensionBits { get; init; }
    }
}
