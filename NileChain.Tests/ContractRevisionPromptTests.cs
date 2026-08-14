using NileChain.AI.Agents;
using NileChain.AI.Plugins;

namespace NileChain.Tests;

public class ContractRevisionPromptTests
{
    [Fact]
    public void BuildRevisionPrompt_IncludesInstructionsAndCurrentText()
    {
        var plugin = new ContractPlugin();
        var prompt = plugin.BuildRevisionPrompt(
            "بسم الله الرحمن الرحيم\nعقد توريد زراعي\nالمادة الأولى",
            "غيّر الكمية إلى 150 طن");

        Assert.Contains("غيّر الكمية إلى 150 طن", prompt);
        Assert.Contains("عقد توريد زراعي", prompt);
        Assert.Contains("أعد العقد كاملاً", prompt);
    }
}
