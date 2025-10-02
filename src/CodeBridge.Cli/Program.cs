using System.CommandLine;
using CodeBridge.Cli.Commands;

namespace CodeBridge.Cli;

internal sealed class Program
{
    private static async Task<int> Main(string[] args)
    {
        var rootCommand = new RootCommand("CodeBridge - TypeScript SDK Generator for .NET APIs")
        {
            new InitCommand(),
            new GenerateCommand(),
            new ValidateCommand()
        };

        rootCommand.Description = """
            CodeBridge automatically generates TypeScript/JavaScript SDKs from your .NET APIs.
            
            Simply add [GenerateSdk] attributes to your endpoints and run 'codebridge generate'.
            """;

        return await rootCommand.InvokeAsync(args);
    }
}
