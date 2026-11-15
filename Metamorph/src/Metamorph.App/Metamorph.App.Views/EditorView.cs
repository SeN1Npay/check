using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.App.Infrastructure;
using Metamorph.Core.Colors;
using Metamorph.Core.Localization;
using Metamorph.Core.Models;
using Metamorph.Core.Services;
using Microsoft.Win32;

namespace Metamorph.App.Views;

internal sealed class EditorView : System.Windows.Controls.UserControl
{
	private readonly App _app;

	private readonly Action<string?> _reloadThemes;

	private readonly Action<string, bool> _status;

	private readonly System.Windows.Controls.ListBox _sections = new System.Windows.Controls.ListBox
	{
		Width = 200.0,
		Margin = new Thickness(0.0, 0.0, 16.0, 0.0)
	};

	private readonly StackPanel _fields = new StackPanel();

	private readonly TextBlock _title = new TextBlock
	{
		FontSize = 20.0,
		FontWeight = FontWeights.SemiBold
	};

	private ThemeProfile _profile = new ThemeProfile
	{
		Id = "my-theme",
		Name = "Моя тема"
	};

	private string? _sourcePath;

	private bool _builtIn;

	private static readonly (string Title, Func<ThemeProfile, object> Get)[] Sections = new(string, Func<ThemeProfile, object>)[13]
	{
		("Общее", (ThemeProfile p) => p),
		("Палитра", (ThemeProfile p) => p.Palette),
		("Обои", (ThemeProfile p) => p.Wallpaper),
		("Система", (ThemeProfile p) => p.System),
		("Панель задач", (ThemeProfile p) => p.Taskbar),
		("Панель (бар)", (ThemeProfile p) => p.Bar),
		("Док", (ThemeProfile p) => p.Dock),
		("Остров", (ThemeProfile p) => p.Island),
		("Окна", (ThemeProfile p) => p.Windows),
		("Рабочий стол", (ThemeProfile p) => p.Desktop),
		("Приложения", (ThemeProfile p) => p.Apps),
		("Курсоры", (ThemeProfile p) => p.Cursors),
		("Звуки", (ThemeProfile p) => p.Sounds)
	};

