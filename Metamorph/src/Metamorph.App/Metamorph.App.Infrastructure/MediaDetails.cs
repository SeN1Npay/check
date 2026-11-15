using System;

namespace Metamorph.App.Infrastructure;

public sealed record MediaDetails(string Title, string Artist, string Album, bool Playing, string App, TimeSpan Position, TimeSpan Duration)
{
	public string Key => App + "|" + Artist + "|" + Title;
}
