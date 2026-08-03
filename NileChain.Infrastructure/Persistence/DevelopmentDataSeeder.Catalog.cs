using NileChain.Domain.Enums;

namespace NileChain.Infrastructure.Persistence;

/// <summary>
/// Static catalogs for the development seeder — Egyptian names, governorates, crops, copy.
/// </summary>
public static partial class DevelopmentDataSeeder
{
    private const string SeedPassword = "Seed123@!";
    private const string SeedMarker = "[SEED]";
    private const string MarketPriceSource = "DevelopmentSeed";
    private const string SuperAdminEmail = "seed.superadmin@nilechain.dev";

    private static readonly string[] AdminEmails =
    [
        "seed.admin1@nilechain.dev",
        "seed.admin2@nilechain.dev",
        "seed.admin3@nilechain.dev"
    ];

    private static readonly string[] CropNames =
    [
        "Wheat", "Potato", "Corn", "Tomato", "Rice", "Cotton",
        "Onion", "Orange", "Mango", "Sugarcane", "Beans", "Cucumber"
    ];

    private static readonly string[] CertificationNames =
    [
        "Organic", "GlobalGAP", "ISO 22000", "Fair Trade", "HACCP", "ISO 9001"
    ];

    /// <summary>All 27 Egyptian governorates (aligned with frontend register list).</summary>
    private static readonly string[] Governorates =
    [
        "Alexandria", "Aswan", "Asyut", "Beheira", "Beni Suef", "Cairo",
        "Dakahlia", "Damietta", "Faiyum", "Gharbia", "Giza", "Ismailia",
        "Kafr El Sheikh", "Luxor", "Matrouh", "Minya", "Monufia", "New Valley",
        "North Sinai", "Port Said", "Qalyubia", "Qena", "Red Sea", "Sharqia",
        "Sohag", "South Sinai", "Suez"
    ];

    private static readonly string[] EgyptianFirstNames =
    [
        "Ahmed", "Mohamed", "Mahmoud", "Omar", "Hassan", "Youssef", "Ibrahim",
        "Khaled", "Mostafa", "Amr", "Tarek", "Hossam", "Karim", "Sherif",
        "Nader", "Samir", "Fady", "Ramy", "Walid", "Ashraf", "Sara", "Nour",
        "Fatma", "Mona", "Heba", "Amira", "Dina", "Layla", "Yasmin", "Nada"
    ];

    private static readonly string[] EgyptianLastNames =
    [
        "Hassan", "Ali", "Ibrahim", "Mahmoud", "Said", "Farouk", "Nassar",
        "El-Sayed", "Abdelrahman", "Mansour", "Soliman", "Osman", "Gamal",
        "Fathy", "Zaki", "Helmy", "Shawky", "Rashad", "Kamal", "Badawy"
    ];

    private static readonly string[] FarmNamePrefixes =
    [
        "Green Valley", "Delta Gold", "Nile Breeze", "Sunrise", "Fertile Crescent",
        "Palm Grove", "Golden Harvest", "Riverbank", "Oasis", "Sahara Edge",
        "Lotus Fields", "Pharaoh Crops", "Upper Nile", "Lower Delta", "Cotton Road",
        "Wheat Crown", "Emerald Acre", "Blue Nile", "Red Earth", "Silver Canal",
        "Amber Soil", "Crescent Moon", "Desert Bloom", "Valley Star", "Harvest Gate"
    ];

    private static readonly string[] FarmLocations =
    [
        "Abu Sir", "Zagazig", "Damanhur", "Mansoura", "Tanta", "Minya City",
        "Asyut Center", "Faiyum Oasis", "Kafr El Dawar", "Belbeis", "Mit Ghamr",
        "Desouk", "Quesna", "Beni Mazar", "Mallawi", "Sohag City", "Qena City",
        "Kom Ombo", "Edku", "Rosetta", "Shebin El Kom", "Banha", "Ismailia City",
        "Port Said South", "Luxor West Bank"
    ];

    private static readonly string[] FactoryNameTemplates =
    [
        "Misr {0} Industries",
        "Nile {0} Processing Co.",
        "Delta {0} Foods",
        "Cairo {0} Manufacturing",
        "Alexandria {0} Packing",
        "Upper Egypt {0} Mills",
        "Pharaoh {0} Canning",
        "Sphinx {0} AgriFood",
        "Pyramid {0} Exports",
        "Lotus {0} Beverages"
    ];

    private static readonly string[] FactoryIndustries =
    [
        "Food Processing", "Canning & Packaging", "Flour Milling", "Dairy & Juice",
        "Frozen Vegetables", "Tomato Paste", "Rice Milling", "Oil Extraction",
        "Snack Foods", "Export Packing"
    ];