	private static readonly Dictionary<string, string> Labels = new Dictionary<string, string>
	{
		["Id"] = "Идентификатор",
		["Name"] = "Название",
		["Description"] = "Описание",
		["Author"] = "Автор",
		["Mode"] = "Режим (тёмный/светлый)",
		["Background"] = "Фон",
		["Surface"] = "Поверхность",
		["Foreground"] = "Текст",
		["Muted"] = "Приглушённый",
		["Accent"] = "Акцент",
		["Accent2"] = "Второй акцент",
		["Red"] = "Красный",
		["Green"] = "Зелёный",
		["Yellow"] = "Жёлтый",
		["Kind"] = "Тип",
		["Path"] = "Путь",
		["From"] = "Цвет 1",
		["To"] = "Цвет 2",
		["Direction"] = "Направление градиента",
		["Style"] = "Стиль",
		["Folder"] = "Папка",
		["IntervalMinutes"] = "Интервал, мин",
		["Shuffle"] = "Случайный порядок",
		["Schedule"] = "Расписание",
		["Transparency"] = "Прозрачность Windows",
		["AccentOnTaskbar"] = "Акцент на панели задач",
		["AccentOnTitleBars"] = "Акцент в заголовках окон",
		["CenterIcons"] = "Значки по центру",
		["Opacity"] = "Непрозрачность",
		["HideSearch"] = "Скрыть поиск",
		["HideTaskView"] = "Скрыть «Представление задач»",
		["HideCortana"] = "Скрыть Кортану",
		["HidePeople"] = "Скрыть «Люди»",
		["HideNews"] = "Скрыть «Новости и интересы»",
		["SmallIcons"] = "Маленькие значки",
		["Grouping"] = "Группировка кнопок",
		["Enabled"] = "Включено",
		["Position"] = "Позиция",
		["Height"] = "Высота",
		["Blur"] = "Размытие",
		["FontFamily"] = "Шрифт",
		["FontSize"] = "Размер шрифта",
		["ClockFormat"] = "Формат часов",
		["Left"] = "Слева",
		["Center"] = "По центру",
		["Right"] = "Справа",
		["Screens"] = "Мониторы",
		["Weather"] = "Погода",
		["City"] = "Город",
		["Latitude"] = "Широта",
		["Longitude"] = "Долгота",
		["Fahrenheit"] = "Фаренгейты",
		["Scripts"] = "Свои модули (скрипты)",
		["Behavior"] = "Поведение",
		["IconSize"] = "Размер значков",
		["HoverEffect"] = "Эффект наведения",
		["Magnification"] = "Увеличение",
		["MagnifyRange"] = "Радиус увеличения",
		["Spacing"] = "Промежуток",
		["Padding"] = "Внутренний отступ",
		["Margin"] = "Отступ от края",
		["CornerRadius"] = "Скругление",
		["BorderColor"] = "Цвет рамки",
		["Indicator"] = "Индикатор запуска",
		["BounceOnLaunch"] = "Подпрыгивать при запуске",
		["BounceOnAttention"] = "Подпрыгивать при внимании",
		["ShowLabels"] = "Подписи",
		["ShowAppsButton"] = "Кнопка приложений",
		["ShowTrash"] = "Корзина",
		["ShowRunningApps"] = "Запущенные приложения",
		["AnimationSpeed"] = "Скорость анимации",
		["Previews"] = "Живые превью окон",
		["ShowRecentFiles"] = "Недавние файлы в меню",
		["ShowBadges"] = "Бейджи (прогресс, оверлей)",
		["ActiveBorder"] = "Рамка активного окна",
		["Width"] = "Толщина",
		["Color"] = "Цвет",
		["Gradient"] = "Градиент",
		["Radius"] = "Скругление",
		["Tiling"] = "Тайлинг",
		["Hotkeys"] = "Горячие клавиши",
		["Gaps"] = "Отступы",
		["MasterRatio"] = "Доля главного окна",
		["Engine"] = "Тайлинговый менеджер",
		["StartEngine"] = "Запускать менеджер",
		["HideIcons"] = "Скрыть значки рабочего стола",
		["Widget"] = "Виджет (conky)",
		["Corner"] = "Угол",
		["Modules"] = "Модули",
		["Scale"] = "Масштаб",
		["WindowsTerminal"] = "Windows Terminal",
		["VsCode"] = "VS Code",
		["OhMyPosh"] = "oh-my-posh",
		["Size"] = "Размер",
		["Scheme"] = "Схема",
		["Speed"] = "Скорость",
		["Colors"] = "Цвета",
		["PauseWhenCovered"] = "Пауза, когда обои закрыты окном",
		["PauseOnBattery"] = "Пауза от батареи",
		["OverBar"] = "Поверх панели (бара)",
		["Offset"] = "Отступ сверху",
		["ExpandedWidth"] = "Ширина в развёрнутом виде",
		["Idle"] = "В покое",
		["AlbumArt"] = "Обложка альбома",
		["ExpandOnHover"] = "Разворачивать при наведении",
		["TrackPopupSeconds"] = "Показ нового трека, с",
		["Activities"] = "Уведомления (активности)",
		["HideInFullscreen"] = "Прятать в полноэкранном режиме",
		["AutoHide"] = "Автоскрытие (выезжает от края экрана)",
		["Animations"] = "Анимации и подсветка при наведении",
		["DodgeWindows"] = "Уступать место окнам (прятаться над вкладками)",
		["Cards"] = "Карточки (виджеты-стекло)",
		["Effects"] = "Эффекты",
		["Shelves"] = "Полки (как Fences)",
		["HotCorners"] = "Горячие углы",
		["Preset"] = "Раскладка",
		["Items"] = "Элементы",
		["Glass"] = "Стекло (размытие)",
		["Grid"] = "Шаг сетки",
		["ClockStyle"] = "Стиль часов",
		["Visualizer"] = "Визуализатор музыки",
		["Particles"] = "Частицы",
		["Parallax"] = "Параллакс",
		["Transition"] = "Смена обоев",
		["TimeTint"] = "Тонировка по времени суток",
		["AccentFromWallpaper"] = "Акцент из обоев",
		["MaxFps"] = "Макс. кадров/с",
		["Placement"] = "Расположение",
		["Bars"] = "Полос",
		["Gradient"] = "Градиент",
		["Density"] = "Плотность",
		["MouseInteraction"] = "Реагировать на курсор",
		["Strength"] = "Сила",
		["Seconds"] = "Длительность, с",
		["TopLeft"] = "Левый верхний",
		["TopRight"] = "Правый верхний",
		["BottomLeft"] = "Левый нижний",
		["BottomRight"] = "Правый нижний",
		["DelayMs"] = "Задержка, мс"
	};

