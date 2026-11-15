using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;
using Microsoft.Win32;

namespace Metamorph.App.Views;

internal sealed class GalleryView : UserControl
{
	private readonly App _app;

	private readonly Action<string?> _reloadThemes;

	private readonly Action<string, bool> _status;

	private readonly TextBox _url;

	private readonly WrapPanel _list = new WrapPanel();

	private readonly TextBlock _info = new TextBlock
	{
		TextWrapping = TextWrapping.Wrap,
		Margin = new Thickness(0.0, 6.0, 0.0, 6.0)
	};

	public GalleryView(App app, Action<string?> reloadThemes, Action<string, bool> status)
	{
		_app = app;
		_reloadThemes = reloadThemes;
		_status = status;
		_url = Ui.TextBox(GalleryService.ResolveIndexUrl(app.Services.Settings.Current) ?? "");
		_info.Foreground = Ui.Res("SubTextBrush");
		StackPanel stackPanel = new StackPanel
		{
			Children = 
			{
				(UIElement)Ui.Header("Галерея тем"),
				(UIElement)Ui.Hint("Адрес index.json галереи. По умолчанию — папка gallery в репозитории обновлений. Можно указать локальный файл.")
			}
		};
		DockPanel dockPanel = new DockPanel
		{
			Margin = new Thickness(0.0, 4.0, 0.0, 4.0)
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0)
		};
		stackPanel2.Children.Add(Ui.Button("Загрузить", Load, primary: true));
		stackPanel2.Children.Add(Ui.Button("Файл…", Browse));
		DockPanel.SetDock(stackPanel2, System.Windows.Controls.Dock.Right);
		dockPanel.Children.Add(stackPanel2);
		dockPanel.Children.Add(_url);
		stackPanel.Children.Add(dockPanel);
		stackPanel.Children.Add(_info);
		DockPanel dockPanel2 = new DockPanel();
		DockPanel.SetDock(stackPanel, System.Windows.Controls.Dock.Top);
		dockPanel2.Children.Add(stackPanel);
		dockPanel2.Children.Add(new ScrollViewer
		{
			Content = _list,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto
		});
		base.Content = Ui.Card(dockPanel2);
		_info.Text = ((_url.Text.Length == 0) ? Loc.T("Укажите адрес галереи или репозиторий обновлений во вкладке «Настройки».") : "");
	}

	private void Browse()
	{
		OpenFileDialog openFileDialog = new OpenFileDialog
		{
			Filter = "index.json|*.json"
		};
		if (openFileDialog.ShowDialog().GetValueOrDefault())
		{
			_url.Text = openFileDialog.FileName;
			Load();
		}
	}

	private async void Load()
	{
		string url = _url.Text.Trim();
		if (url.Length == 0)
		{
			return;
		}
		_list.Children.Clear();
		_info.Text = Loc.T("Загружаю…");
		try
		{
			GalleryIndex galleryIndex = await _app.Services.Gallery.LoadAsync(url);
			AppSettings appSettings = _app.Services.Settings.Load();
			if (appSettings.Gallery.IndexUrl != url && GalleryService.ResolveIndexUrl(appSettings) != url)
			{
				appSettings.Gallery.IndexUrl = url;
				_app.Services.Settings.Save(appSettings);
			}
			HashSet<string> hashSet = (from p in _app.Services.Profiles.LoadAll()
				select p.Id).ToHashSet<string>(StringComparer.OrdinalIgnoreCase);
			foreach (GalleryEntry theme in galleryIndex.Themes)
			{
				_list.Children.Add(Card(url, theme, hashSet.Contains(theme.Id)));
			}
			_info.Text = Loc.F("Тем в галерее: {0}", galleryIndex.Themes.Count);
		}
		catch (Exception ex)
		{
			_info.Text = Loc.T("Не удалось загрузить галерею: ") + ex.Message;
		}
	}

	private UIElement Card(string url, GalleryEntry entry, bool installed)
	{
		string url2 = url;
		GalleryEntry entry2 = entry;
		StackPanel stackPanel = new StackPanel
		{
			Width = 250.0
		};
		StackPanel stackPanel2 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			Margin = new Thickness(0.0, 0.0, 0.0, 8.0)
		};
		foreach (string item in entry2.Preview.Take(8))
		{
			try
			{
				stackPanel2.Children.Add(new Border
				{
					Width = 22.0,
					Height = 22.0,
					CornerRadius = new CornerRadius(11.0),
					Margin = new Thickness(0.0, 0.0, 5.0, 0.0),
					Background = BrushFactory.Solid(item)
				});
			}
			catch
			{
			}
		}
		stackPanel.Children.Add(stackPanel2);
		stackPanel.Children.Add(new TextBlock
		{
			Text = entry2.Name,
			FontSize = 15.0,
			FontWeight = FontWeights.SemiBold
		});
		if (entry2.Author.Length > 0)
		{
			stackPanel.Children.Add(new TextBlock
			{
				Text = entry2.Author,
				Foreground = Ui.Res("SubTextBrush"),
				FontSize = 11.0
			});
		}
		stackPanel.Children.Add(new TextBlock
		{
			Text = entry2.Description,
			TextWrapping = TextWrapping.Wrap,
			Foreground = Ui.Res("SubTextBrush"),
			Margin = new Thickness(0.0, 4.0, 0.0, 8.0)
		});
		Button button = Ui.Button(installed ? "Переустановить" : "Установить", delegate
		{
		}, !installed);
		button.Click += async delegate
		{
			button.IsEnabled = false;
			try
			{
				ThemeProfile themeProfile = await _app.Services.Gallery.InstallAsync(url2, entry2);
				_reloadThemes(themeProfile.Id);
				_status(Loc.F("Тема «{0}» установлена из галереи.", themeProfile.Name), arg2: false);
				button.Content = Loc.T("Установлена");
			}
			catch (Exception ex)
			{
				_status(Loc.T("Не удалось установить тему: ") + ex.Message, arg2: true);
				button.IsEnabled = true;
			}
		};
		stackPanel.Children.Add(button);
		return new Border
		{
			Child = stackPanel,
			Background = Ui.Res("CardBrush"),
			BorderBrush = Ui.Res("BorderBrush"),
			BorderThickness = new Thickness(1.0),
			CornerRadius = new CornerRadius(12.0),
			Padding = new Thickness(14.0),
			Margin = new Thickness(0.0, 0.0, 12.0, 12.0)
		};
	}
}
