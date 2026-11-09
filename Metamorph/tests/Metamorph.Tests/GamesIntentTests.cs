using Metamorph.Core.Assistant;
using Xunit;

namespace Metamorph.Tests;

public class GamesIntentTests
{
	[Theory]
	[InlineData("давай поиграем", null)]
	[InlineData("Мета, поиграем?", null)]
	[InlineData("хочу играть", null)]
	[InlineData("хочу поиграть", null)]
	[InlineData("let's play", null)]
	[InlineData("игры", null)]
	[InlineData("тетрис", "tetris")]
	[InlineData("сыграем в тетрис", "tetris")]
	[InlineData("play tetris", "tetris")]
	[InlineData("запусти тетрис", "tetris")]
	[InlineData("динозаврик", "dino")]
	[InlineData("давай в дино", "dino")]
	[InlineData("мета-раннер", "dino")]
	[InlineData("play dino", "dino")]
	public void ParsesPlayGame(string text, string? game)
	{
		Intent intent = IntentParser.Parse(text);
		Assert.Equal(IntentKind.PlayGame, intent.Kind);
		Assert.Equal(game, intent.Text);
	}

	[Theory]
	[InlineData("play", IntentKind.PlayPause)]
	[InlineData("играй", IntentKind.PlayPause)]
	[InlineData("открой блокнот", IntentKind.OpenApp)]
	[InlineData("что играет", IntentKind.WhatsPlaying)]
	public void OtherCommandsUnchanged(string text, IntentKind kind)
	{
		Assert.Equal(kind, IntentParser.Parse(text).Kind);
	}
}
