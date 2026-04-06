using Microsoft.Maui.Controls;
using System.Windows.Input;

namespace NBH.Resources.Controls
{
    public partial class EmptyStateView : ContentView
    {
        public static readonly BindableProperty IconSourceProperty =
            BindableProperty.Create(nameof(IconSource), typeof(ImageSource), typeof(EmptyStateView), default(ImageSource));

        public ImageSource IconSource
        {
            get => (ImageSource)GetValue(IconSourceProperty);
            set => SetValue(IconSourceProperty, value);
        }

        public static readonly BindableProperty TitleProperty =
            BindableProperty.Create(nameof(Title), typeof(string), typeof(EmptyStateView), string.Empty);

        public string Title
        {
            get => (string)GetValue(TitleProperty);
            set => SetValue(TitleProperty, value);
        }

        public static readonly BindableProperty DescriptionProperty =
            BindableProperty.Create(nameof(Description), typeof(string), typeof(EmptyStateView), string.Empty);

        public string Description
        {
            get => (string)GetValue(DescriptionProperty);
            set => SetValue(DescriptionProperty, value);
        }

        public static readonly BindableProperty ActionButtonTextProperty =
            BindableProperty.Create(nameof(ActionButtonText), typeof(string), typeof(EmptyStateView), string.Empty, propertyChanged: OnActionButtonTextChanged);

        public string ActionButtonText
        {
            get => (string)GetValue(ActionButtonTextProperty);
            set => SetValue(ActionButtonTextProperty, value);
        }

        public static readonly BindableProperty ActionCommandProperty =
            BindableProperty.Create(nameof(ActionCommand), typeof(ICommand), typeof(EmptyStateView), null);

        public ICommand ActionCommand
        {
            get => (ICommand)GetValue(ActionCommandProperty);
            set => SetValue(ActionCommandProperty, value);
        }

        public static readonly BindableProperty HasActionProperty =
            BindableProperty.Create(nameof(HasAction), typeof(bool), typeof(EmptyStateView), false);

        public bool HasAction
        {
            get => (bool)GetValue(HasActionProperty);
            private set => SetValue(HasActionProperty, value);
        }

        private static void OnActionButtonTextChanged(BindableObject bindable, object oldValue, object newValue)
        {
            if (bindable is EmptyStateView control)
            {
                var text = (string)newValue;
                control.HasAction = !string.IsNullOrWhiteSpace(text);
            }
        }

        public EmptyStateView()
        {
            InitializeComponent();
        }
    }
}
