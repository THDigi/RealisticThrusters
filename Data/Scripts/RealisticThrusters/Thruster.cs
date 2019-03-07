using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Utils;

namespace Digi.RealisticThrusters
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), useEntityUpdate: false)]
    public class Thruster : MyGameLogicComponent
    {
        private MyThrust thruster;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            thruster = (MyThrust)Entity;
            NeedsUpdate = MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                var grid = thruster.CubeGrid;

                if(grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic || !thruster.IsWorking)
                    return;

                var thrustMatrix = thruster.WorldMatrix;
                var force = thrustMatrix.Backward * thruster.BlockDefinition.ForceMagnitude * thruster.CurrentStrength;
                var groupProperties = MyGridPhysicalGroupData.GetGroupSharedProperties(grid);

                // cancel the thruster's force at grid-group center of mass
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, groupProperties.CoMWorld, null);

                // apply the thruster's force at its position
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, thrustMatrix.Translation, null);
            }
            catch(Exception e)
            {
                MyLog.Default.WriteLineAndConsole($"{e.Message}\n{e.StackTrace}");

                if(MyAPIGateway.Session?.Player != null)
                    MyAPIGateway.Utilities.ShowNotification($"[ ERROR: {GetType().FullName}: {e.Message} | Send SpaceEngineers.Log to mod author ]", 10000, MyFontEnum.Red);
            }
        }
    }
}