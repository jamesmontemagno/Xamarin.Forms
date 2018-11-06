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
using AMotionEventActions = Android.Views.MotionEventActions;
using static System.String;
using Android.Support.V4.View;
using Xamarin.Forms.Platform.Android.Material;
using Xamarin.Forms;
using Xamarin.Forms.Platform.Android.FastRenderers;
using System.Collections.Generic;
using Android.Content.Res;
using Android.Text;
using Android.Text.Method;
using Android.Views.InputMethods;
using Android.Widget;
using Java.Lang;
using Xamarin.Forms.PlatformConfiguration.AndroidSpecific;
using Android.Support.Design.Widget;
using MTextInputLayout = Android.Support.Design.Widget.TextInputLayout;

// this won't go here permanently it's just for testing at this point
[assembly: ExportRenderer(typeof(Xamarin.Forms.Entry), typeof(MaterialEntryRenderer), new[] { typeof(Visual.MaterialVisual) })]
namespace Xamarin.Forms.Platform.Android.Material
{
	public class MaterialEntryRenderer :
		ViewRenderer<Entry, global::Android.Support.Design.Widget.TextInputLayout>,
		ITextWatcher, TextView.IOnEditorActionListener
	{
		TextColorSwitcher _hintColorSwitcher;
		TextColorSwitcher _textColorSwitcher;
		bool _disposed;
		ImeAction _currentInputImeFlag;
		IElementController ElementController => Element as IElementController;

		bool _cursorPositionChangePending;
		bool _selectionLengthChangePending;
		bool _nativeSelectionIsUpdating;
		private MaterialFormsEditText _textInputEditText;

		public MaterialEntryRenderer(Context context) : base(context)
		{
			AutoPackage = false;
		}

		bool TextView.IOnEditorActionListener.OnEditorAction(TextView v, ImeAction actionId, KeyEvent e)
		{
			// Fire Completed and dismiss keyboard for hardware / physical keyboards
			if (actionId == ImeAction.Done || actionId == _currentInputImeFlag || (actionId == ImeAction.ImeNull && e.KeyCode == Keycode.Enter && e.Action == KeyEventActions.Up))
			{
				Control.ClearFocus();
				v.HideKeyboard();
				((IEntryController)Element).SendCompleted();
			}

			return true;
		}

		void ITextWatcher.AfterTextChanged(IEditable s)
		{
		}

		void ITextWatcher.BeforeTextChanged(ICharSequence s, int start, int count, int after)
		{
		}

		void ITextWatcher.OnTextChanged(ICharSequence s, int start, int before, int count)
		{
			if (string.IsNullOrEmpty(Element.Text) && s.Length() == 0)
				return;

			((IElementController)Element).SetValueFromRenderer(Entry.TextProperty, s.ToString());
		}

		protected override MTextInputLayout CreateNativeControl()
		{
			//var id = ResourceManager.GetStyleByName("Widget_MaterialComponents_TextInputLayout_OutlinedBox");

			var layout = new MTextInputLayout(Context);
			_textInputEditText = new MaterialFormsEditText(layout.Context);

			System.Diagnostics.Debug.WriteLine($"{layout.Context}");
			layout.AddView(_textInputEditText);
			layout.Hint = Element.Placeholder;
			
			return layout;
		}

		protected override void OnElementChanged(ElementChangedEventArgs<Entry> e)
		{
			base.OnElementChanged(e);

			HandleKeyboardOnFocus = true;

			if (e.OldElement == null)
			{
				var textView = CreateNativeControl();

				_textInputEditText.AddTextChangedListener(this);
				_textInputEditText.SetOnEditorActionListener(this);
				_textInputEditText.OnKeyboardBackPressed += OnKeyboardBackPressed;
				_textInputEditText.SelectionChanged += SelectionChanged;

				var useLegacyColorManagement = e.NewElement.UseLegacyColorManagement();

				_textColorSwitcher = new TextColorSwitcher(_textInputEditText.TextColors, useLegacyColorManagement);
				_hintColorSwitcher = new TextColorSwitcher(_textInputEditText.HintTextColors, useLegacyColorManagement);
				SetNativeControl(textView);
			}

			// When we set the control text, it triggers the SelectionChanged event, which updates CursorPosition and SelectionLength;
			// These one-time-use variables will let us initialize a CursorPosition and SelectionLength via ctor/xaml/etc.
			_cursorPositionChangePending = Element.IsSet(Entry.CursorPositionProperty);
			_selectionLengthChangePending = Element.IsSet(Entry.SelectionLengthProperty);


			Control.Hint = Element.Placeholder;
			_textInputEditText.Text = Element.Text;
			UpdateInputType();

			UpdateColor();
			UpdateAlignment();
			UpdateFont();
			UpdatePlaceholderColor();
			UpdateMaxLength();
			UpdateImeOptions();
			UpdateReturnType();

			if (_cursorPositionChangePending || _selectionLengthChangePending)
				UpdateCursorSelection();
		}

		protected override void Dispose(bool disposing)
		{
			if (_disposed)
			{
				return;
			}

			_disposed = true;

			if (disposing)
			{
				if (Control != null)
				{
					_textInputEditText.OnKeyboardBackPressed -= OnKeyboardBackPressed;
					_textInputEditText.SelectionChanged -= SelectionChanged;
				}
			}

			base.Dispose(disposing);
		}

		void UpdatePlaceholderColor()
		{
			int[][] States =
			{
				new int[0]
			};

			ColorStateList myList = new ColorStateList(
						States,
						new int[]{
							Element.PlaceholderColor.ToAndroid()
						}
				);

			Control.DefaultHintTextColor = myList;
			//ViewCompat.SetBackgroundTintList(Control, myList);
			ViewCompat.SetBackgroundTintList(_textInputEditText, myList);

			_textInputEditText.Background.SetColorFilter(Element.PlaceholderColor.ToAndroid(), PorterDuff.Mode.SrcAtop);
			// figure out how to reuse this
			//_hintColorSwitcher.UpdateTextColor(Control, Element.PlaceholderColor, _textInputEditText.SetHintTextColor);
		}

		protected override void OnElementPropertyChanged(object sender, PropertyChangedEventArgs e)
		{
			if (e.PropertyName == Entry.PlaceholderProperty.PropertyName)
				Control.Hint = Element.Placeholder;
			else if (e.PropertyName == Entry.IsPasswordProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == Entry.TextProperty.PropertyName)
			{
				if (_textInputEditText.Text != Element.Text)
				{
					_textInputEditText.Text = Element.Text;
					if (Control.IsFocused)
					{
						_textInputEditText.SetSelection(_textInputEditText.Text.Length);
						Control.ShowKeyboard();
					}
				}
			}
			else if (e.PropertyName == Entry.TextColorProperty.PropertyName)
				UpdateColor();
			else if (e.PropertyName == InputView.KeyboardProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == InputView.IsSpellCheckEnabledProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == Entry.IsTextPredictionEnabledProperty.PropertyName)
				UpdateInputType();
			else if (e.PropertyName == Entry.HorizontalTextAlignmentProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == Entry.FontAttributesProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontFamilyProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.FontSizeProperty.PropertyName)
				UpdateFont();
			else if (e.PropertyName == Entry.PlaceholderColorProperty.PropertyName)
				UpdatePlaceholderColor();
			else if (e.PropertyName == VisualElement.FlowDirectionProperty.PropertyName)
				UpdateAlignment();
			else if (e.PropertyName == InputView.MaxLengthProperty.PropertyName)
				UpdateMaxLength();
			else if (e.PropertyName == PlatformConfiguration.AndroidSpecific.Entry.ImeOptionsProperty.PropertyName)
				UpdateImeOptions();
			else if (e.PropertyName == Entry.ReturnTypeProperty.PropertyName)
				UpdateReturnType();
			else if (e.PropertyName == Entry.SelectionLengthProperty.PropertyName)
				UpdateCursorSelection();
			else if (e.PropertyName == Entry.CursorPositionProperty.PropertyName)
				UpdateCursorSelection();

			base.OnElementPropertyChanged(sender, e);
		}

		protected virtual NumberKeyListener GetDigitsKeyListener(InputTypes inputTypes)
		{
			// Override this in a custom renderer to use a different NumberKeyListener
			// or to filter out input types you don't want to allow
			// (e.g., inputTypes &= ~InputTypes.NumberFlagSigned to disallow the sign)
			return LocalizedDigitsKeyListener.Create(inputTypes);
		}

		protected virtual void UpdateImeOptions()
		{
			if (Element == null || Control == null)
				return;
			var imeOptions = Element.OnThisPlatform().ImeOptions();
			_currentInputImeFlag = imeOptions.ToAndroidImeOptions();
			_textInputEditText.ImeOptions = _currentInputImeFlag;
		}

		void UpdateAlignment()
		{
			_textInputEditText.UpdateHorizontalAlignment(Element.HorizontalTextAlignment, Context.HasRtlSupport());
		}

		void UpdateColor()
		{
			_textColorSwitcher.UpdateTextColor(_textInputEditText, Element.TextColor);
		}

		void UpdateFont()
		{
			Control.Typeface = Element.ToTypeface();
			_textInputEditText.SetTextSize(ComplexUnitType.Sp, (float)Element.FontSize);
		}

		void UpdateInputType()
		{
			Entry model = Element;
			var keyboard = model.Keyboard;

			_textInputEditText.InputType = keyboard.ToInputType();
			if (!(keyboard is Internals.CustomKeyboard))
			{
				if (model.IsSet(InputView.IsSpellCheckEnabledProperty))
				{
					if ((_textInputEditText.InputType & InputTypes.TextFlagNoSuggestions) != InputTypes.TextFlagNoSuggestions)
					{
						if (!model.IsSpellCheckEnabled)
							_textInputEditText.InputType = _textInputEditText.InputType | InputTypes.TextFlagNoSuggestions;
					}
				}
				if (model.IsSet(Entry.IsTextPredictionEnabledProperty))
				{
					if ((_textInputEditText.InputType & InputTypes.TextFlagNoSuggestions) != InputTypes.TextFlagNoSuggestions)
					{
						if (!model.IsTextPredictionEnabled)
							_textInputEditText.InputType = _textInputEditText.InputType | InputTypes.TextFlagNoSuggestions;
					}
				}
			}

			if (keyboard == Keyboard.Numeric)
			{
				_textInputEditText.KeyListener = GetDigitsKeyListener(_textInputEditText.InputType);
			}

			if (model.IsPassword && ((_textInputEditText.InputType & InputTypes.ClassText) == InputTypes.ClassText))
				_textInputEditText.InputType = _textInputEditText.InputType | InputTypes.TextVariationPassword;
			if (model.IsPassword && ((_textInputEditText.InputType & InputTypes.ClassNumber) == InputTypes.ClassNumber))
				_textInputEditText.InputType = _textInputEditText.InputType | InputTypes.NumberVariationPassword;

			UpdateFont();
		}

		void OnKeyboardBackPressed(object sender, EventArgs eventArgs)
		{
			Control?.ClearFocus();
		}

		void UpdateMaxLength()
		{
			var currentFilters = new List<IInputFilter>(_textInputEditText?.GetFilters() ?? new IInputFilter[0]);

			for (var i = 0; i < currentFilters.Count; i++)
			{
				if (currentFilters[i] is InputFilterLengthFilter)
				{
					currentFilters.RemoveAt(i);
					break;
				}
			}

			currentFilters.Add(new InputFilterLengthFilter(Element.MaxLength));

			_textInputEditText?.SetFilters(currentFilters.ToArray());

			var currentControlText = _textInputEditText?.Text;

			if (currentControlText.Length > Element.MaxLength)
				_textInputEditText.Text = currentControlText.Substring(0, Element.MaxLength);
		}

		void UpdateReturnType()
		{
			if (Control == null || Element == null)
				return;

			_textInputEditText.ImeOptions = Element.ReturnType.ToAndroidImeAction();
			_currentInputImeFlag = _textInputEditText.ImeOptions;
		}

		void SelectionChanged(object sender, SelectionChangedEventArgs e)
		{
			if (_nativeSelectionIsUpdating || Control == null || Element == null)
				return;

			int cursorPosition = Element.CursorPosition;
			int selectionStart = _textInputEditText.SelectionStart;

			if (!_cursorPositionChangePending)
			{
				var start = cursorPosition;

				if (selectionStart != start)
					SetCursorPositionFromRenderer(selectionStart);
			}

			if (!_selectionLengthChangePending)
			{
				int elementSelectionLength = System.Math.Min(_textInputEditText.Text.Length - cursorPosition, Element.SelectionLength);

				var controlSelectionLength = _textInputEditText.SelectionEnd - selectionStart;
				if (controlSelectionLength != elementSelectionLength)
					SetSelectionLengthFromRenderer(controlSelectionLength);
			}
		}

		void UpdateCursorSelection()
		{
			if (_nativeSelectionIsUpdating || Control == null || Element == null)
				return;

			if (Control.RequestFocus())
			{
				try
				{
					int start = GetSelectionStart();
					int end = GetSelectionEnd(start);

					_textInputEditText.SetSelection(start, end);
				}
				catch (System.Exception ex)
				{
					Internals.Log.Warning("Entry", $"Failed to set Control.Selection from CursorPosition/SelectionLength: {ex}");
				}
				finally
				{
					_cursorPositionChangePending = _selectionLengthChangePending = false;
				}
			}
		}

		int GetSelectionEnd(int start)
		{
			int end = start;
			int selectionLength = Element.SelectionLength;

			if (Element.IsSet(Entry.SelectionLengthProperty))
				end = System.Math.Max(start, System.Math.Min(_textInputEditText.Length(), start + selectionLength));

			int newSelectionLength = System.Math.Max(0, end - start);
			if (newSelectionLength != selectionLength)
				SetSelectionLengthFromRenderer(newSelectionLength);

			return end;
		}

		int GetSelectionStart()
		{
			int start = _textInputEditText.Length();
			int cursorPosition = Element.CursorPosition;

			if (Element.IsSet(Entry.CursorPositionProperty))
				start = System.Math.Min(_textInputEditText.Text.Length, cursorPosition);

			if (start != cursorPosition)
				SetCursorPositionFromRenderer(start);

			return start;
		}

		void SetCursorPositionFromRenderer(int start)
		{
			try
			{
				_nativeSelectionIsUpdating = true;
				ElementController?.SetValueFromRenderer(Entry.CursorPositionProperty, start);
			}
			catch (System.Exception ex)
			{
				Internals.Log.Warning("Entry", $"Failed to set CursorPosition from renderer: {ex}");
			}
			finally
			{
				_nativeSelectionIsUpdating = false;
			}
		}

		void SetSelectionLengthFromRenderer(int selectionLength)
		{
			try
			{
				_nativeSelectionIsUpdating = true;
				ElementController?.SetValueFromRenderer(Entry.SelectionLengthProperty, selectionLength);
			}
			catch (System.Exception ex)
			{
				Internals.Log.Warning("Entry", $"Failed to set SelectionLength from renderer: {ex}");
			}
			finally
			{
				_nativeSelectionIsUpdating = false;
			}
		}

		//void UpdatePlaceholderColor()
		//{
		//	_hintColorSwitcher.UpdateTextColor(_textInputEditText, Element.PlaceholderColor, _textInputEditText.SetHintTextColor);
		//}
	}
}