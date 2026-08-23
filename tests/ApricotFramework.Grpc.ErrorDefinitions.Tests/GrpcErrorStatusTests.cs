using System.Text;
using System.Text.Json;
using ApricotFramework.ErrorDefinitions;
using ApricotFramework.Grpc.ErrorDefinitions.Contract;
using Google.Protobuf;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using GoogleRpcStatus = Google.Rpc.Status;

namespace ApricotFramework.Grpc.ErrorDefinitions.Tests;

/// <summary>
/// Covers both directions of the error contract, including everything a peer might send that this
/// library did not.
/// </summary>
public class GrpcErrorStatusTests
{
    [Fact]
    public void ToStatus_NoErrors_Throws()
    {
        Assert.Throws<ArgumentException>(() => GrpcErrorStatus.ToStatus([]));
    }

    [Fact]
    public void ToStatus_NullErrors_Throws()
    {
        Assert.Throws<ArgumentNullException>(() => GrpcErrorStatus.ToStatus(null!));
    }

    [Fact]
    public void ToStatus_ManyErrors_TakesTheCodeAndMessageFromTheFirst()
    {
        var status = GrpcErrorStatus.ToStatus([Err.NotFound(message: "gone"), Err.Internal(message: "broken")]);

        Assert.Equal(ErrorKindStatus.ToGrpcStatusCode(ErrorKinds.NotFound), status.Code);
        Assert.Equal("gone", status.Message);
    }

    [Fact]
    public void ToStatus_CustomKind_IsUnknown()
    {
        var status = GrpcErrorStatus.ToStatus([Err.From("my_service_kind", "MY_CODE")]);

        Assert.Equal((int)StatusCode.Unknown, status.Code);

        // The kind itself still travels, so the caller can act on it even though gRPC cannot name it.
        Assert.Equal("my_service_kind", GrpcErrorStatus.ToErrors(status.ToRpcException())[0].Kind);
    }

    [Fact]
    public void ToErrors_RoundTrip_KeepsEveryError()
    {
        List<ErrorDefinition> errors =
        [
            Err.Validation("EMAIL_REQUIRED", "email is required"),
            Err.Validation("NAME_TOO_LONG", "name is too long"),
        ];

        var read = GrpcErrorStatus.ToErrors(GrpcErrorStatus.ToRpcException(errors));

        Assert.Equal(2, read.Count);
        Assert.Equal(errors.Select(error => (error.Kind, error.Code, error.Message)), read.Select(error => (error.Kind, error.Code, error.Message)));
    }

    [Fact]
    public void ToErrors_RoundTrip_KeepsEveryPayloadValueKind()
    {
        var payload = new Dictionary<string, object?>
        {
            ["text"] = "value",
            ["number"] = 42,
            ["fraction"] = 1.5,
            ["flag"] = true,
            ["nothing"] = null,
            ["list"] = new object[] { 1, "two" },
            ["nested"] = new Dictionary<string, object?> { ["inner"] = "deep" },
        };

        var read = Assert.Single(GrpcErrorStatus.ToErrors(
            GrpcErrorStatus.ToRpcException([Err.Validation("BAD", "bad", payload)])));

        Assert.NotNull(read.Payload);
        Assert.Equal("value", Element(read, "text").GetString());
        Assert.Equal(42, Element(read, "number").GetInt32());
        Assert.Equal(1.5, Element(read, "fraction").GetDouble());
        Assert.True(Element(read, "flag").GetBoolean());
        Assert.Null(read.Payload["nothing"]);
        Assert.Equal(2, Element(read, "list").GetArrayLength());
        Assert.Equal("deep", Element(read, "nested").GetProperty("inner").GetString());
    }

    [Fact]
    public void ToErrors_PayloadWithACycle_LosesThePayloadRatherThanTheError()
    {
        var cyclic = new List<object?>();
        cyclic.Add(cyclic);

        var read = Assert.Single(GrpcErrorStatus.ToErrors(
            GrpcErrorStatus.ToRpcException([Err.Validation("BAD", "bad", new Dictionary<string, object?> { ["loop"] = cyclic })])));

        Assert.Equal("BAD", read.Code);
        Assert.Null(read.Payload);
    }

