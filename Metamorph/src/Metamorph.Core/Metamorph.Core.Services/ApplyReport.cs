using System;
using System.Collections.Generic;
using System.Linq;
using Metamorph.Core.Localization;

namespace Metamorph.Core.Services;

public sealed class ApplyReport
{
	public string Title { get; }

	public List<string> Done { get; } = new List<string>();


	public List<string> Failed { get; } = new List<string>();


	public List<string> Notes { get; } = new List<string>();


	public bool Success => Failed.Count == 0;

	public ApplyReport(string title)
	{
		Title = title;
	}

	public void Run(string step, Action action)
	{
		try
		{
			action();
			Done.Add(step);
		}
		catch (Exception ex)
		{
			Failed.Add(step + ": " + ex.Message);
			Log.Error("[" + Title + "] " + step, ex);
		}
	}

	public string Summary()
	{
		string text = (Success ? Loc.F("{0}: готово ({1} шагов).", Title, Done.Count) : (Loc.F("{0}: выполнено {1}, ошибок {2}.", Title, Done.Count, Failed.Count) + Environment.NewLine + string.Join(Environment.NewLine, Failed)));
		List<string> list = Notes.Where((string n) => !string.IsNullOrWhiteSpace(n)).ToList();
		if (list.Count != 0)
		{
			return text + Environment.NewLine + string.Join(Environment.NewLine, list);
		}
		return text;
	}
}
