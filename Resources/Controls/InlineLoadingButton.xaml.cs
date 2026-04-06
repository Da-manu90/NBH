using System.Windows.Input;
using NBH.Resources.Animations;

namespace NBH.Resources.Controls;

public partial class InlineLoadingButton : ContentView
{
	public static readonly BindableProperty TextProperty =
		BindableProperty.Create(nameof(Text), typeof(string), typeof(InlineLoadingButton), string.Empty);

	public string Text
	{
		get => (string)GetValue(TextProperty);
		set => SetValue(TextProperty, value);
	}

	public static readonly BindableProperty CommandProperty =
		BindableProperty.Create(nameof(Command), typeof(ICommand), typeof(InlineLoadingButton), null);

	public ICommand Command
	{
		get => (ICommand)GetValue(CommandProperty);
		set => SetValue(CommandProperty, value);
	}

	public static readonly BindableProperty CommandParameterProperty =
		BindableProperty.Create(nameof(CommandParameter), typeof(object), typeof(InlineLoadingButton), null);

	public object CommandParameter
	{
		get => GetValue(CommandParameterProperty);
		set => SetValue(CommandParameterProperty, value);
	}

	public static readonly BindableProperty IsLoadingProperty =
		BindableProperty.Create(nameof(IsLoading), typeof(bool), typeof(InlineLoadingButton), false);

	public bool IsLoading
	{
		get => (bool)GetValue(IsLoadingProperty);
		set => SetValue(IsLoadingProperty, value);
	}

	public static readonly BindableProperty ButtonColorProperty =
		BindableProperty.Create(nameof(ButtonColor), typeof(Color), typeof(InlineLoadingButton), Color.FromArgb("#00897b"));

	public Color ButtonColor
	{
		get => (Color)GetValue(ButtonColorProperty);
		set => SetValue(ButtonColorProperty, value);
	}

	public InlineLoadingButton()
	{
		InitializeComponent();
	}

	private async void OnTapped(object sender, TappedEventArgs e)
	{
		if (IsLoading) return;

		HapticFeedback.Perform(HapticFeedbackType.Click);
		
		if (sender is VisualElement el)
		{
			await AnimationHelper.ButtonPressAsync(el);
		}

		if (Command?.CanExecute(CommandParameter) == true)
		{
			Command.Execute(CommandParameter);
		}
	}
}
