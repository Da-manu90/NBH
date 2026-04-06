using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Supabase;
using System.Collections.ObjectModel;
using NBH.Resources.Models;
using System.Linq;
using System.Threading.Tasks;

namespace NBH.Resources.ViewModels
{
    // ViewModel für die FAQPage
    public partial class FAQPageViewModel : ObservableObject
    {
        private readonly Client _supabaseClient;

        [ObservableProperty]
        private ObservableCollection<FAQCategory> _categories = new();

        [ObservableProperty]
        private bool _isLoading = false;

        [ObservableProperty]
        private string _errorMessage = "";

        public IAsyncRelayCommand LoadFAQCommand { get; }

        private bool _hasLoaded = false;

        public FAQPageViewModel(Client supabaseClient)
        {
            _supabaseClient = supabaseClient;
            LoadFAQCommand = new AsyncRelayCommand(LoadFAQFromDBAsync);
        }

        private async Task LoadFAQFromDBAsync()
        {
            if (IsLoading) return;
            IsLoading = true;
            ErrorMessage = "";

            try
            {
                var response = await _supabaseClient.From<FAQRow>()
                    .Order(f => f.SortOrder, Supabase.Postgrest.Constants.Ordering.Ascending)
                    .Get();

                if (response.Models == null || !response.Models.Any())
                {
                    ErrorMessage = "Keine FAQ-Einträge gefunden.";
                    return;
                }

                var grouped = response.Models
                    .GroupBy(r => r.CategoryTitle)
                    .Select(g => new FAQCategory
                    {
                        CategoryTitle = g.Key,
                        Items = g.OrderBy(x => x.SortOrder).ToList()
                    })
                    .ToList();

                Categories.Clear();
                foreach (var cat in grouped)
                    Categories.Add(cat);

                _hasLoaded = true;
            }
            catch (System.Exception ex)
            {
                ErrorMessage = "Fehler beim Laden der FAQ.";
                System.Diagnostics.Debug.WriteLine($"[FAQ ERROR] {ex.Message}");
            }
            finally
            {
                IsLoading = false;
            }
        }
    }
}
