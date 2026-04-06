using Microsoft.Maui.Controls;
using System;

namespace NBH.Resources.Models
{
    public class ChatMessageDataTemplateSelector : DataTemplateSelector
    {
        // Von außen gesetzt, damit wir wissen, welcher User "ich" bin
        public Guid CurrentUserId { get; set; }

        public DataTemplate IncomingTemplate { get; set; }
        public DataTemplate OutgoingTemplate { get; set; }

        protected override DataTemplate OnSelectTemplate(object item, BindableObject container)
        {
            if (item is ChatMessage chatMsg)
            {
                // Bestimme, ob's incoming ist: SenderId != CurrentUserId
                bool isIncoming = (chatMsg.SenderId != CurrentUserId);
                return isIncoming ? IncomingTemplate : OutgoingTemplate;
            }

            // Im Fehlerfall nichts anzeigen
            return null;
        }
    }
}
