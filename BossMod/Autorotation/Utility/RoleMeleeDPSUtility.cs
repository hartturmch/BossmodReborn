namespace BossMod.Autorotation;

public sealed class RoleMeleeDPSUtility(RotationModuleManager manager, Actor player) : GenericUtility(manager, player)
{
    public enum Track { Bloodbath, Feint, LimitBreak }
    public enum FeintOption { None, Use, UseEx }
    public enum LimitBreakOption { None, FullOrExecute }

    public static RotationModuleDefinition Definition()
    {
        var res = new RotationModuleDefinition("Melee DPS", "Role utility actions for melee DPS.", "Utility for Roles", "xan", RotationModuleQuality.Basic, BitMask.Build(Class.PGL, Class.MNK, Class.LNC, Class.DRG, Class.ROG, Class.NIN, Class.SAM, Class.RPR, Class.VPR), 100);

        DefineSimpleConfig(res, Track.Bloodbath, "Bloodbath", "", 200, ClassShared.AID.Bloodbath, 20);

        res.Define(Track.Feint).As<FeintOption>("Feint", "", 250)
            .AddOption(FeintOption.None, "Do not use automatically")
            .AddOption(FeintOption.Use, "Use Feint (10s)", 90, 10, ActionTargets.Hostile, 22, 97)
            .AddOption(FeintOption.UseEx, "Use Feint (15s)", 90, 15, ActionTargets.Hostile, 98)
            .AddAssociatedActions(ClassShared.AID.Feint);

        res.Define(Track.LimitBreak).As<LimitBreakOption>("LimitBreak", "LB", 300)
            .AddOption(LimitBreakOption.None, "Do not use automatically")
            .AddOption(LimitBreakOption.FullOrExecute, "Use on boss when max bars are full or boss is below 10%", 0, 0, ActionTargets.Hostile, defaultPriority: ActionQueue.Priority.VeryHigh)
            .AddAssociatedAction(ActionDefinitions.IDGeneralLimitBreak);

        return res;
    }

    public override void Execute(StrategyValues strategy, Actor? primaryTarget, float estimatedAnimLockDelay, bool isMoving)
    {
        ExecuteSimple(strategy.Option(Track.Bloodbath), ClassShared.AID.Bloodbath, Player);

        var feint = strategy.Option(Track.Feint);
        if (feint.As<FeintOption>() != FeintOption.None)
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.Feint), ResolveTargetOverride(feint.Value) ?? primaryTarget, feint.Priority(), feint.Value.ExpireIn);

        var lb = strategy.Option(Track.LimitBreak);
        var boss = Bossmods.ActiveModule?.PrimaryActor;
        if (lb.As<LimitBreakOption>() == LimitBreakOption.FullOrExecute && boss != null && IsValidLimitBreakTarget(boss) && ShouldUseLimitBreak(boss))
            Hints.ActionsToExecute.Push(ActionDefinitions.IDGeneralLimitBreak, boss, lb.Priority(), lb.Value.ExpireIn, castTime: 4.5f);
    }

    private bool ShouldUseLimitBreak(Actor boss)
    {
        if (!Player.InCombat)
            return false;

        var full = World.Party.LimitBreakMax > 0 && World.Party.LimitBreakCur >= World.Party.LimitBreakMax * Math.Max(World.Party.LimitBreakBars, 1);
        var execute = World.Party.LimitBreakLevel >= 1 && boss.PendingHPRatio <= 0.1f;
        return full || execute;
    }

    private static bool IsValidLimitBreakTarget(Actor? target) => target != null && target.HPMP.MaxHP > 0 && target.IsTargetable && !target.IsDeadOrDestroyed && !target.IsAlly;
}
