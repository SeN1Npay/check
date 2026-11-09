using Microsoft.Win32;

namespace Metamorph.Core.Services;

public readonly record struct TrackedValue(string Key, string Name, RegistryValueKind Kind);
