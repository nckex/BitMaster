namespace SampleSerialization
{
    internal class Program
    {
        static void Main(string[] args)
        {
            TestBool();
            TestValues();
            TestValuesExtended();
            TestValuesExtendedWithoutKeepBits();
            TestUnion();

            Console.ReadLine();
        }

        private static void TestValues()
        {
            Console.WriteLine($"============================== {nameof(TestValues)} ==============================");

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

        private static void TestValuesExtended()
        {
            Console.WriteLine($"============================== {nameof(TestValuesExtended)} ==============================");

            var structInstance = new SampleExtendedStruct
            {
                FirstValue = 33556158,
                SecondValue = 70
            };

            var serialized = structInstance.Serialize();

            Console.WriteLine($"Serialized Struct: {serialized}");
            Console.WriteLine();

            var deserializedStruct = SampleExtendedStruct.Deserialize(in serialized);

            Console.WriteLine("Deserialized Struct");
            Console.WriteLine($"FirstValue: {deserializedStruct.FirstValue}");
            Console.WriteLine($"SecondValue: {deserializedStruct.SecondValue}");
        }

        private static void TestValuesExtendedWithoutKeepBits()
        {
            Console.WriteLine($"============================== {nameof(TestValuesExtendedWithoutKeepBits)} ==============================");

            var structInstance = new SampleExtendedWithoutKeepStruct
            {
                FirstValue = 9,
                SecondValue = 999
            };

            var serialized = structInstance.Serialize();

            Console.WriteLine($"Serialized Struct: {serialized}");
            Console.WriteLine();

            var deserializedStruct = SampleExtendedWithoutKeepStruct.Deserialize(in serialized);

            Console.WriteLine("Deserialized Struct");
            Console.WriteLine($"FirstValue: {deserializedStruct.FirstValue}");
            Console.WriteLine($"SecondValue: {deserializedStruct.SecondValue}");
        }

        private static void TestBool()
        {
            Console.WriteLine($"============================== {nameof(TestBool)} ==============================");

            var boolStr = new SampleBooleanStruct
            {
                Field1 = true,
                Field2 = true,
                Field3 = false,
                Field4 = 7
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

        private static void TestUnion()
        {
            Console.WriteLine($"============================== {nameof(TestUnion)} ==============================");

            var structInstance = new SampleUnionStruct
            {
                All = 150,
                LastVal = 77
            };

            var serialized = structInstance.Serialize();

            Console.WriteLine($"Serialized Struct: {serialized}");
            Console.WriteLine();

            var deserializedStruct = SampleUnionStruct.Deserialize(in serialized);

            Console.WriteLine("Deserialized Struct");
            Console.WriteLine($"All: {deserializedStruct.All}");
            Console.WriteLine($"Half_One_All: {deserializedStruct.Half_One_All}");
            Console.WriteLine($"Half_Two_All: {deserializedStruct.Half_Two_All}");
        }
    }
}