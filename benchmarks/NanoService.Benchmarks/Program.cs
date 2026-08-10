using BenchmarkDotNet.Running;
using NanoService.Benchmarks;

if (args.Length > 0 && args[0].Equals("load", StringComparison.OrdinalIgnoreCase))
{
    await LoadScenarios.RunAsync(args.Skip(1).ToArray());
    return;
}

BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args);
