// ============================================================
// Routes.cs – Zentrale Routen-Konstanten
// ============================================================
// Ersetzt alle Magic Strings wie "//MainTabs/ProfileTab/ProfilePage"
// die bisher an 15+ Stellen im Code verstreut waren.
// Eine Stelle ändern = überall geändert.
// ============================================================

namespace NBH
{
    public static class Routes
    {
        // Shell-Routen (absolute Navigation mit //)
        public const string ProfilePage = "//MainTabs/ProfileTab/ProfilePage";
        public const string LoginPage   = "//LoginPage";
        public const string MapPage     = "//MainTabs/MapTab/MapPage";
        public const string AddPage     = "//MainTabs/AddTab/AddPage";
        public const string ChatView    = "//MainTabs/ChatTab/ChatViewPage";
        public const string FaqPage     = "//MainTabs/FaqTab/FAQPage";

        // Relative Routen (für Push-Navigation)
        public const string RegisterPage = "RegisterPage";
        public const string DetailPage   = "detail";
        public const string ChatPage     = "ChatPage";
        public const string RatingPage   = "RatingPage";
        public const string UserProfile  = "UserProfilePage";
        public const string CropImage    = "CropImagePage";
        public const string CreateEvent  = "CreateEventPage";
        public const string SettingsPage = "SettingsPage";
        public const string MyEventsPage = "MyEventsPage";
        public const string EditEventPage = "EditEventPage";
    }
}
