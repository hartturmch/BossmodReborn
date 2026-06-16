namespace BossMod.Autorotation;

public sealed class RoleMeleeDPSUtility(RotationModuleManager manager, Actor player) : GenericUtility(manager, player)
{
    public enum Track { Bloodbath, Feint, LimitBreak, Sprint, SecondWind, ArmsLength, GapCloser }
    public enum FeintOption { None, Use, UseEx }
    public enum LimitBreakOption { None, FullOrExecute }
    public enum GapCloserOption { None, Use }

    public static RotationModuleDefinition Definition()
    {
        var res = new RotationModuleDefinition("Melee DPS Utility", "Role utility actions for melee DPS.", "Utility for Roles", "xan", RotationModuleQuality.Basic, BitMask.Build(Class.PGL, Class.MNK, Class.LNC, Class.DRG, Class.ROG, Class.NIN, Class.SAM, Class.RPR, Class.VPR), 100);

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

        DefineSimpleConfig(res, Track.Sprint, "Sprint", "", 100, ClassShared.AID.Sprint, 10);
        DefineSimpleConfig(res, Track.SecondWind, "SecondWind", "S.Wind", 150, ClassShared.AID.SecondWind);
        DefineSimpleConfig(res, Track.ArmsLength, "ArmsLength", "ArmsL", 300, ClassShared.AID.ArmsLength, 6);

        res.Define(Track.GapCloser).As<GapCloserOption>("GapCloser", "Gap", 50)
            .AddOption(GapCloserOption.None, "Do not use automatically")
            .AddOption(GapCloserOption.Use, "Use class gap closer if outside melee range", 30, 0, ActionTargets.Hostile, 20)
            .AddAssociatedAction(ActionID.MakeSpell(MNK.AID.Thunderclap))
            .AddAssociatedAction(ActionID.MakeSpell(DRG.AID.WingedGlide))
            .AddAssociatedAction(ActionID.MakeSpell(NIN.AID.Shukuchi))
            .AddAssociatedAction(ActionID.MakeSpell(SAM.AID.HissatsuGyoten))
            .AddAssociatedAction(ActionID.MakeSpell(RPR.AID.HellsIngress))
            .AddAssociatedAction(ActionID.MakeSpell(VPR.AID.Slither));

        return res;
    }

    public override void Execute(StrategyValues strategy, Actor? primaryTarget, float estimatedAnimLockDelay, bool isMoving)
    {
        ExecuteSimple(strategy.Option(Track.Sprint), ClassShared.AID.Sprint, Player);
        ExecuteSimple(strategy.Option(Track.SecondWind), ClassShared.AID.SecondWind, Player);
        ExecuteSimple(strategy.Option(Track.Bloodbath), ClassShared.AID.Bloodbath, Player);
        ExecuteSimple(strategy.Option(Track.ArmsLength), ClassShared.AID.ArmsLength, Player);

        var feint = strategy.Option(Track.Feint);
        if (feint.As<FeintOption>() != FeintOption.None)
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.Feint), ResolveTargetOverride(feint.Value) ?? primaryTarget, feint.Priority(), feint.Value.ExpireIn);

        ExecuteGapCloser(strategy.Option(Track.GapCloser), primaryTarget);

        var lb = strategy.Option(Track.LimitBreak);
        var boss = Bossmods.ActiveModule?.PrimaryActor;
        if (lb.As<LimitBreakOption>() == LimitBreakOption.FullOrExecute && boss != null && IsValidHostileTarget(boss) && ShouldUseLimitBreak(boss))
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

    private void ExecuteGapCloser(in StrategyValues.OptionRef gap, Actor? primaryTarget)
    {
        if (gap.As<GapCloserOption>() == GapCloserOption.None)
            return;

        var target = ResolveTargetOverride(gap.Value) ?? primaryTarget;
        if (target == null || !IsValidHostileTarget(target))
            return;

        var distance = Player.DistanceToHitbox(target);
        if (distance is <= 3f or > 20f)
            return;

        var action = Player.Class switch
        {
            Class.PGL or Class.MNK => ActionID.MakeSpell(MNK.AID.Thunderclap),
            Class.LNC or Class.DRG => ActionID.MakeSpell(DRG.AID.WingedGlide),
            Class.ROG or Class.NIN => ActionID.MakeSpell(NIN.AID.Shukuchi),
            Class.SAM => ActionID.MakeSpell(SAM.AID.HissatsuGyoten),
            Class.RPR => ActionID.MakeSpell(RPR.AID.HellsIngress),
            Class.VPR => ActionID.MakeSpell(VPR.AID.Slither),
            _ => default
        };
        if (action == default)
            return;

        if (action == ActionID.MakeSpell(NIN.AID.Shukuchi))
            Hints.ActionsToExecute.Push(action, null, gap.Priority(), gap.Value.ExpireIn, targetPos: target.Position.ToVec3(Player.PosRot.Y));
        else if (action == ActionID.MakeSpell(RPR.AID.HellsIngress))
            Hints.ActionsToExecute.Push(action, Player, gap.Priority(), gap.Value.ExpireIn, facingAngle: Player.AngleTo(target));
        else
            Hints.ActionsToExecute.Push(action, target, gap.Priority(), gap.Value.ExpireIn);
    }

    private static bool IsValidHostileTarget(Actor target) => target.HPMP.MaxHP > 0 && target.IsTargetable && !target.IsDeadOrDestroyed && !target.IsAlly;
}
