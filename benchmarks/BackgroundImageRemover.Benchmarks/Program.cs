using BenchmarkDotNet.Running;

// Run everything:   dotnet run -c Release
// Run a subset:    dotnet run -c Release -- --filter *BlendByMask*
BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
