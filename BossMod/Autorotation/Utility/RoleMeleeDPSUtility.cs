namespace BossMod.Autorotation;

public sealed class RoleMeleeDPSUtility(RotationModuleManager manager, Actor player) : GenericUtility(manager, player)
{
    public enum Track { Bloodbath, Feint, LimitBreak, Sprint, SecondWind, ArmsLength, LegSweep, TrueNorth, GapCloser }
    public enum FeintOption { None, Use, UseEx }
    public enum LimitBreakOption { None, FullOrExecute }
    public enum GapCloserOption { None, Use }
    public enum TrueNorthOption { None, Use }

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
        DefineSimpleConfig(res, Track.LegSweep, "LegSweep", "Stun", -150, ClassShared.AID.LegSweep, 3, ActionQueue.Priority.VeryLow);

        res.Define(Track.TrueNorth).As<TrueNorthOption>("TrueNorth", "T.North", 75)
            .AddOption(TrueNorthOption.None, "Do not use automatically")
            .AddOption(TrueNorthOption.Use, "Use True North when next positional is imminent and incorrect", 45, 10, ActionTargets.Self, 50, defaultPriority: ActionQueue.Priority.Low)
            .AddAssociatedActions(ClassShared.AID.TrueNorth);

        res.Define(Track.GapCloser).As<GapCloserOption>("GapCloser", "Gap", 50)
            .AddOption(GapCloserOption.None, "Do not use automatically")
            .AddOption(GapCloserOption.Use, "Use class gap closer if outside melee range; always keeps 1 charge", 30, 0, ActionTargets.Hostile, 20)
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
        ExecuteLegSweep(strategy.Option(Track.LegSweep));

