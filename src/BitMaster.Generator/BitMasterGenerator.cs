using System;
using System.IO;
using System.Text;
using System.Linq;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using System.Diagnostics;

namespace BitMaster.Generator
{
    [Generator]
    public class BitMasterGenerator : ISourceGenerator
    {
        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            // Debugger.Launch();

            try
            {
                ExecuteInternal(context);
            }
            catch (GeneratorException e)
            {
                var descriptor = new DiagnosticDescriptor(
                    nameof(BitMaster), 
                    "Error", 
                    e.Message, 
                    "Error", 
                    DiagnosticSeverity.Error, 
                    true);

                var diagnostic = Diagnostic.Create(descriptor, e.Location ?? Location.None);
                
                context.ReportDiagnostic(diagnostic);
            }
            catch (Exception e)
            {
                var descriptor = new DiagnosticDescriptor(nameof(BitMaster), "Error", e.ToString(), "Error", DiagnosticSeverity.Error, true);
                var diagnostic = Diagnostic.Create(descriptor, Location.None);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private void ExecuteInternal(GeneratorExecutionContext context)
        {
            if (context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            var compilation = context.Compilation;

            var structAttributeSymbol = compilation.GetTypeByMetadataName("BitMaster.BitSerializableAttribute`1");
            if (structAttributeSymbol == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    "SP0001",
                    "Internal analyzer error.",
                    "Struct Attribute Symbol not found",
                    "Internal",
                    DiagnosticSeverity.Error,
                    true), null));
                return;
            }

