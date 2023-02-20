using System.Numerics;

namespace BitMaster
{
    [AttributeUsage(AttributeTargets.Struct)]
    public class BitSerializableAttribute<T> : Attribute
        where T : IUnsignedNumber<T>
    {
        public T? NumberType { get; }
    }
}