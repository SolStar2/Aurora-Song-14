// Aurora Song - AS Bluespace Bounty System
// Based on New Frontier Station 14's Bluespace Error System
// Original implementation: https://github.com/new-frontiers-14/frontier-station-14
// This system extends bluespace error events to check for specific mob prototypes and their cuff states

using Content.Server.Cargo.Systems;
using Content.Server.Chat.Systems;
using Content.Server.GameTicking;
using Content.Server.Radio.EntitySystems;
using Content.Server.Station.Systems;
using Content.Server.StationEvents.Components;
using Content.Server.StationEvents.Events;
using Content.Shared.Cuffs.Components;
using Content.Shared.GameTicking.Components;
using Content.Shared.Mobs;
using Content.Shared.Mobs.Components;
using Content.Server._NF.Bank;
using Content.Server._AS.StationEvents.Components;
using Content.Shared._NF.Bank.BUI;
using Content.Server._NF.Salvage;
using Content.Server._NF.StationEvents.Components;
using Content.Server._NF.Station.Systems;
using Robust.Server.GameObjects;
using Robust.Shared.Map;
using Robust.Shared.Random;
using Robust.Shared.Prototypes;
using Content.Server.Shuttles.Components;
using Content.Server.Shuttles.Systems;
using Content.Shared.Salvage;
using Content.Server.Maps.NameGenerators;
using System.Numerics;
using Content.Shared.Dataset;
using Robust.Shared.EntitySerialization.Systems;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Server._AS.StationEvents.Events;

/// <summary>
/// Aurora Song - Bounty variant of New Frontier's BluespaceErrorRule
/// Checks for specific mob prototypes and their cuff states for bonus rewards
/// Most logic adapted from Frontier's BluespaceErrorRule with additional bounty checking
/// </summary>
public sealed partial class BluespaceErrorBountyRule : StationEventSystem<BluespaceErrorBountyRuleComponent>
{
    NanotrasenNameGenerator _nameGenerator = new();
    [Dependency] private MapSystem _map = default!;
    [Dependency] private SharedMapSystem _mapSystem = default!;
    [Dependency] private IPrototypeManager _protoManager = default!;
    [Dependency] private IRobustRandom _random = default!;
    [Dependency] private MapLoaderSystem _loader = default!;
    [Dependency] private MetaDataSystem _metadata = default!;
    [Dependency] private BankSystem _bank = default!;
    [Dependency] private ChatSystem _chatManager = default!;
    [Dependency] private PricingSystem _pricing = default!;
    [Dependency] private SharedTransformSystem _transform = default!;
    [Dependency] private ShuttleSystem _shuttle = default!;
    [Dependency] private LinkedLifecycleGridSystem _linkedLifecycleGrid = default!;
    [Dependency] private SharedSalvageSystem _salvage = default!;
    [Dependency] private RadioSystem _radio = default!;
    [Dependency] private StationRenameWarpsSystems _renameWarps = default!;
    [Dependency] private SharedAudioSystem _audio = default!; // Aurora Song

