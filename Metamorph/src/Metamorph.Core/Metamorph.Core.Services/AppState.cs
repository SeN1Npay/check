using System;
using Metamorph.Core.Models;

namespace Metamorph.Core.Services;

public sealed class AppState
{
	public string? CurrentThemeId { get; set; }

	public ThemeProfile? AppliedProfile { get; set; }

	public DateTime? AppliedAt { get; set; }
}
