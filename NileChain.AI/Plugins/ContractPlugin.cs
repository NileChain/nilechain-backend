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
            أنت مستشار قانوني متخصص في عقود التوريد الزراعية وفق القانون المصري.
            أنشئ نص بنود عقد توريد زراعي كامل وواضح باللغة العربية الفصحى المعاصرة (صياغة قانونية مهنية بدون حشو).

            بيانات الصفقة (الزم بها حرفياً):
            - الطرف الأول — المشتري (المصنع): {factoryName}
            - الطرف الثاني — المورد (المزرعة): {farmName}
            - المحصول: {cropType}
            - الكمية: {quantityTons} طن متري
            - سعر الطن: {pricePerTon} جنيه مصري
            - القيمة الإجمالية: {totalValue} جنيه مصري
            - تاريخ التسليم الأقصى: {deliveryDate}
            - نقطة التسليم: {deliveryPointArabic}
            - أجرة النقل يتحملها: {freightPayerArabic}
            - مخاطر التلف أثناء النقل يتحملها: {transitRiskArabic}
            - مواصفات الجودة: {qualitySpecs}

            مرجع جودة من قاعدة المعرفة (استخدمه عند الاقتضاء دون اختراع أرقام مخالفة للصفقة):
            {ragContext}

            الهيكل المطلوب (عناوين واضحة بصيغة «المادة …»):
            1) ديباجة قصيرة بعد البسملة تعرّف الطرفين بالأسماء أعلاه وتشير إليهما لاحقاً بـ «المشتري» و«المورد».
            2) المادة الأولى — موضوع العقد
            3) المادة الثانية — الكمية والمواصفات والجودة
            4) المادة الثالثة — الثمن وشروط السداد (إلزامي: 30٪ مقدماً، و70٪ عند الاستلام والقبول)
            5) المادة الرابعة — التسليم والنقل والمخاطر — يجب أن تذكر حرفياً نقطة التسليم ({deliveryPointArabic}) ومن يتحمل أجرة النقل ({freightPayerArabic}) ومن يتحمل مخاطر الطريق ({transitRiskArabic}). رفض الحمولة عند بوابة المصنع قبل الاستلام يعيد العربات بحسب من يملك النقل، ويعيد أي مبلغ محجوز للمشتري.
            6) المادة الخامسة — التزامات الطرفين
            7) المادة السادسة — الجزاءات والإخلال
            8) المادة السابعة — القوة القاهرة
            9) المادة الثامنة — فض النزاعات (محاكم القاهرة الاقتصادية والقانون المصري)
            10) المادة التاسعة — أحكام عامة، ومنها أن العقد يُبرم ويُوقَّع إلكترونياً عبر منصة NileChain وأن التوقيع الإلكتروني عبر المنصة ملزم للطرفين، وأن تاريخ بدء العقد يُثبت بتاريخ اكتمال توقيع الطرفين، وأن تاريخ انتهاء العقد هو تاريخ التسليم الأقصى أعلاه ما لم يُعدَّل باتفاق إلكتروني لاحق عبر المنصة.

            قواعد إلزامية (لا تخالفها):
            - اسم المشتري يجب أن يكون حرفياً: "{factoryName}".
            - اسم المورد يجب أن يكون حرفياً: "{farmName}".
            - لا تستبدل الأسماء ولا تختصرها ولا تخترع أطرافاً من سياق سابق أو من قاعدة المعرفة.
            - لا تُدرج جدولاً مكرراً لبيانات التوريد إذا كانت مذكورة في الديباجة/المواد؛ ركّز على الأحكام القانونية.
            - ممنوع تماماً إدراج خانات توقيع خطية أو فراغات مثل «التوقيع: ……» أو «التاريخ: ……» أو قسم بعنوان «التوقيعات» — المنصة تعرض التوقيع الإلكتروني بشكل منفصل.
            - ممنوع كتابة قيم بين أقواس مربعة مثل «[تاريخ بدء العقد]» أو «[تاريخ انتهاء العقد]». اكتب صراحةً:
              تاريخ بدء العقد: يُحدَّد بتاريخ اكتمال التوقيع الإلكتروني من الطرفين عبر منصة NileChain.
              تاريخ انتهاء العقد: {deliveryDate} (موعد التسليم الأقصى)، مع جواز تعديله لاحقاً باتفاق الطرفين عبر المنصة عند التأخير.
            - لا تكتب أن العقد يحتاج توقيعاً ورقياً أو شهوداً.

            ابدأ بـ «بسم الله الرحمن الرحيم» ثم عنوان «عقد توريد زراعي».
            """;
    }

    [KernelFunction("build_contract_revision_prompt")]
    [Description("Builds the prompt for revising an existing agricultural supply contract from party instructions")]
    public string BuildRevisionPrompt(
        [Description("Current contract draft text")] string currentContractText,
        [Description("Free-text change instructions from a contracting party")] string changeInstructions)
    {
        return $"""
            أنت مستشار قانوني متخصص في عقود التوريد الزراعية وفق القانون المصري.
            راجع نص العقد الحالي وطَبِّق تعليمات التعديل التالية بدقة، ثم أعد نص العقد الكامل المحدَّث باللغة العربية الفصحى المعاصرة.

            تعليمات التعديل المطلوبة من أحد الطرفين (التزم بها):
            ---
            {changeInstructions}
            ---

            نص العقد الحالي:
            ---
            {currentContractText}
            ---

            قواعد إلزامية:
            - أعد العقد كاملاً بعد التعديل (وليس ملخصاً ولا قائمة تغييرات فقط).
            - طبّق التعليمات دون حذف الأحكام غير المتعلقة بالتعديل.
            - حافظ على أسماء الأطراف والأرقام المتفق عليها ما لم تطلب التعليمات تغييرها صراحة.
            - ابقِ هيكل «المادة …» واضحاً.
            - المادة الخاصة بالثمن يجب أن تبقى على أساس 30٪ مقدماً و70٪ عند الاستلام والقبول ما لم تطلب التعليمات خلاف ذلك صراحة.
            - ممنوع إدراج خانات توقيع خطية أو فراغات «التوقيع: ……» أو قسم «التوقيعات».
            - ممنوع الأقواس المربعة مثل «[تاريخ بدء العقد]». اكتب:
              تاريخ بدء العقد: يُحدَّد بتاريخ اكتمال التوقيع الإلكتروني من الطرفين عبر منصة NileChain.
              تاريخ انتهاء العقد: موعد التسليم الأقصى المتفق عليه (أو الموعد الجديد إن طلبت التعليمات تغييره)، مع جواز التعديل لاحقاً عبر المنصة.
            - لا تكتب أن العقد يحتاج توقيعاً ورقياً أو شهوداً.
            - لا تضف مقدّمة عن «ما تم تعديله»؛ أخرج نص العقد النهائي فقط.

            ابدأ بـ «بسم الله الرحمن الرحيم» ثم عنوان «عقد توريد زراعي».
            """;
    }
}
