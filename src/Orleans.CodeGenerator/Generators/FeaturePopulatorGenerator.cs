using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Orleans.CodeGenerator.Compatibility;
using Orleans.CodeGenerator.Model;
using Orleans.CodeGenerator.Utilities;
using static Microsoft.CodeAnalysis.CSharp.SyntaxFactory;

namespace Orleans.CodeGenerator.Generators
{
    internal static class FeaturePopulatorGenerator
    {
        internal const string NamespaceName = "OrleansGeneratedCode";
        private const string ClassSuffix = "FeaturePopulator";

        public static (List<AttributeListSyntax>, List<MemberDeclarationSyntax>) GenerateSyntax(WellKnownTypes wellKnownTypes, AggregatedModel model)
        {
            var attributes = new List<AttributeListSyntax>();
            var members = new List<MemberDeclarationSyntax>();
            var className = CodeGenerator.ToolName + Guid.NewGuid().ToString("N").Substring(0, 10) + ClassSuffix;

            // Generate a class for populating the metadata.
            var classSyntax = ClassDeclaration(className)
                .AddBaseListTypes(
                    SimpleBaseType(wellKnownTypes.IFeaturePopulator_1.Construct(wellKnownTypes.GrainInterfaceFeature).ToTypeSyntax()),
                    SimpleBaseType(wellKnownTypes.IFeaturePopulator_1.Construct(wellKnownTypes.GrainClassFeature).ToTypeSyntax()),
                    SimpleBaseType(wellKnownTypes.IFeaturePopulator_1.Construct(wellKnownTypes.SerializerFeature).ToTypeSyntax()))
                .AddModifiers(Token(SyntaxKind.InternalKeyword), Token(SyntaxKind.SealedKeyword))
                .AddMembers(
                    GeneratePopulateMethod(wellKnownTypes, model.GrainInterfaces),
                    GeneratePopulateMethod(wellKnownTypes, model.GrainClasses),
                    GeneratePopulateMethod(wellKnownTypes, model.Serializers))
                .AddAttributeLists(AttributeList(SingletonSeparatedList(GeneratedCodeAttributeGenerator.GetGeneratedCodeAttributeSyntax(wellKnownTypes))));

            var namespaceSyntax = NamespaceDeclaration(NamespaceName.ToIdentifierName()).AddMembers(classSyntax);
            members.Add(namespaceSyntax);

            // Generate an assembly-level attribute with an instance of that class.
            var attribute = AttributeList(
                AttributeTargetSpecifier(Token(SyntaxKind.AssemblyKeyword)),
                SingletonSeparatedList(
                    Attribute(wellKnownTypes.FeaturePopulatorAttribute.ToNameSyntax())
                        .AddArgumentListArguments(AttributeArgument(TypeOfExpression(ParseTypeName(NamespaceName + "." + className))))));
            attributes.Add(attribute);

            return (attributes, members);
        }

        private static MemberDeclarationSyntax GeneratePopulateMethod(WellKnownTypes wellKnownTypes, List<GrainInterfaceDescription> grains)
        {
            var interfaceMethod = wellKnownTypes.IFeaturePopulator_1.Construct(wellKnownTypes.GrainInterfaceFeature).Method("Populate");
            var featureParameter = interfaceMethod.Parameters.First().Name.ToIdentifierName();

            var bodyStatements = new List<StatementSyntax>();
            foreach (var metadata in grains)
            {
                var newMetadataExpression = ObjectCreationExpression(wellKnownTypes.GrainInterfaceMetadata.ToTypeSyntax())
                    .AddArgumentListArguments(
                        Argument(TypeOfExpression(metadata.Type.WithoutTypeParameters().ToTypeSyntax())),
                        Argument(TypeOfExpression(metadata.ReferenceType)),
                        Argument(TypeOfExpression(metadata.InvokerType)),
                        Argument(metadata.InterfaceId.ToHexLiteral()));
                bodyStatements.Add(
                    ExpressionStatement(
                        InvocationExpression(featureParameter.Member("Interfaces").Member("Add"))
                            .AddArgumentListArguments(
                                Argument(newMetadataExpression))));
            }

            return interfaceMethod.GetDeclarationSyntax().AddBodyStatements(bodyStatements.ToArray());
        }

        private static MemberDeclarationSyntax GeneratePopulateMethod(WellKnownTypes wellKnownTypes, List<GrainClassDescription> grains)
        {
            var interfaceMethod = wellKnownTypes.IFeaturePopulator_1.Construct(wellKnownTypes.GrainClassFeature).Method("Populate");
            var featureParameter = interfaceMethod.Parameters.First().Name.ToIdentifierName();

            var bodyStatements = new List<StatementSyntax>();
            foreach (var metadata in grains)
            {
                var newMetadataExpression = ObjectCreationExpression(wellKnownTypes.GrainClassMetadata.ToTypeSyntax())
                    .AddArgumentListArguments(
                        Argument(TypeOfExpression(metadata.Type.WithoutTypeParameters().ToTypeSyntax())));
                bodyStatements.Add(
                    ExpressionStatement(
                        InvocationExpression(featureParameter.Member("Classes").Member("Add"))
                            .AddArgumentListArguments(
                                Argument(newMetadataExpression))));
            }

            return interfaceMethod.GetDeclarationSyntax().AddBodyStatements(bodyStatements.ToArray());
        }

        private static MemberDeclarationSyntax GeneratePopulateMethod(WellKnownTypes wellKnownTypes, SerializationTypeDescriptions typeDescriptions)
        {
            var interfaceMethod = wellKnownTypes.IFeaturePopulator_1.Construct(wellKnownTypes.SerializerFeature).Method("Populate");
            var featureParameter = interfaceMethod.Parameters.First().Name.ToIdentifierName();

            var bodyStatements = new List<StatementSyntax>();
            
            foreach (var serializerType in typeDescriptions.SerializerTypes)
            {
                if (serializerType.SerializerTypeSyntax == null) continue;
                var overrideExisting = serializerType.OverrideExistingSerializer ? SyntaxKind.TrueLiteralExpression : SyntaxKind.FalseLiteralExpression;
                bodyStatements.Add(
                    ExpressionStatement(
                        InvocationExpression(featureParameter.Member("AddSerializerType"))
                            .AddArgumentListArguments(
                                Argument(TypeOfExpression(serializerType.Target.WithoutTypeParameters().ToTypeSyntax())),
                                Argument(TypeOfExpression(serializerType.SerializerTypeSyntax)),
                                Argument(LiteralExpression(overrideExisting)))));
            }
            
            foreach (var knownType in typeDescriptions.KnownTypes)
            {
                bodyStatements.Add(
                    ExpressionStatement(
                        InvocationExpression(featureParameter.Member("AddKnownType"))
                            .AddArgumentListArguments(
                                Argument(RoslynTypeNameFormatter.Format(knownType.Type, RoslynTypeNameFormatter.Style.RuntimeTypeNameFormatter).ToLiteralExpression()),
                                Argument(knownType.TypeKey.ToLiteralExpression()))));
            }

            return interfaceMethod.GetDeclarationSyntax().AddBodyStatements(bodyStatements.ToArray());
        }
    }
}