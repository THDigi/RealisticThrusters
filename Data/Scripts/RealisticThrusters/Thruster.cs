﻿using System;
using Sandbox.Common.ObjectBuilders;
using Sandbox.Game.Entities;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.ObjectBuilders;
using VRageMath;

namespace Digi.RealisticThrusters
{
    [MyEntityComponentDescriptor(typeof(MyObjectBuilder_Thrust), false)]
    public class Thruster : MyGameLogicComponent
    {
        public MyThrust Block;
        bool RealisticMode = true;

        public override void Init(MyObjectBuilder_EntityBase objectBuilder)
        {
            Block = (MyThrust)Entity;
            Block.IsWorkingChanged += WorkingChanged;
        }

        public override void Close()
        {
            Block.IsWorkingChanged -= WorkingChanged;
        }

        void WorkingChanged(MyCubeBlock obj)
        {
            SetRealisticMode(RealisticMode);
        }

        public void SetRealisticMode(bool realisticMode)
        {
            RealisticMode = realisticMode;

            if(RealisticMode && Block.IsWorking)
                NeedsUpdate |= MyEntityUpdateEnum.EACH_FRAME;
            else
                NeedsUpdate &= ~MyEntityUpdateEnum.EACH_FRAME;
        }

        public override void UpdateBeforeSimulation()
        {
            try
            {
                if(!RealisticMode || !Block.IsWorking)
                    return;

                var grid = Block.CubeGrid;
                if(grid.IsPreview || grid.Physics == null || !grid.Physics.Enabled || grid.Physics.IsStatic)
                    return;

                float strength = Block.BlockDefinition.ForceMagnitude * Block.CurrentStrength;

                if(Math.Abs(strength) < 0.00001f)
                    return;

                Vector3D force = Block.WorldMatrix.Backward * strength;

                var groupProperties = MyGridPhysicalGroupData.GetGroupSharedProperties(grid);

                // cancel the thruster's force at grid-group center of mass
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, -force, groupProperties.CoMWorld, null);

                // apply the thruster's force at its position
                grid.Physics.AddForce(MyPhysicsForceType.APPLY_WORLD_FORCE, force, Block.PositionComp.WorldVolume.Center, null);
            }
            catch(Exception e)
            {
                Log.Error(e);
            }
        }
    }
}