using ApricotFramework.ErrorDefinitions;
using ApricotFramework.Grpc.ErrorDefinitions.Contract;
using Google.Protobuf;
using Grpc.Core;

namespace ApricotFramework.Grpc.ErrorDefinitions.Tests;

/// <summary>
/// Pins the bytes on the wire. The expected values were encoded by hand from the protobuf
/// specification, not produced by this library, so agreeing with them means something.
/// </summary>
/// <remarks>
/// A failure here is a change to a contract other services already speak. Do not update an expected
/// value unless breaking every peer is the intent.
/// </remarks>
public class GoldenWireTests
{
    /// <summary>
    /// <c>ErrorDetails</c> holding one error with a one-key payload.
    /// </summary>
    private const string Details =
        "Cj8KCW5vdF9mb3VuZBIPT1JERVJfTk9UX0ZPVU5EGg1ubyBzdWNoIG9yZGVyIhIKEAoHb3JkZXJJZBIFGgNBLTE=";

    /// <summary>
    /// The same error with no payload and the truncated flag set.
    /// </summary>
    private const string TruncatedDetails =
        "CisKCW5vdF9mb3VuZBIPT1JERVJfTk9UX0ZPVU5EGg1ubyBzdWNoIG9yZGVyEAE=";

    /// <summary>
    /// The whole <c>google.rpc.Status</c>, which is what the trailer carries. Pins the type URL.
    /// </summary>
    private const string Status =
        "CAUSDW5vIHN1Y2ggb3JkZXIadwoydHlwZS5nb29nbGVhcGlzLmNvbS9hcHJpY290LmVycm9ycy52MS5FcnJvckRldGFpbHMSQQo/Cglub3RfZm91bmQSD09SREVSX05PVF9GT1VORBoNbm8gc3VjaCBvcmRlciISChAKB29yZGVySWQSBRoDQS0x";

    [Fact]
    public void ToStatus_OneErrorWithPayload_MatchesTheGoldenStatus()
    {
        Assert.Equal(Status, Convert.ToBase64String(GrpcErrorStatus.ToStatus([NotFound()]).ToByteArray()));
    }

    [Fact]
    public void ToStatus_OneErrorWithPayload_PacksTheGoldenDetails()
    {
        var status = GrpcErrorStatus.ToStatus([NotFound()]);

        Assert.Equal(Details, Convert.ToBase64String(status.Details[0].Value.ToByteArray()));
    }

    [Fact]
    public void ToStatus_BudgetBelowThePayload_PacksTheGoldenTruncatedDetails()
    {
        // Room for the error but not for its payload: the golden details are 65 bytes with it, 47 without.
        var status = GrpcErrorStatus.ToStatus([NotFound()], new GrpcErrorOptions { MaxDetailsBytes = 135 });

        Assert.Equal(TruncatedDetails, Convert.ToBase64String(status.Details[0].Value.ToByteArray()));
    }

    [Fact]
    public void GoldenDetails_ReadByAnIndependentDecoder_HoldTheContractFields()
    {
        var fields = WireReader.ReadFields(Convert.FromBase64String(Details));

        // Field 1 is the repeated error; field 2 is the truncated flag, absent because it is false.
        var error = WireReader.ReadFields(Assert.IsType<byte[]>(Assert.Single(fields[1])));

        Assert.Equal("not_found", WireReader.Text(error[1][0]));
        Assert.Equal("ORDER_NOT_FOUND", WireReader.Text(error[2][0]));
        Assert.Equal("no such order", WireReader.Text(error[3][0]));
        Assert.False(fields.ContainsKey(2));
        Assert.True(error.ContainsKey(4));
    }

    [Fact]
    public void GoldenStatus_ReadByAnIndependentDecoder_CarriesTheContractTypeUrl()
    {
        var status = WireReader.ReadFields(Convert.FromBase64String(Status));
        var any = WireReader.ReadFields(Assert.IsType<byte[]>(Assert.Single(status[3])));

        Assert.Equal(5UL, Assert.IsType<ulong>(Assert.Single(status[1])));
        Assert.Equal("type.googleapis.com/apricot.errors.v1.ErrorDetails", WireReader.Text(any[1][0]));
    }