	public bool Dirty { get; private set; }

	public EditorView(App app, Action<string?> reloadThemes, Action<string, bool> status)
	{
		_app = app;
		_reloadThemes = reloadThemes;
		_status = status;
		(string, Func<ThemeProfile, object>)[] sections = Sections;
		for (int i = 0; i < sections.Length; i++)
		{
			string item = sections[i].Item1;
			_sections.Items.Add(Loc.T(item));
		}
		_sections.Background = Ui.Res("PanelBrush");
		_sections.Foreground = Ui.Res("TextBrush");
		_sections.BorderThickness = new Thickness(0.0);
		_sections.Padding = new Thickness(6.0);
		_sections.FontSize = 14.0;
		_sections.SelectionChanged += delegate
		{
			ShowSection();
		};
		WrapPanel wrapPanel = new WrapPanel
		{
			Margin = new Thickness(0.0, 0.0, 0.0, 10.0)
		};
		wrapPanel.Children.Add(Ui.Button("Палитра из картинки…", PaletteFromImage));
		wrapPanel.Children.Add(Ui.Button("Импорт base16…", ImportBase16));
		wrapPanel.Children.Add(Ui.Button("Экспорт base16…", ExportBase16));
		wrapPanel.Children.Add(Ui.Button("Импорт .mmtheme…", ImportPackage));
		wrapPanel.Children.Add(Ui.Button("Экспорт .mmtheme…", ExportPackage));
		wrapPanel.Children.Add(Ui.Button("Сохранить как свою", Save));
		wrapPanel.Children.Add(Ui.Button("Применить", Apply, primary: true));
		DockPanel dockPanel = new DockPanel();
		DockPanel.SetDock(_title, System.Windows.Controls.Dock.Top);
		dockPanel.Children.Add(_title);
		TextBlock element = Ui.Hint("Изменения применяются к копии темы. «Сохранить как свою» кладёт её в папку пользовательских тем; встроенные темы не перезаписываются.");
		DockPanel.SetDock(element, System.Windows.Controls.Dock.Top);
		dockPanel.Children.Add(element);
		dockPanel.Children.Add(new ScrollViewer
		{
			Content = _fields,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			Padding = new Thickness(0.0, 0.0, 12.0, 0.0)
		});
		DockPanel dockPanel2 = new DockPanel();
		DockPanel.SetDock(_sections, System.Windows.Controls.Dock.Left);
		dockPanel2.Children.Add(_sections);
		dockPanel2.Children.Add(Ui.Card(dockPanel));
		DockPanel dockPanel3 = new DockPanel();
		DockPanel.SetDock(wrapPanel, System.Windows.Controls.Dock.Top);
		dockPanel3.Children.Add(wrapPanel);
		dockPanel3.Children.Add(dockPanel2);
		base.Content = dockPanel3;
		_sections.SelectedIndex = 0;
	}

	public void Load(ThemeProfile profile)
	{
		_profile = ThemeJson.Clone(profile);
		_sourcePath = profile.SourcePath;
		_builtIn = profile.IsBuiltIn;
		_profile.SourcePath = _sourcePath;
		_profile.IsBuiltIn = _builtIn;
		Dirty = false;
		ShowSection();
	}

	private void Changed()
	{
		Dirty = true;
		_title.Text = _profile.Name + " *";
	}

	private void ShowSection()
	{
		_fields.Children.Clear();
		_title.Text = _profile.Name + (Dirty ? " *" : "");
		int num = Math.Max(0, _sections.SelectedIndex);
		object target = Sections[num].Get(_profile);
		AddFields(_fields, target, num == 0);
	}

