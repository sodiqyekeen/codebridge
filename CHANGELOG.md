````markdown
# CodeBridge Release Notes

## Version 1.0.0-preview.1 (Preview Release - October 2025)

### 🎉 First Preview Release

This is the initial preview release of CodeBridge! We're excited to share this with the community and gather feedback before the stable 1.0.0 release.

### ✨ What's Included

All features planned for 1.0.0 are included in this preview:

- ✅ Attribute-based API endpoint discovery
- ✅ TypeScript SDK generation for React and Next.js
- ✅ CLI tool with init, generate, and validate commands
- ✅ MSBuild integration for automatic generation
- ✅ Complete type mapping (50+ C# to TypeScript mappings)
- ✅ Working examples (Minimal API + React frontend)
- ✅ Comprehensive documentation with DocFX

### 📦 Preview Packages

- **CodeBridge.Core** (v1.0.0-preview.1) - Core abstractions and services
- **CodeBridge.Cli** (v1.0.0-preview.1) - CLI tool
- **CodeBridge.MSBuild** (v1.0.0-preview.1) - MSBuild integration

### 📥 Installation

```bash
# Install CLI tool
dotnet tool install -g CodeBridge.Cli --version 1.0.0-preview.1

# Add MSBuild integration to your project
dotnet add package CodeBridge.MSBuild --version 1.0.0-preview.1
```

### 🧪 Testing & Feedback

This is a preview release for testing and feedback. Please:

1. Try it in your projects
2. Report issues on [GitHub](https://github.com/sodiqyekeen/CodeBridge/issues)
3. Share your feedback and suggestions
4. Check out the examples in `/examples`

### ⚠️ Known Limitations

- This is a preview - APIs may change before 1.0.0 stable
- Feedback and testing needed before stable release

### 🔜 Road to 1.0.0

After gathering feedback from this preview, we'll release the stable 1.0.0 version.

---

## Version 1.0.0 (Planned Stable Release)

### 🎉 Features

#### Core Functionality
- **Attribute-Based Discovery**: Simple `[GenerateSdk]` attribute to mark API endpoints
- **Roslyn-Powered Analysis**: Deep C# code analysis using Microsoft.CodeAnalysis
- **Multi-Framework Support**: React, Next.js, Vue, Angular, and Vanilla TypeScript
- **Type Safety**: Complete TypeScript type generation from C# types

#### Code Generation
- **TypeScript Interfaces**: Auto-generated from C# classes and records
- **TypeScript Enums**: From C# enums with proper value mapping
- **API Client Functions**: Typed HTTP functions for each endpoint
- **Validation Schemas**: Zod schemas from FluentValidation rules
- **React Query Hooks**: useQuery/useMutation hooks for React
- **Next.js Server Actions**: Server-side functions with 'use server' directive

#### Developer Experience
- **CLI Tool**: Interactive command-line interface
  - `codebridge init` - Initialize configuration
  - `codebridge generate` - Generate SDK
  - `codebridge validate` - Validate configuration
- **MSBuild Integration**: Automatic generation during build
- **Watch Mode**: Auto-regenerate on file changes
- **Incremental Generation**: Skip generation if no changes detected
- **Verbose Logging**: Detailed progress information

#### Configuration
- **Flexible Configuration**: JSON-based configuration with IntelliSense
- **Environment Overrides**: Support for environment-specific configs
- **Template System**: Pre-configured templates for popular frameworks
- **Custom Type Mappings**: Override default type mappings

### 📦 Packages

- **CodeBridge.Core** (v1.0.0)
  - Core abstractions and services
  - Source analyzer with Roslyn
  - Type mapper with 50+ type mappings
  - Code generator for all output types

- **CodeBridge.Cli** (v1.0.0)
  - Global .NET tool
  - Interactive CLI commands
  - Watch mode support
  - Configuration validation

- **CodeBridge.MSBuild** (v1.0.0)
  - MSBuild task integration
  - Automatic build-time generation
  - Incremental build support
  - Clean integration

### 🎯 Supported Scenarios

#### API Patterns
- ✅ ASP.NET Core Controllers
- ✅ Minimal APIs
- ✅ Standalone classes with `[GenerateSdk]`

#### Type Mappings
- ✅ Primitives (string, int, bool, etc.)
- ✅ Collections (List, Array, IEnumerable)
- ✅ Dictionaries (Dictionary, Record, Map)
- ✅ Nullable types (T?, Nullable<T>)
- ✅ Generic types (Result<T>, Task<T>)
- ✅ File types (IFormFile, Stream)
- ✅ Custom classes and records
- ✅ Enums with numeric values

#### Validation Rules
- ✅ Required
- ✅ MinLength/MaxLength
- ✅ Email
- ✅ Pattern/Regex
- ✅ Custom error messages

### 🛠️ Technical Details

- **Target Framework**: .NET 9.0
- **Language**: C# 13 with nullable reference types
- **Dependencies**:
  - Microsoft.CodeAnalysis.CSharp 4.14.0
  - System.CommandLine 2.0.0-beta4.22272.1
  - Microsoft.Extensions.Logging 9.0.0
- **Package Management**: Central Package Management (CPM)

### 📝 Configuration Options

#### Source Options
- Solution file path or explicit project paths
- Include/exclude patterns for project discovery
- Namespace filtering

#### Output Options
- Output directory path
- Package name and version
- Author, description, license
- Clean before generate

#### Target Options
- Framework selection (React, Next.js, Vue, Angular, Vanilla)
- Language (TypeScript/JavaScript)
- Module system (ESM/CommonJS)

#### Feature Options
- Include validation schemas
- Generate React hooks
- Generate Next.js helpers
- Custom type mappings

#### Generation Options
- Generation mode (Manual/Auto)
- Build event timing (BeforeBuild/AfterBuild)
- Incremental generation

### 🚀 Getting Started

1. **Install CLI Tool**:
   ```bash
   dotnet tool install -g CodeBridge.Cli
   ```

2. **Initialize Project**:
   ```bash
   codebridge init
   ```

3. **Mark Endpoints**:
   ```csharp
   [GenerateSdk(Summary = "Get all products", Group = "products")]
   public async Task<Result<List<Product>>> GetAll() { }
   ```

4. **Generate SDK**:
   ```bash
   codebridge generate
   ```

### 📚 Documentation

- GitHub Repository: https://github.com/sodiqyekeen/CodeBridge
- Documentation: README.md
- Examples: See `/examples` folder

### 🐛 Known Issues

None in initial release.

### 🔮 Future Enhancements

- Solution file parsing
- GraphQL support
- OpenAPI/Swagger integration
- More validation libraries
- Custom template support
- Visual Studio extension

### 🙏 Acknowledgments

Built with:
- Microsoft.CodeAnalysis (Roslyn)
- System.CommandLine
- Microsoft.Extensions.Logging

---

**Thank you for using CodeBridge!** 🎉
