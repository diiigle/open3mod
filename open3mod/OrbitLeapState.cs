using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace open3mod
{
    class OrbitLeapState : LeapState
    {
        public OrbitLeapState()
        {
            base.StateIndex = LeapStates.OrbitLeapState;
            base.Translation = CoordinateValues.Delta;
            base.Rotation = CoordinateValues.Absolute;
            base._angleorder = new LeapListener.DataTypes[3] 
            { 
                LeapListener.DataTypes.Roll, 
                LeapListener.DataTypes.Yaw, 
                LeapListener.DataTypes.Pitch
            };
        }
    }
}
