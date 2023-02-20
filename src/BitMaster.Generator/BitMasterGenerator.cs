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
        private GeneratorExecutionContext _context;

        public void Initialize(GeneratorInitializationContext context)
        {
            context.RegisterForSyntaxNotifications(() => new SyntaxReceiver());
        }

        public void Execute(GeneratorExecutionContext context)
        {
            _context = context;

            try
            {
                ExecuteInternal();
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

        private void ExecuteInternal()
        {
            if (_context.SyntaxReceiver is not SyntaxReceiver receiver)
                return;

            var compilation = _context.Compilation;

            var structAttributeSymbol = compilation.GetTypeByMetadataName("BitMaster.BitSerializableAttribute`1");
            if (structAttributeSymbol == null)
            {
                _context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
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
                _context.ReportDiagnostic(Diagnostic.Create(new DiagnosticDescriptor(
                    "SP0002",
                    "Internal analyzer error.",
                    "Property Attribute Symbol not found",
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

                var structSource = ProcessStruct(structSymbol, structAttr, bitAttributeSymbol);
                if (structSource == null)
                    continue;

                _context.AddSource($"{structSymbol.Name}_BitMaster.g.cs", SourceText.From(structSource, Encoding.UTF8));
            }
        }

        private string ProcessStruct(INamedTypeSymbol structSymbol, AttributeData structAttrData, INamedTypeSymbol propAttributeSymbol)
        {
            var writer = new IndentedTextWriter(new StringWriter(), "   ");

            writer.WriteLine("using System;");
            writer.WriteLine();

            writer.WriteLine($"namespace {structSymbol.ContainingNamespace.ToDisplayString()}");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"public readonly partial struct {structSymbol.Name}");
            writer.WriteLine("{");
            writer.Indent++;

            var structBitFields = new List<(ISymbol, IEnumerable<AttributeData>)>();
            var structArgValType = structAttrData.AttributeClass.TypeArguments[0];

            foreach (var member in structSymbol.GetMembers())
            {
                var bitAttributes = member.GetAttributes().Where(y =>
                    y.AttributeClass.Equals(propAttributeSymbol, SymbolEqualityComparer.Default));

                if (!bitAttributes.Any())
                    continue;

                structBitFields.Add((member, bitAttributes));
            }

            GenerateMasksSection(writer, structSymbol, structArgValType, structBitFields, structAttrData.AttributeClass);
            GenerateDeserializeMethodSection(writer, structSymbol, structArgValType, structBitFields);
            GenerateSerializeMethodSection(writer, structArgValType, structBitFields);
            GenerateImplicitOperator(writer, structSymbol, structArgValType);

            writer.Indent--;
            writer.WriteLine("}");

            writer.Indent--;
            writer.WriteLine("}");

            return writer.InnerWriter.ToString()!;
        }

        private void GenerateMasksSection(
            IndentedTextWriter writer,
            INamedTypeSymbol structSymbol,
            ITypeSymbol structValType,
            IEnumerable<(ISymbol, IEnumerable<AttributeData>)> members,
            INamedTypeSymbol structSerializableAttribute)
        {
            var typeMaxBits = structValType.Name.StartsWith("UInt") ? int.Parse(structValType.Name.Replace("UInt", "")) : sizeof(byte) * 8;

            byte offset = 0;

            foreach (var (symbol, attributes) in members)
            {
                ulong mask = 0;

                writer.WriteLine($"private const byte {symbol.Name}_Offset = {offset};");

                foreach (var attribute in attributes)
                {
                    var offsetArg = attribute.NamedArguments.FirstOrDefault(x => x.Key == "Offset").Value.Value;
                    if (offsetArg != null)
                    {
                        offset = (byte)Math.Max(0, byte.Parse(offsetArg.ToString()) - 1);
                    }

                    var length = byte.Parse(attribute.NamedArguments.FirstOrDefault(x => x.Key == "Length").Value.Value?.ToString() ?? "1");

                    mask += (((ulong)1 << length) - 1) << Math.Max((byte)0, offset);
                    offset += length;

                    if (offset > typeMaxBits)
                    {
                        throw new GeneratorException(
                            $"Declared struct '{structSymbol.Name}' exceeded max bit size at '{symbol.Name}'. Offset Size = {offset}; Max Size = {typeMaxBits}",
                            structSymbol.Locations[0]);
                    }
                }

                writer.WriteLine($"private const {structValType.ToDisplayString()} {symbol.Name}_Mask = {mask};");
            }

            writer.WriteLine();
        }

        private void GenerateDeserializeMethodSection(
            IndentedTextWriter writer,
            INamedTypeSymbol structSymbol,
            ITypeSymbol structValType,
            IEnumerable<(ISymbol, IEnumerable<AttributeData>)> members)
        {
            writer.WriteLine($"public static {structSymbol.Name} Deserialize(in {structValType.ToDisplayString()} value)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"return new {structSymbol.Name}");
            writer.WriteLine("{");
            writer.Indent++;

            foreach (var (member, _) in members)
            {
                var propSymbol = (IPropertySymbol)member;

                if (propSymbol.Type.Name == "Boolean")
                {
                    writer.WriteLine($"{propSymbol.Name} = ((value & {propSymbol.Name}_Mask) >> {propSymbol.Name}_Offset) > 0,");
                }
                else
                {
                    writer.WriteLine($"{propSymbol.Name} = ({propSymbol.Type.ToDisplayString()})((value & {propSymbol.Name}_Mask) >> {propSymbol.Name}_Offset),");
                }
            }

            writer.Indent--;
            writer.WriteLine("};");

            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();
        }

        private void GenerateSerializeMethodSection(
            IndentedTextWriter writer,
            ITypeSymbol structValType,
            IEnumerable<(ISymbol, IEnumerable<AttributeData>)> members)
        {
            var valTypeStr = structValType.ToDisplayString();

            writer.WriteLine($"public {valTypeStr} Serialize()");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine($"{valTypeStr} result = 0;");
            writer.WriteLine();

            foreach (var (member, _) in members)
            {
                var propSymbol = (IPropertySymbol)member;

                if (propSymbol.Type.Name == "Boolean")
                {
                    writer.WriteLine($"result += (({valTypeStr})({propSymbol.Name} ? 1 : 0)) << {propSymbol.Name}_Offset;");
                }
                else
                {
                    writer.WriteLine($"{valTypeStr} {propSymbol.Name}_Value = (({valTypeStr}){propSymbol.Name}) << {propSymbol.Name}_Offset;");

                    writer.WriteLine($"if ((({propSymbol.Name}_Value & {propSymbol.Name}_Mask) >> {propSymbol.Name}_Offset) != {propSymbol.Name})");
                    writer.WriteLine("{");
                    writer.Indent++;
                    writer.WriteLine($"throw new Exception($\"'{propSymbol.Name}' exceeded mask value\");");
                    writer.Indent--;
                    writer.WriteLine("}");
                    writer.WriteLine();

                    writer.WriteLine($"result += {propSymbol.Name}_Value;");
                }

                writer.WriteLine();
            }

            writer.WriteLine("return result;");

            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();
        }

        private void GenerateImplicitOperator(
            IndentedTextWriter writer,
            INamedTypeSymbol structSymbol,
            ITypeSymbol structValType)
        {
            var valTypeStr = structValType.ToDisplayString();

            writer.WriteLine($"public static implicit operator {valTypeStr}({structSymbol.Name} value)");
            writer.WriteLine("{");
            writer.Indent++;

            writer.WriteLine("return value.Serialize();");

            writer.Indent--;
            writer.WriteLine("}");

            writer.WriteLine();
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
    }
}