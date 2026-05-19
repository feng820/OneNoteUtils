# Rewrite from PowerShell to .NET 8+ C# console application

The original POC is a single ~880-line PowerShell script. We're rewriting it as a .NET 8+ console application to gain static typing, testability, and a clean layered architecture.

C# gives us interfaces for layer boundaries, a mature DI container, and xUnit for testing — none of which are practical in a monolithic PowerShell script. .NET 8+ (rather than .NET Framework) gives us modern language features and cross-compilation support, while still supporting the Win32 COM Interop that OneNote requires.
