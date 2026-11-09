using System.Collections.Generic;

namespace Metamorph.Core.Services;

public sealed record VirtualDesktopInfo(int Count, int Current, IReadOnlyList<string> Names);
