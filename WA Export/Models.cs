using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace WAExport;

public record ChatMessage(DateTime Date, string Sender, MessageContent Content);

public abstract record MessageContent
{
    public record Text(string Value) : MessageContent;
    public record Media(string Filename, MediaType Type) : MessageContent;
    public record System(string Value) : MessageContent;
}

public enum MediaType { Image, Audio, Video, Document }

public record ParsedChat(List<ChatMessage> Messages, List<string> Senders, string ChatName)
{
    public bool IsGroup => Senders.Count >= 3;
}

public class ParticipantInfo : INotifyPropertyChanged
{
    public event PropertyChangedEventHandler? PropertyChanged;
    private void Notify([CallerMemberName] string? n = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(n));

    public string SenderRaw { get; init; } = "";

    private string _displayName = "";
    public string DisplayName { get => _displayName; set { _displayName = value; Notify(); } }

    private string _phone = "";
    public string Phone { get => _phone; set { _phone = value; Notify(); } }
}
