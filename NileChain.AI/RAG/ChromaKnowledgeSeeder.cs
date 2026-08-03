using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;

namespace NileChain.AI.RAG;

/// <summary>
/// Best-effort development seeding of the Chroma knowledge collection.
/// Soft-fails when Chroma is unavailable so API startup is never blocked.
/// </summary>
public static class ChromaKnowledgeSeeder
{
    private const string CollectionName = "nilechain_knowledge";

    public static async Task<int> SeedAsync(
        ChromaService chromaService,
        ILogger? logger = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var docs = BuildDocuments();
            var upserted = await chromaService.UpsertDocumentsAsync(
                CollectionName,
                docs,
                cancellationToken);

            logger?.LogInformation(
                "Chroma knowledge seed upserted {Count} documents into '{Collection}'.",
                upserted,
                CollectionName);
            Console.WriteLine($"Chroma Documents upserted: {upserted}");
            return upserted;
        }
        catch (Exception ex)
        {
            logger?.LogWarning(ex,
                "Chroma knowledge seed skipped (service unavailable or ingest unsupported).");
            Console.WriteLine("Chroma Documents upserted: 0 (skipped — Chroma unavailable)");
            return 0;
        }
    }

    private static List<ChromaSeedDocument> BuildDocuments()
    {
        // Keep in sync with DevelopmentDataSeeder.KnowledgeDocuments categories.
        var items = new (string Category, string Title, string Body)[]
        {
            ("Quality Standards", "Egyptian Wheat Grade A Standards",
                "Grade A Egyptian wheat requires moisture below 12%, protein above 11%, and foreign matter under 1%. Mills prefer uniform kernel size for consistent flour extraction."),
            ("Quality Standards", "Tomato Processing Grade Requirements",
                "Processing tomatoes should be firm, uniformly red, and free of cracks. Brix levels of 4.5–5.5% are preferred for paste production in Egyptian canneries."),
            ("Quality Standards", "Rice Moisture and Broken Grain Limits",
                "Egyptian short-grain rice for domestic mills should not exceed 14% moisture. Broken grain ratio above 15% typically downgrades the lot to industrial grade."),
            ("Contract Templates", "Standard Forward Supply Contract Clauses",
                "A NileChain forward contract should include crop type, quantity in tons, quality specs, price per ton, delivery window, force majeure, and dispute resolution under Egyptian civil code."),
            ("Contract Templates", "Payment Terms for Agri Supply Deals",
                "Common structures: 30% advance on signing, 70% on delivery inspection. Late payment interest of 1% per month is frequently negotiated between factories and farms."),
            ("Contract Templates", "Rejection and Reinspection Clause",
                "Buyer may reject lots failing lab tests within 48 hours of delivery. Seller may request a second accredited lab reinspection at shared cost."),
            ("Agri Science", "Wheat Irrigation Scheduling in the Delta",
                "Delta wheat typically needs 4–5 irrigations. Avoid waterlogging during grain fill. Deficit irrigation late season can improve protein concentration."),
            ("Agri Science", "Potato Tuber Initiation Best Practices",
                "Maintain cool soil and consistent moisture during tuber initiation. Sudden drought followed by heavy irrigation increases hollow heart risk."),
            ("Agri Science", "Corn Nitrogen Timing for Nile Valley",
                "Split nitrogen applications at planting and V6–V8 stages improve uptake efficiency on sandy soils common in reclaimed desert farms."),
            ("Market Intelligence", "Seasonal Wheat Price Patterns Egypt",
                "Egyptian wheat farm-gate prices typically firm after harvest as mills rebuild stocks. Import parity with Black Sea wheat sets an upper bound in short crop years."),
            ("Market Intelligence", "Tomato Price Volatility Drivers",
                "Tomato prices spike during heat waves that reduce open-field yields. Greenhouse supply from Beheira and Ismailia dampens winter peaks."),
            ("Market Intelligence", "Cotton Export Window Notes",
                "Egyptian long-staple cotton commands premiums in May–August export contracts. Early commitments reduce basis risk for gins."),
            ("Egyptian Regulations", "Agricultural Quarantine Export Rules",
                "Exports require phytosanitary certificates from MALR quarantine. Residue testing must meet destination MRLs before container sealing."),
            ("Egyptian Regulations", "Farm Land Documentation Basics",
                "Verified farms should hold agricultural land title or usufruct documents. Municipal tax receipts support profile completeness reviews."),
            ("Egyptian Regulations", "Food Factory Licensing Overview",
                "Food processors need National Food Safety Authority registration, industrial license, and environmental approvals before scaling offtake contracts."),
            ("Food Safety", "HACCP Critical Control Points for Packhouses",
                "Packhouses should monitor wash-water chlorine, cold-room temperature, and metal detection. Records must be retained for traceability audits."),
            ("Food Safety", "Aflatoxin Control in Corn Storage",
                "Store corn below 13% moisture with aeration. Test incoming lots; reject visible mold. Aflatoxin B1 limits for feed vs food differ."),
            ("Food Safety", "Hygiene for Fresh Produce Handling",
                "Workers must use clean harvest crates, sanitized knives, and handwashing stations. Field toilets should be distant from packing lines."),
            ("Export Standards", "GlobalGAP for Egyptian Growers",
                "GlobalGAP requires risk assessments, spray records, worker welfare, and traceability from field block to packed unit. Annual audits renew certification."),
            ("Export Standards", "EU Entry Requirements for Fresh Produce",
                "EU consignments need phytosanitary certificates, residue compliance, and often GLOBALG.A.P. or equivalent private standards for supermarket buyers."),
            ("Export Standards", "Cold Chain for Orange Exports",
                "Maintain 3–5°C with high humidity for navel oranges. Breaks in cold chain accelerate decay and claim rates at destination."),
            ("Irrigation", "Furrow vs Drip on Clay Delta Soils",
                "Furrow irrigation remains common on heavy clay. Drip improves efficiency on raised beds for vegetables but needs filtration against Nile silt."),
            ("Irrigation", "Scheduling with Evapotranspiration Estimates",
                "Use local ET0 and crop coefficients to schedule irrigations. Skip events after significant rainfall to avoid root hypoxia."),
            ("Irrigation", "Salinity Management in Coastal Farms",
                "Leaching fractions and gypsum applications help manage saline intrusion near coastal Beheira and Alexandria farms."),
            ("Soil", "Loamy Soil Advantages for Vegetables",
                "Loamy soils balance drainage and water holding — ideal for tomato and cucumber. Add compost annually to sustain organic matter."),
            ("Soil", "Reclaimed Desert Sand Amendments",
                "Sandy reclaimed soils need organic matter, frequent fertigation, and windbreaks. Micronutrient deficiencies (Zn, Fe) are common."),
            ("Soil", "Soil Testing Frequency Recommendations",
                "Test pH, EC, NPK, and organic matter before each major season. Map variability across large holdings for variable-rate fertilizer."),
            ("Pests", "Wheat Aphid Monitoring Thresholds",
                "Scout weekly from tillering. Treat when thresholds are exceeded; rotate chemistries to delay resistance. Beneficial insects reduce pressure."),
            ("Pests", "Tomato Tuta Absoluta Management",
                "Combine pheromone traps, resistant varieties where available, and approved insecticides. Destroy infested residues after harvest."),
            ("Pests", "Rice Stem Borer Cultural Controls",
                "Synchronized planting and destruction of stubbles reduce stem borer carryover between seasons in Dakahlia and Kafr El Sheikh."),
            ("Fertilizers", "Balanced NPK for Egyptian Wheat",
                "Typical wheat programs use basal P and K with split N. Excess late N can lodge crops on fertile Delta soils."),
            ("Fertilizers", "Organic Fertilizer Options for Certified Farms",
                "Composted manure and approved organic fertilizers maintain Organic certification. Keep application records for auditors."),
            ("Fertilizers", "Micronutrients in Alkaline Soils",
                "High pH Egyptian soils lock Fe and Zn. Foliar sprays or chelated forms improve vegetable leaf color and yield.")
        };

        return items.Select((item, i) => new ChromaSeedDocument(
            Id: $"seed-doc-{i:D3}",
            Document: $"# {item.Title}\n\nCategory: {item.Category}\n\n{item.Body}",
            Metadata: new Dictionary<string, object>
            {
                ["category"] = item.Category,
                ["title"] = item.Title,
                ["source"] = "DevelopmentSeed"
            })).ToList();
    }
}

public sealed record ChromaSeedDocument(
    string Id,
    string Document,
    Dictionary<string, object> Metadata);
