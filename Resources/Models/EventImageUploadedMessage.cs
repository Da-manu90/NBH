using CommunityToolkit.Mvvm.Messaging.Messages;

namespace NBH.Resources.Models
{
    public class EventImageUploadedMessage : ValueChangedMessage<string>
    {
        public EventImageUploadedMessage(string value) : base(value)
        {
        }
    }
}
