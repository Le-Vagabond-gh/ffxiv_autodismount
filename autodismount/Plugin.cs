using Dalamud.Game.ClientState.Conditions;
using Dalamud.Hooking;
using Dalamud.Plugin;
using Dalamud.Plugin.Services;
using FFXIVClientStructs.FFXIV.Client.Game;
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

        public unsafe Plugin(IDalamudPluginInterface pluginInterface)
        {
            this.PluginInterface = pluginInterface;
            this.PluginInterface.Create<Service>(this);
            this.Configuration = this.PluginInterface.GetPluginConfig() as Configuration ?? new Configuration();
            this.Configuration.Initialize(this.PluginInterface);

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
                        Service.PluginLog.Info($"Auto-dismounting: Action {actionID} blocked while mounted (status {actionStatus})");

                        // Dismount by using the Mount action (General Action ID 9)
                        self->UseAction(ActionType.GeneralAction, 9, 0xE0000000, 0, 0, 0, null);

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

        public void Dispose()
        {
            this.useActionHook?.Dispose();
            Service.PluginLog.Info("AutoDismount disposed");
        }
    }
}
