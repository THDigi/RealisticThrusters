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

        private bool RealisticThrusters;
        private readonly List<Thruster> Thrusters = new List<Thruster>();

        private bool ForcedRealistic;
        private readonly List<IMyShipController> ShipControllers = new List<IMyShipController>();

        // NOTE: object is re-used, this is called when retrieved from pool.
        public void Init(MyCubeGrid grid)
        {
            try
            {
                Grid = grid;
                RealisticThrusters = true;
                _lastCheckedOwner = -1;
                ForcedRealistic = false;

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
                    var logic = block?.GameLogic?.GetAs<Thruster>();
                    if(logic != null)
                    {
                        Thrusters.Add(logic);
                        logic.SetRealisticMode(RealisticThrusters);
                    }
                    return;
                }

                var shipCtrl = block as IMyShipController;
                if(shipCtrl != null)
                {
                    shipCtrl.CustomDataChanged += ShipCtrl_CustomDataChanged;
                    shipCtrl.OwnershipChanged += ShipCtrl_OwnershipChanged;
                    shipCtrl.OnMarkForClose += ShipCtrl_MarkedForClose;
                    ShipControllers.Add(shipCtrl);
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
                    for(int i = (Thrusters.Count - 1); i >= 0; --i)
                    {
                        if(Thrusters[i].Block == block)
                        {
                            Thrusters.RemoveAtFast(i);
                            break;
                        }
                    }
                    return;
                }

                var shipCtrl = block as IMyShipController;
                if(shipCtrl != null)
                {
                    for(int i = (ShipControllers.Count - 1); i >= 0; --i)
                    {
                        if(ShipControllers[i] == shipCtrl)
                        {
                            shipCtrl.CustomDataChanged -= ShipCtrl_CustomDataChanged;
                            shipCtrl.OwnershipChanged -= ShipCtrl_OwnershipChanged;
                            shipCtrl.OnMarkForClose -= ShipCtrl_MarkedForClose;
                            ShipControllers.RemoveAtFast(i);
                            RefreshShipCtrlCustomData();
                            break;
                        }
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

                bool prevRealistic = RealisticThrusters;

                if(ForcedRealistic || IsPlayerControlled())
                    RealisticThrusters = true;
                else
                    RealisticThrusters = !IsNPCOwned();

                // mode changed, apply it
                if(prevRealistic != RealisticThrusters)
                {
                    for(int i = (Thrusters.Count - 1); i >= 0; --i)
                    {
                        Thrusters[i].SetRealisticMode(RealisticThrusters);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

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
            if(addedBlock && ForcedRealistic)
                return;

            ForcedRealistic = false;

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

                if(!string.IsNullOrEmpty(customData) && customData.IndexOf(RealisticThrustersMod.CUSTOMDATA_FORCE_TAG, StringComparison.OrdinalIgnoreCase) != -1)
                {
                    ForcedRealistic = true;
                    return;
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