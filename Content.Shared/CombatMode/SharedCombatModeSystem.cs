using Content.Shared.Actions;
using Content.Shared.Buckle.Components;
using Content.Shared.Mind;
using Content.Shared.MouseRotator;
using Content.Shared.Mech.Components;
using Content.Shared.Movement.Components;
using Content.Shared.Movement.Systems;
using Content.Shared.Popups;
using Robust.Shared.Network;
using Robust.Shared.Physics.Events;
using Robust.Shared.Timing;

namespace Content.Shared.CombatMode;

public abstract class SharedCombatModeSystem : EntitySystem
{
    [Dependency] protected readonly IGameTiming Timing = default!;
    [Dependency] private   readonly SharedActionsSystem _actionsSystem = default!;
    [Dependency] private   readonly SharedPopupSystem _popup = default!;
    [Dependency] private   readonly SharedMindSystem  _mind = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CombatModeComponent, MapInitEvent>(OnMapInit);
        SubscribeLocalEvent<CombatModeComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CombatModeComponent, ToggleCombatActionEvent>(OnActionPerform);
        SubscribeLocalEvent<CombatModeComponent, AttemptMobCollideEvent>(OnCollide); // Sunrise-Edit
    }

    // Sunrise-Start
    private void OnCollide(EntityUid uid, CombatModeComponent component, ref AttemptMobCollideEvent args)
    {
        /* Fire edit - нам нужна коллизия всегда
        if (!component.IsInCombatMode)
        {
            args.Cancelled = true;
        }
        */
    }
    // Sunrise-End

    private void OnMapInit(EntityUid uid, CombatModeComponent component, MapInitEvent args)
    {
        _actionsSystem.AddAction(uid, ref component.CombatToggleActionEntity, component.CombatToggleAction);
        Dirty(uid, component);
    }

    private void OnShutdown(EntityUid uid, CombatModeComponent component, ComponentShutdown args)
    {
        _actionsSystem.RemoveAction(uid, component.CombatToggleActionEntity);

        SetMouseRotatorComponents(uid, false);
    }

    private void OnActionPerform(EntityUid uid, CombatModeComponent component, ToggleCombatActionEvent args)
    {
        if (args.Handled)
            return;

        args.Handled = true;
        SetInCombatMode(uid, !component.IsInCombatMode, component);

        var msg = component.IsInCombatMode ? "action-popup-combat-enabled" : "action-popup-combat-disabled";
        _popup.PopupClient(Loc.GetString(msg), args.Performer, args.Performer);
    }

    public void SetCanDisarm(EntityUid entity, bool canDisarm, CombatModeComponent? component = null)
    {
        if (!Resolve(entity, ref component))
            return;

        component.CanDisarm = canDisarm;
    }

    public bool IsInCombatMode(EntityUid? entity, CombatModeComponent? component = null)
    {
        return entity != null && Resolve(entity.Value, ref component, false) && component.IsInCombatMode;
    }

    public virtual void SetInCombatMode(EntityUid entity, bool value, CombatModeComponent? component = null)
    {
        if (!Resolve(entity, ref component))
            return;

        if (component.IsInCombatMode == value)
            return;

        component.IsInCombatMode = value;
        Dirty(entity, component);

        if (component.CombatToggleActionEntity != null)
            _actionsSystem.SetToggled(component.CombatToggleActionEntity, component.IsInCombatMode);

        // Change mouse rotator comps if flag is set
        if (!component.ToggleMouseRotator || IsNpc(entity) && !_mind.TryGetMind(entity, out _, out _))
            return;

        SetMouseRotatorComponents(entity, value);
    }

    private void SetMouseRotatorComponents(EntityUid uid, bool value)
    {
        if (value)
        {
            if (TryComp<MechPilotComponent>(uid, out var mechPilot) && !HasComp<NoRotateOnMoveComponent>(mechPilot.Mech))
            {
                EnsureComp<NoRotateOnMoveComponent>(mechPilot.Mech);
            }

            EnsureComp<MouseRotatorComponent>(uid);
            EnsureComp<NoRotateOnMoveComponent>(uid);
        }
        else
        {
            if (TryComp<MechPilotComponent>(uid, out var mechPilot) && HasComp<NoRotateOnMoveComponent>(mechPilot.Mech))
            {
                RemComp<NoRotateOnMoveComponent>(mechPilot.Mech);
            }

            // Fire edit start - для осматривания на стульях
            if (!TryComp<BuckleComponent>(uid, out var buckle) || !buckle.Buckled)
                RemComp<MouseRotatorComponent>(uid);
            // Fire edit end

            RemComp<NoRotateOnMoveComponent>(uid);
        }
    }

    // todo: When we stop making fucking garbage abstract shared components, remove this shit too.
    protected abstract bool IsNpc(EntityUid uid);
}

public sealed partial class ToggleCombatActionEvent : InstantActionEvent
{

}
