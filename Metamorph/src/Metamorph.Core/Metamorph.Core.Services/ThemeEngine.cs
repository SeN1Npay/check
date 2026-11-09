using System;
using System.Collections.Generic;
using Metamorph.Core.Colors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Native;

namespace Metamorph.Core.Services;

public sealed class ThemeEngine
{
	private readonly ProfileStore _store;

	private readonly SettingsBackup _backup;

	private readonly WallpaperService _wallpapers;

	private readonly AppStateStore _state;

	private readonly CursorService _cursors;

	private readonly SoundService _sounds;

	private readonly AppThemers _apps;

	public Func<ThemeProfile, ThemeProfile>? WallpaperOverride { get; set; }

	public ThemeEngine(AppPaths paths, ProfileStore store, SettingsBackup backup, WallpaperService wallpapers, AppStateStore state)
	{
		_store = store;
		_backup = backup;
		_wallpapers = wallpapers;
		_state = state;
		_cursors = new CursorService(paths, backup);
		_sounds = new SoundService(backup);
		_apps = new AppThemers(paths, backup);
	}

	public ApplyReport ApplyWallpaper(ThemeProfile effective)
	{
		ThemeProfile effective2 = effective;
		ApplyReport applyReport = new ApplyReport(Loc.T("Обои"));
		applyReport.Run(Loc.T("Резервная копия исходного вида"), _backup.EnsureOriginalSaved);
		applyReport.Run(Loc.T("Обои"), delegate
		{
			_wallpapers.Apply(effective2, (string? p) => _store.ResolvePath(effective2, p));
		});
		return applyReport;
	}

	public ApplyReport Apply(ThemeProfile profile)
	{
		ThemeProfile profile2 = profile;
		ApplyReport report = new ApplyReport(Loc.F("Тема «{0}»", profile2.Name));
		IReadOnlyList<string> readOnlyList = ThemeValidator.Validate(profile2);
		if (readOnlyList.Count > 0)
		{
			report.Failed.AddRange(readOnlyList);
			return report;
		}
		report.Run(Loc.T("Резервная копия исходного вида"), _backup.EnsureOriginalSaved);
		SystemSnapshot original = _backup.LoadOriginal();
		if (original == null)
		{
			report.Failed.Add(Loc.T("Без резервной копии тема не применяется — так изменения всегда можно отменить."));
			return report;
		}
		report.Run(Loc.T("Светлый/тёмный режим и прозрачность"), delegate
		{
			ApplyModeAndTransparency(profile2);
		});
		report.Run(Loc.T("Акцентный цвет"), delegate
		{
			ApplyAccent(profile2);
		});
		report.Run(Loc.T("Обои"), delegate
		{
			_wallpapers.Apply(WallpaperOverride?.Invoke(profile2) ?? profile2, Resolve);
		});
		report.Run(Loc.T("Панель задач"), delegate
		{
			TaskbarController.Apply(profile2.Taskbar.Mode);
		});
		report.Run(Loc.T("Кнопки и значки панели задач"), delegate
		{
			TaskbarTweaks.Apply(profile2.Taskbar, original);
		});
		report.Run(Loc.T("Значки рабочего стола"), delegate
		{
			DesktopIcons.Apply(profile2.Desktop.HideIcons, original);
		});
		report.Run(Loc.T("Курсоры"), delegate
		{
			_cursors.Apply(WithResolved(profile2, Resolve));
		});
		report.Run(Loc.T("Звуки"), delegate
		{
			_sounds.Apply(WithResolved(profile2, Resolve).Sounds);
		});
		report.Run(Loc.T("Цвета в приложениях"), delegate
		{
			report.Notes.AddRange(_apps.Apply(profile2));
		});
		if (profile2.Windows.Tiling.Engine != 0)
		{
			report.Run(Loc.T("Тайлинговый менеджер"), delegate
			{
				report.Notes.Add(TilingIntegration.Apply(profile2));
			});
		}
		report.Run(Loc.T("Обновление интерфейса"), SystemBroadcast.SettingsChanged);
		report.Run(Loc.T("Сохранение состояния"), delegate
		{
			_state.Save(new AppState
			{
				CurrentThemeId = profile2.Id,
				AppliedProfile = profile2,
				AppliedAt = DateTime.Now
			});
		});
		Log.Info(report.Summary());
		return report;
		string? Resolve(string? path)
		{
			return _store.ResolvePath(profile2, path);
		}
	}

