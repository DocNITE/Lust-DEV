using Content.Server.Explosion.Components;
using Content.Server.Flash.Components;
using Content.Shared.Explosion;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Throwing;
using Robust.Shared.Containers;
using Robust.Shared.Random;

namespace Content.Server.Explosion.EntitySystems;

public sealed class ClusterGrenadeSystem : EntitySystem
{
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly SharedContainerSystem _container = default!;
    [Dependency] private readonly TriggerSystem _trigger = default!;
    [Dependency] private readonly ThrowingSystem _throwingSystem = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<ClusterGrenadeComponent, ComponentInit>(OnClugInit);
        SubscribeLocalEvent<ClusterGrenadeComponent, ComponentStartup>(OnClugStartup);
        SubscribeLocalEvent<ClusterGrenadeComponent, InteractUsingEvent>(OnClugUsing);
        SubscribeLocalEvent<ClusterGrenadeComponent, UseInHandEvent>(OnClugUse);
    }

    private void OnClugInit(EntityUid uid, ClusterGrenadeComponent component, ComponentInit args)
    {
        component.GrenadesContainer = _container.EnsureContainer<Container>(uid, "cluster-flash");
    }

    private void OnClugStartup(Entity<ClusterGrenadeComponent> clug, ref ComponentStartup args)
    {
        var component = clug.Comp;
        if (component.FillPrototype != null)
        {
            component.UnspawnedCount = Math.Max(0, component.MaxGrenades - component.GrenadesContainer.ContainedEntities.Count);
            UpdateAppearance(clug);
        }
    }

    private void OnClugUsing(Entity<ClusterGrenadeComponent> clug, ref InteractUsingEvent args)
    {
        if (args.Handled)
            return;

        var component = clug.Comp;

        // TODO: Should use whitelist.
        if (component.GrenadesContainer.ContainedEntities.Count >= component.MaxGrenades ||
            !HasComp<FlashOnTriggerComponent>(args.Used))
            return;

        component.GrenadesContainer.Insert(args.Used);
        UpdateAppearance(clug);
        args.Handled = true;
    }

    private void OnClugUse(EntityUid uid, ClusterGrenadeComponent component, UseInHandEvent args)
    {
        if (component.CountDown || (component.GrenadesContainer.ContainedEntities.Count + component.UnspawnedCount) <= 0)
            return;

        // TODO: Should be an Update loop
        uid.SpawnTimer((int) (component.Delay * 1000), () =>
        {
            if (Deleted(uid))
                return;

            component.CountDown = true;
            var delay = 20;
            var grenadesInserted = component.GrenadesContainer.ContainedEntities.Count + component.UnspawnedCount;
            var thrownCount = 0;
            var segmentAngle = 360 / grenadesInserted;
            while (TryGetGrenade((uid, component), out var grenade))
            {
                var angleMin = segmentAngle * thrownCount;
                var angleMax = segmentAngle * (thrownCount + 1);
                var angle = Angle.FromDegrees(_random.Next(angleMin, angleMax));
                // var distance = random.NextFloat() * _throwDistance;

                delay += _random.Next(550, 900);
                thrownCount++;

                // TODO: Suss out throw strength
                _throwingSystem.TryThrow(grenade, angle.ToVec().Normalized() * component.ThrowDistance);

                grenade.SpawnTimer(delay, () =>
                {
                    if ((!EntityManager.EntityExists(grenade) ? EntityLifeStage.Deleted : MetaData(grenade).EntityLifeStage) >= EntityLifeStage.Deleted)
                        return;

                    _trigger.Trigger(grenade, args.User);
                });
            }

            EntityManager.DeleteEntity(uid);
        });

        args.Handled = true;
    }

    private bool TryGetGrenade(Entity<ClusterGrenadeComponent> ent, out EntityUid grenade)
    {
        grenade = default;
        var component = ent.Comp;

        if (component.UnspawnedCount > 0)
        {
            component.UnspawnedCount--;
            grenade = EntityManager.SpawnEntity(component.FillPrototype, Transform(ent).MapPosition);
            return true;
        }

        if (component.GrenadesContainer.ContainedEntities.Count > 0)
        {
            grenade = component.GrenadesContainer.ContainedEntities[0];

            // This shouldn't happen but you never know.
            if (!component.GrenadesContainer.Remove(grenade))
                return false;

            return true;
        }

        return false;
    }

    private void UpdateAppearance(Entity<ClusterGrenadeComponent> ent)
    {
        var component = ent.Comp;
        if (!TryComp<AppearanceComponent>(ent, out var appearance))
            return;

        _appearance.SetData(ent, ClusterGrenadeVisuals.GrenadesCounter, component.GrenadesContainer.ContainedEntities.Count + component.UnspawnedCount, appearance);
    }
}