	private void AddFields(System.Windows.Controls.Panel panel, object target, bool topLevel = false, int depth = 0)
	{
		PropertyInfo[] properties = target.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
		foreach (PropertyInfo propertyInfo in properties)
		{
			if (!propertyInfo.CanRead || !propertyInfo.CanWrite || propertyInfo.GetCustomAttribute<JsonIgnoreAttribute>() != null)
			{
				continue;
			}
			Type propertyType = propertyInfo.PropertyType;
			Type underlyingType = Nullable.GetUnderlyingType(propertyType);
			Type type = underlyingType ?? propertyType;
			if (topLevel && type.IsClass && type != typeof(string))
			{
				continue;
			}
			Type type2 = type;
			UIElement uIElement = ((type == typeof(bool) && (object)underlyingType == null) ? BoolEditor(target, propertyInfo) : ((type == typeof(bool)) ? TriStateEditor(target, propertyInfo) : (((object)type2 != null && type2.IsEnum) ? EnumEditor(target, propertyInfo, type, (object)underlyingType != null) : ((type == typeof(int) || type == typeof(double)) ? NumberEditor(target, propertyInfo, type, (object)underlyingType != null) : ((type == typeof(string)) ? StringEditor(target, propertyInfo) : ((type == typeof(List<string>)) ? StringListEditor(target, propertyInfo) : ((!typeof(IList).IsAssignableFrom(type)) ? null : JsonEditor(target, propertyInfo))))))));
			UIElement uIElement2 = uIElement;
			string text;
			string text2;
			if (uIElement2 != null)
			{
				if (Loc.IsEnglish)
				{
					text = PropertyLabel(propertyInfo.Name);
					goto IL_025e;
				}
				string name = propertyInfo.Name;
				if (target is WallpaperSettings)
				{
					if (!(name == "Muted"))
					{
						goto IL_024d;
					}
					text2 = "Видео без звука";
				}
				else
				{
					if (!(target is IslandSettings))
					{
						goto IL_024d;
					}
					switch (name)
					{
					case "Width":
						break;
					case "Background":
						goto IL_0229;
					case "HotEdge":
						goto IL_0232;
					case "FullscreenActivities":
						goto IL_023b;
					case "Style":
						goto IL_0244;
					default:
						goto IL_024d;
					}
					text2 = "Ширина";
				}
				goto IL_025a;
			}
			if (type.IsClass)
			{
				object value = propertyInfo.GetValue(target);
				if (value != null && depth < 3)
				{
					panel.Children.Add(Ui.Header(PropertyLabel(propertyInfo.Name)));
					StackPanel stackPanel = new StackPanel
					{
						Margin = new Thickness(14.0, 0.0, 0.0, 0.0)
					};
					AddFields(stackPanel, value, topLevel: false, depth + 1);
					panel.Children.Add(stackPanel);
				}
			}
			continue;
			IL_0244:
			text2 = "Стиль";
			goto IL_025a;
			IL_024d:
			text2 = PropertyLabel(propertyInfo.Name);
			goto IL_025a;
			IL_0229:
			text2 = "Цвет фона (пусто — из палитры)";
			goto IL_025a;
			IL_025a:
			text = text2;
			goto IL_025e;
			IL_025e:
			string label = text;
			panel.Children.Add(Ui.Row(label, uIElement2, JsonName(propertyInfo.Name)));
			continue;
			IL_023b:
			text2 = "Уведомления поверх полноэкранных окон";
			goto IL_025a;
			IL_0232:
			text2 = "Вызов у верхней кромки";
			goto IL_025a;
		}
	}

	private UIElement BoolEditor(object target, PropertyInfo p)
	{
		PropertyInfo p2 = p;
		object target2 = target;
		System.Windows.Controls.CheckBox checkBox = new System.Windows.Controls.CheckBox();
		checkBox.IsChecked = (bool)p2.GetValue(target2);
		checkBox.Checked += delegate
		{
			p2.SetValue(target2, true);
			Changed();
		};
		checkBox.Unchecked += delegate
		{
			p2.SetValue(target2, false);
			Changed();
		};
		return checkBox;
	}

