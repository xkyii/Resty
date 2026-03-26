using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Kx.Resty.ViewModels.Dialogs
{
    public partial class Preferences : ObservableObject
    {
        public string Theme
        {
            get => _theme;
            set => SetProperty(ref _theme, value);
        }

        public string Locale
        {
            get => _locale;
            set => SetProperty(ref _locale, value);
        }

        public Preferences()
        {
            var pref = ViewModels.Preferences.Instance;
            _theme = pref.Theme;
            _locale = pref.Locale;
        }

        [RelayCommand]
        public void Save()
        {
            var pref = ViewModels.Preferences.Instance;
            pref.Theme = Theme;
            pref.Locale = Locale;
        }

        private string _theme;
        private string _locale;
    }
}