    [Fact]
    public void ToErrors_NoTrailers_ClassifiesFromTheStatusCode()
    {
        var read = Assert.Single(GrpcErrorStatus.ToErrors(
            new RpcException(new Status(StatusCode.Unavailable, "no route"))));

        Assert.Equal(ErrorKinds.Unavailable, read.Kind);
        Assert.Equal(ErrorCodes.ForKind(ErrorKinds.Unavailable), read.Code);
        Assert.Equal("no route", read.Message);
    }

    [Fact]
    public void ToErrors_DetailFromAnotherContract_ClassifiesFromTheStatusCode()
    {
        var status = new GoogleRpcStatus { Code = (int)StatusCode.PermissionDenied, Message = "no" };
        status.Details.Add(Any.Pack(new Google.Rpc.ErrorInfo { Domain = "example.com", Reason = "DENIED" }));

        var read = Assert.Single(GrpcErrorStatus.ToErrors(status.ToRpcException()));

        Assert.Equal(ErrorKinds.AccessDenied, read.Kind);
    }

    [Fact]
    public void ToErrors_OurTypeUrlOverJunk_ClassifiesFromTheStatusCode()
    {
        var status = new GoogleRpcStatus { Code = (int)StatusCode.NotFound, Message = "gone" };
        status.Details.Add(new Any
        {
            TypeUrl = "type.googleapis.com/apricot.errors.v1.ErrorDetails",
            Value = ByteString.CopyFrom(0xFF, 0xFF, 0xFF),
        });

        var read = Assert.Single(GrpcErrorStatus.ToErrors(status.ToRpcException()));

        Assert.Equal(ErrorKinds.NotFound, read.Kind);
    }

    [Fact]
    public void ToErrors_TrailerThatIsNotAStatus_ClassifiesFromTheStatusCode()
    {
        var trailers = new Metadata { { MetadataExtensions.StatusDetailsTrailerName, new byte[] { 0xFF, 0xFF } } };

        var read = Assert.Single(GrpcErrorStatus.ToErrors(
            new RpcException(new Status(StatusCode.Internal, "broken"), trailers)));

        Assert.Equal(ErrorKinds.Internal, read.Kind);
    }

    [Fact]
    public void ToErrors_DetailWithNoKind_UsesTheStatusKind()
    {
        var read = Assert.Single(GrpcErrorStatus.ToErrors(
            Sent(StatusCode.AlreadyExists, new ErrorDetail { Message = "already there" })));

        Assert.Equal(ErrorKinds.AlreadyExists, read.Kind);
        Assert.Equal(ErrorCodes.ForKind(ErrorKinds.AlreadyExists), read.Code);
        Assert.Equal("already there", read.Message);
    }

    [Theory]
    [InlineData("Not Found")]
    [InlineData("NOT_FOUND")]
    [InlineData("")]
    public void ToErrors_KindNoValidatorWouldAccept_IsReadRatherThanRejected(string kind)
    {
        // A peer is not obliged to be this library. Reading its answer must not throw.
        var read = Assert.Single(GrpcErrorStatus.ToErrors(
            Sent(StatusCode.NotFound, new ErrorDetail { Kind = kind, Code = "what ever" })));

        Assert.Equal(kind.Length == 0 ? ErrorKinds.NotFound : kind, read.Kind);
        Assert.Equal("what ever", read.Code);
    }

    [Fact]
    public void ToStatus_PayloadsTooLarge_DropsThePayloadsAndKeepsTheErrors()
    {
        List<ErrorDefinition> errors =
        [
            Err.Validation("FIRST", "first", new Dictionary<string, object?> { ["blob"] = new string('x', 4000) }),
            Err.Validation("SECOND", "second"),
        ];

        var read = GrpcErrorStatus.ToErrors(GrpcErrorStatus.ToRpcException(errors));

        Assert.Equal(["FIRST", "SECOND"], read.Select(error => error.Code));
        Assert.All(read, error => Assert.Null(error.Payload));
    }

