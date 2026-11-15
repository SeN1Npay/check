using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Threading.Tasks;
using Metamorph.Core.Services;
using Microsoft.CSharp.RuntimeBinder;

namespace Metamorph.App.Launcher;

internal static class AppCatalog
{
	[CompilerGenerated]
	private static class _003C_003Eo__9
	{
		public static CallSite<Func<CallSite, object, string, object>> _003C_003Ep__0;

		public static CallSite<Func<CallSite, object, object>> _003C_003Ep__1;

		public static CallSite<Func<CallSite, object, string>> _003C_003Ep__2;

		public static CallSite<Func<CallSite, object, object>> _003C_003Ep__3;

		public static CallSite<Func<CallSite, object, string>> _003C_003Ep__4;

		public static CallSite<Func<CallSite, object, object>> _003C_003Ep__5;

		public static CallSite<Func<CallSite, object, IEnumerable>> _003C_003Ep__6;
	}

	private static readonly TimeSpan MaxAge = TimeSpan.FromMinutes(5.0);

	private static IReadOnlyList<AppEntry> _cache = Array.Empty<AppEntry>();

	private static DateTime _loadedAt;

	private static Task? _loading;

	public static IReadOnlyList<AppEntry> Current => _cache;

	public static Task RefreshAsync()
	{
		Task loading = _loading;
		if (loading != null && !loading.IsCompleted)
		{
			return _loading;
		}
		if (_cache.Count > 0 && DateTime.UtcNow - _loadedAt < MaxAge)
		{
			return Task.CompletedTask;
		}
		_loading = RunSta(Load).ContinueWith(delegate(Task<IReadOnlyList<AppEntry>> t)
		{
			if (t.Exception != null)
			{
				Log.Error("Список приложений", t.Exception);
			}
			else if (t.Result.Count > 0)
			{
				_cache = t.Result;
				_loadedAt = DateTime.UtcNow;
			}
		}, TaskScheduler.Default);
		return _loading;
	}

	private static Task<IReadOnlyList<AppEntry>> RunSta(Func<IReadOnlyList<AppEntry>> work)
	{
		Func<IReadOnlyList<AppEntry>> work2 = work;
		TaskCompletionSource<IReadOnlyList<AppEntry>> tcs = new TaskCompletionSource<IReadOnlyList<AppEntry>>();
		Thread thread = new Thread((ThreadStart)delegate
		{
			try
			{
				tcs.SetResult(work2());
			}
			catch (Exception exception)
			{
				tcs.SetException(exception);
			}
		});
		thread.IsBackground = true;
		thread.Name = "Metamorph.AppCatalog";
		thread.SetApartmentState(ApartmentState.STA);
		thread.Start();
		return tcs.Task;
	}

	private static IReadOnlyList<AppEntry> Load()
	{
		List<AppEntry> list = new List<AppEntry>();
		try
		{
			list.AddRange(FromAppsFolder());
		}
		catch (Exception ex)
		{
			Log.Error("shell:AppsFolder", ex);
		}
		if (list.Count == 0)
		{
			list.AddRange(FromStartMenu());
		}
		return (from g in list.Where((AppEntry a) => a.Name.Length > 0).GroupBy<AppEntry, string>((AppEntry a) => a.Name, StringComparer.OrdinalIgnoreCase)
			select g.First()).OrderBy<AppEntry, string>((AppEntry a) => a.Name, StringComparer.CurrentCultureIgnoreCase).ToList();
	}

	private static IEnumerable<AppEntry> FromAppsFolder()
	{
		Type typeFromProgID = Type.GetTypeFromProgID("Shell.Application");
		if ((object)typeFromProgID == null)
		{
			yield break;
		}
		object arg = Activator.CreateInstance(typeFromProgID);
		if (_003C_003Eo__9._003C_003Ep__0 == null)
		{
			_003C_003Eo__9._003C_003Ep__0 = CallSite<Func<CallSite, object, string, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "NameSpace", null, typeof(AppCatalog), new CSharpArgumentInfo[2]
			{
				CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null),
				CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.UseCompileTimeType | CSharpArgumentInfoFlags.Constant, null)
			}));
		}
		object obj = _003C_003Eo__9._003C_003Ep__0.Target(_003C_003Eo__9._003C_003Ep__0, arg, "shell:AppsFolder");
		if (obj == null)
		{
			yield break;
		}
		if (_003C_003Eo__9._003C_003Ep__5 == null)
		{
			_003C_003Eo__9._003C_003Ep__5 = CallSite<Func<CallSite, object, object>>.Create(Binder.InvokeMember(CSharpBinderFlags.None, "Items", null, typeof(AppCatalog), new CSharpArgumentInfo[1] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) }));
		}
		foreach (object item in (dynamic)_003C_003Eo__9._003C_003Ep__5.Target(_003C_003Eo__9._003C_003Ep__5, obj))
		{
			if (_003C_003Eo__9._003C_003Ep__1 == null)
			{
				_003C_003Eo__9._003C_003Ep__1 = CallSite<Func<CallSite, object, object>>.Create(Binder.GetMember(CSharpBinderFlags.None, "Name", typeof(AppCatalog), new CSharpArgumentInfo[1] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) }));
			}
			string text = (dynamic)(_003C_003Eo__9._003C_003Ep__1.Target(_003C_003Eo__9._003C_003Ep__1, item) ?? "");
			if (_003C_003Eo__9._003C_003Ep__3 == null)
			{
				_003C_003Eo__9._003C_003Ep__3 = CallSite<Func<CallSite, object, object>>.Create(Binder.GetMember(CSharpBinderFlags.None, "Path", typeof(AppCatalog), new CSharpArgumentInfo[1] { CSharpArgumentInfo.Create(CSharpArgumentInfoFlags.None, null) }));
			}
			string text2 = (dynamic)(_003C_003Eo__9._003C_003Ep__3.Target(_003C_003Eo__9._003C_003Ep__3, item) ?? "");
			if (text.Length != 0 && text2.Length != 0 && !text2.StartsWith("http", StringComparison.OrdinalIgnoreCase))
			{
				yield return new AppEntry(text, text2, FromAppsFolder: true);
			}
		}
	}

	private static IEnumerable<AppEntry> FromStartMenu()
	{
		string[] source = new string[2]
		{
			Environment.GetFolderPath(Environment.SpecialFolder.StartMenu),
			Environment.GetFolderPath(Environment.SpecialFolder.CommonStartMenu)
		};
		foreach (string item in source.Where(Directory.Exists))
		{
			IEnumerable<string> enumerable;
			try
			{
				enumerable = Directory.EnumerateFiles(item, "*.lnk", SearchOption.AllDirectories).ToList();
			}
			catch (Exception ex) when (((ex is IOException || ex is UnauthorizedAccessException) ? 1 : 0) != 0)
			{
				continue;
			}
			foreach (string item2 in enumerable)
			{
				string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(item2);
				if (!fileNameWithoutExtension.Contains("uninstall", StringComparison.OrdinalIgnoreCase) && !fileNameWithoutExtension.Contains("удал", StringComparison.OrdinalIgnoreCase))
				{
					yield return new AppEntry(fileNameWithoutExtension, item2, FromAppsFolder: false);
				}
			}
		}
	}
}
