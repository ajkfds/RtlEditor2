using Xunit;

namespace SystemVerilogLanguageServer.Tests;

/// <summary>
/// The chat shows a running tool call as a message. Its header must summarise
/// the arguments the way a reader needs them, not dump the provider's JSON.
/// </summary>
/// <remarks>
/// <para>
/// For <c>replace_in_file</c> the interesting part is the path and the
/// SEARCH/REPLACE diff; the braces and quotes of the raw JSON only get in the
/// way. <c>ToolCallHeaderFormatter</c> therefore renders the few arguments that
/// matter per known tool, and keeps the raw JSON only as the fallback for a
/// tool it does not know.
/// </para>
/// <para>
/// These tests live here because this is the only test project in the
/// repository; CodeEditor2 comes in transitively through
/// CodeEditor2VerilogPlugin.
/// </para>
/// </remarks>
public class ToolCallHeaderFormatterTests
{
    private const string ReplaceInFileArguments =
        "{\n" +
        "  \"path\": \"rtl/top.sv\",\n" +
        "  \"diff\": \"<<<<<<< SEARCH\\n  wire a;\\n=======\\n  wire a;\\n  wire b;\\n>>>>>>> REPLACE\"\n" +
        "}";

    /// <summary>
    /// The reported symptom: a replace_in_file call showed the JSON blob instead
    /// of the path and the diff.
    /// </summary>
    [Fact]
    public void ReplaceInFile_ShowsThePathAndTheDiff()
    {
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build("replace_in_file", ReplaceInFileArguments);

        Assert.Contains("**Tool call: replace_in_file**", header, StringComparison.Ordinal);
        Assert.Contains("**path**: rtl/top.sv", header, StringComparison.Ordinal);
        Assert.Contains("**diff**", header, StringComparison.Ordinal);
        // the diff content itself is visible
        Assert.Contains("wire a;", header, StringComparison.Ordinal);
        Assert.Contains("wire b;", header, StringComparison.Ordinal);
        // ... but not wrapped in the provider's JSON any more
        Assert.DoesNotContain("\"path\"", header, StringComparison.Ordinal);
        Assert.DoesNotContain("\"diff\"", header, StringComparison.Ordinal);
    }

    /// <summary>Several SEARCH/REPLACE blocks are counted in the header.</summary>
    [Fact]
    public void ReplaceInFile_CountsMultipleBlocks()
    {
        const string arguments =
            "{\"path\":\"a.sv\",\"diff\":\"" +
            "<<<<<<< SEARCH\\nx\\n=======\\ny\\n>>>>>>> REPLACE\\n<<<<<<< SEARCH\\nz\\n=======\\nw\\n>>>>>>> REPLACE\"}";

        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build("replace_in_file", arguments);
        Assert.Contains("**diff** (2 blocks)", header, StringComparison.Ordinal);
    }

    /// <summary>A partially streamed call still shows which arguments exist.</summary>
    [Fact]
    public void ReplaceInFile_WithoutDiffYet_MarksItAsNotStreamed()
    {
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build(
            "replace_in_file", "{\"path\":\"a.sv\"}");

        Assert.Contains("**path**: a.sv", header, StringComparison.Ordinal);
        Assert.Contains("**diff**: (not streamed yet)", header, StringComparison.Ordinal);
    }

    /// <summary>read_file shows the path and, when given, the line range.</summary>
    [Fact]
    public void ReadFile_ShowsThePathAndTheLineRange()
    {
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build(
            "read_file", "{\"path\":\"a.sv\",\"start_line\":10,\"end_line\":20}");

        Assert.Contains("**path**: a.sv", header, StringComparison.Ordinal);
        Assert.Contains("**lines**: 10 - 20", header, StringComparison.Ordinal);
        Assert.DoesNotContain("\"start_line\"", header, StringComparison.Ordinal);
    }

    /// <summary>execute_command shows the command as inline code.</summary>
    [Fact]
    public void ExecuteCommand_ShowsTheCommand()
    {
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build(
            "execute_command", "{\"command\":\"dotnet build -clp:ErrorsOnly\"}");

        Assert.Contains("**command**: `dotnet build -clp:ErrorsOnly`", header, StringComparison.Ordinal);
    }

    /// <summary>search_files shows the directory, the regex and the file filter.</summary>
    [Fact]
    public void SearchFiles_ShowsPathRegexAndFilePattern()
    {
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build(
            "search_files",
            "{\"path\":\"src\",\"regex\":\"floating input\",\"file_pattern\":\"*.sv\"}");

        Assert.Contains("**path**: src", header, StringComparison.Ordinal);
        Assert.Contains("**pattern**: `floating input`", header, StringComparison.Ordinal);
        Assert.Contains("**file pattern**: `*.sv`", header, StringComparison.Ordinal);
    }

    /// <summary>
    /// A tool this formatter does not know (a plugin's own, or one added later)
    /// must still show its arguments rather than nothing.
    /// </summary>
    [Fact]
    public void UnknownTool_FallsBackToTheRawArguments()
    {
        const string arguments = "{\"something\":\"value\"}";
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build("my_custom_tool", arguments);

        Assert.Contains("**Tool call: my_custom_tool**", header, StringComparison.Ordinal);
        Assert.Contains("```json", header, StringComparison.Ordinal);
        Assert.Contains("something", header, StringComparison.Ordinal);
    }

    /// <summary>
    /// While the provider is still streaming the arguments the text is not valid
    /// JSON yet; the raw text must still be shown (nothing is hidden).
    /// </summary>
    [Fact]
    public void StreamingArgumentsThatAreNotJsonYet_AreStillShown()
    {
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build("replace_in_file", "{\"path\":\"a.");

        Assert.Contains("**Tool call: replace_in_file**", header, StringComparison.Ordinal);
        Assert.Contains("{\"path\":\"a.", header, StringComparison.Ordinal);
    }

    /// <summary>A tool with no arguments only shows its name.</summary>
    [Fact]
    public void ToolWithoutArguments_ShowsOnlyItsName()
    {
        string header = CodeEditor2.LLM.ToolCallHeaderFormatter.Build("build_dotnet", "");

        Assert.Contains("**Tool call: build_dotnet**", header, StringComparison.Ordinal);
        Assert.DoesNotContain("```", header, StringComparison.Ordinal);
    }
}
