using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.IO;
using Sandbox.Common;
using Sandbox.Common.Components;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Common.ObjectBuilders.VRageData;
using Sandbox.Common.ObjectBuilders.Definitions;
using Sandbox.Definitions;
using Sandbox.Engine;
using Sandbox.Engine.Physics;
using Sandbox.Engine.Multiplayer;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.Game.EntityComponents;
using Sandbox.Game.Gui;
using Sandbox.ModAPI;
using Sandbox.ModAPI.Interfaces;
using VRageMath;
using VRage;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRage.Game.Components;
using VRage.Utils;
using Digi.Utils;
using Ingame = Sandbox.ModAPI.Ingame;

namespace Digi.RealisticThrusters
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    public class RealisticThrusters : MySessionComponentBase
    {
        public bool init { get; private set; }
        
        public void Init()
        {
            Log.Info("Initialized");
            init = true;
        }
        
        protected override void UnloadData()
        {
            init = false;
            
            Log.Info("Mod unloaded");
            Log.Close();
        }
        
        public override void UpdateAfterSimulation()
        {
            if(!init)
            {
                if(MyAPIGateway.Session == null)
                    return;
                
                Init();
            }
        }
    }
    
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust))]
    public class Thruster : MyGameLogicComponent
    {
        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Entity.NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
        }
        
        public override void UpdateAfterSimulation()
        {
            try
            {
                var thruster = Entity as MyThrust;
                
                if(!thruster.IsWorking)
                    return;
                
                var grid = thruster.CubeGrid;
                
                if(grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                    return;
                
                var force = thruster.WorldMatrix.Forward * thruster.BlockDefinition.ForceMagnitude * thruster.CurrentStrength;
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, grid.Physics.CenterOfMassWorld, null); // cancel the thruster's force at center of mass
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, thruster.WorldMatrix.Translation, null); // apply the thruster's force at its position
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
        
        public override void Close()
        {
        }
        
        public override MyObjectBuilder_EntityBase GetObjectBuilder(bool copy = false)
        {
            return Entity.GetObjectBuilder(copy);
        }
    }
}