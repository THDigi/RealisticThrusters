using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;

namespace Digi.RealisticThrusters
{
    public class GridLogic
    {
        public MyCubeGrid Grid;

        private bool _isNPCOwned;
        private long _lastCheckedOwner;

        private bool RealisticThrusters;
        private readonly List<Thruster> Thrusters = new List<Thruster>();

        private readonly List<MyRemoteControl> RemoteControls = new List<MyRemoteControl>();

        // NOTE: object is re-used, this is called when retrieved from pool.
        public void Init(MyCubeGrid grid)
        {
            try
            {
                Grid = grid;
                RealisticThrusters = true;
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
                RemoteControls.Clear();
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

                var rc = block as MyRemoteControl;
                if(rc != null)
                {
                    RemoteControls.Add(rc);
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

                if(block is MyRemoteControl)
                {
                    for(int i = (RemoteControls.Count - 1); i >= 0; --i)
                    {
                        if(RemoteControls[i] == block)
                        {
                            RemoteControls.RemoveAtFast(i);
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

                if(HasDroneAI())
                {
                    RealisticThrusters = false;
                }
                else
                {
                    if(IsPlayerControlled())
                    {
                        RealisticThrusters = true;
                    }
                    else
                    {
                        RealisticThrusters = !IsNPCOwned();
                    }
                }

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

        bool HasDroneAI()
        {
            if(RemoteControls.Count == 0)
                return false;

            foreach(var rc in RemoteControls)
            {
                if(rc.AutomaticBehaviour != null)
                    return true;
            }

            return false;
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

            var owner = Grid.BigOwners[0]; // only check the first one, too edge case to check others 

            if(_lastCheckedOwner == owner)
                return _isNPCOwned;

            _lastCheckedOwner = owner;

            var faction = MyAPIGateway.Session.Factions.TryGetPlayerFaction(owner);
            _isNPCOwned = (faction != null && faction.IsEveryoneNpc());
            return _isNPCOwned;
        }
    }
}