using System;
using System.Windows;
using System.Windows.Media;
using Metamorph.Core.Effects;
using Metamorph.Core.Models;

namespace Metamorph.App.Desktop.Effects;

// Слой эффектов над обоями: тонировка (раз в минуту), частицы и визуализатор (покадрово, только пока анимируются).
internal sealed class EffectsLayer : FrameworkElement
{
	private readonly DrawingVisual _tint = new DrawingVisual();

	private readonly DrawingVisual _particles = new DrawingVisual();

	private readonly DrawingVisual _visualizer = new DrawingVisual();

	private readonly Size _size;

	private bool _particlesDrawn;

	private bool _visualizerDrawn;

	protected override int VisualChildrenCount => 3;

	public EffectsLayer(Size size)
	{
		_size = size;
		IsHitTestVisible = false;
		Focusable = false;
		AddVisualChild(_tint);
		AddVisualChild(_particles);
		AddVisualChild(_visualizer);
	}

	protected override Visual GetVisualChild(int index)
	{
		return index switch
		{
			0 => _tint,
			1 => _particles,
			2 => _visualizer,
			_ => throw new ArgumentOutOfRangeException(nameof(index)),
		};
	}

	protected override HitTestResult? HitTestCore(PointHitTestParameters hitTestParameters)
	{
		return null;
	}

	public void SetTint(Color color, double alpha)
	{
		using DrawingContext dc = _tint.RenderOpen();
		if (alpha > 0.004)
		{
			SolidColorBrush brush = new SolidColorBrush(Color.FromArgb((byte)Math.Round(255.0 * Math.Clamp(alpha, 0.0, 1.0)), color.R, color.G, color.B));
			brush.Freeze();
			dc.DrawRectangle(brush, null, new Rect(_size));
		}
	}

	public void ClearParticles()
	{
		if (_particlesDrawn)
		{
			using (_particles.RenderOpen())
			{
			}
			_particlesDrawn = false;
		}
	}

	public void ClearVisualizer()
	{
		if (_visualizerDrawn)
		{
			using (_visualizer.RenderOpen())
			{
			}
			_visualizerDrawn = false;
		}
	}

	public void DrawVisualizer(VisualizerPainter painter, double fade)
	{
		using (DrawingContext dc = _visualizer.RenderOpen())
		{
			painter.Draw(dc, fade);
		}
		_visualizerDrawn = true;
	}

	public void DrawParticles(ParticleField field, ParticlePaint paint)
	{
		using (DrawingContext dc = _particles.RenderOpen())
		{
			Particle[] items = field.Items;
			int count = field.Count;
			int variants = paint.Solid.Length;
			for (int i = 0; i < count; i++)
			{
				ref Particle p = ref items[i];
				int v = p.Variant < variants ? p.Variant : 0;
				int lvl = ParticlePaint.Level(p.Alpha);
				Point at = new Point(p.X, p.Y);
				switch (field.Kind)
				{
				case ParticleKind.Snow:
				{
					double r = p.Size * 1.8;
					dc.DrawEllipse(paint.Glow[v][lvl], null, at, r, r);
					break;
				}
				case ParticleKind.Stars:
					if (p.Size > 1.5f)
					{
						double g = p.Size * 4.0;
						dc.DrawEllipse(paint.Glow[v][lvl / 3], null, at, g, g);
					}
					dc.DrawEllipse(paint.Solid[v][lvl], null, at, p.Size, p.Size);
					break;
				case ParticleKind.Fireflies:
				{
					double g = p.Size * 5.0;
					dc.DrawEllipse(paint.Glow[v][lvl], null, at, g, g);
					double c = p.Size * 0.8;
					dc.DrawEllipse(paint.Solid[v][Math.Min(ParticlePaint.Levels - 1, lvl + 4)], null, at, c, c);
					break;
				}
				case ParticleKind.Sakura:
				case ParticleKind.Leaves:
				{
					ImageSource[][] shapes = paint.Shapes;
					if (shapes == null)
					{
						break;
					}
					// Переворот в воздухе: сжимаем по ширине по |cos(фазы)|.
					double flip = Math.Abs(Math.Cos(p.Phase));
					double h = p.Size * 1.5;
					double w = h * Math.Max(0.22, flip);
					dc.DrawImage(shapes[v][ParticlePaint.AngleIndex(p.Angle)], new Rect(p.X - w / 2.0, p.Y - h / 2.0, w, h));
					break;
				}
				case ParticleKind.Rain:
				{
					double speed = Math.Sqrt(p.Vx * p.Vx + p.Vy * p.Vy);
					double k = speed > 0.0 ? p.Size / speed : 0.0;
					dc.DrawLine(paint.Line[v][lvl], new Point(p.X - p.Vx * k, p.Y - p.Vy * k), at);
					break;
				}
				case ParticleKind.Bubbles:
				{
					double r = p.Size / 2.0;
					dc.DrawEllipse(paint.Solid[v][lvl / 5], paint.Line[v][lvl], at, r, r);
					double hl = Math.Max(0.8, r * 0.22);
					dc.DrawEllipse(paint.Solid[v][Math.Min(ParticlePaint.Levels - 1, lvl + 3)], null, new Point(p.X - r * 0.38, p.Y - r * 0.38), hl, hl);
					break;
				}
				}
			}
			if (field.Kind == ParticleKind.Stars)
			{
				ref readonly ShootingStar s = ref field.Shooting;
				if (s.Active)
				{
					const double Trail = 0.13;
					Point head = new Point(s.X, s.Y);
					Point tail = new Point(s.X - s.Vx * Trail, s.Y - s.Vy * Trail);
					dc.PushOpacity(s.Alpha);
					dc.DrawLine(s.Vx >= 0f ? paint.TrailRight : paint.TrailLeft, tail, head);
					dc.DrawEllipse(paint.Head, null, head, 1.6, 1.6);
					dc.Pop();
				}
			}
		}
		_particlesDrawn = true;
	}
}
