using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Ipc;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
using FFXIVClientStructs.FFXIV.Client.Game.Character;
using FFXIVClientStructs.FFXIV.Client.Game.UI;
using System;

namespace autodismount
{
    public sealed class Plugin : IDalamudPlugin
    {
        public string Name => "AutoDismount";
        private IDalamudPluginInterface PluginInterface { get; init; }
        public Configuration Configuration { get; init; }

        private unsafe delegate bool UseActionDelegate(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7);
        private Hook<UseActionDelegate>? useActionHook;

        // vnavmesh IPC
        private ICallGateSubscriber<bool>? vnavIsRunning;
        private ICallGateSubscriber<bool>? vnavPathfindInProgress;

        public unsafe Plugin(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
            this.PluginInterface.Create<Service>(this);
            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

            // vnavmesh IPC - subscribing is safe even if vnavmesh isn't installed
            this.vnavIsRunning = this.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.Path.IsRunning");
            this.vnavPathfindInProgress = this.PluginInterface.GetIpcSubscriber<bool>("vnavmesh.SimpleMove.PathfindInProgress");

            // Hook ActionManager.UseAction
            this.useActionHook = Service.GameInteropProvider.HookFromAddress<UseActionDelegate>(
                (nint)ActionManager.MemberFunctionPointers.UseAction,
                this.UseActionDetour
            );
            this.useActionHook?.Enable();

            Service.PluginLog.Info("AutoDismount initialized with UseAction hook");
        }

        private unsafe bool UseActionDetour(ActionManager* self, ActionType actionType, uint actionID, ulong targetID, uint a4, uint a5, uint a6, void* a7)
        {
            try
            {
                // Check action status
                var actionStatus = self->GetActionStatus(actionType, actionID);

                // Check for "action unavailable while mounted" status (579)
                if (actionStatus == 579)
                {
                    // Check if player is mounted
                    var isMounted = Service.Condition[ConditionFlag.Mounted];
                    var isRidingPillion = Service.Condition[ConditionFlag.RidingPillion];

                    if (isMounted || isRidingPillion)
                    {
                        // Skip if vnavmesh is actively pathing (another plugin is controlling movement)
                        if (IsVnavmeshPathing())
                        {
                            Service.PluginLog.Info($"Skipping auto-dismount: vnavmesh is actively pathing");
                            return this.useActionHook!.Original(self, actionType, actionID, targetID, a4, a5, a6, a7);
                        }

                        // Skip if this isn't a real player mount (FATE vehicles, cosmic mechs, etc.)
                        if (!IsPlayerMount())
                        {
                            Service.PluginLog.Info($"Skipping auto-dismount: not a real player mount");
                            return this.useActionHook!.Original(self, actionType, actionID, targetID, a4, a5, a6, a7);
                        }

                        Service.PluginLog.Info($"Auto-dismounting: Action {actionID} blocked while mounted (status {actionStatus})");

                        try
                        {
                            // Dismount by using the Mount action (General Action ID 9)
                            self->UseAction(ActionType.GeneralAction, 9, 0xE0000000, 0, 0, 0, null);
                        }
                        catch (Exception ex)
                        {
                            Service.PluginLog.Error(ex, "Error during dismount action");
                        }

                        // Don't execute the original blocked action - it will fail anyway
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Service.PluginLog.Error(ex, "Error in UseAction hook - executing original action anyway");
            }

            // CRITICAL: Always call Original with untouched arguments
            return this.useActionHook!.Original(self, actionType, actionID, targetID, a4, a5, a6, a7);
        }

        private bool IsVnavmeshPathing()
        {
            try
            {
                if (this.vnavIsRunning != null && this.vnavIsRunning.InvokeFunc())
                    return true;
                if (this.vnavPathfindInProgress != null && this.vnavPathfindInProgress.InvokeFunc())
                    return true;
            }
            catch
            {
                // vnavmesh not installed or IPC unavailable - that's fine
            }
            return false;
        }

        private unsafe bool IsPlayerMount()
        {
            // Cosmic mech has a dedicated condition flag
            if (Service.Condition[ConditionFlag.PilotingMech])
                return false;

            // Check if the current mount is one the player actually owns
            var localPlayer = (Character*)Service.ClientState.LocalPlayer?.Address;
            if (localPlayer == null)
                return false;

            var mountId = localPlayer->Mount.MountId;
            if (mountId == 0)
                return false;

            return PlayerState.Instance()->IsMountUnlocked(mountId);
        }

        public void Dispose()
        {
            this.useActionHook?.Dispose();
            Service.PluginLog.Info("AutoDismount disposed");
        }
    }
}