    /// <summary>
    /// Adapted from New Frontier's BluespaceErrorRule.Started
    /// Spawns grid(s) for the bounty event
    /// Aurora Song - Added arrival announcement support for 4-announcement system
    /// </summary>
    protected override void Started(EntityUid uid, BluespaceErrorBountyRuleComponent component, GameRuleComponent gameRule, GameRuleStartedEvent args)
    {
        base.Started(uid, component, gameRule, args);

        Log.Info($"[BOUNTY DEBUG] BluespaceErrorBountyRule.Started called");
        Log.Info($"[BOUNTY DEBUG] Groups count: {component.Groups.Count}");

        // Aurora Song - Send arrival announcement when grid spawns (4-announcement system)
        if (TryComp<StationEventComponent>(uid, out var stationEvent))
        {
            if (!stationEvent.ArrivalAnnounced && stationEvent.ArrivalAnnouncement != null)
            {
                var allPlayersInGame = Filter.Empty().AddWhere(GameTicker.UserHasJoinedGame);

                // Send chat announcement
                _chatManager.DispatchFilteredAnnouncement(allPlayersInGame,
                    Loc.GetString(stationEvent.ArrivalAnnouncement),
                    playSound: false,
                    colorOverride: stationEvent.ArrivalAnnouncementColor);

                // Send radio announcement if configured
                if (stationEvent.ArrivalRadioAnnouncement != null)
                {
                    var message = Loc.GetString(stationEvent.ArrivalRadioAnnouncement);
                    var announcementMapUid = _mapSystem.GetMap(GameTicker.DefaultMap);
                    _radio.SendRadioMessage(uid, message, stationEvent.ArrivalRadioAnnouncementChannel, announcementMapUid, escapeMarkup: false);
                }

                // Play audio if configured
                _audio.PlayGlobal(stationEvent.ArrivalAudio, allPlayersInGame, true);

                stationEvent.ArrivalAnnounced = true;
                Log.Info($"[BOUNTY DEBUG] Arrival announcement sent");
            }
        }

        if (!_map.TryGetMap(GameTicker.DefaultMap, out var mapUid))
        {
            Log.Error($"[BOUNTY DEBUG] Failed to get default map!");
            return;
        }

        var spawnCoords = new EntityCoordinates(mapUid.Value, Vector2.Zero);

        // Spawn on a dummy map and try to FTL if possible, otherwise dump it.
        _map.CreateMap(out var mapId);
        Log.Info($"[BOUNTY DEBUG] Created temporary map: {mapId}");

        foreach (var (groupKey, group) in component.Groups)
        {
            Log.Info($"[BOUNTY DEBUG] Processing group: {groupKey}");
            var count = _random.Next(group.MinCount, group.MaxCount + 1);
            Log.Info($"[BOUNTY DEBUG] Will spawn {count} grids from this group");

            for (var i = 0; i < count; i++)
            {
                EntityUid spawned;

                if (group.MinimumDistance > 0f)
                {
                    spawnCoords = spawnCoords.WithPosition(_random.NextVector2(group.MinimumDistance, group.MaximumDistance));
                    Log.Info($"[BOUNTY DEBUG] Spawn coords: {spawnCoords.Position}");
                }

                switch (group)
                {
                    case BluespaceGridSpawnGroup grid:
                        if (!TryGridSpawn(spawnCoords, uid, mapId, ref grid, i, out spawned))
                        {
                            Log.Error($"[BOUNTY DEBUG] Failed to spawn grid {i}");
                            continue;
                        }
                        Log.Info($"[BOUNTY DEBUG] Successfully spawned grid: {spawned}");
                        break;
                    default:
                        Log.Error($"[BOUNTY DEBUG] Unknown spawn group type: {group.GetType()}");
                        throw new NotImplementedException();
                }

                if (group.NameLoc != null && group.NameLoc.Count > 0)
                {
                    var gridName = Loc.GetString(_random.Pick(group.NameLoc));
                    _metadata.SetEntityName(spawned, gridName);
                    Log.Info($"[BOUNTY DEBUG] Named grid: {gridName}");
                }
                else if (_protoManager.TryIndex(group.NameDataset, out var dataset))
                {
                    string gridName;
                    switch (group.NameDatasetType)
                    {
                        case BluespaceDatasetNameType.FTL:
                            gridName = _salvage.GetFTLName(dataset, _random.Next());
                            break;
                        case BluespaceDatasetNameType.Nanotrasen:
                            gridName = _nameGenerator.FormatName(Loc.GetString(_random.Pick(dataset.Values)) + " {1}");
                            break;
                        default:
                            throw new NotImplementedException();
                    }
                    _metadata.SetEntityName(spawned, gridName);
                    Log.Info($"[BOUNTY DEBUG] Named grid from dataset: {gridName}");
                }

                // Sync warp points to use the grid's name
                if (group.NameWarp)
                {
                    bool? adminOnly = group.HideWarp ? true : null;
                    _renameWarps.SyncWarpPointsToGrid(spawned, forceAdminOnly: adminOnly);
                    Log.Info($"[BOUNTY DEBUG] Synced warp points to grid, adminOnly: {adminOnly}");
                }

                // Apply any additional components specified in the YAML
                EntityManager.AddComponents(spawned, group.AddComponents);
                Log.Info($"[BOUNTY DEBUG] Applied components to grid");

                component.GridsUid.Add(spawned);
                Log.Info($"[BOUNTY DEBUG] Added grid to component.GridsUid, total count: {component.GridsUid.Count}");
            }
        }
    }

