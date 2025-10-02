# Contributing to CodeBridge

Thank you for your interest in contributing to CodeBridge! This document provides guidelines and instructions for contributing.

## Ways to Contribute

- 🐛 **Report bugs** - Open an issue describing the bug
- 💡 **Suggest features** - Open an issue with your feature request
- 📝 **Improve documentation** - Submit PRs for documentation improvements
- 🔧 **Fix issues** - Pick an issue and submit a PR
- ✨ **Add features** - Implement new functionality

## Development Setup

### Prerequisites

- .NET 9.0 SDK or later
- Git
- Your favorite IDE (VS Code, Visual Studio, Rider)

### Getting Started

1. **Fork the repository**
   ```bash
   # Click "Fork" button on GitHub
   ```

2. **Clone your fork**
   ```bash
   git clone https://github.com/YOUR_USERNAME/CodeBridge.git
   cd CodeBridge
   ```

3. **Add upstream remote**
   ```bash
   git remote add upstream https://github.com/ORIGINAL_OWNER/CodeBridge.git
   ```

4. **Create a branch**
   ```bash
   git checkout -b feature/your-feature-name
   ```

5. **Build the project**
   ```bash
   dotnet restore
   dotnet build
   ```

6. **Run tests**
   ```bash
   dotnet test
   ```

## Making Changes

### Code Guidelines

1. **Follow C# conventions**
   - Use meaningful variable names
   - Add XML documentation for public APIs
   - Keep methods focused and small

2. **Write tests**
   - Add unit tests for new features
   - Ensure existing tests pass
   - Aim for 80%+ code coverage

3. **Update documentation**
   - Update XML comments
   - Update relevant markdown docs
   - Add examples if applicable

### Documentation Guidelines

1. **Writing Style**
   - Use clear, concise language
   - Include code examples
   - Add "Why" explanations, not just "How"

2. **Building Docs Locally**
   ```bash
   ./build-docs.sh
   docfx serve _site
   ```

3. **Documentation Structure**
   - `docs/getting-started/` - Introductory guides
   - `docs/guides/` - Detailed feature documentation
   - `docs/examples/` - Framework-specific examples

### Commit Messages

Follow [Conventional Commits](https://www.conventionalcommits.org/):

```
feat: add support for Vue.js SDK generation
fix: resolve type mapping issue with nullable types
docs: update installation guide
test: add tests for TypeMapper
chore: update dependencies
```

**Types:**
- `feat` - New feature
- `fix` - Bug fix
- `docs` - Documentation changes
- `test` - Test changes
- `chore` - Maintenance tasks
- `refactor` - Code refactoring
- `perf` - Performance improvements

## Pull Request Process

### Before Submitting

- [ ] Code builds successfully
- [ ] All tests pass
- [ ] New tests added for new features
- [ ] Documentation updated
- [ ] XML comments added/updated
- [ ] No compiler warnings

### Submitting PR

1. **Push your changes**
   ```bash
   git push origin feature/your-feature-name
   ```

2. **Open Pull Request**
   - Go to GitHub and click "New Pull Request"
   - Select your fork and branch
   - Fill out the PR template

3. **PR Title Format**
   ```
   feat: Add Vue.js example documentation
   fix: Resolve nullable type mapping issue
   ```

4. **PR Description Template**
   ```markdown
   ## Description
   Brief description of changes
   
   ## Type of Change
   - [ ] Bug fix
   - [ ] New feature
   - [ ] Documentation update
   - [ ] Breaking change
   
   ## Testing
   - [ ] Unit tests added/updated
   - [ ] Manual testing completed
   
   ## Checklist
   - [ ] Code builds without warnings
   - [ ] Tests pass
   - [ ] Documentation updated
   - [ ] XML comments added
   ```

### Review Process

1. Automated checks will run (build, tests, docs)
2. Maintainers will review your PR
3. Address any feedback
4. Once approved, PR will be merged

## Project Structure

```
CodeBridge/
├── src/
│   ├── CodeBridge.Core/       # Core types and services
│   ├── CodeBridge.Cli/        # CLI implementation
│   └── CodeBridge.MSBuild/    # MSBuild integration
├── tests/
│   └── CodeBridge.Tests/      # Unit tests
├── docs/                      # Documentation source
├── .github/
│   └── workflows/             # GitHub Actions
└── artifacts/                 # Build outputs
```

## Running Tests

```bash
# Run all tests
dotnet test

# Run specific test
dotnet test --filter "FullyQualifiedName~TypeMapperTests"

# Run with coverage
dotnet test --collect:"XPlat Code Coverage"
```

## Building Documentation

```bash
# Build docs
./build-docs.sh

# Serve locally
docfx serve _site --port 8080

# Open in browser
open http://localhost:8080
```

## Common Tasks

### Adding a New Feature

1. Create issue describing the feature
2. Wait for approval from maintainers
3. Create branch: `feature/feature-name`
4. Implement feature with tests
5. Update documentation
6. Submit PR

### Fixing a Bug

1. Create issue describing the bug (if not exists)
2. Create branch: `fix/bug-name`
3. Write failing test demonstrating bug
4. Fix the bug
5. Ensure test passes
6. Submit PR

### Updating Documentation

1. Create branch: `docs/what-you-are-documenting`
2. Make changes in `docs/` folder
3. Build locally to verify
4. Submit PR

## Release Process

Releases are managed by maintainers:

1. Version bump in `Directory.Build.props`
2. Update `CHANGELOG.md`
3. Create release tag
4. Publish to NuGet
5. Deploy documentation
6. Create GitHub release

## Getting Help

- 💬 **Discussions** - Use GitHub Discussions for questions
- 🐛 **Issues** - Report bugs via GitHub Issues
- 📧 **Email** - Contact maintainers for private matters

## Code of Conduct

Be respectful, inclusive, and professional. We follow the [Contributor Covenant Code of Conduct](https://www.contributor-covenant.org/version/2/1/code_of_conduct/).

## License

By contributing, you agree that your contributions will be licensed under the MIT License.

---

Thank you for contributing to CodeBridge! 🚀
