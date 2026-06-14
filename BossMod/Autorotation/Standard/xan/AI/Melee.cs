namespace BossMod.Autorotation.xan;

public sealed class MeleeAI(RotationModuleManager manager, Actor player) : AIBase<MeleeAI.Strategy>(manager, player)
{
    public struct Strategy
    {
        [Track("Second Wind", InternalName = "Second Wind")]
        public Track<EnabledByDefault> SecondWind;
        public Track<EnabledByDefault> Bloodbath;
        public Track<EnabledByDefault> Stun;
    }

    public static RotationModuleDefinition Definition()
    {
        return new RotationModuleDefinition("Melee DPS AI", "Utilities for melee - bloodbath, second wind, stun", "AI (xan)", "xan", RotationModuleQuality.Basic, BitMask.Build(Class.PGL, Class.MNK, Class.LNC, Class.DRG, Class.ROG, Class.NIN, Class.SAM, Class.RPR, Class.VPR), 100).WithStrategies<Strategy>();
    }

    public override void Execute(in Strategy strategy, Actor? primaryTarget, float estimatedAnimLockDelay, bool isMoving)
    {
        if (Player.Statuses.Any(x => x.ID is (uint)BossMod.NIN.SID.TenChiJin or (uint)BossMod.NIN.SID.Mudra or 1092))
            return;

        // second wind
        if (strategy.SecondWind.IsEnabled() && Player.InCombat && Player.PendingHPRatio <= 0.5)
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.SecondWind), Player, ActionQueue.Priority.Medium);

        // bloodbath
        if (strategy.Bloodbath.IsEnabled() && Player.InCombat && Player.PendingHPRatio <= 0.3)
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.Bloodbath), Player, ActionQueue.Priority.Medium);

        // low blow
        if (strategy.Stun.IsEnabled() && NextChargeIn(ClassShared.AID.LegSweep) == 0)
        {
            var stunnableEnemy = Hints.PotentialTargets.FirstOrDefault(e => ShouldStun(e) && Player.DistanceToHitbox(e.Actor) <= 3);
            if (stunnableEnemy != null)
                Hints.ActionsToExecute.Push(ActionID.MakeSpell(ClassShared.AID.LegSweep), stunnableEnemy.Actor, ActionQueue.Priority.VeryLow);
        }

        if (Player.FindStatus(2324) != null && Bossmods.ActiveModule?.Info?.GroupType is BossModuleInfo.GroupType.BozjaDuel)
        {
            var gcdLength = ActionSpeed.GCDRounded(World.Client.PlayerStats.SkillSpeed, World.Client.PlayerStats.Haste, Player.Level);
            var fopLeft = Player.FindStatus(2346) is ActorStatus st ? StatusDuration(st.ExpireAt) : 0;
            if (GCD + gcdLength < fopLeft)
                Hints.ActionsToExecute.Push(BozjaActionID.GetNormal(BozjaHolsterID.LostAssassination), primaryTarget, ActionQueue.Priority.Low);
        }

        if (Player.Class == Class.RPR && Hints.PotentialTargets.Any(t => t.Actor.TargetID == Player.InstanceID && t.Actor.CastInfo == null && t.Actor.DistanceToHitbox(Player) < 6))
            Hints.ActionsToExecute.Push(ActionID.MakeSpell(BossMod.RPR.AID.ArcaneCrest), Player, ActionQueue.Priority.VeryLow);
    }
}
