using System;

namespace NBH.Resources.Models
{
    public class ProfileBadge
    {
        public string Key { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string IconText { get; set; } = "🏅";
        public string? IconSource { get; set; }
        public string AccentColor { get; set; } = "#00897B";
        public bool IsUnlocked { get; set; }
        public double Progress { get; set; }

        public bool IsLocked => !IsUnlocked;

        public int ProgressPercent
        {
            get
            {
                var clamped = Progress;
                if (clamped < 0) clamped = 0;
                if (clamped > 1) clamped = 1;
                return (int)Math.Round(clamped * 100, MidpointRounding.AwayFromZero);
            }
        }

        public string ProgressLabel => IsUnlocked ? "Freigeschaltet" : $"{ProgressPercent}%";
    }
}