    /// <summary>
    /// Adapted from New Frontier's BluespaceErrorRule.TryGridSpawn
    /// Loads a grid from a map file
    /// </summary>
    private bool TryGridSpawn(EntityCoordinates spawnCoords, EntityUid stationUid, MapId mapId, ref BluespaceGridSpawnGroup group, int i, out EntityUid spawned)
    {
        spawned = EntityUid.Invalid;

        if (group.Paths.Count == 0)
        {
            Log.Error($"[BOUNTY DEBUG] Found no paths for GridSpawn");
            return false;
        }

        Log.Info($"[BOUNTY DEBUG] TryGridSpawn - paths available: {group.Paths.Count}");

        // Pick a random path without modifying the original list
        var path = _random.Pick(group.Paths);
        Log.Info($"[BOUNTY DEBUG] Selected path: {path}");

        // Load the grid
        if (_loader.TryLoadGrid(mapId, path, out var ent))
        {
            Log.Info($"[BOUNTY DEBUG] Grid loaded successfully: {ent}");

            if (HasComp<ShuttleComponent>(ent.Value))
            {
                Log.Info($"[BOUNTY DEBUG] Grid has shuttle component, attempting FTL");
                _shuttle.TryFTLProximity(ent.Value.Owner, spawnCoords);
            }

            if (group.NameGrid)
            {
                var name = path.FilenameWithoutExtension;
                _metadata.SetEntityName(ent.Value, name);
                Log.Info($"[BOUNTY DEBUG] Named grid from filename: {name}");
            }

            spawned = ent.Value;
            return true;
        }

        Log.Error($"[BOUNTY DEBUG] Error loading gridspawn for {ToPrettyString(stationUid)} / {path}");
        return false;
    }