        var feint = strategy.Option(Track.Feint);
        if (feint.As<FeintOption>() != FeintOption.None)
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.Feint), ResolveTargetOverride(feint.Value) ?? primaryTarget, feint.Priority(), feint.Value.ExpireIn);

        ExecuteTrueNorth(strategy.Option(Track.TrueNorth), primaryTarget);
        ExecuteGapCloser(strategy.Option(Track.GapCloser), primaryTarget);

        var lb = strategy.Option(Track.LimitBreak);
        var boss = Bossmods.ActiveModule?.PrimaryActor;
        if (lb.As<LimitBreakOption>() == LimitBreakOption.FullOrExecute && boss != null && IsValidHostileTarget(boss) && ShouldUseLimitBreak(boss))
            Hints.ActionsToExecute.Push(ActionDefinitions.IDGeneralLimitBreak, boss, lb.Priority(), lb.Value.ExpireIn, castTime: 4.5f);
    }

    private void ExecuteLegSweep(in StrategyValues.OptionRef stun)
    {
        if (stun.As<SimpleOption>() != SimpleOption.Use)
            return;

        var target = ResolveTargetOverride(stun.Value) ?? Hints.PotentialTargets.FirstOrDefault(e => e.ShouldBeStunned && Player.DistanceToHitbox(e.Actor) <= 3)?.Actor;
        if (target != null && IsValidHostileTarget(target))
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.LegSweep), target, stun.Priority(), stun.Value.ExpireIn);
    }

    private void ExecuteTrueNorth(in StrategyValues.OptionRef trueNorth, Actor? primaryTarget)
    {
        if (trueNorth.As<TrueNorthOption>() == TrueNorthOption.None || SelfStatusLeft(ClassShared.SID.TrueNorth, 10) > 0)
            return;

        if (NeedsTrueNorth(primaryTarget))
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.TrueNorth), Player, trueNorth.Priority(), trueNorth.Value.ExpireIn);
    }

    private bool NeedsTrueNorth(Actor? primaryTarget)
    {
        var (hintTarget, hintPositional, hintImminent, hintCorrect) = Hints.RecommendedPositional;
        if (hintTarget != null && hintPositional != Positional.Any && hintImminent)
            return !hintCorrect && CanUsePositionalOn(hintTarget);

        var target = primaryTarget;
        if (target == null || !CanUsePositionalOn(target))
            return false;

        var inferred = InferImmediatePositional(target);
        return inferred != Positional.Any && !IsCorrectPositional(target, inferred);
    }

    private Positional InferImmediatePositional(Actor target)
    {
        var combo = World.Client.ComboState.Action;
        return Player.Class switch
        {
            Class.LNC or Class.DRG => InferDRGPositional(combo),
            Class.ROG or Class.NIN => InferNINPositional(combo, target),
            Class.SAM => InferSAMPositional(combo),
            Class.RPR => InferRPRPositional(),
            Class.VPR => InferVPRPositional(combo),
            _ => Positional.Any
        };
    }

    private Positional InferDRGPositional(uint combo)
    {
        if (!ActionUnlocked(ActionID.MakeSpell(DRG.AID.ChaosThrust)))
            return Positional.Any;

        if (combo == (uint)DRG.AID.Disembowel || combo == (uint)DRG.AID.SpiralBlow)
            return Positional.Rear;

        if (ActionUnlocked(ActionID.MakeSpell(DRG.AID.WheelingThrust)) && (combo == (uint)DRG.AID.ChaosThrust || combo == (uint)DRG.AID.ChaoticSpring))
            return Positional.Rear;

        if (ActionUnlocked(ActionID.MakeSpell(DRG.AID.FangAndClaw)) && (combo == (uint)DRG.AID.FullThrust || combo == (uint)DRG.AID.HeavensThrust))
            return Positional.Flank;

        return Positional.Any;
    }

    private Positional InferNINPositional(uint combo, Actor target)
    {
        if (!ActionUnlocked(ActionID.MakeSpell(NIN.AID.AeolianEdge)) || combo != (uint)NIN.AID.GustSlash)
            return Positional.Any;

        return CurrentPositional(target) == Positional.Front ? Positional.Rear : Positional.Any;
    }

    private Positional InferSAMPositional(uint combo)
    {
        if (!ActionUnlocked(ActionID.MakeSpell(SAM.AID.Gekko)))
            return Positional.Any;

        return combo switch
        {
            (uint)SAM.AID.Jinpu => Positional.Rear,
            (uint)SAM.AID.Shifu when ActionUnlocked(ActionID.MakeSpell(SAM.AID.Kasha)) => Positional.Flank,
            _ => Positional.Any
        };
    }

    private Positional InferRPRPositional()
    {
        if (!ActionUnlocked(ActionID.MakeSpell(RPR.AID.Gibbet)) || SelfStatusLeft(RPR.SID.SoulReaver) <= 0)
            return Positional.Any;

        if (SelfStatusLeft(RPR.SID.EnhancedGallows) > 0)
            return Positional.Rear;

        if (SelfStatusLeft(RPR.SID.EnhancedGibbet) > 0)
            return Positional.Flank;

        return Positional.Any;
    }

    private Positional InferVPRPositional(uint combo)
    {
        if (!ActionUnlocked(ActionID.MakeSpell(VPR.AID.FlankstingStrike)))
            return Positional.Any;

        return combo switch
        {
            (uint)VPR.AID.HuntersSting => Positional.Flank,
            (uint)VPR.AID.SwiftskinsSting => Positional.Rear,
            _ => Positional.Any
        };
    }

    private bool CanUsePositionalOn(Actor? target)
        => target != null && IsValidHostileTarget(target) && !target.Omnidirectional && Player.DistanceToHitbox(target) < 6
        && !(target.TargetID == Player.InstanceID && target.CastInfo == null && !target.IsStrikingDummy);

    private bool IsCorrectPositional(Actor target, Positional positional)
        => positional switch
        {
            Positional.Flank => Math.Abs(target.Rotation.ToDirection().Dot((Player.Position - target.Position).Normalized())) < 0.7071067f,
            Positional.Rear => target.Rotation.ToDirection().Dot((Player.Position - target.Position).Normalized()) < -0.7071068f,
            _ => true
        };

    private Positional CurrentPositional(Actor target)
        => target.Omnidirectional
            ? Positional.Any
            : (Player.Position - target.Position).Normalized().Dot(target.Rotation.ToDirection()) switch
            {
                < -0.7071068f => Positional.Rear,
                < 0.7071068f => Positional.Flank,
                _ => Positional.Front
            };

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

        if (AvailableCharges(action) <= 1)
            return;

        if (action == ActionID.MakeSpell(NIN.AID.Shukuchi))
            Hints.ActionsToExecute.Push(action, null, gap.Priority(), gap.Value.ExpireIn, targetPos: target.Position.ToVec3(Player.PosRot.Y));
        else if (action == ActionID.MakeSpell(RPR.AID.HellsIngress))
            Hints.ActionsToExecute.Push(action, Player, gap.Priority(), gap.Value.ExpireIn, facingAngle: Player.AngleTo(target));
        else
            Hints.ActionsToExecute.Push(action, target, gap.Priority(), gap.Value.ExpireIn);
    }

    private int AvailableCharges(ActionID action)
    {
        var def = ActionDefinitions.Instance[action];
        if (def == null)
            return 0;

        var maxCharges = def.MaxChargesAtLevel(Player.Level);
        if (maxCharges <= 1)
            return def.ReadyIn(World.Client.Cooldowns, World.Client.DutyActions) <= 0.5f ? 1 : 0;

        var capIn = def.ChargeCapIn(World.Client.Cooldowns, World.Client.DutyActions, Player.Level);
        if (capIn <= 0.05f)
            return maxCharges;

        var singleChargeCooldown = def.Cooldown;
        if (singleChargeCooldown <= 0)
            return maxCharges;

        var chargesMissing = (int)Math.Ceiling(Math.Max(0, capIn - 0.05f) / singleChargeCooldown);
        return Math.Clamp(maxCharges - chargesMissing, 0, maxCharges);
    }

    private static bool IsValidHostileTarget(Actor target) => target.HPMP.MaxHP > 0 && target.IsTargetable && !target.IsDeadOrDestroyed && !target.IsAlly;
}