            var bitAttributeSymbol = compilation.GetTypeByMetadataName("BitMaster.BitAttribute");
            if (bitAttributeSymbol == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    "SP0002",
                    "Internal analyzer error.",
                    "Property Attribute Symbol not found",
                    "Internal",
                    DiagnosticSeverity.Error,
                    true), null));
                return;
            }

            var bitExtensionAttributeSymbol = compilation.GetTypeByMetadataName("BitMaster.BitExtensionAttribute");
            if (bitExtensionAttributeSymbol == null)
            {
                context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    "SP0002",
                    "Internal analyzer error.",
                    "Extension Attribute Symbol not found",
                    "Internal",
                    DiagnosticSeverity.Error,
                    true), null));
                return;
            }

            foreach (var structDeclaration in receiver.DeclaretedStructs)
            {
                var model = compilation.GetSemanticModel(structDeclaration.SyntaxTree);
                var structSymbol = model.GetDeclaredSymbol(structDeclaration);

                if (structSymbol == null)
                    continue;

                var structAttr = structSymbol.GetAttributes().SingleOrDefault(x =>
                    x.AttributeClass != null
                    && x.AttributeClass.OriginalDefinition.Equals(structAttributeSymbol, SymbolEqualityComparer.Default));

                if (structAttr == null)
                    continue;

                var propertyGenModels = new List<PropertyGenModel>();
                foreach (var member in structSymbol.GetMembers())
                {
                    var bitAttribute = member.GetAttributes().FirstOrDefault(y =>
                        y.AttributeClass.Equals(bitAttributeSymbol, SymbolEqualityComparer.Default));

                    if (bitAttribute == null)
                        continue;

                    var extensionAttribute = member.GetAttributes().FirstOrDefault(y =>
                        y.AttributeClass.Equals(bitExtensionAttributeSymbol, SymbolEqualityComparer.Default));

                    var propertyGenModel = new PropertyGenModel(member, bitAttribute, extensionAttribute);
                    propertyGenModels.Add(propertyGenModel);
                }

                var structGenModel = new StructGenModel(structSymbol, structAttr, propertyGenModels);

                var structSource = ProcessStruct(structGenModel);
                if (structSource == null)
                    continue;

                context.AddSource($"{structSymbol.Name}_BitMaster.g.cs", SourceText.From(structSource, Encoding.UTF8));
            }
        }

        private string ProcessStruct(StructGenModel structGenModel)
        {
            var writer = new IndentedTextWriter(new StringWriter(), "   ");

            writer.WriteLine("using System;");
            writer.WriteLine();

            writer.WriteLine($"namespace {structGenModel.StructSymbol.ContainingNamespace.ToDisplayString()}");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"public readonly partial struct {structGenModel.StructSymbol.Name}");
            writer.WriteLine("{");
            writer.Indent++;

            GenerateMasksSection(writer, structGenModel);
            writer.WriteLine();

            GenerateDeserializeMethodSection(writer, structGenModel);
            writer.WriteLine();

            GenerateSerializeMethodSection(writer, structGenModel);
            writer.WriteLine();

            GenerateImplicitOperator(writer, structGenModel);

            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");

            return writer.InnerWriter.ToString()!;
        }

        private void GenerateMasksSection(IndentedTextWriter writer, StructGenModel structGenModel)
        {
            byte offset = 0;

            foreach (var propertyModel in structGenModel.Properties)
            {
                var propertyMask = CalculateMask(in offset, propertyModel.Length);

                writer.WriteLine($"private const byte {propertyModel.PropertySymbol.Name}_Offset = {offset};");
                writer.WriteLine($"private const {structGenModel.StructValueTypeName} {propertyModel.PropertySymbol.Name}_Mask = {propertyMask};");

                if (propertyModel.BitExtensionAttributeData != null)
                {
                    var extensionMask = CalculateMask(propertyModel.ExtensionOffset, propertyModel.ExtensionLength);
                    var extendedMask = propertyMask + extensionMask;

                    writer.WriteLine($"private const byte {propertyModel.PropertySymbol.Name}_ExtensionOffset = {propertyModel.ExtensionOffset};");
                    writer.WriteLine($"private const {structGenModel.StructValueTypeName} {propertyModel.PropertySymbol.Name}_ExtensionMask = {extensionMask};");
                    writer.WriteLine($"private const {structGenModel.StructValueTypeName} {propertyModel.PropertySymbol.Name}_ExtendedMask = {extendedMask};");
                }

                offset += (byte)(propertyModel.Length + propertyModel.SkipNextLength);

                if (offset > structGenModel.BitCapacity)
                {
                    throw new GeneratorException(
                        $"Declared struct '{structGenModel.StructSymbol.Name}' exceeded max bit size at '{propertyModel.PropertySymbol.Name}'. Offset Size = {offset}; Max Size = {structGenModel.BitCapacity}",
                        structGenModel.StructSymbol.Locations[0]);
                }
            }
        }

        private void GenerateDeserializeMethodSection(IndentedTextWriter writer, StructGenModel structGenModel)
        {
            writer.WriteLine($"public static {structGenModel.StructSymbol.Name} Deserialize(in {structGenModel.StructValueTypeName} value)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"return new {structGenModel.StructSymbol.Name}");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var propertyModel in structGenModel.Properties)
            {
                var propertySymbol = propertyModel.PropertySymbol;

                if (propertySymbol.Type.Name == "Boolean")
                {
                    writer.WriteLine($"{propertySymbol.Name} = ((value & {propertySymbol.Name}_Mask) >> {propertySymbol.Name}_Offset) > 0,");
                    continue;
                }

                if (propertyModel.BitExtensionAttributeData != null)
                {
                    if (propertyModel.KeepExtensionBits)
                    {
                        writer.WriteLine($"{propertySymbol.Name} = ({propertyModel.ValueTypeName})((value & {propertySymbol.Name}_ExtendedMask) >> {propertySymbol.Name}_Offset),");
                        continue;
                    }

                    writer.Write($"{propertySymbol.Name} = ({propertyModel.ValueTypeName})((((value & {propertySymbol.Name}_ExtensionMask) >> {propertySymbol.Name}_ExtensionOffset) << (byte){propertyModel.Length})");
                    writer.WriteLine($" + ({propertyModel.ValueTypeName})(value & {propertySymbol.Name}_Mask)),");
                    continue;
                }

                writer.WriteLine($"{propertySymbol.Name} = ({propertyModel.ValueTypeName})((value & {propertySymbol.Name}_Mask) >> {propertySymbol.Name}_Offset),");
            }

            writer.Indent--;
            writer.WriteLine("};");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void GenerateSerializeMethodSection(IndentedTextWriter writer, StructGenModel structGenModel)
        {
            var returnValType = structGenModel.StructValueTypeName;

            writer.WriteLine($"public {returnValType} Serialize()");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"{returnValType} result = 0;");
            writer.WriteLine();


            foreach (var propertyModel in structGenModel.Properties)
            {
                var propSymbol = propertyModel.PropertySymbol;

                if (propSymbol.Type.Name == "Boolean")
                {
                    writer.WriteLine($"result += ({returnValType})((({returnValType})({propSymbol.Name} ? 1 : 0)) << {propSymbol.Name}_Offset);");
                    writer.WriteLine();
                    continue;
                }

                if (propertyModel.BitExtensionAttributeData != null && propertyModel.KeepExtensionBits == false)
                {
                    writer.WriteLine($"{returnValType} {propSymbol.Name}_FirstPart_Value = ({returnValType})(({returnValType}){propSymbol.Name} & {propSymbol.Name}_Mask);");
                    writer.WriteLine($"{returnValType} {propSymbol.Name}_SecondPart_Value = ({returnValType})((({returnValType})({returnValType}){propSymbol.Name} >> {propertyModel.Length})) << {propertyModel.ExtensionOffset};");
                    writer.WriteLine($"result += ({returnValType})({propSymbol.Name}_FirstPart_Value + {propSymbol.Name}_SecondPart_Value);");
                    writer.WriteLine();
                    continue;
                }

                writer.WriteLine($"{returnValType} {propSymbol.Name}_Value = ({returnValType})((({returnValType}){propSymbol.Name}) << {propSymbol.Name}_Offset);");
                writer.WriteLine($"result += {propSymbol.Name}_Value;");
                writer.WriteLine();

                //writer.WriteLine($"if ((({valTypeStr})({propSymbol.Name}_Value & {propSymbol.Name}_Mask) >> {propSymbol.Name}_Offset) != {propSymbol.Name})");
                //writer.WriteLine("{");
                //writer.Indent++;
                //writer.WriteLine($"throw new Exception($\"'{propSymbol.Name}' exceeded mask value\");");
                //writer.Indent--;
                //writer.WriteLine("}");
                //writer.WriteLine();
            }

            writer.WriteLine("return result;");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private void GenerateImplicitOperator(IndentedTextWriter writer, StructGenModel structGenModel)
        {
            writer.WriteLine($"public static implicit operator {structGenModel.StructValueTypeName}({structGenModel.StructSymbol.Name} value)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("return value.Serialize();");

            writer.Indent--;
            writer.WriteLine("}");
        }

        private static ulong CalculateMask(in byte offset, in byte length)
        {
            return (((ulong)1 << length) - 1) << Math.Max((byte)0, offset);
        }

        internal class SyntaxReceiver : ISyntaxReceiver
        {
            public List<StructDeclarationSyntax> DeclaretedStructs { get; } = new();

            public void OnVisitSyntaxNode(SyntaxNode syntaxNode)
            {
                if (syntaxNode is StructDeclarationSyntax structDeclarationSyntax && structDeclarationSyntax.AttributeLists.Count > 0)
                {
                    DeclaretedStructs.Add(structDeclarationSyntax);
                }
            }
        }

        internal class StructGenModel
        {
            public INamedTypeSymbol StructSymbol { get; }
            public ITypeSymbol StructValueType { get; }
            public string StructValueTypeName { get; }
            public byte BitCapacity { get; }
            public List<PropertyGenModel> Properties { get; }

            public StructGenModel(
                INamedTypeSymbol structType,
                AttributeData bitAttributeData,
                List<PropertyGenModel> properties)
            {
                var structValTypeSymbol = bitAttributeData.AttributeClass.TypeArguments[0];

                StructSymbol = structType;
                StructValueType = structValTypeSymbol;
                StructValueTypeName = structValTypeSymbol.ToDisplayString();
                BitCapacity = (byte)(structValTypeSymbol.Name.StartsWith("UInt") ? int.Parse(structValTypeSymbol.Name.Replace("UInt", "")) : sizeof(byte) * 8);
                Properties = properties;
            }
        }

        internal class PropertyGenModel
        {
            public IPropertySymbol PropertySymbol { get; set; }
            public string ValueTypeName { get; }
            public AttributeData BitAttributeData { get; set; }
            public AttributeData BitExtensionAttributeData { get; set; }
            public byte Length { get; }
            public byte SkipNextLength { get; }
            public byte ExtensionOffset { get; }
            public byte ExtensionLength { get; }
            public bool KeepExtensionBits { get; }

            public PropertyGenModel(
                ISymbol propertyType, 
                AttributeData bitAttributeData,
                AttributeData bitExtensionAttributeData)
            {
                var propSymbol = (IPropertySymbol)propertyType;

                PropertySymbol = propSymbol;
                ValueTypeName = propSymbol.Type.ToDisplayString();
                BitAttributeData = bitAttributeData;
                BitExtensionAttributeData = bitExtensionAttributeData;

                var length = byte.Parse(bitAttributeData.NamedArguments.FirstOrDefault(x => x.Key == "Length").Value.Value?.ToString() ?? "1");
                var skipNextLength = byte.Parse(bitAttributeData.NamedArguments.FirstOrDefault(x => x.Key == "SkipNextLength").Value.Value?.ToString() ?? "0");
                var extensionOffset = byte.Parse(bitExtensionAttributeData?.NamedArguments.FirstOrDefault(x => x.Key == "Offset").Value.Value?.ToString() ?? "0");
                var extensionLength = byte.Parse(bitExtensionAttributeData?.NamedArguments.FirstOrDefault(x => x.Key == "Length").Value.Value?.ToString() ?? "0");
                var keepExtensionBits = bool.Parse(bitExtensionAttributeData?.NamedArguments.FirstOrDefault(x => x.Key == "KeepExtensionBits").Value.Value?.ToString() ?? "false");

                Length = length;
                SkipNextLength = skipNextLength;
                ExtensionOffset = extensionOffset;
                ExtensionLength = extensionLength;
                KeepExtensionBits = keepExtensionBits;
            }
        }
    }
}