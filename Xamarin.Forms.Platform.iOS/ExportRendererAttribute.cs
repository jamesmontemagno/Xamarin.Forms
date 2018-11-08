using System;

#if __MOBILE__
using UIKit;
#endif

namespace Xamarin.Forms
{
	[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = true)]
	public sealed class ExportRendererAttribute : HandlerAttribute
	{
#if __MOBILE__
		public ExportRendererAttribute(Type handler, Type target, UIUserInterfaceIdiom idiom, Type[] supportedVisuals = null) : base(handler, target, supportedVisuals)
		{
			Idiomatic = true;
			Idiom = idiom;
		}
		internal UIUserInterfaceIdiom Idiom { get; }
#endif

		public ExportRendererAttribute(Type handler, Type target, Type[] supportedVisuals = null) : base(handler, target, supportedVisuals)
		{
			Idiomatic = false;
		}

		internal bool Idiomatic { get; }

		public override bool ShouldRegister()
		{
#if __MOBILE__
			return !Idiomatic || Idiom == UIDevice.CurrentDevice.UserInterfaceIdiom;
#else
			return !Idiomatic;
#endif
		}
	}
}