	private static ThemeProfile WithResolved(ThemeProfile profile, Func<string?, string?> resolve)
	{
		ThemeProfile themeProfile = ThemeJson.Clone(profile);
		themeProfile.Cursors.Path = resolve(profile.Cursors.Path);
		themeProfile.Sounds.Path = resolve(profile.Sounds.Path);
		return themeProfile;
	}

	public ApplyReport RestoreOriginal()
	{
		ApplyReport applyReport = new ApplyReport(Loc.T("Исходный вид Windows"));
		SystemSnapshot snapshot = _backup.LoadOriginal();
		if (snapshot == null)
		{
			applyReport.Failed.Add(Loc.T("Резервной копии нет: Metamorph ещё не менял систему."));
			return applyReport;
		}
		applyReport.Run(Loc.T("Реестр"), delegate
		{
			SettingsBackup.RestoreRegistry(snapshot);
		});
		applyReport.Run(Loc.T("Обои"), delegate
		{
			SettingsBackup.RestoreWallpaper(snapshot);
		});
		applyReport.Run(Loc.T("Панель задач"), delegate
		{
			TaskbarController.SetAutoHide(snapshot.TaskbarAutoHide);
			TaskbarController.SetVisible(visible: true);
			TaskbarStyler.Reset();
			TaskbarTweaks.NotifyExplorer();
		});
		applyReport.Run(Loc.T("Значки рабочего стола"), delegate
		{
			int? num2 = snapshot.Find("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Advanced", "HideIcons")?.Dword;
			DesktopIcons.SetVisible(!num2.HasValue || num2.GetValueOrDefault() != 1);
		});
		applyReport.Run(Loc.T("Курсоры"), CursorService.Reload);
		applyReport.Run(Loc.T("Цвета в приложениях"), _apps.RestoreAll);
		applyReport.Run(Loc.T("Тайлинговый менеджер"), TilingIntegration.RestoreConfigs);
		applyReport.Run(Loc.T("Цвет рамок окон"), delegate
		{
			int? num = snapshot.Find("Software\\Microsoft\\Windows\\DWM", "ColorizationColor")?.Dword;
			if (num.HasValue)
			{
				int valueOrDefault = num.GetValueOrDefault();
				DwmColorization.TrySet(Rgba.FromArgb((uint)valueOrDefault));
			}
		});
		applyReport.Run(Loc.T("Обновление интерфейса"), SystemBroadcast.SettingsChanged);
		applyReport.Run(Loc.T("Сброс состояния"), delegate
		{
			_state.Save(new AppState());
		});
		if (applyReport.Success)
		{
			_backup.DeleteOriginal();
		}
		Log.Info(applyReport.Summary());
		return applyReport;
	}

	private static void ApplyModeAndTransparency(ThemeProfile p)
	{
		bool value = p.Mode == ColorMode.Light;
		Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "AppsUseLightTheme", value);
		Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "SystemUsesLightTheme", value);
		Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "EnableTransparency", p.System.Transparency);
	}

	private static void ApplyAccent(ThemeProfile p)
	{
		Rgba accent = Rgba.Parse(p.Palette.Accent).WithAlpha(byte.MaxValue);
		Rgba[] array = AccentPalette.Build(accent);
		Reg.SetBinary("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", "AccentPalette", AccentPalette.ToRegistryBytes(array));
		Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", "AccentColorMenu", array[3].ToAbgr());
		Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Explorer\\Accent", "StartColorMenu", array[4].ToAbgr());
		Rgba color = accent.WithAlpha(196);
		Reg.SetDword("Software\\Microsoft\\Windows\\DWM", "AccentColor", accent.ToAbgr());
		Reg.SetDword("Software\\Microsoft\\Windows\\DWM", "ColorizationColor", color.ToArgb());
		Reg.SetDword("Software\\Microsoft\\Windows\\DWM", "ColorizationAfterglow", color.ToArgb());
		Reg.SetDword("Software\\Microsoft\\Windows\\DWM", "ColorPrevalence", p.System.AccentOnTitleBars);
		Reg.SetDword("Software\\Microsoft\\Windows\\CurrentVersion\\Themes\\Personalize", "ColorPrevalence", p.System.AccentOnTaskbar);
		if (p.System.AccentOnTitleBars)
		{
			Reg.SetDword("Software\\Microsoft\\Windows\\DWM", "AccentColorInactive", Rgba.Parse(p.Palette.Surface).WithAlpha(byte.MaxValue).ToAbgr());
		}
		else
		{
			Reg.Delete("Software\\Microsoft\\Windows\\DWM", "AccentColorInactive");
		}
		DwmColorization.TrySet(color);
	}
}
