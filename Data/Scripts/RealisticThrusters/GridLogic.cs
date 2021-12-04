using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.ModAPI;

namespace Digi.RealisticThrusters
{
    public class GridLogic
    {
        public MyCubeGrid Grid;

        private bool _isNPCOwned;
        private long _lastCheckedOwner;

        private float RealismAmount;
        private readonly HashSet<Thruster> Thrusters = new HashSet<Thruster>();

        private float? ForcedRealismAmount;
        private readonly HashSet<IMyShipController> ShipControllers = new HashSet<IMyShipController>();

        // NOTE: object is re-used, this is called when retrieved from pool.
        public void Init(MyCubeGrid grid)
        {
            try
            {
                if(grid == null)
                    throw new Exception("given grid was null!");

                Grid = grid;
                RealismAmount = 1.0f;
                ForcedRealismAmount = null;
                _lastCheckedOwner = -1;

                // NOTE: not all blocks are fatblocks, but the kind of blocks we need are always fatblocks.
                foreach(var block in Grid.GetFatBlocks())
                {
                    BlockAdded(block);
                }

                Grid.OnFatBlockAdded += BlockAdded;
                Grid.OnFatBlockRemoved += BlockRemoved;

                Update();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        // ...and this is called when returned to pool.
        public void Reset()
        {
            try
            {
                if(Grid != null)
                {
                    Grid.OnFatBlockAdded -= BlockAdded;
                    Grid.OnFatBlockRemoved -= BlockRemoved;
                    Grid = null;
                }

                Thrusters.Clear();
                ShipControllers.Clear();
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void BlockAdded(MyCubeBlock block)
        {
            try
            {
                if(block is MyThrust)
                {
                    var logic = block.GameLogic?.GetAs<Thruster>();
                    if(logic != null && Thrusters.Add(logic))
                    {
                        logic.SetRealismAmount(RealismAmount);
                    }
                    return;
                }

                var shipCtrl = block as IMyShipController;
                if(shipCtrl != null && ShipControllers.Add(shipCtrl))
                {
                    shipCtrl.CustomDataChanged += ShipCtrl_CustomDataChanged;
                    shipCtrl.OwnershipChanged += ShipCtrl_OwnershipChanged;
                    shipCtrl.OnMarkForClose += ShipCtrl_MarkedForClose;
                    RefreshShipCtrlCustomData(addedBlock: true);
                    return;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void BlockRemoved(MyCubeBlock block)
        {
            try
            {
                if(block is MyThrust)
                {
                    var logic = block.GameLogic?.GetAs<Thruster>();
                    if(logic != null)
                        Thrusters.Remove(logic);
                    return;
                }

                var shipCtrl = block as IMyShipController;
                if(shipCtrl != null)
                {
                    if(ShipControllers.Remove(shipCtrl))
                    {
                        shipCtrl.CustomDataChanged -= ShipCtrl_CustomDataChanged;
                        shipCtrl.OwnershipChanged -= ShipCtrl_OwnershipChanged;
                        shipCtrl.OnMarkForClose -= ShipCtrl_MarkedForClose;
                        RefreshShipCtrlCustomData();
                    }
                    return;
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public void Update()
        {
            try
            {
                if(Grid == null || Grid.IsPreview || Grid.Physics == null || !Grid.Physics.Enabled || Grid.Physics.IsStatic)
                    return;

                if(Thrusters.Count == 0 || Grid.EntityThrustComponent == null)
                    return; // no thrusters, skip.

                float prevRealism = RealismAmount;

                if(ForcedRealismAmount != null)
                    RealismAmount = ForcedRealismAmount.Value;
                else if(IsPlayerControlled())
                    RealismAmount = 1.0f;
                else
                    RealismAmount = IsNPCOwned() ? 0.0f : 1.0f;

                // mode changed, apply it
                if(prevRealism != RealismAmount)
                {
                    foreach(var thruster in Thrusters)
                    {
                        thruster.SetRealismAmount(RealismAmount);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        // Note: this event doesn't seem to fire anymore, nor is it listed in the Mod API docs
        void ShipCtrl_CustomDataChanged(IMyTerminalBlock _unused)
        {
            RefreshShipCtrlCustomData();
        }

        void ShipCtrl_OwnershipChanged(IMyTerminalBlock _unused)
        {
            RefreshShipCtrlCustomData();
        }

        void RefreshShipCtrlCustomData(bool addedBlock = false)
        {
            // some other block is already forcing realistic mode grid-wide, don't recompute for newly added blocks.
            if(addedBlock && ForcedRealismAmount != null)
                return;

            ForcedRealismAmount = null;

            foreach(var shipCtrl in ShipControllers)
            {
                if(Grid.BigOwners != null && Grid.BigOwners.Count > 0)
                {
                    long shipOwner = Grid.BigOwners[0]; // only check the first one, too edge case to check others 

                    // avoid exploits where players can add a cockpit with this tag onto NPC ships to make them unable to fly properly
                    if(shipOwner != shipCtrl.OwnerId)
                        continue;
                }

                string customData = shipCtrl.CustomData; // cache because it allocates string on every call

                if(!string.IsNullOrEmpty(customData)) {
                    if (customData.IndexOf(RealisticThrustersMod.CUSTOMDATA_FORCE_TAG, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        ForcedRealismAmount = 1.0f;
                        return;
                    }
                    int customDataPercentIndex = customData.IndexOf("realistic-thrust-", StringComparison.OrdinalIgnoreCase);
                    if (customDataPercentIndex != -1)
                    {
                        int customDataPercentEndIndex = customData.IndexOf("%", customDataPercentIndex, StringComparison.OrdinalIgnoreCase);
                        if (customDataPercentEndIndex != -1)
                        {
                            string percent = customData.Remove(customDataPercentEndIndex).Substring(customDataPercentIndex + ("realistic-thrust-").Length);
                            ForcedRealismAmount = float.Parse(percent) / 100;
                            return;
                        }
                    }
                    if (customData.IndexOf(RealisticThrustersMod.CUSTOMDATA_DISABLE_TAG, StringComparison.OrdinalIgnoreCase) != -1)
                    {
                        ForcedRealismAmount = 0.0f;
                        return;
                    }
                }
            }
        }

        // extra safeguard for those edge cases xD
        void ShipCtrl_MarkedForClose(IMyEntity ent)
        {
            var block = ent as MyCubeBlock;
            if(block == null)
                Log.Error($"ShipCtrl_OnMarkForClose - entity is not MyCubeBlock??? ent={ent}, id={ent.EntityId.ToString()}, type={ent.GetType()}");
            else
                BlockRemoved(block);
        }

        bool IsPlayerControlled()
        {
            var players = RealisticThrustersMod.Instance.Players; // list is updated in session comp

            foreach(var player in players)
            {
                if(player.IsBot || player.Character == null)
                    continue;

                // NOTE: controlling cockpits only means you're in them, it doesn't mean you have flight control over the grid.
                // CubeGrid.GridSystems.ControlSystem.GetShipController() is the controller that actually flies the ship, but inaccessible.
                var controlled = player.Controller?.ControlledEntity;
                if(controlled == null)
                    continue;

                var shipCtrl = controlled as MyShipController;

                // HACK (SE v1.194) MyShipController.NeedsPerFrameUpdate is true when that controller has flight controls over the ship.
                // but it's not true for the cockpit you're sitting in while controlling a turret and flying the ship.

                // assuming they're controlling the ship
                if(shipCtrl != null && shipCtrl.EnableShipControl && shipCtrl.CubeGrid == Grid && shipCtrl.NeedsPerFrameUpdate)
                    return true;

                var turret = controlled as IMyLargeTurretBase;
                if(turret != null && turret.CubeGrid == Grid)
                {
                    // if controlling a turret, find the seat they're in
                    var cockpit = player.Character.Parent as MyCockpit;
                    if(cockpit != null && cockpit.EnableShipControl && cockpit.CubeGrid == Grid)
                        return true;
                }
            }

            return false;
        }

        bool IsNPCOwned()
        {
            if(Grid.BigOwners == null || Grid.BigOwners.Count == 0)
            {
                _lastCheckedOwner = -1;
                _isNPCOwned = false;
                return false;
            }

            long owner = Grid.BigOwners[0]; // only check the first one, too edge case to check others 

            if(_lastCheckedOwner == owner)
                return _isNPCOwned;

            _lastCheckedOwner = owner;

            var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);

            if(faction == null)
            {
                _isNPCOwned = false;
            }
            else if(!string.IsNullOrEmpty(faction.PrivateInfo) && faction.PrivateInfo.IndexOf(RealisticThrustersMod.CUSTOMDATA_FORCE_TAG, StringComparison.OrdinalIgnoreCase) != -1)
            {
                _isNPCOwned = false;
            }
            else
            {
                _isNPCOwned = faction.IsEveryoneNpc();
            }

            return _isNPCOwned;
        }

        public void FactionEdited()
        {
            _lastCheckedOwner = -1;
        }
    }
}