    private static readonly string[] FactoryLocations =
    [
        "10th of Ramadan", "6th of October", "Borg El Arab", "Sadat City",
        "New Borg El Arab", "Obour City", "New Cairo Industrial", "Ain Sokhna",
        "Damietta Port Zone", "Alexandria Free Zone"
    ];

    private static readonly SoilType[] SoilTypes =
    [
        SoilType.Clay, SoilType.Sandy, SoilType.Loamy, SoilType.Silty,
        SoilType.Peaty, SoilType.Chalky, SoilType.Saline
    ];

    private static readonly Dictionary<string, decimal> BaseCropPrices =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["Wheat"] = 12000m,
            ["Potato"] = 8000m,
            ["Corn"] = 9500m,
            ["Tomato"] = 7000m,
            ["Rice"] = 11000m,
            ["Cotton"] = 45000m,
            ["Onion"] = 6500m,
            ["Orange"] = 9000m,
            ["Mango"] = 14000m,
            ["Sugarcane"] = 5500m,
            ["Beans"] = 16000m,
            ["Cucumber"] = 6000m
        };

    private static readonly string[] QualitySpecTemplates =
    [
        "Grade A, moisture < 12%",
        "Export grade, low aflatoxin",
        "Processing grade, firm texture",
        "Egyptian short-grain, clean",
        "Industrial size 45-65mm",
        "Bread quality, protein > 11%",
        "Organic preferred, pesticide residue below MRLs",
        "Uniform color, no mechanical damage",
        "Cold-chain ready, harvest within 48h of delivery",
        "Standard domestic grade, sorted and packed"
    ];

    private static readonly string[] ReviewComments =
    [
        "Reliable delivery and excellent crop quality throughout the season.",
        "Good quality overall, slight delay on the last shipment.",
        "Consistent supply — will gladly work with them again.",
        "Acceptable product, packaging and labeling need improvement.",
        "Professional communication and fair contract terms.",
        "Outstanding harvest quality; exceeded moisture specifications.",
        "Payment was timely and paperwork was clear.",
        "Delivery window slipped by two days but quality compensated.",
        "Poor coordination on logistics; rating reflects that.",
        "Top-tier partner for export-bound produce.",
        "Average experience — met the minimum quality bar.",
        "Excellent traceability documents and certification support.",
        "Crop arrived bruised; expect better handling next time.",
        "Transparent pricing and helpful negotiation.",
        "One of the best farms we matched with this quarter."
    ];

    private static readonly string[] MessageTemplatesFactory =
    [
        "Hello — we are interested in your crop for our current supply request.",
        "Could you confirm available tonnage and earliest delivery date?",
        "Our quality team needs moisture and grade certificates before signing.",
        "We can offer a slight premium if you guarantee delivery within 14 days.",
        "Please review the draft contract and share any amendments.",
        "Thanks for the samples — they look promising for processing.",
        "Can you also provide your latest GlobalGAP certificate scan?",
        "We would like to schedule a farm visit next week if possible.",
        "Pricing looks fair; awaiting internal approval from procurement.",
        "Confirming logistics: our trucks can collect from your packing shed."
    ];

    private static readonly string[] MessageTemplatesFarm =
    [
        "Thank you for reaching out — we can meet the requested volume.",
        "Our harvest window aligns with your delivery date.",
        "Specs confirmed. Moisture typically stays under 11.5%.",
        "We prefer payment within 15 days of delivery as discussed.",
        "Attached conceptually: land title and organic cert already on profile.",
        "Happy to host a visit; Mondays and Wednesdays work best.",
        "We can commit 80% of the volume now and the rest after second cut.",
        "Please send the contract draft when ready — we will review carefully.",
        "Transport from our side is available if that reduces your cost.",
        "Looking forward to a long-term supply relationship."
    ];

    private static readonly string[] NotificationTypes =
    [
        "Match", "Contract", "Message", "System", "Admin", "Review", "Payment"
    ];

    private static readonly (string Category, string Title, string Body)[] KnowledgeDocuments =
    [
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
    ];

    private static string FarmEmail(int index) => $"seed.farm{index:D2}@nilechain.dev";
    private static string FactoryEmail(int index) => $"seed.factory{index:D2}@nilechain.dev";

    private static IEnumerable<string> AllSeedFarmEmails() =>
        Enumerable.Range(1, 25).Select(FarmEmail);

    private static IEnumerable<string> AllSeedFactoryEmails() =>
        Enumerable.Range(1, 10).Select(FactoryEmail);
}
