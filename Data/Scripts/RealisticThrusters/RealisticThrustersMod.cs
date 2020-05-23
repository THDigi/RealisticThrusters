using System;
using System.Collections.Generic;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Collections;
using VRage.Game.Components;
using VRage.Game.ModAPI;
using VRage.ModAPI;

namespace Digi.RealisticThrusters
{
    [MySessionComponentDescriptor(MyUpdateOrder.BeforeSimulation)]
    public class RealisticThrustersMod : MySessionComponentBase
    {
        public static RealisticThrustersMod Instance;

        public const int LogicUpdateInterval = 60 * 2;
        private int LogicUpdateIndex;
        private readonly List<GridLogic> GridLogic = new List<GridLogic>();
        private readonly List<int> RemoveLogicIndex = new List<int>();
        private readonly MyConcurrentPool<GridLogic> LogicPool = new MyConcurrentPool<GridLogic>();

        private const int PlayersUpdateInterval = LogicUpdateInterval;
        private int PlayersUpdateTick = 0;
        public readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        public override void LoadData()
        {
            Instance = this;
            Log.ModName = "Realistic Thrusters";
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
        }

        protected override void UnloadData()
        {
            Instance = null;
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
        }

        void EntityAdded(IMyEntity ent)
        {
            try
            {
                var grid = ent as MyCubeGrid;
                if(grid != null)
                {
                    var logic = LogicPool.Get();
                    logic.Init(grid);
                    GridLogic.Add(logic);
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if(GridLogic.Count == 0)
                    return;

                if(++PlayersUpdateTick > PlayersUpdateInterval)
                {
                    PlayersUpdateTick = 0;
                    Players.Clear();
                    MyAPIGateway.Players.GetPlayers(Players);
                }

                // logic from MyDistributedUpdater
                LogicUpdateIndex = (LogicUpdateIndex + 1) % LogicUpdateInterval;

                for(int i = LogicUpdateIndex; i < GridLogic.Count; i += LogicUpdateInterval)
                {
                    var logic = GridLogic[i];

                    if(logic.Grid.MarkedForClose)
                    {
                        RemoveLogicIndex.Add(i);
                        continue;
                    }

                    logic.Update();
                }

                if(RemoveLogicIndex.Count > 0)
                {
                    foreach(int index in RemoveLogicIndex)
                    {
                        var logic = GridLogic[index];
                        logic.Reset();
                        LogicPool.Return(logic);

                        GridLogic.RemoveAtFast(index);
                    }

                    RemoveLogicIndex.Clear();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