    [Fact]
    public void ToErrors_GoldenStatusWithTheMessageNameAltered_FallsBackToTheStatusCode()
    {
        var bytes = Convert.FromBase64String(Status);
        // Only the name after the last slash identifies the message, so that is what has to change.
        bytes[bytes.AsSpan().IndexOf("apricot.errors.v1.ErrorDetails"u8)]++;

        var trailers = new Metadata { { MetadataExtensions.StatusDetailsTrailerName, bytes } };
        var status = new global::Grpc.Core.Status(StatusCode.NotFound, "no such order");

        var only = Assert.Single(GrpcErrorStatus.ToErrors(new RpcException(status, trailers)));

        Assert.Equal(ErrorKinds.NotFound, only.Kind);
        Assert.Equal(ErrorCodes.ForKind(ErrorKinds.NotFound), only.Code);
        Assert.Equal("no such order", only.Message);
    }

    /// <summary>
    /// The error the golden vectors describe.
    /// </summary>
    /// <returns>The error.</returns>
    private static ErrorDefinition NotFound()
    {
        return Err.NotFound(
            "ORDER_NOT_FOUND",
            "no such order",
            new Dictionary<string, object?> { ["orderId"] = "A-1" });
    }

    /// <summary>
    /// A protobuf reader written from the specification, so a golden value is not compared against the
    /// generated code that produced it.
    /// </summary>
    private static class WireReader
    {
        /// <summary>
        /// Reads a message into its fields.
        /// </summary>
        /// <param name="bytes">The encoded message.</param>
        /// <returns>The values of each field number, in order.</returns>
        internal static Dictionary<int, List<object>> ReadFields(ReadOnlySpan<byte> bytes)
        {
            var fields = new Dictionary<int, List<object>>();
            var at = 0;

            while (at < bytes.Length)
            {
                var key = ReadVarint(bytes, ref at);
                var number = (int)(key >> 3);
                var wire = (int)(key & 7);

                object value = wire switch
                {
                    0 => ReadVarint(bytes, ref at),
                    2 => ReadBytes(bytes, ref at),
                    _ => throw new InvalidOperationException($"Unexpected wire type {wire}."),
                };

                if (!fields.TryGetValue(number, out var values))
                {
                    values = [];
                    fields[number] = values;
                }

                values.Add(value);
            }

            return fields;
        }

        /// <summary>
        /// Reads a length-delimited value as text.
        /// </summary>
        /// <param name="value">The value read from a field.</param>
        /// <returns>The text.</returns>
        internal static string Text(object value)
        {
            return System.Text.Encoding.UTF8.GetString((byte[])value);
        }

        /// <summary>
        /// Reads a base 128 varint.
        /// </summary>
        /// <param name="bytes">The message.</param>
        /// <param name="at">Where to read from, advanced past the value.</param>
        /// <returns>The value.</returns>
        private static ulong ReadVarint(ReadOnlySpan<byte> bytes, ref int at)
        {
            ulong value = 0;
            var shift = 0;

            while (true)
            {
                var b = bytes[at++];
                value |= (ulong)(b & 0x7F) << shift;

                if ((b & 0x80) == 0)
                {
                    return value;
                }

                shift += 7;
            }
        }

        /// <summary>
        /// Reads a length-prefixed byte string.
        /// </summary>
        /// <param name="bytes">The message.</param>
        /// <param name="at">Where to read from, advanced past the value.</param>
        /// <returns>The bytes.</returns>
        private static byte[] ReadBytes(ReadOnlySpan<byte> bytes, ref int at)
        {
            var length = (int)ReadVarint(bytes, ref at);
            var value = bytes.Slice(at, length).ToArray();
            at += length;

            return value;
        }
    }
}
