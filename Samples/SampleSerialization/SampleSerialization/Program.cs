namespace SampleSerialization
{
    internal class Program
    {
        static void Main(string[] args)
        {
            var structInstance = new SampleStruct
            {
                Field_One = uint.MaxValue,
                Field_Two = true,
                Field_Three = ushort.MaxValue
            };

            var serialized = structInstance.Serialize();

            Console.WriteLine($"Serialized Struct: {serialized}");
            Console.WriteLine();

            var deserializedStruct = SampleStruct.Deserialize(in serialized);

            Console.WriteLine("Deserialized Struct");
            Console.WriteLine($"Field_One: {deserializedStruct.Field_One}");
            Console.WriteLine($"Field_Two: {deserializedStruct.Field_Two}");
            Console.WriteLine($"Field_Three: {deserializedStruct.Field_Three}");

            Console.ReadLine();
        }
    }
}