    /// <summary>
    /// Processes all bounty objectives and calculates flat rewards/penalties
    /// Aurora Song - Custom bounty logic
    /// </summary>
    private (int totalReward, Dictionary<string, (int success, int expected)> stats) ProcessBountyObjectives(BluespaceErrorBountyRuleComponent component)
    {
        var totalReward = 0;
        var stats = new Dictionary<string, (int success, int expected)>();

        // DEBUG: Log objective counts
        Log.Info($"[BOUNTY DEBUG] Starting ProcessBountyObjectives");
        Log.Info($"[BOUNTY DEBUG] Capture targets: {component.CaptureTargets.Count}");
        Log.Info($"[BOUNTY DEBUG] Elimination targets: {component.EliminationTargets.Count}");
        Log.Info($"[BOUNTY DEBUG] Removal targets: {component.RemovalTargets.Count}");
        Log.Info($"[BOUNTY DEBUG] Rescue targets: {component.RescueTargets.Count}");
        Log.Info($"[BOUNTY DEBUG] Grids to check: {component.GridsUid.Count}");

        // Early exit if no objectives
        var hasObjectives = component.CaptureTargets.Count > 0 ||
                           component.EliminationTargets.Count > 0 ||
                           component.RemovalTargets.Count > 0 ||
                           component.RescueTargets.Count > 0;

        if (!hasObjectives)
        {
            Log.Info($"[BOUNTY DEBUG] No objectives configured, exiting early");
            return (totalReward, stats);
        }

        // Initialize counters OUTSIDE grid loop - accumulate across all grids
        var captureCounters = new Dictionary<string, int>();
        var eliminationCounters = new Dictionary<string, (int dead, int alive)>();
        var removalCounters = new Dictionary<string, int>();
        var rescueCounters = new Dictionary<string, int>();

        foreach (var gridUid in component.GridsUid)
        {
            Log.Info($"[BOUNTY DEBUG] Processing grid: {gridUid}");

            if (!TryComp(gridUid, out TransformComponent? gridTransform))
            {
                Log.Warning($"[BOUNTY DEBUG] Grid {gridUid} has no transform component");
                continue;
            }

            if (gridTransform.GridUid is not EntityUid actualGridUid)
            {
                Log.Warning($"[BOUNTY DEBUG] Grid {gridUid} has no actual grid UID");
                continue;
            }

            Log.Info($"[BOUNTY DEBUG] Actual grid UID: {actualGridUid}");

            // Single pass through all mobs on the grid
            if (component.CaptureTargets.Count > 0 || component.EliminationTargets.Count > 0 || component.RescueTargets.Count > 0)
            {
                Log.Info($"[BOUNTY DEBUG] Starting mob scan on grid {actualGridUid}");
                var mobCount = 0;
                var mobQuery = AllEntityQuery<MobStateComponent, TransformComponent, MetaDataComponent>();
                while (mobQuery.MoveNext(out var mobUid, out var mobState, out var transform, out var metadata))
                {
                    if (transform.GridUid != actualGridUid)
                        continue;

                    mobCount++;
                    var protoId = metadata.EntityPrototype?.ID;
                    if (protoId == null)
                        continue;

                    var isAlive = mobState.CurrentState == MobState.Alive;
                    var isDead = mobState.CurrentState == MobState.Dead;

                    Log.Info($"[BOUNTY DEBUG] Found mob: {protoId} (UID: {mobUid}, State: {mobState.CurrentState})");

                    // Check capture targets (alive AND cuffed)
                    if (component.CaptureTargets.ContainsKey(protoId) && isAlive)
                    {
                        var isCuffed = TryComp<CuffableComponent>(mobUid, out var cuffable) && cuffable.CuffedHandCount > 0;
                        Log.Info($"[BOUNTY DEBUG] Capture target {protoId}: alive={isAlive}, cuffed={isCuffed}");
                        if (isCuffed)
                            captureCounters[protoId] = captureCounters.GetValueOrDefault(protoId) + 1;
                    }

                    // Check elimination targets (dead)
                    if (component.EliminationTargets.ContainsKey(protoId))
                    {
                        var (deadCount, aliveCount) = eliminationCounters.GetValueOrDefault(protoId);
                        eliminationCounters[protoId] = isDead ? (deadCount + 1, aliveCount) : (deadCount, aliveCount + 1);
                        Log.Info($"[BOUNTY DEBUG] Elimination target {protoId}: dead={isDead}");
                    }

                    // Check rescue targets (alive AND not cuffed)
                    if (component.RescueTargets.ContainsKey(protoId) && isAlive)
                    {
                        var isCuffed = TryComp<CuffableComponent>(mobUid, out var cuffableRescue) && cuffableRescue.CuffedHandCount > 0;
                        var isNotCuffed = !isCuffed;
                        Log.Info($"[BOUNTY DEBUG] Rescue target {protoId}: alive={isAlive}, notCuffed={isNotCuffed}, cuffed={isCuffed}");
                        if (isNotCuffed)
                            rescueCounters[protoId] = rescueCounters.GetValueOrDefault(protoId) + 1;
                    }
                }
                Log.Info($"[BOUNTY DEBUG] Mob scan complete. Total mobs on grid: {mobCount}");
            }

            // Single pass through all entities for removal targets
            if (component.RemovalTargets.Count > 0)
            {
                Log.Info($"[BOUNTY DEBUG] Starting entity scan for removal targets on grid {actualGridUid}");
                var entityCount = 0;
                var entityQuery = AllEntityQuery<TransformComponent, MetaDataComponent>();
                while (entityQuery.MoveNext(out _, out var transform, out var metadata))
                {
                    if (transform.GridUid != actualGridUid)
                        continue;

                    entityCount++;
                    var protoId = metadata.EntityPrototype?.ID;
                    if (protoId != null && component.RemovalTargets.ContainsKey(protoId))
                    {
                        removalCounters[protoId] = removalCounters.GetValueOrDefault(protoId) + 1;
                        Log.Info($"[BOUNTY DEBUG] Found removal target: {protoId}");
                    }
                }
                Log.Info($"[BOUNTY DEBUG] Entity scan complete. Total entities on grid: {entityCount}");
            }
        }

        // Process all results AFTER scanning all grids
        // Process capture results
        foreach (var (protoId, target) in component.CaptureTargets)
        {
            if (target.ExpectedCount <= 0)
                continue;

            var captured = captureCounters.GetValueOrDefault(protoId);
            var missing = Math.Max(0, target.ExpectedCount - captured);
            var reward = captured * target.RewardPerTarget;
            var penalty = missing * target.PenaltyPerMissing;
            totalReward += reward - penalty;
            stats[$"capture_{protoId}"] = (captured, target.ExpectedCount);

            if (captured >= target.ExpectedCount)
            {
                Log.Info($"[BOUNTY DEBUG] ✓ Capture {protoId}: SUCCESS - {captured}/{target.ExpectedCount} captured (reward: +{reward})");
            }
            else if (captured > 0)
            {
                Log.Info($"[BOUNTY DEBUG] ⚠ Capture {protoId}: PARTIAL - {captured}/{target.ExpectedCount} captured, {missing} missing (reward: +{reward}, penalty: -{penalty}, net: {reward - penalty})");
            }
            else
            {
                Log.Warning($"[BOUNTY DEBUG] ✗ Capture {protoId}: FAILED - 0/{target.ExpectedCount} captured (penalty: -{penalty})");
            }
        }

        // Process elimination results
        foreach (var (protoId, target) in component.EliminationTargets)
        {
            if (target.ExpectedCount <= 0)
                continue;

            var (dead, alive) = eliminationCounters.GetValueOrDefault(protoId);
            var reward = dead * target.RewardPerTarget;
            var penalty = alive * target.PenaltyPerSurvivor;
            totalReward += reward - penalty;
            stats[$"eliminate_{protoId}"] = (dead, target.ExpectedCount);

            if (dead >= target.ExpectedCount && alive == 0)
            {
                Log.Info($"[BOUNTY DEBUG] ✓ Eliminate {protoId}: SUCCESS - {dead}/{target.ExpectedCount} eliminated, 0 survivors (reward: +{reward})");
            }
            else if (dead > 0)
            {
                Log.Info($"[BOUNTY DEBUG] ⚠ Eliminate {protoId}: PARTIAL - {dead}/{target.ExpectedCount} eliminated, {alive} still alive (reward: +{reward}, penalty: -{penalty}, net: {reward - penalty})");
            }
            else
            {
                Log.Warning($"[BOUNTY DEBUG] ✗ Eliminate {protoId}: FAILED - 0/{target.ExpectedCount} eliminated, {alive} still alive (penalty: -{penalty})");
            }
        }

        // Process removal results
        foreach (var (protoId, target) in component.RemovalTargets)
        {
            if (target.ExpectedCount <= 0)
                continue;

            var remaining = removalCounters.GetValueOrDefault(protoId);
            var removed = Math.Max(0, target.ExpectedCount - remaining);
            var reward = removed * target.RewardPerRemoved;
            var penalty = remaining * target.PenaltyPerRemaining;
            totalReward += reward - penalty;
            stats[$"remove_{protoId}"] = (removed, target.ExpectedCount);

            if (remaining == 0 && removed >= target.ExpectedCount)
            {
                Log.Info($"[BOUNTY DEBUG] ✓ Remove {protoId}: SUCCESS - {removed}/{target.ExpectedCount} removed, 0 remaining (reward: +{reward})");
            }
            else if (removed > 0)
            {
                Log.Info($"[BOUNTY DEBUG] ⚠ Remove {protoId}: PARTIAL - {removed}/{target.ExpectedCount} removed, {remaining} still on grid (reward: +{reward}, penalty: -{penalty}, net: {reward - penalty})");
            }
            else
            {
                Log.Warning($"[BOUNTY DEBUG] ✗ Remove {protoId}: FAILED - 0/{target.ExpectedCount} removed, {remaining} still on grid (penalty: -{penalty})");
            }
        }

        // Process rescue results
        foreach (var (protoId, target) in component.RescueTargets)
        {
            if (target.ExpectedCount <= 0)
                continue;

            var rescued = rescueCounters.GetValueOrDefault(protoId);
            var missing = Math.Max(0, target.ExpectedCount - rescued);
            var reward = rescued * target.RewardPerTarget;
            var penalty = missing * target.PenaltyPerMissing;
            totalReward += reward - penalty;
            stats[$"rescue_{protoId}"] = (rescued, target.ExpectedCount);

            if (rescued >= target.ExpectedCount)
            {
                Log.Info($"[BOUNTY DEBUG] ✓ Rescue {protoId}: SUCCESS - {rescued}/{target.ExpectedCount} rescued (alive & not cuffed) (reward: +{reward})");
            }
            else if (rescued > 0)
            {
                Log.Info($"[BOUNTY DEBUG] ⚠ Rescue {protoId}: PARTIAL - {rescued}/{target.ExpectedCount} rescued, {missing} failed (dead or cuffed) (reward: +{reward}, penalty: -{penalty}, net: {reward - penalty})");
            }
            else
            {
                Log.Warning($"[BOUNTY DEBUG] ✗ Rescue {protoId}: FAILED - 0/{target.ExpectedCount} rescued, all dead or cuffed (penalty: -{penalty})");
            }
        }

        Log.Info($"[BOUNTY DEBUG] ProcessBountyObjectives complete. Total reward: {totalReward}");
        return (totalReward, stats);
    }

