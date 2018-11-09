using System;

namespace Xamarin.Forms
{
	public static class Visual
	{
		public static IVisual MatchParent { get; } = new MatchParentVisual();
		public static IVisual Default { get; } = new DefaultVisual();
		public static IVisual Material { get; } = new MaterialVisual();

		public sealed class MaterialVisual : IVisual { }
		public sealed class DefaultVisual : IVisual { }
		public sealed class MatchParentVisual : IVisual { }
	}


	[TypeConverter(typeof(VisualTypeConverter))]
	public interface IVisual
	{

	}

	[Xaml.TypeConversion(typeof(IVisual))]
	public class VisualTypeConverter : TypeConverter
	{
		public override object ConvertFromInvariantString(string value)
		{
			if (value != null)
			{
				switch (value.Trim().ToLowerInvariant())
				{
					case "material": return Visual.Material;
					case "default":
					default: return Visual.Default;
				}
			}
			throw new InvalidOperationException($"Cannot convert \"{value}\" into {typeof(IVisual)}");
		}
	}
}