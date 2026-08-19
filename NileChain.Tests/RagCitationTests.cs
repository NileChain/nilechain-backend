using System.Net;
using System.Text;
using NileChain.AI.RAG;

namespace NileChain.Tests;

public class RagCitationTests
{
    private static RagContext ThreeSources() =>
        RagContext.FromSections(
        [
            (RagPipeline.SectionQuality, new RagChunk("q1", "مواصفات القمح المصري", "الرطوبة القصوى المقبولة صناعياً.")),
            (RagPipeline.SectionContract, new RagChunk("c1", "نموذج بند التسليم", "أحكام التسليم عند بوابة المصنع.")),
            (RagPipeline.SectionAgriScience, new RagChunk("a1", "دورة زراعة القمح", "مواعيد الزراعة والحصاد."))
        ]);

    [Fact]
    public void FromSections_NumbersSourcesAcrossSectionsInOrder()
    {
        var rag = ThreeSources();

        Assert.True(rag.HasKnowledge);
        Assert.Equal([1, 2, 3], rag.Citations.Select(c => c.Index));
        Assert.Equal(RagPipeline.SectionQuality, rag.Citations[0].Section);
        Assert.Equal("نموذج بند التسليم", rag.Citations[1].Title);
    }

    [Fact]
    public void CitedText_LabelsPassages_WhilePlainTextStaysMarkerFree()
    {
        var rag = ThreeSources();

        Assert.Contains("[1]", rag.CitedText);
        Assert.Contains("[3]", rag.CitedText);
        Assert.DoesNotContain("[1]", rag.PlainText);
        Assert.Contains("الرطوبة القصوى المقبولة صناعياً.", rag.PlainText);
    }

    [Fact]
    public void ResolveCitations_KeepsOnlyTheSourcesTheAnswerCited()
    {
        var (answer, used) = ThreeSources()
            .ResolveCitations("الرطوبة القصوى محددة [1] ويتم التسليم عند البوابة [2].");

        Assert.Equal([1, 2], used.Select(c => c.Index));
        Assert.Contains("[1]", answer);
        Assert.Contains("[2]", answer);
    }

    [Fact]
    public void ResolveCitations_DropsMarkersForSourcesThatWereNeverRetrieved()
    {
        // A dangling [7] is a fabricated source, so it must not reach the UI.
        var (answer, used) = ThreeSources()
            .ResolveCitations("حسب المعيار الدولي [7] فإن الرطوبة أقل [1].");

        Assert.Equal([1], used.Select(c => c.Index));
        Assert.DoesNotContain("[7]", answer);
        Assert.Contains("[1]", answer);
    }

    [Fact]
    public void ResolveCitations_RepeatedMarker_IsListedOnce()
    {
        var (_, used) = ThreeSources().ResolveCitations("[2] ثم [2] مرة أخرى.");

        Assert.Single(used);
        Assert.Equal(2, used[0].Index);
    }

    [Fact]
    public void ResolveCitations_WithoutKnowledge_StripsEveryMarker()
    {
        var (answer, used) = RagContext.Empty().ResolveCitations("حسب المصدر [1] فإن هذا صحيح.");

        Assert.Empty(used);
        Assert.DoesNotContain("[1]", answer);
    }

    [Fact]
    public void Empty_And_Unavailable_AreBothUngrounded()
    {
        Assert.False(RagContext.Empty().HasKnowledge);
        Assert.False(RagContext.Unavailable("down").HasKnowledge);

        // Availability still distinguishes "nothing matched" from "Chroma is down".
        Assert.True(RagContext.Empty().IsAvailable);
        Assert.False(RagContext.Unavailable("down").IsAvailable);
    }

    [Fact]
    public void Citation_Excerpt_IsTruncated()
    {
        var long_ = new string('ن', 900);
        var rag = RagContext.FromSections([(RagPipeline.SectionQuality, new RagChunk("q", "t", long_))]);

        Assert.True(rag.Citations[0].Excerpt.Length < long_.Length);
        Assert.EndsWith("…", rag.Citations[0].Excerpt);
    }

    [Fact]
    public async Task ChromaService_QueryAsync_CarriesIdsAndTitles()
    {
        const string body = """
            {
              "ids": [["kb-7", "kb-9"]],
              "documents": [["نص أول", "نص ثانٍ"]],
              "metadatas": [[{"title": "دليل الجودة"}, {"source": "لائحة العقود"}]]
            }
            """;

        var lookup = await Query(body);

        Assert.True(lookup.IsAvailable);
        Assert.Equal(["kb-7", "kb-9"], lookup.Chunks.Select(c => c.Id));
        Assert.Equal(["دليل الجودة", "لائحة العقود"], lookup.Chunks.Select(c => c.Title));
        Assert.Contains("نص أول", lookup.Content);
    }

    [Fact]
    public async Task ChromaService_MissingMetadata_FallsBackToPositionalIdAndUnnamedTitle()
    {
        var lookup = await Query("""{"documents": [["نص بلا وسم"]]}""");

        var chunk = Assert.Single(lookup.Chunks);
        Assert.Equal("chunk-0-0", chunk.Id);
        Assert.Equal(RagChunk.UnknownTitle, chunk.Title);
    }

    [Fact]
    public async Task ChromaService_NoDocuments_IsEmptyNotUnavailable()
    {
        var lookup = await Query("""{"documents": []}""");

        Assert.True(lookup.IsAvailable);
        Assert.Empty(lookup.Chunks);
    }

    [Fact]
    public async Task ChromaService_ServerError_IsUnavailable()
    {
        var lookup = await Query("{}", HttpStatusCode.ServiceUnavailable);

        Assert.False(lookup.IsAvailable);
        Assert.Empty(lookup.Chunks);
    }

    private static async Task<ChromaLookupResult> Query(
        string responseBody,
        HttpStatusCode status = HttpStatusCode.OK)
    {
        var http = new HttpClient(new StubHandler(responseBody, status))
        {
            BaseAddress = new Uri("http://chroma.test")
        };

        return await new ChromaService(http).QueryAsync("قمح");
    }

    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly string _body;
        private readonly HttpStatusCode _status;

        public StubHandler(string body, HttpStatusCode status)
        {
            _body = body;
            _status = status;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken) =>
            Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, "application/json")
            });
    }
}
