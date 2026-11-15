using System;
using System.Collections.Generic;

namespace Metamorph.Core.Services;

public sealed record ReleaseInfo(string Tag, Version Version, string Notes, string HtmlUrl, IReadOnlyList<ReleaseAsset> Assets);
