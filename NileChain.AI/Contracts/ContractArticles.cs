namespace NileChain.AI.Contracts;

/// <summary>
/// One article of the supply contract.
/// <paramref name="FactLine"/> carries the agreed numbers and is always emitted verbatim.
/// <paramref name="DefaultBody"/> is the legal prose used when no model draft survives validation.
/// </summary>
public sealed record ContractArticle(
    string Key,
    string Title,
    string? FactLine,
    string DefaultBody);

/// <summary>
/// The fixed skeleton of a NileChain supply contract: nine articles in a fixed order,
/// with the numeric content bound to <see cref="ContractFacts"/>.
/// </summary>
public static class ContractArticles
{
    public const string Bismillah = "بسم الله الرحمن الرحيم";
    public const string Title = "عقد توريد زراعي";

    /// <summary>Prose keys a model draft is allowed to fill in.</summary>
    public static readonly IReadOnlyList<string> Keys =
    [
        "subject",
        "quantitySpecs",
        "priceTerms",
        "deliveryRisk",
        "obligations",
        "penalties",
        "forceMajeure",
        "disputes",
        "general"
    ];

    public static string Preamble(ContractFacts facts) =>
        string.Join(
            Environment.NewLine,
            "إنه في تاريخ تحرير هذا العقد إلكترونياً عبر منصة NileChain، تم الاتفاق بين كلٍ من:",
            $"الطرف الأول (المشتري / المصنع): {facts.FactoryName}، ويُشار إليه لاحقاً بـ «المشتري».",
            $"الطرف الثاني (المورد / المزرعة): {facts.FarmName}، ويُشار إليه لاحقاً بـ «المورد».");

    public static IReadOnlyList<ContractArticle> Build(ContractFacts facts)
    {
        ArgumentNullException.ThrowIfNull(facts);

        return
        [
            new ContractArticle(
                "subject",
                "المادة الأولى — موضوع العقد",
                $"محل هذا العقد: توريد محصول «{facts.CropType}».",
                "يتعهد المورد بتوريد المحصول المبيَّن أعلاه إلى المشتري وفقاً لأحكام هذا العقد، ويتعهد المشتري بقبول التوريد المطابق للمواصفات وسداد الثمن المتفق عليه."),

            new ContractArticle(
                "quantitySpecs",
                "المادة الثانية — الكمية والمواصفات",
                $"الكمية المتعاقد عليها: {facts.QuantityTons:0.##} طن متري من محصول «{facts.CropType}». مواصفات الجودة: {facts.QualityArabic}",
                "يلتزم المورد بمطابقة الكمية والمواصفات المذكورة أعلاه، وللمشتري حق الفحص والمعاينة قبل الاستلام ورفض ما لا يطابق المواصفات."),

            new ContractArticle(
                "priceTerms",
                "المادة الثالثة — الثمن وشروط السداد",
                $"سعر الطن: {facts.PricePerTon:0.##} جنيه مصري. القيمة الإجمالية: {facts.TotalValueEgp:0.##} جنيه مصري. يتم السداد بنسبة 30٪ مقدماً، و70٪ عند الاستلام والقبول.",
                "يتم السداد عبر وسائل الدفع المتاحة على منصة NileChain، ويُعد سجل المنصة دليلاً على تواريخ ومبالغ السداد."),

            new ContractArticle(
                "deliveryRisk",
                "المادة الرابعة — التسليم والنقل والمخاطر",
                $"تاريخ التسليم الأقصى: {facts.DeliveryDateArabic}. نقطة التسليم: {facts.DeliveryPointArabic}. أجرة النقل يتحملها: {facts.FreightPayerArabic}. مخاطر التلف أثناء النقل يتحملها: {facts.TransitRiskArabic}.",
                "رفض الحمولة عند بوابة المصنع قبل الاستلام يعيد العربات بحسب من يملك النقل، ويعيد أي مبلغ محجوز للمشتري."),

            new ContractArticle(
                "obligations",
                "المادة الخامسة — التزامات الطرفين",
                null,
                "يلتزم المورد بتوريد الكمية في الموعد والمواصفات المتفق عليها، ويلتزم المشتري بتيسير إجراءات الاستلام والفحص وسداد المستحقات وفق جدول السداد."),

            new ContractArticle(
                "penalties",
                "المادة السادسة — الجزاءات",
                null,
                "إذا أخلّ أي طرف بالتزام جوهري دون عذر مشروع، يحق للطرف الآخر المطالبة بالتعويض الاتفاقي عن الضرر المباشر الناشئ عن الإخلال، مع الاحتفاظ بالحقوق القانونية الأخرى."),

            new ContractArticle(
                "forceMajeure",
                "المادة السابعة — القوة القاهرة",
                null,
                "لا يُسأل أي طرف عن التأخر أو عدم التنفيذ الناتج عن قوة قاهرة خارجة عن إرادته، على أن يُخطر الطرف الآخر فور العلم، وأن يسعى لاستئناف التنفيذ فور زوال السبب."),

            new ContractArticle(
                "disputes",
                "المادة الثامنة — فض النزاعات",
                null,
                "يخضع هذا العقد للقانون المصري، وتختص محاكم القاهرة الاقتصادية بنظر أي نزاع ينشأ عنه أو عن تنفيذه."),

            new ContractArticle(
                "general",
                "المادة التاسعة — أحكام عامة",
                string.Join(
                    Environment.NewLine,
                    "تاريخ بدء العقد: يُحدَّد بتاريخ اكتمال التوقيع الإلكتروني من الطرفين عبر منصة NileChain.",
                    $"تاريخ انتهاء العقد: {facts.DeliveryDateArabic} (موعد التسليم الأقصى)، ويجوز تعديله لاحقاً باتفاق الطرفين عبر المنصة عند التأخير."),
                "يُبرم هذا العقد ويُوقَّع إلكترونياً عبر منصة NileChain، ويُعد التوقيع الإلكتروني لكل طرف عبر المنصة توقيعاً ملزماً يغني عن التوقيع الخطي على أصل ورقي.")
        ];
    }
}
