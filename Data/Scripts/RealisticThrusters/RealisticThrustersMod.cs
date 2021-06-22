using System;
using System.Collections.Generic;
using Sandbox.Game;
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
        public const string CUSTOMDATA_FORCE_TAG = "force-realistic-thrust";

        public static RealisticThrustersMod Instance;

        public const int LogicUpdateInterval = 60 * 2;
        private int LogicUpdateIndex;
        private readonly List<GridLogic> GridLogic = new List<GridLogic>();
        private readonly Dictionary<long, GridLogic> GridLogicLookup = new Dictionary<long, GridLogic>();
        private readonly List<int> RemoveLogicIndex = new List<int>();
        private readonly MyConcurrentPool<GridLogic> LogicPool = new MyConcurrentPool<GridLogic>();

        private const int PlayersUpdateInterval = 60 * 5;
        private int PlayersUpdateTick = 0;
        public readonly List<IMyPlayer> Players = new List<IMyPlayer>();

        public override void LoadData()
        {
            Instance = this;
            Log.ModName = "Realistic Thrusters";
            MyAPIGateway.Entities.OnEntityAdd += EntityAdded;
            MyVisualScriptLogicProvider.PlayerConnected += PlayersChanged;
            MyVisualScriptLogicProvider.PlayerDisconnected += PlayersChanged;
            MyAPIGateway.Session.Factions.FactionEdited += FactionEdited;
        }

        protected override void UnloadData()
        {
            Instance = null;
            MyAPIGateway.Entities.OnEntityAdd -= EntityAdded;
            MyVisualScriptLogicProvider.PlayerConnected -= PlayersChanged;
            MyVisualScriptLogicProvider.PlayerDisconnected -= PlayersChanged;
            MyAPIGateway.Session.Factions.FactionEdited -= FactionEdited;
        }

        void EntityAdded(IMyEntity ent)
        {
            try
            {
                var grid = ent as MyCubeGrid;
                if(grid != null && grid.CreatePhysics)
                {
                    var logic = GridLogicLookup.GetValueOrDefault(grid.EntityId, null);
                    if(logic != null)
                    {
                        logic.Reset();
                        logic.Init(grid);
                    }
                    else
                    {
                        logic = LogicPool.Get();
                        logic.Init(grid);

                        GridLogic.Add(logic);
                        GridLogicLookup.Add(grid.EntityId, logic);
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }

        void PlayersChanged(long playerId)
        {
            PlayersUpdateTick = PlayersUpdateInterval; // force an early players list update next tick
        }

        void FactionEdited(long factionId)
        {
            try
            {
                var faction = MyAPIGateway.Session.Factions.TryGetFactionById(factionId);

                if(faction == null)
                    return;

                if(!faction.IsEveryoneNpc())
                    return;

                foreach(var gridLogic in GridLogic)
                {
                    gridLogic.FactionEdited();
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
                int logicCount = GridLogic.Count;
                if(logicCount == 0)
                    return;

                if(++PlayersUpdateTick > PlayersUpdateInterval)
                {
                    PlayersUpdateTick = 0;
                    Players.Clear();
                    MyAPIGateway.Players.GetPlayers(Players);
                }

                // logic from MyDistributedUpdater
                LogicUpdateIndex = (LogicUpdateIndex + 1) % LogicUpdateInterval;

                for(int i = LogicUpdateIndex; i < logicCount; i += LogicUpdateInterval)
                {
                    GridLogic logic = GridLogic[i];
                    if(logic.Grid.MarkedForClose)
                    {
                        RemoveLogicIndex.Add(i);
                        continue;
                    }

                    logic.Update();
                }

                if(RemoveLogicIndex.Count > 0)
                {
                    try
                    {
                        // sort ascending + iterate in reverse is required to avoid shifting indexes as we're removing.
                        RemoveLogicIndex.Sort();

                        for(int i = (RemoveLogicIndex.Count - 1); i >= 0; i--)
                        {
                            int index = RemoveLogicIndex[i];
                            GridLogic logic = GridLogic[index];

                            GridLogic.RemoveAtFast(index);
                            GridLogicLookup.Remove(logic.Grid.EntityId);

                            logic.Reset();
                            LogicPool.Return(logic);
                        }
                    }
                    finally
                    {
                        RemoveLogicIndex.Clear();
                    }
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}
