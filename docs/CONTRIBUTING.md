# Contributing to ACC RPM Monitor

Thank you for your interest in contributing to ACC RPM Monitor! This document provides guidelines and instructions for contributing to the project.

## Getting Started

### Prerequisites
- Windows operating system
- .NET 8.0 SDK or later
- Visual Studio 2022, Visual Studio Code, or your preferred C# IDE
- Git

### Setting Up Your Environment

1. Fork the repository on GitHub
2. Clone your fork locally:
   ```bash
   git clone https://github.com/YOUR_USERNAME/ACC-RPM-Monitor.git
   cd ACC-RPM-Monitor
   ```
3. Add the upstream repository:
   ```bash
   git remote add upstream https://github.com/HaydenJM/ACC-RPM-Monitor.git
   ```
4. Build the project:
   ```bash
   dotnet build
   ```

## Development Workflow

### Creating a Branch

Create a new branch for your feature or bugfix:
```bash
git checkout -b feature/your-feature-name
```

Use descriptive branch names:
- `feature/` for new features
- `fix/` for bug fixes
- `docs/` for documentation changes
- `refactor/` for code refactoring

### Making Changes

1. Make your changes in your feature branch
2. Test your changes thoroughly with ACC
3. Follow the code style guidelines (see below)
4. Commit your changes with clear, descriptive messages:
   ```bash
   git commit -m "Brief description of changes"
   ```

### Code Style Guidelines

- **Naming Conventions**:
  - Classes and public methods: PascalCase
  - Private methods and fields: camelCase or _camelCase for fields
  - Constants: PascalCase or UPPER_CASE

- **Formatting**:
  - Use 4 spaces for indentation (not tabs)
  - Keep lines reasonably short (under 120 characters when possible)
  - Use meaningful variable names

- **Comments**:
  - Add comments for complex logic
  - Use XML documentation comments for public APIs
  - Keep comments up-to-date with code changes

### Testing

Before submitting a pull request:
1. Test your changes with actual ACC gameplay
2. Test both default and custom configurations
3. Test vehicle switching and configuration modes
4. Verify audio feedback works correctly
5. Test on different audio devices if possible

## Submitting Changes

### Push to Your Fork

```bash
git push origin feature/your-feature-name
```

### Create a Pull Request

1. Go to the original repository on GitHub
2. Click "New Pull Request"
3. Select your branch as the compare branch
4. Fill in the PR description with:
   - What changes you made
   - Why you made these changes
   - How to test the changes
   - Any related issues

### PR Title Format

Use clear, descriptive titles:
- `Add [feature name]` for new features
- `Fix [issue description]` for bug fixes
- `Improve [component]` for improvements
- `Refactor [component]` for refactoring

## Reporting Issues

### Bug Reports

When reporting bugs, include:
1. ACC version
2. Vehicle(s) affected
3. Steps to reproduce
4. Expected behavior
5. Actual behavior
6. Console output or error messages if applicable
7. Your configuration (if relevant)

### Feature Requests

For feature requests, explain:
1. What you want to add
2. Why you think it would be useful
3. How you envision it working
4. Any potential implementation approaches

## Architecture Overview

### Main Components

- **ConfigUI.cs**: Console menu system and user interface
- **Program.cs**: Main application loop and workflow orchestration
- **ConfigManager.cs**: Configuration persistence and management
- **ACCSharedMemorySimple.cs**: Telemetry reading from ACC shared memory
- **DynamicAudioEngine.cs**: Audio synthesis and feedback system
- **OptimalShiftAnalyzer.cs**: Shift point calculation and analysis
- **AutoConfigWorkflow.cs**: Automated configuration workflow
- **ShiftPatternAnalyzer.cs**: Shift detection and lap performance tracking
- **PerformanceLearningEngine.cs**: Machine learning shift optimization

### Key Interfaces

- **Configuration System**: GearRPMConfig, ConfigManager
- **Telemetry Reading**: ACCSharedMemorySimple
- **Audio System**: DynamicAudioEngine, TriangleWaveProvider
- **Analysis Engines**: OptimalShiftAnalyzer, ShiftPatternAnalyzer, PerformanceLearningEngine

## Before Merging

Your PR should:
1. Have clear, descriptive commit messages
2. Follow the code style guidelines
3. Include necessary documentation updates
4. Work correctly with the latest ACC game version
5. Not break existing functionality

## Documentation

If your changes affect user-facing functionality:
1. Update README.md with new features or changes
2. Update CHANGELOG.md with a new version entry
3. Update the Help menu in ConfigUI.cs if applicable
4. Add comments to complex code

## Questions?

If you have questions about contributing:
1. Check existing issues and pull requests
2. Open a new issue with your question
3. Review the code comments and documentation

## Code of Conduct

- Be respectful and constructive
- Focus on the code, not the person
- Avoid controversial or offensive language
- Help other contributors improve their work

---

Thank you for contributing to ACC RPM Monitor!
