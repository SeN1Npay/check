using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Metamorph.Core.Services;

public sealed record PrivacySnapshot(IReadOnlyList<string> Microphone, IReadOnlyList<string> Camera)
{
	public bool Any
	{
		get
		{
			if (Microphone.Count <= 0)
			{
				return Camera.Count > 0;
			}
			return true;
		}
	}

	public static readonly PrivacySnapshot Empty = new PrivacySnapshot(Array.Empty<string>(), Array.Empty<string>());

	public (IReadOnlyList<string> Microphone, IReadOnlyList<string> Camera) StartedSince(PrivacySnapshot before)
	{
		return (Microphone: Microphone.Except<string>(before.Microphone, StringComparer.OrdinalIgnoreCase).ToList(), Camera: Camera.Except<string>(before.Camera, StringComparer.OrdinalIgnoreCase).ToList());
	}

	public bool SameAs(PrivacySnapshot other)
	{
		if (Microphone.SequenceEqual<string>(other.Microphone, StringComparer.OrdinalIgnoreCase))
		{
			return Camera.SequenceEqual<string>(other.Camera, StringComparer.OrdinalIgnoreCase);
		}
		return false;
	}

	[CompilerGenerated]
	private PrivacySnapshot(PrivacySnapshot original)
	{
		Microphone = original.Microphone;
		Camera = original.Camera;
	}
}
