using System.Collections.Generic;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using Metamorph.Core.Localization;

namespace Metamorph.App.Infrastructure;

public static class Localizer
{
	private static readonly ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, string>> Originals = new ConditionalWeakTable<DependencyObject, Dictionary<DependencyProperty, string>>();

	public static void Apply(DependencyObject root)
	{
		Visit(root, 0);
	}

	private static void Visit(DependencyObject node, int depth)
	{
		if (depth > 64)
		{
			return;
		}
		if (!(node is Window element))
		{
			if (!(node is TextBlock textBlock))
			{
				if (!(node is HeaderedContentControl element2))
				{
					if (!(node is HeaderedItemsControl element3))
					{
						if (node is ContentControl element4)
						{
							Translate(element4, ContentControl.ContentProperty);
						}
					}
					else
					{
						Translate(element3, HeaderedItemsControl.HeaderProperty);
					}
				}
				else
				{
					Translate(element2, HeaderedContentControl.HeaderProperty);
					Translate(element2, ContentControl.ContentProperty);
				}
			}
			else if (textBlock.Inlines.Count <= 1)
			{
				Translate(textBlock, TextBlock.TextProperty);
			}
		}
		else
		{
			Translate(element, Window.TitleProperty);
		}
		if (node is FrameworkElement element5)
		{
			Translate(element5, FrameworkElement.ToolTipProperty);
		}
		foreach (object child in LogicalTreeHelper.GetChildren(node))
		{
			if (child is DependencyObject node2)
			{
				Visit(node2, depth + 1);
			}
		}
	}

	private static void Translate(DependencyObject element, DependencyProperty property)
	{
		if (BindingOperations.GetBindingExpressionBase(element, property) != null)
		{
			return;
		}
		Dictionary<DependencyProperty, string> orCreateValue = Originals.GetOrCreateValue(element);
		if (!orCreateValue.TryGetValue(property, out var value))
		{
			if (!(element.GetValue(property) is string text) || string.IsNullOrWhiteSpace(text))
			{
				return;
			}
			value = (orCreateValue[property] = text);
		}
		string text3 = Loc.T(value);
		if (!object.Equals(element.GetValue(property), text3))
		{
			element.SetValue(property, text3);
		}
	}
}
