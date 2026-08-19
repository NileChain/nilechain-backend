using System.Diagnostics;

namespace NileChain.AI.Telemetry;

/// <summary>
/// Meters every HTTP round-trip an OpenAI-compatible kernel makes into the run's ledger.
/// This sits at the transport layer on purpose: with native tool calling a single
/// <c>GetChatMessageContentsAsync</c> can issue several completions, and only the transport
/// sees them all.
/// </summary>
public sealed class LlmUsageHandler : DelegatingHandler
{
    private readonly LlmUsageLedger _usage;
    private readonly string _provider;
    private readonly string _model;

    public LlmUsageHandler(LlmUsageLedger usage, string provider, string model, HttpMessageHandler innerHandler)
        : base(innerHandler)
    {
        _usage = usage;
        _provider = provider;
        _model = model;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var sw = Stopwatch.StartNew();
        HttpResponseMessage response;
        try
        {
            response = await base.SendAsync(request, cancellationToken);
        }
        catch
        {
            sw.Stop();
            // A failed provider call still burned wall-clock time worth reporting.
            _usage.Record(_provider, _model, null, null, sw.ElapsedMilliseconds);
            throw;
        }

        sw.Stop();

        // Buffer so reading usage here does not consume the stream the SDK still needs.
        await response.Content.LoadIntoBufferAsync(cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        var (prompt, completion) = LlmUsageJson.TryRead(body);

        _usage.Record(_provider, _model, prompt, completion, sw.ElapsedMilliseconds);
        return response;
    }
}
