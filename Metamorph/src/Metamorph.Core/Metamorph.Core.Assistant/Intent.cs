using System;

namespace Metamorph.Core.Assistant;

public sealed record Intent(IntentKind Kind, string? Text = null, double? Number = null, TimeSpan? Duration = null)
{
	public bool IsCommand
	{
		get
		{
			bool flag;
			switch (Kind)
			{
			case IntentKind.Unknown:
			case IntentKind.Greeting:
			case IntentKind.Thanks:
			case IntentKind.HowAreYou:
			case IntentKind.WhoAreYou:
			case IntentKind.Help:
			case IntentKind.Time:
			case IntentKind.Date:
			case IntentKind.Joke:
			case IntentKind.Status:
			case IntentKind.WhatsPlaying:
			case IntentKind.TimerStatus:
			case IntentKind.Weather:
			case IntentKind.Calc:
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			return !flag;
		}
	}

	public bool IsLocalOnly
	{
		get
		{
			IntentKind kind = Kind;
			if ((uint)(kind - 30) <= 1u || (uint)(kind - 38) <= 1u)
			{
				return true;
			}
			return false;
		}
	}
}
