namespace ApricotFramework.Grpc.ErrorDefinitions;

/// <summary>
/// How much room a failed call's errors may take on the wire.
/// </summary>
/// <remarks>
/// Errors travel in a trailer, and a trailer too large for the peer's header list fails the whole
/// response as a protocol error — turning a clean <c>not_found</c> into a transport failure the caller
/// cannot classify. So the errors are trimmed to fit rather than sent whole.
/// </remarks>
public sealed class GrpcErrorOptions
{
    /// <summary>
    /// The budget used when none is given.
    /// </summary>
    /// <remarks>
    /// Chosen against the 8 KB header list that peers commonly allow, leaving room for the other
    /// trailers and for the third that base64 adds.
    /// </remarks>
    public const int DefaultMaxDetailsBytes = 4096;

    /// <summary>
    /// The most the status message may take, whatever the budget allows.
    /// </summary>
    /// <remarks>
    /// The message is a summary for a log — an error's full message travels in the details — so it is
    /// capped rather than allowed to crowd out the errors it summarises, and never takes more than half
    /// the budget.
    /// </remarks>
    public const int MaxMessageBytes = 512;

    /// <summary>
    /// Gets or sets the largest encoded status the errors may produce, in bytes. Defaults to
    /// <see cref="DefaultMaxDetailsBytes"/>; zero or less sends the status code and message alone.
    /// </summary>
    /// <remarks>
    /// Measured on the serialized <c>google.rpc.Status</c>, before the base64 encoding that puts it in
    /// the trailer — so the trailer itself is about a third larger than this.
    /// </remarks>
    public int MaxDetailsBytes { get; set; } = DefaultMaxDetailsBytes;
}
