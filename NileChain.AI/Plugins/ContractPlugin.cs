using System.ComponentModel;
using Microsoft.SemanticKernel;

namespace NileChain.AI.Plugins;

public class ContractPlugin
{
    [KernelFunction("build_contract_prompt")]
    [Description("Builds the prompt for contract generation using deal details and RAG context")]
    public string BuildContractPrompt(
        [Description("Farm name")] string farmName,
        [Description("Factory name")] string factoryName,
        [Description("Crop type")] string cropType,
        [Description("Quantity in tons")] decimal quantityTons,
        [Description("Price per ton in EGP")] decimal pricePerTon,
        [Description("Delivery date")] string deliveryDate,
        [Description("Quality specifications")] string qualitySpecs,
        [Description("RAG context from knowledge base")] string ragContext,
        [Description("Delivery point Arabic label")] string deliveryPointArabic = "باب المصنع",
        [Description("Who pays freight Arabic")] string freightPayerArabic = "المزرعة",
        [Description("Who bears transit risk Arabic")] string transitRiskArabic = "المزرعة")
    {
        var totalValue = quantityTons * pricePerTon;

        return $"""
            أنت نظام ذكاء اصطناعي متخصص في إنشاء عقود التوريد الزراعية.
            استخدم المعلومات التالية لإنشاء عقد توريد قانوني كامل باللغة العربية.

            معلومات الصفقة:
            - الطرف الأول (المورد): {farmName}
            - الطرف الثاني (المشتري): {factoryName}
            - المحصول: {cropType}
            - الكمية: {quantityTons} طن متري
            - السعر: {pricePerTon} جنيه/طن
            - الإجمالي: {totalValue} جنيه مصري
            - تاريخ التسليم: {deliveryDate}
            - نقطة التسليم: {deliveryPointArabic}
            - أجرة النقل يتحملها: {freightPayerArabic}
            - مخاطر التلف أثناء النقل يتحملها: {transitRiskArabic}
            - مواصفات الجودة: {qualitySpecs}

            معايير الجودة المرجعية من قاعدة المعرفة:
            {ragContext}

            أنشئ عقداً كاملاً يتضمن:
            1. بيانات الطرفين
            2. موضوع العقد
            3. الكمية والمواصفات
            4. السعر وشروط الدفع (30% مقدم، 70% عند الاستلام)
            5. شروط التسليم — يجب أن تذكر حرفياً نقطة التسليم ({deliveryPointArabic}) ومن يتحمل أجرة النقل ({freightPayerArabic}) ومن يتحمل مخاطر الطريق ({transitRiskArabic}). رفض الحمولة عند بوابة المصنع قبل الاستلام يعيد العربات حسب من يملك النقل، ويعيد أي مبلغ محجوز للمشتري.
            6. جزاءات الإخلال
            7. فض النزاعات (محاكم القاهرة الاقتصادية)
            8. خانات التوقيع

            قواعد إلزامية (لا تخالفها):
            - اسم المورد في العقد يجب أن يكون حرفياً: "{farmName}" — لا تستبدل ولا تختصر ولا تخترع اسماً آخر.
            - اسم المشتري في العقد يجب أن يكون حرفياً: "{factoryName}".
            - لا تستخدم أسماء مزارع أو مصانع من سياق سابق أو من قاعدة المعرفة إذا اختلفت عن الأسماء أعلاه.

            ابدأ بـ "بسم الله الرحمن الرحيم"
            """;
    }
}
