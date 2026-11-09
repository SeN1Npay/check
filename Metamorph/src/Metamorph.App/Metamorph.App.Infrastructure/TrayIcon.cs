using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;
using Metamorph.Core.Wallpaper;

namespace Metamorph.App.Infrastructure;

internal sealed class TrayIcon : IDisposable
{
	private readonly App _app;

	private readonly NotifyIcon _icon;

	private ToolStripMenuItem _themesMenu = new ToolStripMenuItem("Темы");

	private bool _hintShown;

	private ToolStripMenuItem? _wallpaperMenu;

	private ToolStripMenuItem? _mascotItem;

	public TrayIcon(App app)
	{
		_app = app;
		_icon = new NotifyIcon
		{
			Text = "Metamorph",
			Icon = LoadIcon(),
			Visible = true,
			ContextMenuStrip = BuildMenu()
		};
		_icon.DoubleClick += delegate
		{
			_app.ShowMainWindow();
		};
	}

	public void ShowHint(string title, string text)
	{
		_icon.BalloonTipTitle = title;
		_icon.BalloonTipText = text;
		_icon.ShowBalloonTip(3000);
	}

	public void ShowHintOnce(string title, string text)
	{
		if (!_hintShown)
		{
			_hintShown = true;
			ShowHint(title, text);
		}
	}

	public void Rebuild()
	{
		ContextMenuStrip? contextMenuStrip = _icon.ContextMenuStrip;
		_icon.ContextMenuStrip = BuildMenu();
		contextMenuStrip?.Dispose();
	}

	private ContextMenuStrip BuildMenu()
	{
		ContextMenuStrip contextMenuStrip = new ContextMenuStrip();
		_themesMenu = new ToolStripMenuItem(Loc.T("Темы"));
		contextMenuStrip.Items.Add(Loc.T("Открыть Metamorph"), null, delegate
		{
			_app.ShowMainWindow();
		});
		contextMenuStrip.Items.Add(Loc.T("Лаунчер"), null, delegate
		{
			_app.ShowLauncher();
		});
		contextMenuStrip.Items.Add(_themesMenu);
		contextMenuStrip.Items.Add(Loc.T("Показать/скрыть панель"), null, delegate
		{
			_app.Session.ToggleBar();
		});
		contextMenuStrip.Items.Add(Loc.T("Показать/скрыть док"), null, delegate
		{
			_app.Session.ToggleDock();
		});
		contextMenuStrip.Items.Add(Loc.T("Показать остров"), null, delegate
		{
			_app.Session.SummonIsland();
		});
		_wallpaperMenu = new ToolStripMenuItem(Loc.T("Обои"));
		contextMenuStrip.Items.Add(_wallpaperMenu);
		_mascotItem = new ToolStripMenuItem(Loc.T("Мета-тян"), null, delegate
		{
			_app.ToggleMascot();
		});
		contextMenuStrip.Items.Add(_mascotItem);
		contextMenuStrip.Items.Add(Loc.T("Спросить Мету…"), null, delegate
		{
			_app.ShowAssistant();
		});
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(Loc.T("Вернуть исходный вид Windows"), null, async delegate
		{
			await _app.RestoreOriginalAsync();
		});
		contextMenuStrip.Items.Add(Loc.T("Удалённая помощь (для ИИ)…"), null, delegate
		{
			App.StartRemoteHelp();
		});
		contextMenuStrip.Items.Add(new ToolStripSeparator());
		contextMenuStrip.Items.Add(Loc.T("Выход"), null, delegate
		{
			_app.ExitApp();
		});
		contextMenuStrip.Opening += delegate
		{
			RebuildThemes();
			RebuildWallpapers();
			_mascotItem.Checked = _app.IsMascotVisible;
		};
		return contextMenuStrip;
	}

