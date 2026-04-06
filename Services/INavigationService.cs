// ============================================================
// INavigationService.cs – Erweitert mit GoToAsync()
// ============================================================
// ÄNDERUNG: GoToAsync(string route) hinzugefügt.
//
// Die Profi-ViewModels rufen _navigationService.GoToAsync(Routes.LoginPage)
// auf – das ist flexibler als benannte Methoden für jede einzelne Seite.
//
// Die alten benannten Methoden bleiben erhalten, damit bestehender Code
// nicht bricht. Sie können langfristig entfernt werden, sobald alle
// Aufrufer auf GoToAsync umgestellt sind.
// ============================================================

namespace NBH.Services
{
    public interface INavigationService
    {
        /// <summary>
        /// Generische Navigation zu einer beliebigen Route.
        /// Nutze Routes-Konstanten: await GoToAsync(Routes.ProfilePage);
        /// </summary>
        Task GoToAsync(string route);

        // ----------------------------------------------------------
        // Legacy-Methoden (bleiben für Abwärtskompatibilität)
        // Können entfernt werden sobald alle Aufrufer auf GoToAsync umgestellt sind.
        // ----------------------------------------------------------
        Task NavigateToMainPageAsync();
        Task NavigateToRegisterPageAsync();
        Task NavigateBackAsync();
        Task NavigateToLoginPageAsync();
        Task NavigateToLastPageAsync();
        Task NavigateToCropImagePageAsync();
    }
}
