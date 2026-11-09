using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Metamorph.Core.Services;

namespace Metamorph.App.Bar;

public abstract class LabeledModule : BarModule
{
	private readonly TextBlock _text = new TextBlock
	{
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(7.0, 0.0, 7.0, 0.0)
	};

	private readonly Run _value = new Run();

	protected readonly BarContext Context;

	public override FrameworkElement View => _text;

	protected TextBlock TextView => _text;

	protected LabeledModule(BarContext context, string label)
	{
		Context = context;
		if (!string.IsNullOrEmpty(label))
		{
			_text.Inlines.Add(new Run(label + " ")
			{
				Foreground = context.Accent
			});
		}
		_text.Inlines.Add(_value);
	}

	protected void SetValue(string value, Brush? brush = null)
	{
		if (_value.Text != value)
		{
			_value.Text = value;
		}
		_value.Foreground = brush ?? Context.Foreground;
	}

	protected void SetVisible(bool visible)
	{
		_text.Visibility = ((!visible) ? Visibility.Collapsed : Visibility.Visible);
	}

	protected void Clickable(Action? left, Action? right = null, Action? middle = null, Action<int>? wheel = null)
	{
		Action left2 = left;
		Action right2 = right;
		Action middle2 = middle;
		Action<int> wheel2 = wheel;
		_text.Cursor = Cursors.Hand;
		_text.Background = Brushes.Transparent;
		_text.MouseUp += delegate(object _, MouseButtonEventArgs e)
		{
			try
			{
				if (e.ChangedButton == MouseButton.Left)
				{
					left2?.Invoke();
				}
				else if (e.ChangedButton == MouseButton.Right)
				{
					right2?.Invoke();
				}
				else if (e.ChangedButton == MouseButton.Middle)
				{
					middle2?.Invoke();
				}
			}
			catch (Exception ex2)
			{
				Log.Error("Клик по модулю " + GetType().Name, ex2);
			}
			e.Handled = true;
		};
		if (wheel2 == null)
		{
			return;
		}
		_text.MouseWheel += delegate(object _, MouseWheelEventArgs e)
		{
			try
			{
				wheel2(Math.Sign(e.Delta));
			}
			catch (Exception ex)
			{
				Log.Error("Колесо на модуле " + GetType().Name, ex);
			}
		};
	}
}