	private UIElement TriStateEditor(object target, PropertyInfo p)
	{
		PropertyInfo p2 = p;
		object target2 = target;
		System.Windows.Controls.ComboBox combo = new System.Windows.Controls.ComboBox
		{
			Width = 160.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left
		};
		combo.Items.Add(Loc.T("не менять"));
		combo.Items.Add(Loc.T("да"));
		combo.Items.Add(Loc.T("нет"));
		System.Windows.Controls.ComboBox comboBox = combo;
		bool? flag = (bool?)p2.GetValue(target2);
		int selectedIndex = (flag.HasValue ? (flag.GetValueOrDefault() ? 1 : 2) : 0);
		comboBox.SelectedIndex = selectedIndex;
		combo.SelectionChanged += delegate
		{
			PropertyInfo propertyInfo = p2;
			object obj = target2;
			propertyInfo.SetValue(obj, combo.SelectedIndex switch
			{
				1 => true, 
				2 => false, 
				_ => null, 
			});
			Changed();
		};
		return combo;
	}

	private UIElement EnumEditor(object target, PropertyInfo p, Type enumType, bool nullable)
	{
		Type enumType2 = enumType;
		PropertyInfo p2 = p;
		object target2 = target;
		System.Windows.Controls.ComboBox combo = new System.Windows.Controls.ComboBox
		{
			Width = 200.0,
			HorizontalAlignment = System.Windows.HorizontalAlignment.Left
		};
		if (nullable)
		{
			combo.Items.Add(Loc.T("не менять"));
		}
		object value = p2.GetValue(target2);
		string[] names = Enum.GetNames(enumType2).Where(delegate(string n)
		{
			if (!(n == value?.ToString()))
			{
				FieldInfo? field = enumType2.GetField(n);
				if ((object)field == null)
				{
					return true;
				}
				return field.GetCustomAttribute<BrowsableAttribute>()?.Browsable != false;
			}
			return true;
		}).ToArray();
		string[] array = names;
		foreach (string newItem in array)
		{
			combo.Items.Add(newItem);
		}
		combo.SelectedIndex = ((value != null) ? (Array.IndexOf<string>(names, value.ToString()) + (nullable ? 1 : 0)) : 0);
		combo.SelectionChanged += delegate
		{
			int num = combo.SelectedIndex - (nullable ? 1 : 0);
			p2.SetValue(target2, (num < 0) ? null : Enum.Parse(enumType2, names[num]));
			Changed();
		};
		return combo;
	}

	private UIElement NumberEditor(object target, PropertyInfo p, Type type, bool nullable)
	{
		PropertyInfo p2 = p;
		object target2 = target;
		Type type2 = type;
		System.Windows.Controls.TextBox box = Ui.TextBox(Convert.ToString(p2.GetValue(target2), CultureInfo.InvariantCulture), 120.0);
		box.TextChanged += delegate
		{
			string text = box.Text.Trim().Replace(',', '.');
			if (nullable && text.Length == 0)
			{
				p2.SetValue(target2, null);
				box.BorderBrush = Ui.Res("BorderBrush");
				Changed();
			}
			else
			{
				object obj = null;
				if (type2 == typeof(int) && int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var result))
				{
					obj = result;
				}
				if (type2 == typeof(double) && double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
				{
					obj = result2;
				}
				box.BorderBrush = ((obj == null) ? Ui.Res("DangerBrush") : Ui.Res("BorderBrush"));
				if (obj != null)
				{
					p2.SetValue(target2, obj);
					Changed();
				}
			}
		};
		return box;
	}

	private static bool IsColor(object target, string name)
	{
		bool flag = target is Palette;
		if (!flag)
		{
			bool flag2;
			switch (name)
			{
			case "From":
			case "To":
			case "Color":
			case "Background":
			case "BorderColor":
				flag2 = true;
				break;
			default:
				flag2 = false;
				break;
			}
			flag = flag2;
		}
		return flag;
	}