    /// <summary>
    /// Adapted from New Frontier's BluespaceErrorRule.Ended method
    /// Extended with flat bounty rewards/penalties
    /// </summary>
    protected override void Ended(EntityUid uid, BluespaceErrorBountyRuleComponent component, GameRuleComponent gameRule, GameRuleEndedEvent args)
    {
        base.Ended(uid, component, gameRule, args);

        Log.Info($"[BOUNTY DEBUG] BluespaceErrorBountyRule.Ended called");
        Log.Info($"[BOUNTY DEBUG] GridsUid count: {component.GridsUid.Count}");

        if (component.GridsUid.Count == 0)
        {
            Log.Warning($"[BOUNTY DEBUG] No grids to process, exiting");
            return;
        }

        // Aurora Song - Process all bounty objectives for flat rewards
        Log.Info($"[BOUNTY DEBUG] Calling ProcessBountyObjectives");
        var (bountyReward, stats) = ProcessBountyObjectives(component);
        Log.Info($"[BOUNTY DEBUG] ProcessBountyObjectives returned: bountyReward={bountyReward}, stats count={stats.Count}");

        // Process each grid (adapted from Frontier's logic)
        foreach (var gridUid in component.GridsUid)
        {
            if (!TryComp(gridUid, out TransformComponent? gridTransform))
            {
                Log.Error("bluespace error bounty objective was missing transform component");
                continue;
            }

            if (gridTransform.GridUid is not EntityUid actualGridUid)
            {
                Log.Error("bluespace error bounty has no associated grid?");
                continue;
            }

            if (component.DeleteGridsOnEnd)
            {
                // Handle mob restrictions getting deleted (adapted from Frontier)
                var query = AllEntityQuery<NFSalvageMobRestrictionsComponent>();
                while (query.MoveNext(out var salvUid, out var salvMob))
                {
                    if (!salvMob.DespawnIfOffLinkedGrid)
                    {
                        var salvTransform = Transform(salvUid);
                        if (salvTransform.GridUid != salvMob.LinkedGridEntity)
                        {
                            RemComp<NFSalvageMobRestrictionsComponent>(salvUid);
                            continue;
                        }
                    }

                    if (gridTransform.GridUid == salvMob.LinkedGridEntity)
                    {
                        QueueDel(salvUid);
                    }
                }

                var playerMobs = _linkedLifecycleGrid.GetEntitiesToReparent(actualGridUid);
                foreach (var mob in playerMobs)
                {
                    _transform.DetachEntity(mob.Entity.Owner, mob.Entity.Comp);
                }

                // Calculate base grid value (Frontier)
                var gridValue = _pricing.AppraiseGrid(actualGridUid, null);

                // Deletion has to happen before grid traversal re-parents players (adapted from Frontier)
                Del(actualGridUid);

                foreach (var mob in playerMobs)
                {
                    _transform.SetCoordinates(mob.Entity.Owner, new EntityCoordinates(mob.MapUid, mob.MapPosition));
                }

                // Award base salvage rewards (Frontier logic) - only if includeGridValue is true
                if (component.IncludeGridValue)
                {
                    foreach (var (account, rewardCoeff) in component.RewardAccounts)
                    {
                        var baseReward = (int)(gridValue * rewardCoeff);
                        Log.Info($"[BOUNTY DEBUG] Awarding base salvage to {account}: {baseReward} (gridValue={gridValue}, coeff={rewardCoeff})");
                        _bank.TrySectorDeposit(account, baseReward, LedgerEntryType.BluespaceReward);
                    }
                }
                else
                {
                    Log.Info($"[BOUNTY DEBUG] Skipping grid value rewards (IncludeGridValue=false, gridValue={gridValue})");
                }

                // Award flat bounty bonus/penalty (Aurora Song)
                if (bountyReward > 0)
                {
                    Log.Info($"[BOUNTY DEBUG] Awarding bounty rewards: {bountyReward}");
                    foreach (var (account, rewardCoeff) in component.RewardAccounts)
                    {
                        var accountBonus = (int)(bountyReward * rewardCoeff);
                        Log.Info($"[BOUNTY DEBUG] Awarding bounty to {account}: {accountBonus} (bountyReward={bountyReward}, coeff={rewardCoeff})");
                        _bank.TrySectorDeposit(account, accountBonus, LedgerEntryType.BluespaceReward);
                    }
                }
                else if (bountyReward < 0)
                {
                    Log.Info($"[BOUNTY DEBUG] Applying bounty penalties: {bountyReward}");
                    foreach (var (account, rewardCoeff) in component.RewardAccounts)
                    {
                        var accountPenalty = (int)(Math.Abs(bountyReward) * rewardCoeff);
                        Log.Info($"[BOUNTY DEBUG] Withdrawing penalty from {account}: {accountPenalty} (bountyReward={bountyReward}, coeff={rewardCoeff})");
                        _bank.TrySectorWithdraw(account, accountPenalty, LedgerEntryType.BluespaceReward);
                    }
                }
                else
                {
                    Log.Info($"[BOUNTY DEBUG] No bounty reward/penalty to award (bountyReward=0)");
                }
            }
        }

        // Clean up maps (adapted from Frontier)
        foreach (var mapId in component.MapsUid)
        {
            if (_mapSystem.MapExists(mapId))
                _mapSystem.DeleteMap(mapId);
        }

        // Announce bounty results via radio (Aurora Song)
        if (stats.Count > 0)
        {
            Log.Info($"[BOUNTY DEBUG] Sending radio announcement (stats count: {stats.Count})");
            Log.Info($"[BOUNTY DEBUG] Configured channels count: {component.AnnouncementChannels.Count}");

            // Debug: Print all channels in the list
            for (int i = 0; i < component.AnnouncementChannels.Count; i++)
            {
                Log.Info($"[BOUNTY DEBUG] Channel[{i}]: '{component.AnnouncementChannels[i]}'");
            }

            var announcement = BuildAnnouncementMessage(stats, bountyReward);
            var mapUid = _mapSystem.GetMap(GameTicker.DefaultMap);
            Log.Info($"[BOUNTY DEBUG] MapUid: {mapUid}, Event UID: {uid}");

            // Send to all configured channels
            var channelIndex = 0;
            foreach (var channel in component.AnnouncementChannels)
            {
                channelIndex++;
                Log.Info($"[BOUNTY DEBUG] [{channelIndex}] Attempting to send to channel: '{channel}'");
                try
                {
                    _radio.SendRadioMessage(uid, announcement, channel, mapUid, escapeMarkup: false);
                    Log.Info($"[BOUNTY DEBUG] [{channelIndex}] Successfully sent to channel: '{channel}'");
                }
                catch (Exception ex)
                {
                    Log.Error($"[BOUNTY DEBUG] [{channelIndex}] Failed to send to channel '{channel}': {ex.Message}");
                }
            }
            Log.Info($"[BOUNTY DEBUG] Finished sending radio announcements");
        }
        else
        {
            Log.Info($"[BOUNTY DEBUG] No stats to announce");
        }
    }

