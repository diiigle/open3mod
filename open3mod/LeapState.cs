using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;

namespace open3mod
{
    /// <summary>
    /// define two types of coordinates that can be retrieved from the LeapMotion Controller
    /// </summary>
    public enum CoordinateValues
    {
        Delta = 0,
        Absolute = 1
    }

    /// <summary>
    /// Enumeration of the diffrent LeapStates.
    /// used to identify each LeapState
    /// </summary>
    public enum LeapStates
    {
        OrbitLeapState = 0,
        FpsLeapState = 1,        
    }

    /// <summary>
    /// State abstraction for the LeapListener machinery
    /// 
    /// define set of configs that different states can define
    /// </summary>
    class LeapState
    {
        /// <summary>
        /// Identification of the state
        /// </summary>
        public LeapStates StateIndex
        {
            get;
            protected set;
        }

        /// <summary>
        /// which type of Translation values the state requires
        /// </summary>
        public CoordinateValues Translation
        {
            get;
            protected set;
        }

        /// <summary>
        /// which type of Rotation values the state requires
        /// </summary>
        public CoordinateValues Rotation
        {
            get;
            protected set;
        }

        /// <summary>
        /// Define in which order the rotation angles should be applied
        /// 
        /// This includes always all 3 rotation angles (from LeapListener.DataTypes)
        /// </summary>
        public LeapListener.DataTypes[] Angleorder
        {
            get
            {
                Debug.Assert(
                            _angleorder.Length == 3
                            & _angleorder.All(
                                (elem) =>
                                    elem > LeapListener.DataTypes.Z 
                                    & elem < LeapListener.DataTypes._Max
                                )
                            );
                return _angleorder;
            }
        }
        /// <summary>
        /// Define in which order the rotation angles should be applied
        /// 
        /// This includes always all 3 rotation angles (from LeapListener.DataTypes)
        /// </summary>
        protected LeapListener.DataTypes[] _angleorder = new LeapListener.DataTypes[3]
        {
            LeapListener.DataTypes.Pitch,
            LeapListener.DataTypes.Yaw,
            LeapListener.DataTypes.Roll
        };
    }
}
