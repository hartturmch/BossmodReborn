namespace BossMod.Autorotation;

public sealed class RoleMeleeDPSUtility(RotationModuleManager manager, Actor player) : GenericUtility(manager, player)
{
    public enum Track { Bloodbath, Feint }
    public enum FeintOption { None, Use, UseEx }

    public static RotationModuleDefinition Definition()
    {
        var res = new RotationModuleDefinition("Melee DPS", "Role utility actions for melee DPS.", "Utility for Roles", "xan", RotationModuleQuality.Basic, BitMask.Build(Class.PGL, Class.MNK, Class.LNC, Class.DRG, Class.ROG, Class.NIN, Class.SAM, Class.RPR, Class.VPR), 100);

        DefineSimpleConfig(res, Track.Bloodbath, "Bloodbath", "", 200, ClassShared.AID.Bloodbath, 20);

        res.Define(Track.Feint).As<FeintOption>("Feint", "", 250)
            .AddOption(FeintOption.None, "Do not use automatically")
            .AddOption(FeintOption.Use, "Use Feint (10s)", 90, 10, ActionTargets.Hostile, 22, 97)
            .AddOption(FeintOption.UseEx, "Use Feint (15s)", 90, 15, ActionTargets.Hostile, 98)
            .AddAssociatedActions(ClassShared.AID.Feint);

        return res;
    }

    public override void Execute(StrategyValues strategy, Actor? primaryTarget, float estimatedAnimLockDelay, bool isMoving)
    {
        ExecuteSimple(strategy.Option(Track.Bloodbath), ClassShared.AID.Bloodbath, Player);

        var feint = strategy.Option(Track.Feint);
        if (feint.As<FeintOption>() != FeintOption.None)
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.Feint), ResolveTargetOverride(feint.Value) ?? primaryTarget, feint.Priority(), feint.Value.ExpireIn);
    }
}
