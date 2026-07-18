# Contributing to DeployKit

Thank you for considering contributing! Here's how to get started.

## Code of Conduct

This project adheres to the [Contributor Covenant Code of Conduct](CODE_OF_CONDUCT.md). By participating, you agree to uphold it.

## How to Contribute

### 1. Report a Bug

Open an [issue](https://github.com/dahakabdellah12/DeployKit/issues/new?template=bug_report.md) with:
- A clear title and description
- Steps to reproduce
- Expected vs actual behavior
- Environment (OS, .NET version, browser)

### 2. Suggest a Feature

Open a [feature request](https://github.com/dahakabdellah12/DeployKit/issues/new?template=feature_request.md) with:
- A clear title and description
- Use case and motivation
- Any relevant examples

### 3. Submit a Pull Request

1. Fork the repo and create your branch: `git checkout -b feature/my-feature`
2. Make your changes
3. Ensure the build passes: `dotnet build`
4. Commit with a clear message
5. Push and open a PR

## Development Setup

```bash
git clone git@github.com:dahakabdellah12/DeployKit.git
cd DeployKit
dotnet restore
dotnet build
dotnet run --project DeployKit.Cloud.Api
```

Open `http://localhost:5000` in your browser.

## Guidelines

- Keep pull requests focused — one feature / fix per PR
- Follow existing code style (no comments, dark mode, Arabic labels)
- Update documentation if you change behavior
- Ensure no secrets or tokens are committed
