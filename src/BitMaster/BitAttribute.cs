namespace BitMaster
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
    public class BitAttribute : Attribute
    {
        public int Length { get; init; }
        public int Offset { get; init; }
    }
}
