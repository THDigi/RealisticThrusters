using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;

namespace Digi.RealisticThrusters
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class RealisticThrustersMod : MySessionComponentBase
    {
        private bool init = false;

        public override void LoadData()
        {
            Log.SetUp("Realistic Thrusters", 575893643, "RealisticThrusters");
        }

        public void Init()
        {
            Log.Init();
            init = true;

            // stop updating
            MyAPIGateway.Utilities.InvokeOnGameThread(() => SetUpdateOrder(MyUpdateOrder.NoUpdate));
        }

        protected override void UnloadData()
        {
            init = false;
            Log.Close();
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                if(!init)
                {
                    if(MyAPIGateway.Session == null)
                        return;

                    Init();
                }
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }

    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), useEntityUpdate: false)]
    public class Thruster : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateAfterSimulation()
        {
            try
            {
                var thruster = (MyThrust)Entity;
                var grid = thruster.CubeGrid;

                if(grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic || !thruster.IsWorking)
                    return;

                var thrustMatrix = thruster.WorldMatrix;
                var force = thrustMatrix.Backward * thruster.BlockDefinition.ForceMagnitude * thruster.CurrentStrength;
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, grid.Physics.CenterOfMassWorld, null); // cancel the thruster's force at center of mass
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, thrustMatrix.Translation, null); // apply the thruster's force at its position
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}