using CommunityToolkit.Mvvm.Messaging.Messages;

namespace Me221CrossApp.UI.Desktop.ViewModels;

public class DoExitMessage : ValueChangedMessage<int>
{
    public DoExitMessage() : base(0)
    {        
    }
}
