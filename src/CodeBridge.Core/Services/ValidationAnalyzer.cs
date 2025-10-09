using System.Text.RegularExpressions;
using CodeBridge.Core.Models;
using CodeBridge.Core.Models.Configuration;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.Extensions.Logging;

namespace CodeBridge.Core.Services;

/// <summary>
/// Analyzes FluentValidation validators to extract validation rules for TypeScript schema generation.
/// </summary>
public interface IValidationAnalyzer
{
    /// <summary>
    /// Analyzes FluentValidation validator classes to extract validation rules.
    /// </summary>
    Task<List<ValidationRule>> AnalyzeValidationRulesAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Implementation of validation analyzer that extracts FluentValidation rules from C# source code.
/// </summary>
public sealed class ValidationAnalyzer(ILogger<ValidationAnalyzer> logger) : IValidationAnalyzer
{
    private readonly ILogger<ValidationAnalyzer> _logger = logger ?? throw new ArgumentNullException(nameof(logger));

    public async Task<List<ValidationRule>> AnalyzeValidationRulesAsync(
        List<ProjectInfo> projects,
        CancellationToken cancellationToken = default)
    {
        var allRules = new List<ValidationRule>();

        foreach (var project in projects)
        {
            try
            {
                _logger.LogInformation("Analyzing FluentValidation rules in project: {ProjectName}", project.Name);

                var projectDir = Path.GetDirectoryName(project.Path)!;
                var csharpFiles = Directory.GetFiles(projectDir, "*Validator.cs", SearchOption.AllDirectories)
                    .Where(f => !f.Contains("/bin/") && !f.Contains("/obj/"))
                    .ToList();

                foreach (var file in csharpFiles)
                {
                    var fileRules = await AnalyzeValidatorFileAsync(file, cancellationToken);
                    allRules.AddRange(fileRules);
                }

                _logger.LogInformation("Found {Count} validation rules in {ProjectName}",
                    allRules.Count, project.Name);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error analyzing validation rules in project: {ProjectName}", project.Name);
            }
        }

        return allRules;
    }

