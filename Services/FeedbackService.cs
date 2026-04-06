using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.Communication;

namespace NBH.Services
{
    public class FeedbackService
    {
        public async Task SubmitFeedbackAsync(string message, string category, string userId)
        {
            try
            {
                var emailMessage = new EmailMessage
                {
                    Subject = $"NBH Feedback \u2013 {category}",
                    Body = $"Von: {userId}\n\n{message}",
                    To = { "feedback@nbh-app.de" }
                };
                
                await Email.ComposeAsync(emailMessage);
            }
            catch (FeatureNotSupportedException)
            {
                // Email is not supported on this device
            }
            catch (Exception)
            {
                // Other error has occurred.
            }
        }
    }
}

