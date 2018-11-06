using System;
using System.ComponentModel;
using Android.Content;
using Android.Graphics;
using Android.Graphics.Drawables;
using Android.Support.V7.Widget;
using Android.Util;
using Android.Views;
using Xamarin.Forms.Internals;
using AView = Android.Views.View;
using static System.String;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android.FastRenderers;
using MButton = Android.Support.Design.Button.MaterialButton;
using Xamarin.Forms.Platform.Android.Material;
using Android.Content.Res;
using Android.Support.V4.Widget;
using AMotionEventActions = Android.Views.MotionEventActions;
using Android.Support.V4.View;
using AColor = Android.Graphics.Color;

[assembly: ExportRenderer(typeof(Button), typeof(MaterialButtonRenderer), new[] { typeof(Visual.MaterialVisual) })]
namespace Xamarin.Forms.Platform.Android.Material
{
	internal sealed class MaterialButtonRenderer : MButton, IVisualElementRenderer, AView.IOnAttachStateChangeListener,
		AView.IOnFocusChangeListener, IEffectControlProvider, AView.IOnClickListener, AView.IOnTouchListener, IViewRenderer, ITabStop
	{
		float _defaultFontSize;
		int? _defaultLabelFor;
		Typeface _defaultTypeface;
		int _imageHeight = -1;
		bool _isDisposed;
		bool _inputTransparent;
		Lazy<TextColorSwitcher> _textColorSwitcher;
		readonly AutomationPropertiesProvider _automationPropertiesProvider;
		readonly EffectControlProvider _effectControlProvider;
		VisualElementTracker _tracker;
		Thickness _paddingDeltaPix = new Thickness();

		public event EventHandler<VisualElementChangedEventArgs> ElementChanged;
		public event EventHandler<PropertyChangedEventArgs> ElementPropertyChanged;

		public MaterialButtonRenderer(Context context) : base(context)
		{
			_automationPropertiesProvider = new AutomationPropertiesProvider(this);
			_effectControlProvider = new EffectControlProvider(this);

			Initialize();			
		}

		public VisualElement Element => Button;
		AView IVisualElementRenderer.View => this;
		ViewGroup IVisualElementRenderer.ViewGroup => null;
		VisualElementTracker IVisualElementRenderer.Tracker => _tracker;

		Button Button { get; set; }

		AView ITabStop.TabStop => this;

		public void OnClick(AView v)
		{
			((IButtonController)Button)?.SendClicked();
		}

		public bool OnTouch(AView v, MotionEvent e)
		{
			var buttonController = Element as IButtonController;
			switch (e.Action)
			{
				case AMotionEventActions.Down:
					buttonController?.SendPressed();
					break;
				case AMotionEventActions.Up:
					buttonController?.SendReleased();
					break;
			}

			return false;
		}

		void IEffectControlProvider.RegisterEffect(Effect effect)
		{
			_effectControlProvider.RegisterEffect(effect);
		}

		void IOnAttachStateChangeListener.OnViewAttachedToWindow(AView attachedView)
		{
			UpdateText();
		}

		void IOnAttachStateChangeListener.OnViewDetachedFromWindow(AView detachedView)
		{
		}

		void IOnFocusChangeListener.OnFocusChange(AView v, bool hasFocus)
		{
			OnNativeFocusChanged(hasFocus);

			((IElementController)Button).SetValueFromRenderer(VisualElement.IsFocusedPropertyKey, hasFocus);
		}

		SizeRequest IVisualElementRenderer.GetDesiredSize(int widthConstraint, int heightConstraint)
		{
			UpdateText();

			AView view = this;
			view.Measure(widthConstraint, heightConstraint);

			return new SizeRequest(new Size(MeasuredWidth, MeasuredHeight), MinimumSize());
		}

		void IVisualElementRenderer.SetElement(VisualElement element)
		{
			if (element == null)
			{
				throw new ArgumentNullException(nameof(element));
			}

			if (!(element is Button))
			{
				throw new ArgumentException($"{nameof(element)} must be of type {nameof(Button)}");
			}

			VisualElement oldElement = Button;
			Button = (Button)element;

			Performance.Start(out string reference);

			if (oldElement != null)
			{
				oldElement.PropertyChanged -= OnElementPropertyChanged;
			}

			Color currentColor = oldElement?.BackgroundColor ?? Color.Default;
			if (element.BackgroundColor != currentColor)
			{
				UpdateBackgroundColor();
			}

			element.PropertyChanged += OnElementPropertyChanged;

			if (_tracker == null)
			{
				// Can't set up the tracker in the constructor because it access the Element (for now)
				SetTracker(new VisualElementTracker(this));
			}

			OnElementChanged(new ElementChangedEventArgs<Button>(oldElement as Button, Button));

			SendVisualElementInitialized(element, this);

			EffectUtilities.RegisterEffectControlProvider(this, oldElement, element);

			Performance.Stop(reference);
		}

		void IVisualElementRenderer.SetLabelFor(int? id)
		{
			if (_defaultLabelFor == null)
			{
				_defaultLabelFor = ViewCompat.GetLabelFor(this);
			}

			ViewCompat.SetLabelFor(this, (int)(id ?? _defaultLabelFor));
		}

		void IVisualElementRenderer.UpdateLayout()
		{
			var reference = Guid.NewGuid().ToString();
			_tracker?.UpdateLayout();
		}

		void IViewRenderer.MeasureExactly()
		{
			ViewRenderer.MeasureExactly(this, Element, Context);
		}

		protected override void Dispose(bool disposing)
		{
			if (_isDisposed)
			{
				return;
			}

			_isDisposed = true;

			if (disposing)
			{
				SetOnClickListener(null);
				SetOnTouchListener(null);
				RemoveOnAttachStateChangeListener(this);

				_automationPropertiesProvider?.Dispose();
				_tracker?.Dispose();

				if (Element != null)
				{
					Element.PropertyChanged -= OnElementPropertyChanged;

					if (Platform.GetRenderer(Element) == this)
						Element.ClearValue(Platform.RendererProperty);
				}
			}

			base.Dispose(disposing);
		}

		public override bool OnTouchEvent(MotionEvent e)
		{
			if (!Enabled || (_inputTransparent && Enabled))
				return false;

			return base.OnTouchEvent(e);
		}

		Size MinimumSize()
		{
			return new Size();
		}

		void OnElementChanged(ElementChangedEventArgs<Button> e)
		{
			if (e.OldElement != null)
			{
			}
			if (e.NewElement != null && !_isDisposed)
			{
				this.EnsureId();

				_textColorSwitcher = new Lazy<TextColorSwitcher>(
					() => new TextColorSwitcher(TextColors, e.NewElement.UseLegacyColorManagement()));

				UpdateFont();
				UpdateText();
				UpdateBitmap();
				UpdateTextColor();
				UpdateIsEnabled();
				UpdateInputTransparent();
				UpdateBackgroundColor();
				UpdatePadding();
				UpdateBorder();

				ElevationHelper.SetElevation(this, e.NewElement);
			}

			ElementChanged?.Invoke(this, new VisualElementChangedEventArgs(e.OldElement, e.NewElement));
		}

		void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Button.TextProperty.PropertyName)
			{
				UpdateText();
			}
			else if (e.PropertyName == Button.TextColorProperty.PropertyName)
			{
				UpdateTextColor();
			}
			else if (e.PropertyName == VisualElement.IsEnabledProperty.PropertyName)
			{
				UpdateIsEnabled();
			}
			else if (e.PropertyName == Button.FontProperty.PropertyName)
			{
				UpdateFont();
			}
			else if (e.PropertyName == Button.ImageProperty.PropertyName)
			{
				UpdateBitmap();
			}
			else if (e.PropertyName == VisualElement.IsVisibleProperty.PropertyName)
			{
				UpdateText();
			}
			else if (e.PropertyName == VisualElement.InputTransparentProperty.PropertyName)
			{
				UpdateInputTransparent();
			}
			else if (e.PropertyName == Button.PaddingProperty.PropertyName)
			{
				UpdatePadding();
			}
			else if (e.PropertyName == Button.BorderWidthProperty.PropertyName || e.PropertyName == Button.CornerRadiusProperty.PropertyName || e.PropertyName == Button.BorderColorProperty.PropertyName)
				UpdateBorder();