    /// <summary>
    /// Builds announcement message from bounty statistics
    /// Aurora Song - Custom announcement logic with localization
    /// </summary>
    private string BuildAnnouncementMessage(Dictionary<string, (int success, int expected)> stats, int totalReward)
    {
        var lines = new List<string>();
        var allComplete = true;
        var anyProgress = false;

        foreach (var (key, (success, expected)) in stats)
        {
            var parts = key.Split('_', 2);
            var type = parts[0];
            var proto = parts.Length > 1 ? parts[1] : "unknown";

            // Track completion status for summary
            if (success >= expected)
            {
                // This objective is complete
                anyProgress = true;
            }
            else if (success > 0)
            {
                // This objective has partial progress
                allComplete = false;
                anyProgress = true;
            }
            else
            {
                // This objective failed completely
                allComplete = false;
            }

            string line = type switch
            {
                "capture" => Loc.GetString("bluespace-bounty-captured",
                    ("success", success), ("expected", expected), ("prototype", proto)),
                "eliminate" => Loc.GetString("bluespace-bounty-eliminated",
                    ("success", success), ("expected", expected), ("prototype", proto)),
                "remove" => Loc.GetString("bluespace-bounty-removed",
                    ("success", success), ("expected", expected), ("prototype", proto)),
                "rescue" => Loc.GetString("bluespace-bounty-rescued",
                    ("success", success), ("expected", expected), ("prototype", proto)),
                _ => $"{key}: {success}/{expected}"
            };

            lines.Add(line);
        }

        // Determine summary based on completion status
        string summary;
        if (allComplete)
        {
            // All objectives fully completed
            summary = Loc.GetString("bluespace-bounty-success-summary", ("reward", totalReward));
        }
        else if (anyProgress)
        {
            // Some objectives completed or partially completed
            summary = Loc.GetString("bluespace-bounty-partial-summary", ("reward", totalReward));
        }
        else
        {
            // All objectives completely failed
            summary = Loc.GetString("bluespace-bounty-failure-summary", ("penalty", Math.Abs(totalReward)));
        }

        return $"{summary}\n{string.Join("\n", lines)}";
    }
}
