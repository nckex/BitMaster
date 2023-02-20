using System;
using Microsoft.CodeAnalysis;

namespace BitMaster.Generator
{
    public class GeneratorException : Exception
    {
        public Location Location { get; }

        public GeneratorException(string message, Location location) : base(message)
        {
            Location = location;
        }
    }
}
