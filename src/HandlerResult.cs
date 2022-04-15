namespace MudProxy;

public record HandlerResult(int BytesProcessed, bool PassThrough, bool CompressionStarted);
