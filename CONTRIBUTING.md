# Contributing to LibreMetaverse

Thank you for your interest in contributing to LibreMetaverse! This guide will help you get started.

## Getting Started

1. **Fork the repository** on GitHub
2. **Clone your fork** locally:
   ```bash
   git clone https://github.com/YOUR-USERNAME/libremetaverse.git
   cd libremetaverse
   ```
3. **Create a branch** for your changes:
   ```bash
   git checkout -b feature/my-new-feature
   ```

## Development Setup

### Prerequisites

- .NET SDK 8.0 or 9.0
- Git
- A code editor (Visual Studio, VS Code, Rider, etc.)

### Building

```bash
dotnet restore
dotnet build
```

### Running Tests

```bash
dotnet test
```

## Code Style

- Follow existing code conventions in the project
- Use meaningful variable and method names
- Add XML documentation comments for public APIs
- Keep lines under 120 characters where practical
- Use 4 spaces for indentation (no tabs)

### C# Conventions

```csharp
// Good
public class MyClass
{
    private int myField;
    
    public int MyProperty { get; set; }
    
    public void MyMethod()
    {
        // Implementation
    }
}
```

## What to Contribute

### Great First Contributions

- ?? **Bug fixes** - Fix issues labeled "good first issue"
- ?? **Documentation** - Improve README files, add code comments, write tutorials
- ? **Examples** - Add new example applications demonstrating library features
- ?? **Tools** - Create utilities for working with LibreMetaverse
- ?? **Tests** - Add unit tests to improve coverage

### Larger Contributions

- New features (discuss in an issue first)
- Performance improvements
- API enhancements
- Protocol updates

## Contribution Process

1. **Check existing issues** - Someone might already be working on it
2. **Open an issue** - Discuss your idea before starting large changes
3. **Write code** - Implement your changes
4. **Add tests** - Ensure your changes work correctly
5. **Update documentation** - Keep docs in sync with code
6. **Submit a pull request** - Describe your changes clearly

## Pull Request Guidelines

### Before Submitting

- ? Code builds successfully
- ? All tests pass
- ? No new compiler warnings
- ? Documentation updated if needed
- ? Commit messages are clear

### PR Description Should Include

- What the PR does
- Why the change is needed
- Any breaking changes
- Related issue numbers (e.g., "Fixes #123")

### Example PR Template

```markdown
## Description
Brief description of what this PR does

## Motivation
Why this change is needed

## Changes
- Change 1
- Change 2

## Testing
How you tested the changes

## Related Issues
Fixes #123
```

## Adding Examples

Examples are a great way to contribute! They help others learn the library.

### Example Guidelines

1. **Focus** - Demonstrate one or two concepts clearly
2. **Simplicity** - Keep code simple and well-commented
3. **Documentation** - Include a README explaining what it does
4. **Dependencies** - Minimize external dependencies
5. **Cross-platform** - Target .NET 8.0/9.0 for cross-platform support

### Example Structure

```
Programs/examples/YourExample/
??? YourExample.csproj
??? YourExample.cs
??? README.md
```

See existing examples in `Programs/examples/` for reference.

## Adding Tools

Tools are standalone utilities for working with LibreMetaverse data.

### Tool Guidelines

1. **Single purpose** - Tools should do one thing well
2. **CLI-focused** - Command-line interface preferred
3. **Help text** - Include usage instructions
4. **Error handling** - Graceful error messages
5. **Exit codes** - Return 0 for success, 1 for errors

### Tool Structure

```
Programs/tools/YourTool/
??? YourTool.csproj
??? YourTool.cs
??? README.md
```

See existing tools in `Programs/tools/` for reference.

## Reporting Issues

### Bug Reports Should Include

- LibreMetaverse version
- .NET version (`dotnet --version`)
- Operating system
- Steps to reproduce
- Expected vs actual behavior
- Error messages or stack traces

### Feature Requests Should Include

- Use case - What problem does it solve?
- Proposed API or implementation approach
- Why existing features don't work
- Willingness to implement it yourself

## Code Review Process

1. Maintainers will review your PR
2. They may request changes
3. Make requested updates
4. Once approved, your PR will be merged

## Licensing

By contributing, you agree that your contributions will be licensed under the BSD 3-Clause License, matching the project's license.

## Questions?

- Open an issue for questions about contributing
- Check existing issues and PRs for similar discussions
- Be patient - maintainers are volunteers

## Recognition

All contributors are recognized in the project:
- Your name in git history
- Listed in GitHub Contributors
- Mentioned in release notes for significant contributions

Thank you for contributing to LibreMetaverse! ??
