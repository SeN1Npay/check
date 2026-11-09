using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Metamorph.Core.Colors;
using Metamorph.Core.Services;

namespace Metamorph.Core.Models;

public static class ThemeValidator
{
	private static readonly Regex _IdRegexRegex = new Regex("^[a-z0-9][a-z0-9-]{0,63}$", RegexOptions.Compiled);

	private static Regex IdRegex() => _IdRegexRegex;

	public static IReadOnlyList<string> Validate(ThemeProfile p)
	{
		List<string> errors = new List<string>();
		if (!IdRegex().IsMatch(p.Id ?? ""))
		{
			errors.Add("id '" + p.Id + "': только строчные латинские буквы, цифры и дефис (до 64 символов).");
		}
		if (string.IsNullOrWhiteSpace(p.Name))
		{
			errors.Add("name: не задано название темы.");
		}
		Color("palette.background", p.Palette.Background);
		Color("palette.surface", p.Palette.Surface);
		Color("palette.foreground", p.Palette.Foreground);
		Color("palette.muted", p.Palette.Muted);
		Color("palette.accent", p.Palette.Accent);
		Color("palette.accent2", p.Palette.Accent2);
		Color("palette.red", p.Palette.Red);
		Color("palette.green", p.Palette.Green);
		Color("palette.yellow", p.Palette.Yellow);
		switch (p.Wallpaper.Kind)
		{
		case WallpaperKind.Solid:
			Color("wallpaper.from", p.Wallpaper.From);
			break;
		case WallpaperKind.Gradient:
			Color("wallpaper.from", p.Wallpaper.From);
			Color("wallpaper.to", p.Wallpaper.To);
			break;
		case WallpaperKind.Image:
			if (string.IsNullOrWhiteSpace(p.Wallpaper.Path))
			{
				errors.Add("wallpaper.path: для kind=image нужен путь к изображению.");
			}
			break;
		}
		int height = p.Bar.Height;
		if ((height < 16 || height > 80) ? true : false)
		{
			errors.Add($"bar.height: {p.Bar.Height} вне диапазона 16..80.");
		}
		double opacity = p.Bar.Opacity;
		if ((opacity < 0.0 || opacity > 1.0) ? true : false)
		{
			errors.Add($"bar.opacity: {p.Bar.Opacity} вне диапазона 0..1.");
		}
		opacity = p.Bar.FontSize;
		if ((opacity < 8.0 || opacity > 32.0) ? true : false)
		{
			errors.Add($"bar.fontSize: {p.Bar.FontSize} вне диапазона 8..32.");
		}
		DockSettings dock = p.Dock;
		height = dock.IconSize;
		if ((height < 24 || height > 128) ? true : false)
		{
			errors.Add($"dock.iconSize: {dock.IconSize} вне диапазона 24..128.");
		}
		opacity = dock.Magnification;
		if ((opacity < 1.0 || opacity > 3.0) ? true : false)
		{
			errors.Add($"dock.magnification: {dock.Magnification} вне диапазона 1..3.");
		}
		opacity = dock.MagnifyRange;
		if ((opacity < 1.0 || opacity > 6.0) ? true : false)
		{
			errors.Add($"dock.magnifyRange: {dock.MagnifyRange} вне диапазона 1..6.");
		}
		height = dock.Spacing;
		if ((height < 0 || height > 32) ? true : false)
		{
			errors.Add($"dock.spacing: {dock.Spacing} вне диапазона 0..32.");
		}
		height = dock.Padding;
		if ((height < 2 || height > 32) ? true : false)
		{
			errors.Add($"dock.padding: {dock.Padding} вне диапазона 2..32.");
		}
		height = dock.Margin;
		if ((height < 0 || height > 64) ? true : false)
		{
			errors.Add($"dock.margin: {dock.Margin} вне диапазона 0..64.");
		}
		height = dock.CornerRadius;
		if ((height < 0 || height > 64) ? true : false)
		{
			errors.Add($"dock.cornerRadius: {dock.CornerRadius} вне диапазона 0..64.");
		}
		opacity = dock.Opacity;
		if ((opacity < 0.0 || opacity > 1.0) ? true : false)
		{
			errors.Add($"dock.opacity: {dock.Opacity} вне диапазона 0..1.");
		}
		opacity = dock.AnimationSpeed;
		if ((opacity < 0.25 || opacity > 3.0) ? true : false)
		{
			errors.Add($"dock.animationSpeed: {dock.AnimationSpeed} вне диапазона 0.25..3.");
		}
		if (dock.Background != null)
		{
			Color("dock.background", dock.Background);
		}
		if (dock.BorderColor != null)
		{
			Color("dock.borderColor", dock.BorderColor);
		}
		switch (p.Wallpaper.Kind)
		{
		case WallpaperKind.Slideshow:
			if (string.IsNullOrWhiteSpace(p.Wallpaper.Folder))
			{
				errors.Add("wallpaper.folder: для kind=slideshow нужна папка с картинками.");
			}
			break;
		case WallpaperKind.Video:
			if (string.IsNullOrWhiteSpace(p.Wallpaper.Path))
			{
				errors.Add("wallpaper.path: для kind=video нужен путь к видео.");
			}
			break;
		case WallpaperKind.TimeOfDay:
			if (p.Wallpaper.Schedule.Count == 0)
			{
				errors.Add("wallpaper.schedule: для kind=timeOfDay нужно хотя бы одно время.");
			}
			break;
		}
		Range("wallpaper.intervalMinutes", p.Wallpaper.IntervalMinutes, 1.0, 1440.0);
		for (int i = 0; i < p.Wallpaper.Schedule.Count; i++)
		{
			WallpaperScheduleEntry wallpaperScheduleEntry = p.Wallpaper.Schedule[i];
			Time($"wallpaper.schedule[{i}].at", wallpaperScheduleEntry.At);
			if (string.IsNullOrWhiteSpace(wallpaperScheduleEntry.Path))
			{
				Color($"wallpaper.schedule[{i}].from", wallpaperScheduleEntry.From ?? p.Wallpaper.From);
				Color($"wallpaper.schedule[{i}].to", wallpaperScheduleEntry.To ?? wallpaperScheduleEntry.From ?? p.Wallpaper.To);
			}
		}
		Range("taskbar.opacity", p.Taskbar.Opacity, 0.0, 1.0);
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (ScriptModuleSettings script in p.Bar.Scripts)
		{
			if (string.IsNullOrWhiteSpace(script.Name))
			{
				errors.Add("bar.scripts: у модуля-скрипта нет имени.");
			}
			else if (!hashSet.Add(script.Name.Trim()))
			{
				errors.Add("bar.scripts: имя '" + script.Name + "' повторяется.");
			}
			if (string.IsNullOrWhiteSpace(script.Command))
			{
				errors.Add("bar.scripts '" + script.Name + "': пустая команда.");
			}
			Range("bar.scripts '" + script.Name + "'.intervalSeconds", script.IntervalSeconds, 1.0, 86400.0);
		}
		double? latitude = p.Bar.Weather.Latitude;
		if (latitude.HasValue)
		{
			double valueOrDefault = latitude.GetValueOrDefault();
			Range("bar.weather.latitude", valueOrDefault, -90.0, 90.0);
		}
		latitude = p.Bar.Weather.Longitude;
		if (latitude.HasValue)
		{
			double valueOrDefault2 = latitude.GetValueOrDefault();
			Range("bar.weather.longitude", valueOrDefault2, -180.0, 180.0);
		}
		foreach (string item in p.Bar.Left.Concat(p.Bar.Center).Concat(p.Bar.Right))
		{
			if (BarModules.IsScript(item, out string scriptName))
			{
				if (!hashSet.Contains(scriptName))
				{
					errors.Add("bar: модуль '" + item + "' не описан в bar.scripts.");
				}
			}
			else if (!BarModules.IsKnown(item))
			{
				errors.Add($"bar: неизвестный модуль '{item}'. Доступны: {string.Join(", ", BarModules.Known)}, script:имя.");
			}
		}
		ActiveBorderSettings activeBorder = p.Windows.ActiveBorder;
		Range("windows.activeBorder.width", activeBorder.Width, 1.0, 16.0);
		Range("windows.activeBorder.radius", activeBorder.Radius, 0.0, 32.0);
		if (activeBorder.Color != null)
		{
			Color("windows.activeBorder.color", activeBorder.Color);
		}
		Range("windows.tiling.gaps", p.Windows.Tiling.Gaps, 0.0, 64.0);
		Range("windows.tiling.masterRatio", p.Windows.Tiling.MasterRatio, 0.2, 0.8);
		WidgetSettings widget = p.Desktop.Widget;
		Range("desktop.widget.scale", widget.Scale, 0.5, 3.0);
		Range("desktop.widget.opacity", widget.Opacity, 0.0, 1.0);
		foreach (string module in widget.Modules)
		{
			if (!WidgetSettings.Known.Contains(module.Trim().ToLowerInvariant()))
			{
				errors.Add($"desktop.widget: неизвестный модуль '{module}'. Доступны: {string.Join(", ", WidgetSettings.Known)}.");
			}
		}
		if (p.Wallpaper.Kind == WallpaperKind.Live)
		{
			Color("wallpaper.from", p.Wallpaper.From);
			Color("wallpaper.to", p.Wallpaper.To);
		}
		if (p.Wallpaper.EffectiveKind == WallpaperKind.Video && !string.IsNullOrWhiteSpace(p.Wallpaper.Path) && !WallpaperService.IsAnimated(p.Wallpaper.Path))
		{
			errors.Add("wallpaper.path: для kind=video нужен GIF или видео (" + string.Join(", ", WallpaperService.VideoExtensions) + ").");
		}
		IslandSettings island = p.Island;
		Range("island.height", island.Height, 24.0, 60.0);
		Range("island.width", island.Width, 120.0, 600.0);
		Range("island.expandedWidth", island.ExpandedWidth, 240.0, 800.0);
		Range("island.offset", island.Offset, 0.0, 200.0);
		Range("island.opacity", island.Opacity, 0.2, 1.0);
		Range("island.trackPopupSeconds", island.TrackPopupSeconds, 0.0, 30.0);
		Range("island.animationSpeed", island.AnimationSpeed, 0.25, 3.0);
		if (island.Background != null)
		{
			Color("island.background", island.Background);
		}
		foreach (string activity in island.Activities)
		{
			if (!IslandSettings.KnownActivities.Contains(activity.Trim().ToLowerInvariant()))
			{
				errors.Add($"island.activities: неизвестное событие '{activity}'. Доступны: {string.Join(", ", IslandSettings.KnownActivities)}.");
			}
		}
		Range("cursors.size", p.Cursors.Size, 32.0, 128.0);
		if (p.Cursors.Style == CursorStyle.Custom && string.IsNullOrWhiteSpace(p.Cursors.Path))
		{
			errors.Add("cursors.path: для style=custom нужна папка с курсорами.");
		}
		if (p.Sounds.Scheme == SoundScheme.Custom && string.IsNullOrWhiteSpace(p.Sounds.Path))
		{
			errors.Add("sounds.path: для scheme=custom нужна папка со звуками.");
		}
		return errors;
		void Color(string field, string? value)
		{
			if (!Rgba.TryParse(value, out var _))
			{
				errors.Add(field + ": некорректный цвет '" + value + "'.");
			}
		}
		void Range(string field, double value, double min, double max)
		{
			if (value < min || value > max || double.IsNaN(value))
			{
				errors.Add($"{field}: {value} вне диапазона {min}..{max}.");
			}
		}
		void Time(string field, string? value)
		{
			if (!TimeOfDayParser.TryParse(value, out var _))
			{
				errors.Add(field + ": время '" + value + "' должно быть в формате ЧЧ:ММ.");
			}
		}
	}
}
