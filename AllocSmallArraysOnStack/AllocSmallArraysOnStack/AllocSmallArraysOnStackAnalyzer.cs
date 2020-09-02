using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FindSymbols;

namespace AllocSmallArraysOnStack
{
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
    public class AllocSmallArraysOnStackAnalyzer : DiagnosticAnalyzer
    {
        public const string DiagnosticId = "AllocSmallArraysOnStack";
        const int SizeThreshold = 1024;
        private static readonly LocalizableString Title = new LocalizableResourceString(nameof(Resources.AnalyzerTitle), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString MessageFormat = new LocalizableResourceString(nameof(Resources.AnalyzerMessageFormat), Resources.ResourceManager, typeof(Resources));
        private static readonly LocalizableString Description = new LocalizableResourceString(nameof(Resources.AnalyzerDescription), Resources.ResourceManager, typeof(Resources));
        private const string Category = "Performance";

        private static readonly DiagnosticDescriptor Rule = new DiagnosticDescriptor(DiagnosticId, Title, MessageFormat, Category, DiagnosticSeverity.Warning, isEnabledByDefault: true, description: Description);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get { return ImmutableArray.Create(Rule); } }

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.RegisterSyntaxNodeAction(AnalyzeSymbol, SyntaxKind.ArrayCreationExpression);
        }

        static int GetSize(string name, int size)
        {
            switch (name)
            {
                case "Byte":
                case "SByte":
                case "Bool":
                    return sizeof(bool) * size;
                case "UInt16":
                case "Int16":
                case "Half":
                    return sizeof(short) * size;
                case "UInt32":
                case "Int32":
                case "Float":
                    return sizeof(int) * size;
                case "UInt64":
                case "Int64":
                case "Double":
                    return sizeof(long) * size;
                default:
                    return int.MaxValue; //Prevent triggering by making array to large for unknown types
            }
        }
        static bool IsConstant(ExpressionSyntax syntax, SyntaxNodeAnalysisContext context)
        {
            return context.SemanticModel.GetSymbolInfo(syntax).Symbol is IFieldSymbol info && info.IsConst;
        }

        static int GetConstantValue(SemanticModel model, ExpressionSyntax syntax)
        {
            if (syntax is LiteralExpressionSyntax literalSyntax)
                return (int)literalSyntax.Token.Value;
            return (int)(model.GetSymbolInfo(syntax).Symbol as IFieldSymbol).ConstantValue;
        }

        private static void AnalyzeSymbol(SyntaxNodeAnalysisContext context)
        {
            if (context.Compilation.Options is CSharpCompilationOptions opt && !opt.AllowUnsafe)
                return;

            var creationExpression = context.Node as ArrayCreationExpressionSyntax;
            var method = creationExpression.FirstAncestorOrSelf<SyntaxNode>(x => x is MethodDeclarationSyntax);
            if (method is null)
                return;
            if (method is MethodDeclarationSyntax methodDeclaration && methodDeclaration.Modifiers.Any(mod => mod.IsKind(SyntaxKind.AsyncKeyword)))
                return;

            var type = context.SemanticModel.GetTypeInfo(creationExpression.Type).Type as IArrayTypeSymbol;
            if (type is null)
                return;

            if (creationExpression.FirstAncestorOrSelf<ForEachStatementSyntax>() != null
                || creationExpression.FirstAncestorOrSelf<ForStatementSyntax>() != null)
                return;

            if (creationExpression.Type.RankSpecifiers.Count <= 1)
            {
                int arraySize;
                if(creationExpression.Type.RankSpecifiers.Single().Sizes.Single() is OmittedArraySizeExpressionSyntax)
                {
                    arraySize = creationExpression.Initializer.Expressions.Count;
                }
                else
                {
                    var exp = creationExpression.Type.RankSpecifiers.Single().Sizes.Single();
                    if (!(exp is LiteralExpressionSyntax || IsConstant(exp, context)))
                        return;
                    arraySize = GetConstantValue(context.SemanticModel, exp);
                    
                }
                if (GetSize(type.ElementType.Name, arraySize) > SizeThreshold)
                    return;
                var isVariableDeclaration = creationExpression.FirstAncestorOrSelf<VariableDeclarationSyntax>();
                if (isVariableDeclaration == null)
                    return; // May escape, this way we only capture variables assigned to a local
                if ((method as MethodDeclarationSyntax).Body.Statements.OfType<ReturnStatementSyntax>()
                    .Where(ret => ret.Expression is IdentifierNameSyntax id
                        && id.Identifier.ValueText.Equals(isVariableDeclaration.Variables.First().Identifier.ValueText)).Any())
                    return;
                var variableName = isVariableDeclaration.Variables.First().Identifier.ValueText;
               var isPassedAsParameter = (method as MethodDeclarationSyntax).Body.DescendantNodes().OfType<InvocationExpressionSyntax>()
                    .Where(invocation => invocation.ArgumentList.Arguments.Any(arg =>
                                                                                    arg.Expression is IdentifierNameSyntax id
                        && id.Identifier.ValueText.Equals(variableName))).Any();
                if (isPassedAsParameter)
                    return;

                var isPassedToConstructor = (method as MethodDeclarationSyntax).Body.DescendantNodes().OfType<ObjectCreationExpressionSyntax>()
                    .Where(invocation => invocation.ArgumentList.Arguments.Any(arg =>
                                                                                    arg.Expression is IdentifierNameSyntax id
                        && id.Identifier.ValueText.Equals(isVariableDeclaration.Variables.First().Identifier.ValueText))).Any();
                if (isPassedToConstructor)
                    return;
                var isWrittenOutside = (method as MethodDeclarationSyntax).Body.DescendantNodes().OfType<AssignmentExpressionSyntax>()
                   .Where(invocation => invocation.Right is IdentifierNameSyntax id
                       && id.Identifier.ValueText.Equals(isVariableDeclaration.Variables.First().Identifier.ValueText)).Any();
                if (isWrittenOutside)
                    return;
            }

            var diagnostic = Diagnostic.Create(Rule, creationExpression.Parent.GetLocation(), creationExpression);

            context.ReportDiagnostic(diagnostic);
        }
    }
}