			ElementPropertyChanged?.Invoke(this, e);
		}

		private void UpdateBorder()
		{
			int cornerRadius = BorderDrawable.DefaultCornerRadius;

			if (Button.IsSet(Button.CornerRadiusProperty) && Button.CornerRadius != (int)Button.CornerRadiusProperty.DefaultValue)
				cornerRadius = Button.CornerRadius;

			this.CornerRadius = (int)Context.ToPixels(cornerRadius);

			int[][] States =
			{
				new int[0]
			};

			Color borderColor = Button.BorderColor;
			if (borderColor.IsDefault)
			{
				StrokeColor = new global::Android.Content.Res.ColorStateList
				(
					States,
					new int[] { AColor.Transparent }
				);

				StrokeWidth = 0;
			}
			else
			{
				StrokeColor = new global::Android.Content.Res.ColorStateList
				(
					States,
					new int[] { borderColor.ToAndroid() }
				);

				StrokeWidth = (int)Button.BorderWidth;
			}
		}

		protected override void OnLayout(bool changed, int l, int t, int r, int b)
		{
			if (Element == null || _isDisposed)
			{
				return;
			}

			if (_imageHeight > -1)
			{
				// We've got an image (and no text); it's already centered horizontally,
				// we just need to adjust the padding so it centers vertically
				var diff = ((b - Context.ToPixels(Button.Padding.Bottom + Button.Padding.Top)) - t - _imageHeight) / 2;
				diff = Math.Max(diff, 0);
				UpdateContentEdge(new Thickness(0, diff, 0, -diff));
			}
			else
			{
				UpdateContentEdge();
			}

			base.OnLayout(changed, l, t, r, b);
		}

		void SetTracker(VisualElementTracker tracker)
		{
			_tracker = tracker;
		}

		void UpdateBackgroundColor()
		{
			int[][] States = 
			{
				new[] { global::Android.Resource.Attribute.StateEnabled },
				new[] { -global::Android.Resource.Attribute.StateEnabled },
				new[] { global::Android.Resource.Attribute.StateFocused },
				new[] { global::Android.Resource.Attribute.StateActive }
			};

			ColorStateList colorStateList = new ColorStateList(
						States,
						new int[]{
							Element.BackgroundColor.ToAndroid(),
							Element.BackgroundColor.ToAndroid(),
							Element.BackgroundColor.ToAndroid(),
							Element.BackgroundColor.ToAndroid()
						}
				);

			 
			global::Android.Support.V4.View.ViewCompat.SetBackgroundTintList(this, colorStateList);
			SetBackgroundColor(Color.Pink.ToAndroid());
		}

		internal void OnNativeFocusChanged(bool hasFocus)
		{
		}

		internal void SendVisualElementInitialized(VisualElement element, AView nativeView)
		{
			element.SendViewInitialized(nativeView);
		}

		void Initialize()
		{
			SoundEffectsEnabled = false;
			SetOnClickListener(this);
			SetOnTouchListener(this);
			AddOnAttachStateChangeListener(this);
			OnFocusChangeListener = this;

			Tag = this;
		}

		void UpdateBitmap()
		{
			if (Element == null || _isDisposed)
			{
				return;
			}

			FileImageSource elementImage = Button.Image;
			string imageFile = elementImage?.File;
			_imageHeight = -1;

			if (elementImage == null || IsNullOrEmpty(imageFile))
			{
				SetCompoundDrawablesWithIntrinsicBounds(null, null, null, null);
				return;
			}

			Drawable image = Context.GetDrawable(imageFile);

			if (IsNullOrEmpty(Button.Text))
			{
				// No text, so no need for relative position; just center the image
				// There's no option for just plain-old centering, so we'll use Top 
				// (which handles the horizontal centering) and some tricksy padding (in OnLayout)
				// to handle the vertical centering 

				// Clear any previous padding and set the image as top/center
				UpdateContentEdge();
				SetCompoundDrawablesWithIntrinsicBounds(null, image, null, null);

				// Keep track of the image height so we can use it in OnLayout
				_imageHeight = image.IntrinsicHeight;

				image.Dispose();
				return;
			}

			Button.ButtonContentLayout layout = Button.ContentLayout;

			CompoundDrawablePadding = (int)layout.Spacing;

			switch (layout.Position)
			{
				case Button.ButtonContentLayout.ImagePosition.Top:
					SetCompoundDrawablesWithIntrinsicBounds(null, image, null, null);
					break;
				case Button.ButtonContentLayout.ImagePosition.Bottom:
					SetCompoundDrawablesWithIntrinsicBounds(null, null, null, image);
					break;
				case Button.ButtonContentLayout.ImagePosition.Right:
					SetCompoundDrawablesWithIntrinsicBounds(null, null, image, null);
					break;
				default:
					// Defaults to image on the left
					SetCompoundDrawablesWithIntrinsicBounds(image, null, null, null);
					break;
			}

			image?.Dispose();
		}

		void UpdateFont()
		{
			if (Element == null || _isDisposed)
			{
				return;
			}

			Font font = Button.Font;

			if (font == Font.Default && _defaultFontSize == 0f)
			{
				return;
			}

			if (_defaultFontSize == 0f)
			{
				_defaultTypeface = Typeface;
				_defaultFontSize = TextSize;
			}

			if (font == Font.Default)
			{
				Typeface = _defaultTypeface;
				SetTextSize(ComplexUnitType.Px, _defaultFontSize);
			}
			else
			{
				Typeface = font.ToTypeface();
				SetTextSize(ComplexUnitType.Sp, font.ToScaledPixel());
			}
		}

		void UpdateIsEnabled()
		{
			if (Element == null || _isDisposed)
			{
				return;
			}

			Enabled = Element.IsEnabled;
		}

		void UpdateInputTransparent()
		{
			if (Element == null || _isDisposed)
			{
				return;
			}

			_inputTransparent = Element.InputTransparent;
		}

		void UpdateText()
		{
			if (Element == null || _isDisposed)
			{
				return;
			}

			string oldText = Text;
			Text = Button.Text;

			// If we went from or to having no text, we need to update the image position
			if (IsNullOrEmpty(oldText) != IsNullOrEmpty(Text))
			{
				UpdateBitmap();
			}
		}

		void UpdateTextColor()
		{
			if (Element == null || _isDisposed || _textColorSwitcher == null)
			{
				return;
			}

			
			_textColorSwitcher.Value.UpdateTextColor(this, Button.TextColor);

			
			/*if (Button.TextColor == Color.Default)
				_textColorSwitcher.Value.UpdateTextColor(this, Button.TextColor);
			else
				_textColorSwitcher.Value.UpdateTextColor(this, Color.White);*/
		}

		void UpdatePadding()
		{
			SetPadding(
				(int)(Context.ToPixels(Button.Padding.Left) + _paddingDeltaPix.Left),
				(int)(Context.ToPixels(Button.Padding.Top) + _paddingDeltaPix.Top),
				(int)(Context.ToPixels(Button.Padding.Right) + _paddingDeltaPix.Right),
				(int)(Context.ToPixels(Button.Padding.Bottom) + _paddingDeltaPix.Bottom)
			);
		}

		void UpdateContentEdge(Thickness? delta = null)
		{
			_paddingDeltaPix = delta ?? new Thickness();
			UpdatePadding();
		}


		//protected override void OnElementChanged(ElementChangedEventArgs<Button> e)
		//{
		//	base.OnElementChanged(e);
		//	SetNativeControl(new MButton(Context));
		//	Control.Text = Element.Text;

		//	ColorStateList colorStateList = new ColorStateList(
		//				new int[][]{
		//						new int[]{-global::Android.Resource.Attribute.StateEnabled}
		//                             // new int[]{android.R.attr.state_checked} , // checked
		//                      },
		//				new int[]{
		//						Element.BackgroundColor.ToAndroid(),
		//						//Color.parseColor("##cccccc"),
		//				}
		//		);



		//	var stateList = global::Android.Support.V4.View.ViewCompat.GetBackgroundTintList(Control);
		//	global::Android.Support.V4.View.ViewCompat.SetBackgroundTintList(Control, colorStateList);

		//	Control.SetBackgroundColor(Color.Transparent.ToAndroid());
		//	//AppCompatButton.set
		//	// CompoundButtonCompat.SetButtonTintList(Control, colorStateList);

		//	//Control.SetBackgroundColor(Element.BackgroundColor.ToAndroid());

		//}
	}
}