	private UIElement StringEditor(object target, PropertyInfo p)
	{
		PropertyInfo p2 = p;
		object target2 = target;
		System.Windows.Controls.TextBox box = Ui.TextBox((string)p2.GetValue(target2));
		DockPanel dockPanel = new DockPanel();
		Border swatch;
		if (IsColor(target2, p2.Name))
		{
			swatch = new Border
			{
				Width = 26.0,
				Height = 26.0,
				CornerRadius = new CornerRadius(6.0),
				Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
				BorderBrush = Ui.Res("BorderBrush"),
				BorderThickness = new Thickness(1.0),
				Cursor = System.Windows.Input.Cursors.Hand,
				ToolTip = Loc.T("Выбрать цвет")
			};
			Paint();
			swatch.MouseLeftButtonUp += delegate
			{
				using ColorDialog colorDialog = new ColorDialog
				{
					FullOpen = true,
					AnyColor = true
				};
				try
				{
					Rgba rgba = Rgba.Parse(box.Text);
					colorDialog.Color = System.Drawing.Color.FromArgb(rgba.R, rgba.G, rgba.B);
				}
				catch
				{
				}
				if (colorDialog.ShowDialog() == DialogResult.OK)
				{
					box.Text = $"#{colorDialog.Color.R:X2}{colorDialog.Color.G:X2}{colorDialog.Color.B:X2}";
				}
			};
			box.TextChanged += delegate
			{
				Paint();
			};
			DockPanel.SetDock(swatch, System.Windows.Controls.Dock.Left);
			dockPanel.Children.Add(swatch);
		}
		else
		{
			string name = p2.Name;
			if ((name == "Path" || name == "Folder") ? true : false)
			{
				bool flag = p2.Name == "Folder";
				if (!flag)
				{
					bool flag2 = ((target2 is CursorSettings || target2 is SoundSettings) ? true : false);
					flag = flag2;
				}
				bool folder = flag;
				System.Windows.Controls.Button button = new System.Windows.Controls.Button
				{
					Content = "…",
					Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
					Padding = new Thickness(10.0, 4.0, 10.0, 4.0)
				};
				button.Click += delegate
				{
					if (folder)
					{
						using (FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog())
						{
							if (folderBrowserDialog.ShowDialog() == DialogResult.OK)
							{
								box.Text = folderBrowserDialog.SelectedPath;
							}
							return;
						}
					}
					Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
					{
						Filter = Loc.T("Картинки и видео") + "|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.gif;*.mp4;*.wmv;*.webm;*.mov|*.*|*.*"
					};
					if (openFileDialog.ShowDialog().GetValueOrDefault())
					{
						box.Text = openFileDialog.FileName;
					}
				};
				DockPanel.SetDock(button, System.Windows.Controls.Dock.Right);
				dockPanel.Children.Add(button);
			}
		}
		box.TextChanged += delegate
		{
			bool flag3;
			switch (p2.Name)
			{
			case "Background":
			case "FontFamily":
			case "Color":
			case "Label":
			case "Path":
			case "Folder":
			case "BorderColor":
			case "OnClick":
			case "Latitude":
			case "Longitude":
				flag3 = true;
				break;
			default:
				flag3 = false;
				break;
			}
			bool flag4 = flag3;
			p2.SetValue(target2, (flag4 && string.IsNullOrWhiteSpace(box.Text)) ? null : box.Text);
			if (p2.Name == "Name" && target2 is ThemeProfile)
			{
				_title.Text = box.Text + " *";
			}
			Changed();
		};
		dockPanel.Children.Add(box);
		return dockPanel;
		void Paint()
		{
			try
			{
				swatch.Background = (string.IsNullOrWhiteSpace(box.Text) ? System.Windows.Media.Brushes.Transparent : BrushFactory.Solid(box.Text.Trim()));
			}
			catch
			{
				swatch.Background = System.Windows.Media.Brushes.Transparent;
			}
		}
	}

	private UIElement StringListEditor(object target, PropertyInfo p)
	{
		PropertyInfo p2 = p;
		object target2 = target;
		List<string> values = (List<string>)p2.GetValue(target2);
		System.Windows.Controls.TextBox box = Ui.TextBox(string.Join(", ", values));
		box.ToolTip = Loc.T("Через запятую");
		box.TextChanged += delegate
		{
			p2.SetValue(target2, box.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList());
			Changed();
		};
		return box;
	}