	private void RebuildWallpapers()
	{
		if (_wallpaperMenu == null)
		{
			return;
		}
		_wallpaperMenu.DropDownItems.Clear();
		_wallpaperMenu.DropDownItems.Add(Loc.T("Следующие"), null, delegate
		{
			_app.NextWallpaper();
		});
		_wallpaperMenu.DropDownItems.Add(_app.Session.IsWallpaperPaused ? Loc.T("Продолжить") : Loc.T("Пауза"), null, delegate
		{
			_app.ToggleWallpaperPause();
		});
		ToolStripMenuItem value = new ToolStripMenuItem(Loc.T("Обои темы"), null, async delegate
		{
			await _app.ApplyWallpaperChoiceAsync(null);
		})
		{
			Checked = (_app.WallpaperOverride == null)
		};
		_wallpaperMenu.DropDownItems.Add(value);
		_wallpaperMenu.DropDownItems.Add(new ToolStripSeparator());
		WallpaperChoice current = _app.WallpaperOverride;
		IReadOnlyList<string> readOnlyList;
		try
		{
			readOnlyList = _app.Library.List();
		}
		catch
		{
			readOnlyList = Array.Empty<string>();
		}
		foreach (string item in readOnlyList.Take(15))
		{
			string file = item;
			string fileName = Path.GetFileName(file.TrimEnd('\\', '/'));
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(WallpaperLibrary.MediaOf(file) switch
			{
				WallpaperMedia.Video => "▶", 
				WallpaperMedia.Gif => "\ud83c\udf9e", 
				WallpaperMedia.Folder => "\ud83d\udcc1", 
				_ => "\ud83d\uddbc", 
			} + "  " + fileName)
			{
				Checked = string.Equals(current?.Path, file, StringComparison.OrdinalIgnoreCase)
			};
			toolStripMenuItem.Click += async delegate
			{
				WallpaperChoice wallpaperChoice = current?.Copy() ?? new WallpaperChoice();
				wallpaperChoice.Path = file;
				await _app.ApplyWallpaperChoiceAsync(wallpaperChoice);
			};
			_wallpaperMenu.DropDownItems.Add(toolStripMenuItem);
		}
		if (readOnlyList.Count == 0)
		{
			_wallpaperMenu.DropDownItems.Add(new ToolStripMenuItem(Loc.T("(«Мои обои» пусты)"))
			{
				Enabled = false
			});
		}
		_wallpaperMenu.DropDownItems.Add(new ToolStripSeparator());
		_wallpaperMenu.DropDownItems.Add(Loc.T("Добавить GIF или видео…"), null, delegate
		{
			_app.ShowWallpapersTab();
		});
	}

	private void RebuildThemes()
	{
		_themesMenu.DropDownItems.Clear();
		_themesMenu.DropDownItems.Add("\ud83c\udfb2 " + Loc.T("Удиви меня"), null, async delegate
		{
			await _app.SurpriseThemeAsync();
		});
		_themesMenu.DropDownItems.Add(new ToolStripSeparator());
		string text = _app.Session.Active?.Id;
		foreach (ThemeProfile item in _app.Services.Profiles.LoadAll())
		{
			ToolStripMenuItem toolStripMenuItem = new ToolStripMenuItem(item.Name)
			{
				Checked = (item.Id == text)
			};
			ThemeProfile captured = item;
			toolStripMenuItem.Click += async delegate
			{
				await _app.ApplyThemeAsync(captured);
			};
			_themesMenu.DropDownItems.Add(toolStripMenuItem);
		}
		if (_themesMenu.DropDownItems.Count <= 2)
		{
			_themesMenu.DropDownItems.Add(new ToolStripMenuItem(Loc.T("(темы не найдены)"))
			{
				Enabled = false
			});
		}
	}

	private static Icon LoadIcon()
	{
		try
		{
			string processPath = Environment.ProcessPath;
			if (processPath != null)
			{
				return Icon.ExtractAssociatedIcon(processPath) ?? SystemIcons.Application;
			}
		}
		catch
		{
		}
		return SystemIcons.Application;
	}

	public void Dispose()
	{
		_icon.Visible = false;
		_icon.Dispose();
	}
}
