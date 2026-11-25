namespace NativeQa.Modes;

public sealed class CodeShieldAccuracyMode : NetworkAttackerAccuracyMode
{
    public CodeShieldAccuracyMode() : base(358) { }

    public override string Name => "ability-parity-code-shield-accuracy";
}
