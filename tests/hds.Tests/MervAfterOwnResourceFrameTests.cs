using HeadlessClient;
namespace hds.Tests;
public class MervAfterOwnResourceFrameTests
{
 [Fact] public void ExactCurrentOwnIsFrameIsBounded()
 {
  var raw=Convert.FromHexString("020002808080100100");
  Assert.True(MervAfterCombatFrames.TryRead(raw,0,12,out var frame));Assert.Equal(9,frame.Length);
  for(int n=0;n<raw.Length;n++)Assert.False(MervAfterCombatFrames.TryRead(raw[..n],0,12,out _));
  Assert.False(MervAfterCombatFrames.TryRead(raw,0,599,out _));raw[0]=3;
  Assert.False(MervAfterCombatFrames.TryRead(raw,0,12,out _));
 }
}