    [Fact]
    public void ToStatus_MoreErrorsThanFit_KeepsTheFirstAndSaysItTruncated()
    {
        var errors = Enumerable.Range(0, 200)
            .Select(index => Err.Validation($"CODE_{index}", new string('m', 100)))
            .ToList();

        var status = GrpcErrorStatus.ToStatus(errors);
        var read = GrpcErrorStatus.ToErrors(status.ToRpcException());

        Assert.True(status.CalculateSize() <= GrpcErrorOptions.DefaultMaxDetailsBytes);
        Assert.InRange(read.Count, 1, errors.Count - 1);
        Assert.Equal("CODE_0", read[0].Code);
        Assert.True(Unpack(status).Truncated);
    }

    [Fact]
    public void ToStatus_OneErrorLargerThanTheBudget_KeepsItsKindAndCode()
    {
        var status = GrpcErrorStatus.ToStatus(
            [Err.NotFound("ORDER_NOT_FOUND", new string('m', 10_000))],
            new GrpcErrorOptions { MaxDetailsBytes = 256 });

        var read = Assert.Single(GrpcErrorStatus.ToErrors(status.ToRpcException()));

        Assert.True(status.CalculateSize() <= 256);
        Assert.Equal(ErrorKinds.NotFound, read.Kind);
        Assert.Equal("ORDER_NOT_FOUND", read.Code);
        Assert.True(Unpack(status).Truncated);
    }

    [Fact]
    public void ToStatus_NoBudget_SendsTheStatusAloneWithABoundedMessage()
    {
        var status = GrpcErrorStatus.ToStatus(
            [Err.NotFound(message: new string('m', 10_000))],
            new GrpcErrorOptions { MaxDetailsBytes = 0 });

        Assert.Empty(status.Details);
        Assert.Equal(GrpcErrorOptions.MaxMessageBytes, status.Message.Length);
    }

    [Fact]
    public void ToStatus_MessageCutMidCharacter_KeepsTheCharactersWhole()
    {
        // Four bytes each, so a cut that ignored characters would leave half of one behind.
        var status = GrpcErrorStatus.ToStatus(
            [Err.NotFound(message: string.Concat(Enumerable.Repeat("😀", 200)))],
            new GrpcErrorOptions { MaxDetailsBytes = 61 });

        // A cut through a character survives encoding as a replacement character, so the round trip finds it.
        Assert.Equal(status.Message, Encoding.UTF8.GetString(Encoding.UTF8.GetBytes(status.Message)));
        Assert.DoesNotContain("\uFFFD", status.Message, StringComparison.Ordinal);
        Assert.True(Encoding.UTF8.GetByteCount(status.Message) <= 61);
    }

    /// <summary>
    /// Builds the exception a peer sending one detail would produce.
    /// </summary>
    /// <param name="code">The status code of the call.</param>
    /// <param name="detail">The error the peer packed.</param>
    /// <returns>The exception a caller would catch.</returns>
    private static RpcException Sent(StatusCode code, ErrorDetail detail)
    {
        var details = new ErrorDetails();
        details.Errors.Add(detail);

        var status = new GoogleRpcStatus { Code = (int)code, Message = "sent" };
        status.Details.Add(Any.Pack(details));

        return status.ToRpcException();
    }

    /// <summary>
    /// Reads back what a status packed.
    /// </summary>
    /// <param name="status">The status to read.</param>
    /// <returns>The packed errors.</returns>
    private static ErrorDetails Unpack(GoogleRpcStatus status)
    {
        return status.Details[0].Unpack<ErrorDetails>();
    }

    /// <summary>
    /// Reads one payload value.
    /// </summary>
    /// <param name="error">The error carrying it.</param>
    /// <param name="key">The key to read.</param>
    /// <returns>The value.</returns>
    private static JsonElement Element(ErrorDefinition error, string key)
    {
        return Assert.IsType<JsonElement>(error.Payload![key]);
    }
}
