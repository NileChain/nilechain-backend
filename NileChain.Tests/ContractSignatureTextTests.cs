using NileChain.Domain.Common;

namespace NileChain.Tests;

public class ContractSignatureTextTests
{
    [Fact]
    public void StripHandwrittenBlocks_RemovesSignatureSection()
    {
        var input =
            """
            بسم الله الرحمن الرحيم
            عقد توريد زراعي
            المادة الأولى — موضوع العقد
            يتعهد المورد بالتوريد وفق الشروط.
            التوقيعات
            الطرف الأول: مصنع دلتا
            التوقيع: __________
            التاريخ: __________
            الطرف الثاني: مزرعة النيل
            التوقيع: __________
            التاريخ: __________
            """;

        var result = ContractSignatureText.StripHandwrittenBlocks(input);

        Assert.Contains("المادة الأولى", result, StringComparison.Ordinal);
        Assert.DoesNotContain("التوقيعات", result, StringComparison.Ordinal);
        Assert.DoesNotContain("التوقيع:", result, StringComparison.Ordinal);
        Assert.DoesNotContain("__________", result, StringComparison.Ordinal);
    }

    [Fact]
    public void StripHandwrittenBlocks_RemovesStandaloneSignatureLines()
    {
        var input =
            """
            المادة الثامنة — فض النزاعات
            تختص محاكم القاهرة الاقتصادية.
            توقيع المورد: __________
            توقيع المشتري: __________
            """;

        var result = ContractSignatureText.StripHandwrittenBlocks(input);

        Assert.Contains("فض النزاعات", result, StringComparison.Ordinal);
        Assert.DoesNotContain("توقيع المورد", result, StringComparison.Ordinal);
        Assert.DoesNotContain("توقيع المشتري", result, StringComparison.Ordinal);
    }
}
