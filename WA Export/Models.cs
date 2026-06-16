namespace WAExport;

public record ChatMessage(DateTime Date, string Sender, MessageContent Content);

public abstract record MessageContent
{
    public record Text(string Value) : MessageContent;
    public record Media(string Filename, MediaType Type) : MessageContent;
    public record System(string Value) : MessageContent;
}

public enum MediaType { Image, Audio, Video, Document }

public record ParsedChat(List<ChatMessage> Messages, List<string> Senders, string ChatName);