	private UIElement JsonEditor(object target, PropertyInfo p)
	{
		PropertyInfo p2 = p;
		object target2 = target;
		System.Windows.Controls.TextBox box = Ui.TextBox(JsonSerializer.Serialize(p2.GetValue(target2), p2.PropertyType, ThemeJson.Options));
		box.AcceptsReturn = true;
		box.TextWrapping = TextWrapping.Wrap;
		box.MinHeight = 70.0;
		box.FontFamily = new System.Windows.Media.FontFamily("Cascadia Mono, Consolas");
		box.ToolTip = Loc.T("Список в формате JSON");
		box.TextChanged += delegate
		{
			try
			{
				object obj = JsonSerializer.Deserialize(box.Text, p2.PropertyType, ThemeJson.Options);
				if (obj == null)
				{
					throw new JsonException();
				}
				p2.SetValue(target2, obj);
				box.BorderBrush = Ui.Res("BorderBrush");
				Changed();
			}
			catch (JsonException)
			{
				box.BorderBrush = Ui.Res("DangerBrush");
			}
		};
		return box;
	}

	private static string JsonName(string name)
	{
		char reference = char.ToLowerInvariant(name[0]);
		return string.Concat(new ReadOnlySpan<char>(ref reference), name.Substring(1, name.Length - 1));
	}

	private static string PropertyLabel(string name)
	{
		string name2 = name;
		if (Loc.IsEnglish || !Labels.TryGetValue(name2, out string value))
		{
			return string.Concat(name2.Select(delegate(char c, int i)
			{
				if (i <= 0 || !char.IsUpper(c) || char.IsUpper(name2[i - 1]))
				{
					return c.ToString();
				}
				char reference = ' ';
				ReadOnlySpan<char> readOnlySpan = new ReadOnlySpan<char>(ref reference);
				char reference2 = char.ToLowerInvariant(c);
				return string.Concat(readOnlySpan, new ReadOnlySpan<char>(ref reference2));
			}));
		}
		return value;
	}

	private bool Validate()
	{
		IReadOnlyList<string> readOnlyList = ThemeValidator.Validate(_profile);
		if (readOnlyList.Count == 0)
		{
			return true;
		}
		_status(Loc.T("Ошибки в теме: ") + string.Join("; ", readOnlyList.Take(4)), arg2: true);
		return false;
	}

	private void Save()
	{
		if (!Validate())
		{
			return;
		}
		try
		{
			ThemeProfile themeProfile;
			if (_builtIn || string.IsNullOrWhiteSpace(_profile.Id))
			{
				themeProfile = _app.Services.Profiles.CreateCopy(_profile, _profile.Name);
			}
			else
			{
				_app.Services.Profiles.Save(_profile);
				themeProfile = _profile;
			}
			_builtIn = false;
			_sourcePath = themeProfile.SourcePath;
			Dirty = false;
			_title.Text = themeProfile.Name;
			_reloadThemes(themeProfile.Id);
			_status(Loc.F("Тема сохранена: {0}", themeProfile.SourcePath), arg2: false);
		}
		catch (Exception ex)
		{
			_status(Loc.T("Не удалось сохранить тему: ") + ex.Message, arg2: true);
		}
	}

	private async void Apply()
	{
		if (!Validate())
		{
			return;
		}
		_status(Loc.F("Применяю «{0}»…", _profile.Name), arg2: false);
		try
		{
			ApplyReport applyReport = await _app.ApplyThemeAsync(_profile);
			_status(applyReport.Summary(), !applyReport.Success);
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
		}
	}

