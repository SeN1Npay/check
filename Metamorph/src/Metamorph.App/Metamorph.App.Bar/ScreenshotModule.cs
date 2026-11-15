using System;
using System.IO;
using Metamorph.Core.Localization;

namespace Metamorph.App.Bar;

public sealed class ScreenshotModule : LabeledModule
{
	public override int IntervalTicks => 120;

	public ScreenshotModule(BarContext context)
		: base(context, "")
	{
		Clickable(delegate
		{
			Settings.Open("ms-screenclip:");
		}, OpenFolder);
	}

	private static void OpenFolder()
	{
		string folderPath = Environment.GetFolderPath(Environment.SpecialFolder.MyPictures);
		string text = Path.Combine(folderPath, "Screenshots");
		Settings.Open(Directory.Exists(text) ? text : folderPath);
	}

	public override void Update()
	{
		SetValue("✂");
		base.TextView.ToolTip = Loc.T("Скриншот области (Win+Shift+S). Правый клик — папка снимков");
	}
}
