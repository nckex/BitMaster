namespace SampleSerialization
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TestValues();
            TestBool();

            Console.ReadLine();
        }

        private static void TestValues()
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
        }

        private static void TestBool()
        {
            var boolStr = new SampleBooleanStruct
            {
                Field1 = true,
                Field2 = true,
                Field3 = false,
                Field4 = true
            };

            var serialized = boolStr.Serialize();

            Console.WriteLine($"Serialized Struct: {serialized}");
            Console.WriteLine();

            var deserializedStruct = SampleBooleanStruct.Deserialize(in serialized);

            Console.WriteLine("Deserialized Struct");
            Console.WriteLine($"Field1: {deserializedStruct.Field1}");
            Console.WriteLine($"Field2: {deserializedStruct.Field2}");
            Console.WriteLine($"Field3: {deserializedStruct.Field3}");
            Console.WriteLine($"Field4: {deserializedStruct.Field4}");
        }
    }
}