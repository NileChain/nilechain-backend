using System.Text;
using NileChain.Domain.Common;
using NileChain.Domain.Enums;

namespace NileChain.AI;

/// <summary>
/// Deterministic Arabic supply-contract draft used when LLM/RAG is unavailable.
/// Signatures are intentionally omitted — the platform renders e-signature blocks.
/// </summary>
public static class ContractDraftTemplate
{
    public static string Build(
        string farmName,
        string factoryName,
        string cropType,
        decimal quantityTons,
        decimal pricePerTon,
        DateTime deliveryDate,
        string? qualitySpecs,
        string? deliveryPointRaw = null,
        string? freightPayerRaw = null,
        string? transitRiskRaw = null)
    {
        var total = quantityTons * pricePerTon;
        var deliveryPoint = PointArabic(deliveryPointRaw);
        var freightPayer = PartyArabic(freightPayerRaw);
        var transitRisk = PartyArabic(transitRiskRaw);
        var quality = string.IsNullOrWhiteSpace(qualitySpecs)
            ? "وفق المواصفات المتفق عليها بين الطرفين ومعايير القبول لدى المشتري."
            : qualitySpecs.Trim();

        var sb = new StringBuilder();
        sb.AppendLine("بسم الله الرحمن الرحيم");
        sb.AppendLine();
        sb.AppendLine("عقد توريد زراعي");
        sb.AppendLine();
        sb.AppendLine(
            $"إنه في تاريخ تحرير هذا العقد إلكترونياً عبر منصة NileChain، تم الاتفاق بين كلٍ من:");
        sb.AppendLine(
            $"الطرف الأول (المشتري / المصنع): {factoryName}، ويُشار إليه لاحقاً بـ «المشتري».");
        sb.AppendLine(
            $"الطرف الثاني (المورد / المزرعة): {farmName}، ويُشار إليه لاحقاً بـ «المورد».");
        sb.AppendLine();
        sb.AppendLine("المادة الأولى — موضوع العقد");
        sb.AppendLine(
            $"يتعهد المورد بتوريد محصول «{cropType}» إلى المشتري وفقاً لأحكام هذا العقد، ويتعهد المشتري بقبول التوريد المطابق للمواصفات وسداد الثمن المتفق عليه.");
        sb.AppendLine();
        sb.AppendLine("المادة الثانية — الكمية والمواصفات");
        sb.AppendLine(
            $"الكمية المتعاقد عليها: {quantityTons:0.##} طن متري من محصول «{cropType}». مواصفات الجودة: {quality}");
        sb.AppendLine();
        sb.AppendLine("المادة الثالثة — الثمن وشروط السداد");
        sb.AppendLine(
            $"سعر الطن: {pricePerTon:0.##} جنيه مصري. القيمة الإجمالية: {total:0.##} جنيه مصري. يتم السداد بنسبة 30٪ مقدماً، و70٪ عند الاستلام والقبول.");
        sb.AppendLine();
        sb.AppendLine("المادة الرابعة — التسليم والنقل والمخاطر");
        sb.AppendLine(
            $"تاريخ التسليم الأقصى: {deliveryDate:dd MMMM yyyy}. نقطة التسليم: {deliveryPoint}. أجرة النقل يتحملها: {freightPayer}. مخاطر التلف أثناء النقل يتحملها: {transitRisk}. رفض الحمولة عند بوابة المصنع قبل الاستلام يعيد العربات بحسب من يملك النقل، ويعيد أي مبلغ محجوز للمشتري.");
        sb.AppendLine();
        sb.AppendLine("المادة الخامسة — التزامات الطرفين");
        sb.AppendLine(
            "يلتزم المورد بتوريد الكمية في الموعد والمواصفات المتفق عليها، ويلتزم المشتري بتيسير إجراءات الاستلام والفحص وسداد المستحقات وفق جدول السداد.");
        sb.AppendLine();
        sb.AppendLine("المادة السادسة — الجزاءات");
        sb.AppendLine(
            "إذا أخلّ أي طرف بالتزام جوهري دون عذر مشروع، يحق للطرف الآخر المطالبة بالتعويض الاتفاقي عن الضرر المباشر الناشئ عن الإخلال، مع الاحتفاظ بالحقوق القانونية الأخرى.");
        sb.AppendLine();
        sb.AppendLine("المادة السابعة — القوة القاهرة");
        sb.AppendLine(
            "لا يُسأل أي طرف عن التأخر أو عدم التنفيذ الناتج عن قوة قاهرة خارجة عن إرادته، على أن يُخطر الطرف الآخر فور العلم، وأن يسعى لاستئناف التنفيذ فور زوال السبب.");
        sb.AppendLine();
        sb.AppendLine("المادة الثامنة — فض النزاعات");
        sb.AppendLine(
            "يخضع هذا العقد للقانون المصري، وتختص محاكم القاهرة الاقتصادية بنظر أي نزاع ينشأ عنه أو عن تنفيذه.");
        sb.AppendLine();
        sb.AppendLine("المادة التاسعة — أحكام عامة");
        sb.AppendLine(
            "يُبرم هذا العقد ويُوقَّع إلكترونياً عبر منصة NileChain، ويُعد التوقيع الإلكتروني لكل طرف عبر المنصة توقيعاً ملزماً يغني عن التوقيع الخطي على أصل ورقي.");
        sb.AppendLine(
            "تاريخ بدء العقد: يُحدَّد بتاريخ اكتمال التوقيع الإلكتروني من الطرفين عبر منصة NileChain.");
        sb.AppendLine(
            $"تاريخ انتهاء العقد: {deliveryDate:dd MMMM yyyy} (موعد التسليم الأقصى)، ويجوز تعديله لاحقاً باتفاق الطرفين عبر المنصة عند التأخير.");
        return sb.ToString();
    }

    public static string PointArabic(string? raw)
    {
        DeliveryTermsPolicy.TryParsePoint(raw, out var point);
        if (string.IsNullOrWhiteSpace(raw))
            point = DeliveryPoint.FactoryGate;
        return DeliveryTermsPolicy.ArabicPoint(point);
    }

    public static string PartyArabic(string? raw)
    {
        DeliveryTermsPolicy.TryParseParty(raw, out var party);
        if (string.IsNullOrWhiteSpace(raw))
            party = DealParty.Farm;
        return DeliveryTermsPolicy.ArabicParty(party);
    }
}
