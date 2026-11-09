using System;
using System.Windows;
using System.Windows.Markup;
using Metamorph.Core.Services;

namespace Metamorph.App.Infrastructure;

// Тёмные шаблоны для стандартных контролов, которых нет в styles/appstyles (исходный XAML утерян — подмешиваем из кода).
// Цвета берутся из ключей appstyles через DynamicResource.
internal static class ModernStyles
{
	private const string Xaml = """
		<ResourceDictionary xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
		                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

		  <Style x:Key="MmComboToggle" TargetType="ToggleButton">
		    <Setter Property="OverridesDefaultStyle" Value="True"/>
		    <Setter Property="Focusable" Value="False"/>
		    <Setter Property="ClickMode" Value="Press"/>
		    <Setter Property="Template">
		      <Setter.Value>
		        <ControlTemplate TargetType="ToggleButton">
		          <Border x:Name="Bd" Background="{DynamicResource CardBrush}" BorderBrush="{DynamicResource BorderBrush}"
		                  BorderThickness="1" CornerRadius="8" SnapsToDevicePixels="True">
		            <Path x:Name="Arrow" HorizontalAlignment="Right" VerticalAlignment="Center" Margin="0,1,11,0"
		                  Data="M0,0 L4.5,4.5 L9,0" Stroke="{DynamicResource SubTextBrush}" StrokeThickness="1.6"
		                  StrokeStartLineCap="Round" StrokeEndLineCap="Round" StrokeLineJoin="Round" RenderTransformOrigin="0.5,0.5">
		              <Path.RenderTransform><RotateTransform x:Name="Rot" Angle="0"/></Path.RenderTransform>
		            </Path>
		          </Border>
		          <ControlTemplate.Triggers>
		            <Trigger Property="IsMouseOver" Value="True">
		              <Setter TargetName="Bd" Property="Background" Value="{DynamicResource CardHoverBrush}"/>
		              <Setter TargetName="Arrow" Property="Stroke" Value="{DynamicResource AccentBrush}"/>
		            </Trigger>
		            <Trigger Property="IsChecked" Value="True">
		              <Setter TargetName="Bd" Property="BorderBrush" Value="{DynamicResource AccentBrush}"/>
		              <Setter TargetName="Arrow" Property="Stroke" Value="{DynamicResource AccentBrush}"/>
		              <Trigger.EnterActions>
		                <BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="Rot" Storyboard.TargetProperty="Angle" To="180" Duration="0:0:0.18"/></Storyboard></BeginStoryboard>
		              </Trigger.EnterActions>
		              <Trigger.ExitActions>
		                <BeginStoryboard><Storyboard><DoubleAnimation Storyboard.TargetName="Rot" Storyboard.TargetProperty="Angle" To="0" Duration="0:0:0.18"/></Storyboard></BeginStoryboard>
		              </Trigger.ExitActions>
		            </Trigger>
		          </ControlTemplate.Triggers>
		        </ControlTemplate>
		      </Setter.Value>
		    </Setter>
		  </Style>

		  <Style TargetType="ComboBox">
		    <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
		    <Setter Property="MinHeight" Value="30"/>
		    <Setter Property="Padding" Value="11,5,30,5"/>
		    <Setter Property="VerticalContentAlignment" Value="Center"/>
		    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
		    <Setter Property="ScrollViewer.HorizontalScrollBarVisibility" Value="Disabled"/>
		    <Setter Property="ScrollViewer.CanContentScroll" Value="True"/>
		    <Setter Property="MaxDropDownHeight" Value="360"/>
		    <Setter Property="Template">
		      <Setter.Value>
		        <ControlTemplate TargetType="ComboBox">
		          <Grid SnapsToDevicePixels="True">
		            <ToggleButton x:Name="Toggle" Style="{StaticResource MmComboToggle}"
		                          IsChecked="{Binding IsDropDownOpen, Mode=TwoWay, RelativeSource={RelativeSource TemplatedParent}}"/>
		            <ContentPresenter x:Name="Content" IsHitTestVisible="False" Margin="{TemplateBinding Padding}"
		                              HorizontalAlignment="Left" VerticalAlignment="{TemplateBinding VerticalContentAlignment}"
		                              Content="{TemplateBinding SelectionBoxItem}"
		                              ContentTemplate="{TemplateBinding SelectionBoxItemTemplate}"
		                              ContentTemplateSelector="{TemplateBinding ItemTemplateSelector}"
		                              ContentStringFormat="{TemplateBinding SelectionBoxItemStringFormat}"/>
		            <TextBox x:Name="PART_EditableTextBox" Visibility="Hidden" Margin="7,3,26,3" Background="Transparent"
		                     BorderThickness="0" Foreground="{TemplateBinding Foreground}" CaretBrush="{DynamicResource TextBrush}"
		                     VerticalContentAlignment="Center" IsReadOnly="{TemplateBinding IsReadOnly}"/>
		            <Popup x:Name="PART_Popup" Placement="Bottom" IsOpen="{TemplateBinding IsDropDownOpen}"
		                   AllowsTransparency="True" Focusable="False" PopupAnimation="Fade">
		              <Border MinWidth="{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}"
		                      MaxHeight="{TemplateBinding MaxDropDownHeight}" Margin="0,4,8,8"
		                      Background="{DynamicResource PanelBrush}" BorderBrush="{DynamicResource BorderBrush}"
		                      BorderThickness="1" CornerRadius="10" Padding="4">
		                <Border.Effect><DropShadowEffect BlurRadius="14" ShadowDepth="3" Opacity="0.45" Color="Black"/></Border.Effect>
		                <ScrollViewer SnapsToDevicePixels="True">
		                  <ItemsPresenter KeyboardNavigation.DirectionalNavigation="Contained"/>
		                </ScrollViewer>
		              </Border>
		            </Popup>
		          </Grid>
		          <ControlTemplate.Triggers>
		            <Trigger Property="IsEditable" Value="True">
		              <Setter TargetName="PART_EditableTextBox" Property="Visibility" Value="Visible"/>
		              <Setter TargetName="Content" Property="Visibility" Value="Hidden"/>
		            </Trigger>
		            <Trigger Property="IsEnabled" Value="False">
		              <Setter Property="Opacity" Value="0.45"/>
		            </Trigger>
		          </ControlTemplate.Triggers>
		        </ControlTemplate>
		      </Setter.Value>
		    </Setter>
		  </Style>

		  <Style TargetType="ComboBoxItem">
		    <Setter Property="Foreground" Value="{DynamicResource TextBrush}"/>
		    <Setter Property="Padding" Value="10,6"/>
		    <Setter Property="FocusVisualStyle" Value="{x:Null}"/>
		    <Setter Property="Template">
		      <Setter.Value>
		        <ControlTemplate TargetType="ComboBoxItem">
		          <Border x:Name="Bd" Background="Transparent" CornerRadius="6" Padding="{TemplateBinding Padding}" SnapsToDevicePixels="True">
		            <ContentPresenter/>
		          </Border>
		          <ControlTemplate.Triggers>
		            <Trigger Property="IsHighlighted" Value="True">
		              <Setter TargetName="Bd" Property="Background" Value="{DynamicResource CardHoverBrush}"/>
		            </Trigger>
		            <Trigger Property="IsSelected" Value="True">
		              <Setter TargetName="Bd" Property="Background" Value="{DynamicResource CardBrush}"/>
		              <Setter Property="Foreground" Value="{DynamicResource AccentBrush}"/>
		            </Trigger>
		            <Trigger Property="IsEnabled" Value="False">
		              <Setter Property="Opacity" Value="0.45"/>
		            </Trigger>
		          </ControlTemplate.Triggers>
		        </ControlTemplate>
		      </Setter.Value>
		    </Setter>
		  </Style>

		  <Style x:Key="MmScrollThumb" TargetType="Thumb">
		    <Setter Property="OverridesDefaultStyle" Value="True"/>
		    <Setter Property="Template">
		      <Setter.Value>
		        <ControlTemplate TargetType="Thumb">
		          <Border x:Name="Bd" Background="{DynamicResource SubTextBrush}" Opacity="0.35" CornerRadius="4"/>
		          <ControlTemplate.Triggers>
		            <Trigger Property="IsMouseOver" Value="True"><Setter TargetName="Bd" Property="Opacity" Value="0.6"/></Trigger>
		            <Trigger Property="IsDragging" Value="True">
		              <Setter TargetName="Bd" Property="Opacity" Value="0.9"/>
		              <Setter TargetName="Bd" Property="Background" Value="{DynamicResource AccentBrush}"/>
		            </Trigger>
		          </ControlTemplate.Triggers>
		        </ControlTemplate>
		      </Setter.Value>
		    </Setter>
		  </Style>

		  <Style x:Key="MmScrollPage" TargetType="RepeatButton">
		    <Setter Property="OverridesDefaultStyle" Value="True"/>
		    <Setter Property="Focusable" Value="False"/>
		    <Setter Property="IsTabStop" Value="False"/>
		    <Setter Property="Template">
		      <Setter.Value>
		        <ControlTemplate TargetType="RepeatButton"><Border Background="Transparent"/></ControlTemplate>
		      </Setter.Value>
		    </Setter>
		  </Style>

		  <Style TargetType="ScrollBar">
		    <Setter Property="OverridesDefaultStyle" Value="True"/>
		    <Setter Property="SnapsToDevicePixels" Value="True"/>
		    <Setter Property="Width" Value="10"/>
		    <Setter Property="MinWidth" Value="10"/>
		    <Setter Property="Template">
		      <Setter.Value>
		        <ControlTemplate TargetType="ScrollBar">
		          <Grid Background="Transparent" Margin="2,4">
		            <Track x:Name="PART_Track" IsDirectionReversed="True">
		              <Track.DecreaseRepeatButton><RepeatButton Style="{StaticResource MmScrollPage}" Command="ScrollBar.PageUpCommand"/></Track.DecreaseRepeatButton>
		              <Track.Thumb><Thumb Style="{StaticResource MmScrollThumb}"/></Track.Thumb>
		              <Track.IncreaseRepeatButton><RepeatButton Style="{StaticResource MmScrollPage}" Command="ScrollBar.PageDownCommand"/></Track.IncreaseRepeatButton>
		            </Track>
		          </Grid>
		        </ControlTemplate>
		      </Setter.Value>
		    </Setter>
		    <Style.Triggers>
		      <Trigger Property="Orientation" Value="Horizontal">
		        <Setter Property="Width" Value="Auto"/>
		        <Setter Property="MinWidth" Value="0"/>
		        <Setter Property="Height" Value="10"/>
		        <Setter Property="MinHeight" Value="10"/>
		        <Setter Property="Template">
		          <Setter.Value>
		            <ControlTemplate TargetType="ScrollBar">
		              <Grid Background="Transparent" Margin="4,2">
		                <Track x:Name="PART_Track" IsDirectionReversed="False">
		                  <Track.DecreaseRepeatButton><RepeatButton Style="{StaticResource MmScrollPage}" Command="ScrollBar.PageLeftCommand"/></Track.DecreaseRepeatButton>
		                  <Track.Thumb><Thumb Style="{StaticResource MmScrollThumb}"/></Track.Thumb>
		                  <Track.IncreaseRepeatButton><RepeatButton Style="{StaticResource MmScrollPage}" Command="ScrollBar.PageRightCommand"/></Track.IncreaseRepeatButton>
		                </Track>
		              </Grid>
		            </ControlTemplate>
		          </Setter.Value>
		        </Setter>
		      </Trigger>
		    </Style.Triggers>
		  </Style>
		</ResourceDictionary>
		""";

	public static void Install(Application app)
	{
		try
		{
			app.Resources.MergedDictionaries.Add(Create());
		}
		catch (Exception ex)
		{
			Log.Error("Стили интерфейса", ex);
		}
	}

	internal static ResourceDictionary Create()
	{
		return (ResourceDictionary)XamlReader.Parse(Xaml);
	}
}