    private async Task<List<ValidationRule>> AnalyzeValidatorFileAsync(
        string filePath,
        CancellationToken cancellationToken)
    {
        var rules = new List<ValidationRule>();

        try
        {
            var sourceCode = await File.ReadAllTextAsync(filePath, cancellationToken);
            var syntaxTree = CSharpSyntaxTree.ParseText(sourceCode, cancellationToken: cancellationToken);
            var root = await syntaxTree.GetRootAsync(cancellationToken);

            // Find all classes that inherit from AbstractValidator<T>
            var validatorClasses = root.DescendantNodes()
                .OfType<ClassDeclarationSyntax>()
                .Where(c => c.BaseList?.Types.Any(t =>
                    t.Type.ToString().Contains("AbstractValidator")) == true);

            foreach (var validatorClass in validatorClasses)
            {
                var classRules = ExtractValidationRulesFromClass(validatorClass);
                rules.AddRange(classRules);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing validator file: {File}", filePath);
        }

        return rules;
    }

    private List<ValidationRule> ExtractValidationRulesFromClass(ClassDeclarationSyntax validatorClass)
    {
        var rules = new List<ValidationRule>();

        try
        {
            // Extract the validated type from AbstractValidator<T>
            var baseType = validatorClass.BaseList?.Types
                .FirstOrDefault(t => t.Type.ToString().Contains("AbstractValidator"));

            if (baseType?.Type is not GenericNameSyntax genericName)
                return rules;

            var validatedTypeName = genericName.TypeArgumentList.Arguments[0].ToString();

            _logger.LogDebug("Analyzing validator for type: {TypeName}", validatedTypeName);

            // Find constructor and extract RuleFor calls
            var constructor = validatorClass.Members
                .OfType<ConstructorDeclarationSyntax>()
                .FirstOrDefault();

            if (constructor?.Body == null)
                return rules;

            // Find all RuleFor statements
            var ruleForStatements = constructor.Body.Statements
                .OfType<ExpressionStatementSyntax>()
                .Where(IsRuleForStatement);

            foreach (var statement in ruleForStatements)
            {
                var extractedRules = ExtractRuleForValidations(statement, validatedTypeName);
                rules.AddRange(extractedRules);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting validation rules from class: {ClassName}",
                validatorClass.Identifier.ValueText);
        }

        return rules;
    }

    private static bool IsRuleForStatement(ExpressionStatementSyntax statement)
    {
        var statementText = statement.ToString().Trim();
        return statementText.StartsWith("RuleFor(") ||
               statementText.StartsWith("RuleForEach(");
    }

    private List<ValidationRule> ExtractRuleForValidations(
        ExpressionStatementSyntax statement,
        string typeName)
    {
        var rules = new List<ValidationRule>();

        try
        {
            // Extract property name from RuleFor(x => x.PropertyName)
            var propertyName = ExtractPropertyNameFromRuleFor(statement);
            if (string.IsNullOrEmpty(propertyName))
            {
                _logger.LogDebug("Could not extract property name from RuleFor statement");
                return rules;
            }

            // Get all chained validation method calls
            var chainedCalls = new List<InvocationExpressionSyntax>();
            CollectChainedCalls(statement.Expression, chainedCalls);

            foreach (var call in chainedCalls)
            {
                if (call.Expression is not MemberAccessExpressionSyntax memberAccess)
                    continue;

                var methodName = memberAccess.Name.Identifier.ValueText;
                var rule = CreateValidationRule(typeName, propertyName, methodName, call);

                if (rule != null)
                {
                    rules.Add(rule);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting RuleFor validations for type: {TypeName}", typeName);
        }

        return rules;
    }

    private static string? ExtractPropertyNameFromRuleFor(ExpressionStatementSyntax statement)
    {
        // Find the RuleFor invocation
        var ruleForCall = statement.DescendantNodes()
            .OfType<InvocationExpressionSyntax>()
            .FirstOrDefault(inv => inv.Expression switch
            {
                MemberAccessExpressionSyntax ma => ma.Name.Identifier.ValueText == "RuleFor",
                IdentifierNameSyntax id => id.Identifier.ValueText == "RuleFor",
                _ => false
            });

        if (ruleForCall == null)
        {
            // Try string parsing as fallback
            var match = Regex.Match(
                statement.ToString(),
                @"RuleFor\s*\(\s*\w+\s*=>\s*\w+\.(\w+)\s*\)",
                RegexOptions.IgnoreCase);

            return match.Success && match.Groups.Count > 1 ? match.Groups[1].Value : null;
        }

        // Extract from lambda: x => x.PropertyName
        var lambda = ruleForCall.ArgumentList.Arguments.FirstOrDefault()?.Expression;

        if (lambda is SimpleLambdaExpressionSyntax simpleLambda &&
            simpleLambda.Body is MemberAccessExpressionSyntax memberAccess)
        {
            return memberAccess.Name.Identifier.ValueText;
        }

        if (lambda is ParenthesizedLambdaExpressionSyntax parenthesizedLambda &&
            parenthesizedLambda.Body is MemberAccessExpressionSyntax parenthesizedMemberAccess)
        {
            return parenthesizedMemberAccess.Name.Identifier.ValueText;
        }

        return null;
    }

    private static void CollectChainedCalls(SyntaxNode expression, List<InvocationExpressionSyntax> calls)
    {
        if (expression is InvocationExpressionSyntax invocation)
        {
            calls.Add(invocation);

            if (invocation.Expression is MemberAccessExpressionSyntax memberAccess)
            {
                CollectChainedCalls(memberAccess.Expression, calls);
            }
        }
        else if (expression is MemberAccessExpressionSyntax memberAccess)
        {
            CollectChainedCalls(memberAccess.Expression, calls);
        }
    }

    private ValidationRule? CreateValidationRule(
        string typeName,
        string propertyName,
        string methodName,
        InvocationExpressionSyntax call)
    {
        return methodName switch
        {
            "NotEmpty" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "required",
                Value = true,
                ErrorMessage = ExtractErrorMessage(call)
            },
            "NotNull" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "required",
                Value = true,
                ErrorMessage = ExtractErrorMessage(call)
            },
            "EmailAddress" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "email",
                Value = true,
                ErrorMessage = ExtractErrorMessage(call)
            },
            "MaximumLength" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "maxLength",
                Value = ExtractNumericArgument(call),
                ErrorMessage = ExtractErrorMessage(call)
            },
            "MinimumLength" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "minLength",
                Value = ExtractNumericArgument(call),
                ErrorMessage = ExtractErrorMessage(call)
            },
            "Length" => CreateLengthRules(typeName, propertyName, call),
            "Matches" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "pattern",
                Value = ExtractStringArgument(call),
                ErrorMessage = ExtractErrorMessage(call)
            },
            "GreaterThan" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "min",
                Value = ExtractNumericArgument(call),
                ErrorMessage = ExtractErrorMessage(call),
                Metadata = new Dictionary<string, object> { ["exclusive"] = true }
            },
            "GreaterThanOrEqualTo" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "min",
                Value = ExtractNumericArgument(call),
                ErrorMessage = ExtractErrorMessage(call),
                Metadata = new Dictionary<string, object> { ["exclusive"] = false }
            },
            "LessThan" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "max",
                Value = ExtractNumericArgument(call),
                ErrorMessage = ExtractErrorMessage(call),
                Metadata = new Dictionary<string, object> { ["exclusive"] = true }
            },
            "LessThanOrEqualTo" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "max",
                Value = ExtractNumericArgument(call),
                ErrorMessage = ExtractErrorMessage(call),
                Metadata = new Dictionary<string, object> { ["exclusive"] = false }
            },
            "Must" => new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "custom",
                ErrorMessage = ExtractErrorMessage(call)
            },
            _ => null
        };
    }

    private static ValidationRule? CreateLengthRules(string typeName, string propertyName, InvocationExpressionSyntax call)
    {
        var args = call.ArgumentList.Arguments;
        if (args.Count >= 2)
        {
            // Length has min and max: Length(5, 100)
            // For now, we'll return minLength rule and handle maxLength separately
            return new ValidationRule
            {
                TypeName = typeName,
                PropertyName = propertyName,
                RuleType = "minLength",
                Value = ExtractNumericArgument(call, 0),
                ErrorMessage = ExtractErrorMessage(call),
                Metadata = new Dictionary<string, object>
                {
                    ["maxLength"] = ExtractNumericArgument(call, 1) ?? 0
                }
            };
        }

        return null;
    }

    private static string? ExtractErrorMessage(InvocationExpressionSyntax call)
    {
        // Look for chained WithMessage call
        var parent = call.Parent;
        while (parent != null)
        {
            if (parent is InvocationExpressionSyntax withMessageCall &&
                withMessageCall.Expression is MemberAccessExpressionSyntax memberAccess &&
                memberAccess.Name.Identifier.ValueText == "WithMessage")
            {
                return ExtractStringArgument(withMessageCall);
            }
            parent = parent.Parent;
        }

        return null;
    }

    private static object? ExtractNumericArgument(InvocationExpressionSyntax call, int argumentIndex = 0)
    {
        if (call.ArgumentList.Arguments.Count <= argumentIndex)
            return null;

        var arg = call.ArgumentList.Arguments[argumentIndex].Expression;

        if (arg is LiteralExpressionSyntax literal)
        {
            var value = literal.Token.ValueText;
            if (int.TryParse(value, out var intValue))
                return intValue;
            if (double.TryParse(value, out var doubleValue))
                return doubleValue;
        }

        return null;
    }

    private static string? ExtractStringArgument(InvocationExpressionSyntax call)
    {
        var arg = call.ArgumentList.Arguments.FirstOrDefault();
        if (arg?.Expression is LiteralExpressionSyntax literal &&
            literal.Token.IsKind(SyntaxKind.StringLiteralToken))
        {
            return literal.Token.ValueText;
        }

        return null;
    }
}
