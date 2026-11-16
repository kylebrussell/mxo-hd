using HeadlessClient;
using NativeQa.Modes;
using Xunit;
namespace hds.Tests;
public sealed class ZionAfter101217SeedContractTests
{
    static int[] Valid()=>ZionAfter101217Fixture.Prerequisites.Append(986).ToArray();
    [Fact] public void ExactStoryAndFrameworkTutorialAreAccepted()=>ZionAfter101217Mode.RequireSeedReceipts(Valid().Reverse());
    [Fact] public void MissingStoryRefuses()=>Assert.Throws<InvalidDataException>(()=>ZionAfter101217Mode.RequireSeedReceipts(Valid().Skip(1)));
    [Fact] public void MissingTutorialRefuses()=>Assert.Throws<InvalidDataException>(()=>ZionAfter101217Mode.RequireSeedReceipts(ZionAfter101217Fixture.Prerequisites));
    [Fact] public void DuplicateTutorialRefuses()=>Assert.Throws<InvalidDataException>(()=>ZionAfter101217Mode.RequireSeedReceipts(Valid().Append(986)));
    [Theory][InlineData(101301)][InlineData(101302)][InlineData(101303)][InlineData(101304)][InlineData(101305)][InlineData(101306)][InlineData(102101)][InlineData(999999)]
    public void SelectedMissingNextOrExtraRefuses(int extra)=>Assert.Throws<InvalidDataException>(()=>ZionAfter101217Mode.RequireSeedReceipts(Valid().Append(extra)));
}
