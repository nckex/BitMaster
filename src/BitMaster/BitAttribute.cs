namespace BitMaster
{
    [AttributeUsage(AttributeTargets.Property, AllowMultiple = false)]
    public class BitAttribute : Attribute
    {
        public int Length { get; init; }
        public int SkipNextLength { get; init; }
    }
}
