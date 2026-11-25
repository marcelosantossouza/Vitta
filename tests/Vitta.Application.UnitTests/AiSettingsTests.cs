using Microsoft.Extensions.Logging.Abstractions;
using Vitta.Application.Abstractions.Ai;
using Vitta.Application.Features.Imports;
using Vitta.Application.Features.Settings;
using Vitta.Domain.Imports;
using Vitta.Domain.Settings;

namespace Vitta.Application.UnitTests;

public class AiSettingsTests
{
    [Fact]
    public async Task Save_CreatesThenUpdatesTheSingleRecord()
    {
        var repo = new FakeAiSettingsRepository();
        var uow = new FakeUnitOfWork();
        var handler = new UpdateAiSettingsHandler(repo, uow);

        Assert.True((await handler.Handle(new UpdateAiSettingsCommand(AiProvider.Groq, " openai/gpt-oss-20b ", "LOW"), default)).IsSuccess);
        Assert.Equal((AiProvider.Groq, "openai/gpt-oss-20b", "low"), (repo.Saved!.Provider, repo.Saved.Model, repo.Saved.Effort));
        var id = repo.Saved.Id;

        Assert.True((await handler.Handle(new UpdateAiSettingsCommand(AiProvider.Anthropic, "claude-haiku-5-5", "medium"), default)).IsSuccess);
        Assert.Equal(id, repo.Saved.Id);
        Assert.Equal(AiProvider.Anthropic, repo.Saved.Provider);
        Assert.Equal(2, uow.SaveCount);
    }

    [Theory]
    [InlineData("modelo com espaço", "medium")]
    [InlineData("claude-haiku-5-5", "extreme")]
    public async Task Save_RejectsInvalidModelOrEffort(string model, string effort)
    {
        var repo = new FakeAiSettingsRepository();
        var result = await new UpdateAiSettingsHandler(repo, new FakeUnitOfWork())
            .Handle(new UpdateAiSettingsCommand(AiProvider.Anthropic, model, effort), default);

        Assert.True(result.IsFailure);
        Assert.Null(repo.Saved);
    }

    [Fact]
    public async Task Get_WithServiceOffline_StillReturnsSavedChoice()
    {
        var saved = AiSettings.Create(AiProvider.Groq, "llama-3.3-70b-versatile", "low").Value;
        var dto = await new GetAiSettingsHandler(new FakeAiSettingsRepository(saved), new FakeAiService()).Handle(new GetAiSettingsQuery(), default);

        Assert.False(dto.ServiceOnline);
        Assert.True(dto.IsCustom);
        Assert.Equal(("groq", "llama-3.3-70b-versatile"), (dto.Provider, dto.Model));
        Assert.Empty(dto.Providers);
    }

    [Fact]
    public async Task Analyze_SendsSavedChoiceToAi()
    {
        var saved = AiSettings.Create(AiProvider.Groq, "openai/gpt-oss-120b", "high").Value;
        var ai = new FakeAiService(_ => new SpreadsheetSuggestion(1, [new("name", 1)], null, null, "high", "ai"));
        var handler = new AnalyzeSpreadsheetHandler(
            new FakeSpreadsheetReader(["Nome"], ["Ana"]), ai, new FakeAiSettingsRepository(saved), NullLogger<AnalyzeSpreadsheetHandler>.Instance);

        await handler.Handle(new AnalyzeSpreadsheetCommand(ImportTemplateKind.Prospects, "x.csv", Stream.Null, null), default);

        Assert.Equal(new LlmChoice("groq", "openai/gpt-oss-120b", "high"), ai.LastRequest!.Llm);
    }

    [Fact]
    public async Task Test_WhenServiceIsDown_ReturnsFriendlyFailure()
    {
        var result = await new TestAiConnectionHandler(new FakeAiService())
            .Handle(new TestAiConnectionCommand(AiProvider.Anthropic, "claude-haiku-5-5", "medium"), default);

        Assert.False(result.Ok);
        Assert.Contains("não respondeu", result.Message);
    }
}