	private void PaletteFromImage()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = Loc.T("Картинки") + "|*.jpg;*.jpeg;*.png;*.bmp;*.webp;*.jfif|*.*|*.*"
		};
		if (!openFileDialog.ShowDialog().GetValueOrDefault())
		{
			return;
		}
		try
		{
			PaletteExtractor.Result result = PaletteExtractor.Extract(LoadPixels(openFileDialog.FileName, 100));
			_profile.Palette = result.Palette;
			_profile.Mode = result.Mode;
			_profile.Wallpaper.Kind = WallpaperKind.Image;
			_profile.Wallpaper.Path = openFileDialog.FileName;
			Changed();
			ShowSection();
			_status(Loc.T("Палитра подобрана по картинке, картинка стала обоями темы (как pywal)."), arg2: false);
		}
		catch (Exception ex)
		{
			_status(Loc.T("Не удалось прочитать картинку: ") + ex.Message, arg2: true);
		}
	}

	internal static List<Rgba> LoadPixels(string file, int size)
	{
		BitmapImage bitmapImage = new BitmapImage();
		bitmapImage.BeginInit();
		bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
		bitmapImage.UriSource = new Uri(file, UriKind.Absolute);
		bitmapImage.DecodePixelWidth = size;
		bitmapImage.EndInit();
		FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(bitmapImage, PixelFormats.Bgra32, null, 0.0);
		int num = formatConvertedBitmap.PixelWidth * 4;
		byte[] array = new byte[num * formatConvertedBitmap.PixelHeight];
		formatConvertedBitmap.CopyPixels(array, num, 0);
		List<Rgba> list = new List<Rgba>(formatConvertedBitmap.PixelWidth * formatConvertedBitmap.PixelHeight);
		for (int i = 0; i + 3 < array.Length; i += 4)
		{
			if (array[i + 3] > 128)
			{
				list.Add(new Rgba(array[i + 2], array[i + 1], array[i]));
			}
		}
		return list;
	}

	private void ImportBase16()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "base16 (*.yaml;*.yml)|*.yaml;*.yml|*.*|*.*"
		};
		if (!openFileDialog.ShowDialog().GetValueOrDefault())
		{
			return;
		}
		try
		{
			ThemeProfile themeProfile = Base16.Import(File.ReadAllText(openFileDialog.FileName), Path.GetFileNameWithoutExtension(openFileDialog.FileName));
			_profile.Palette = themeProfile.Palette;
			_profile.Mode = themeProfile.Mode;
			if (!Dirty && _builtIn)
			{
				_profile.Name = themeProfile.Name;
				_profile.Id = themeProfile.Id;
			}
			Changed();
			ShowSection();
			_status(Loc.F("Палитра «{0}» импортирована.", themeProfile.Name), arg2: false);
		}
		catch (Exception ex)
		{
			_status(Loc.T("Не удалось импортировать base16: ") + ex.Message, arg2: true);
		}
	}

	private void ExportBase16()
	{
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "base16 (*.yaml)|*.yaml",
			FileName = Base16.Slug(_profile.Name) + ".yaml"
		};
		if (!saveFileDialog.ShowDialog().GetValueOrDefault())
		{
			return;
		}
		try
		{
			File.WriteAllText(saveFileDialog.FileName, Base16.Export(_profile));
			_status(Loc.F("Сохранено: {0}", saveFileDialog.FileName), arg2: false);
		}
		catch (Exception ex)
		{
			_status(ex.Message, arg2: true);
		}
	}

	private void ImportPackage()
	{
		Microsoft.Win32.OpenFileDialog openFileDialog = new Microsoft.Win32.OpenFileDialog
		{
			Filter = "Metamorph (*.mmtheme)|*.mmtheme|base16 (*.yaml)|*.yaml;*.yml|JSON (*.json)|*.json"
		};
		if (openFileDialog.ShowDialog().GetValueOrDefault())
		{
			ThemeProfile themeProfile = _app.ImportThemeFile(openFileDialog.FileName, askToApply: false);
			if (themeProfile != null)
			{
				_reloadThemes(themeProfile.Id);
				Load(themeProfile);
				_status(Loc.F("Тема «{0}» импортирована.", themeProfile.Name), arg2: false);
			}
		}
	}

	private void ExportPackage()
	{
		if (!Validate())
		{
			return;
		}
		Microsoft.Win32.SaveFileDialog saveFileDialog = new Microsoft.Win32.SaveFileDialog
		{
			Filter = "Metamorph (*.mmtheme)|*.mmtheme",
			FileName = Base16.Slug(_profile.Name) + ".mmtheme"
		};
		if (!saveFileDialog.ShowDialog().GetValueOrDefault())
		{
			return;
		}
		try
		{
			_profile.SourcePath = _sourcePath;
			ThemePackage.Export(_profile, _app.Services.Profiles, saveFileDialog.FileName);
			_status(Loc.F("Пакет темы сохранён: {0}", saveFileDialog.FileName), arg2: false);
		}
		catch (Exception ex)
		{
			_status(Loc.T("Не удалось экспортировать: ") + ex.Message, arg2: true);
		}
	}
}
