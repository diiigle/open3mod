﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Diagnostics;

using Leap;

namespace open3mod
{
    public class LeapListener : Listener
    {
        private Object thisLock = new Object();

        /// <summary>
        /// Identification of the parameters that should be smoothed out
        /// </summary>
        public enum DataTypes
        {
            X = 0,
            Y = 1,
            Z = 2,
            Pitch = 3,
            Roll = 4,
            Yaw = 5,

            _Max = 6,
        };

        /// <summary>
        /// Identification of the current tracking mode
        ///  - Idle: no Hands visible
        ///  - Locking: Hands visible, but not tracked
        ///  - Tracking: normal tracking of hand data
        /// </summary>
        public enum TrackingMode
        {
            Idle = 0,
            Locking = 1,
            Tracking = 2,
        };

        /// <summary>
        /// Stores the Tracking Mode
        /// </summary>
        private TrackingMode _trackingMode = TrackingMode.Idle;

        /// <summary>
        /// Populates the Tracking Mode
        /// </summary>
        public TrackingMode TrackingStatus{ get{ return _trackingMode;} }

        /// <summary>
        /// List holding the data to smooth the values,
        /// one list for each parameter in DataTypes (ENUM)
        /// </summary>
        private List<float>[] _valueHistory = new List<float>[(int) DataTypes._Max];

        /// <summary>
        /// reference to the MainWindow
        /// </summary>
        private readonly MainWindow _mainWindow;

        /// <summary>
        /// hold the ids of the hands that we are tracking
        /// </summary>
        private List<int> _trackingHands = new List<int>();

        /// <summary>
        /// timestamp in ms since the LeapMotion Controller connected,
        /// when a hand was last time seen
        /// </summary>
        private long _lastHandSeen;

        /// <summary>
        /// Timeout to wait, before the tracked data is actually used.
        /// Depends on time, since the tracking mode was idle.
        /// </summary>
        private float _lockTimeout;

        /// <summary>
        /// Interface to the 
        /// index of the current active LeapState
        /// </summary>
        public LeapStates CurrentState
        {
            get
            {
                if (_currentState == null)
                {
                    _currentState = _states[0];
                }
                return _currentState.StateIndex;
            }
            set
            {
                //change the current state
                if (_currentState == null || _currentState.StateIndex != value)
                {
                    _currentState = _states[(int)value];
                }
            }
        }

        /// <summary>
        /// Current active LeapState of the Controller
        /// </summary>
        private LeapState _currentState;

        /// <summary>
        /// data holding list for the different LeapStates
        /// The order must match LeapStates (ENUM)
        /// </summary>
        private LeapState[] _states = new LeapState[] { new OrbitLeapState(), new FpsLeapState() };

        /// <summary>
        /// cache for the active camera mode
        /// </summary>
        private CameraMode _camMode;

        public LeapListener(MainWindow mainwindow)
        {
            _mainWindow = mainwindow;
        }

        private void SafeWriteLine(String line)
        {
            lock (thisLock)
            {
                Console.WriteLine(line);
            }
        }

        public override void OnInit(Controller controller)
        {
            SafeWriteLine("Initialized");
        }

        public override void OnConnect(Controller controller)
        {
            SafeWriteLine("Connected");
            //controller.EnableGesture(Gesture.GestureType.TYPECIRCLE);
            //controller.EnableGesture(Gesture.GestureType.TYPEKEYTAP);
            //controller.EnableGesture(Gesture.GestureType.TYPESCREENTAP);
            //controller.EnableGesture(Gesture.GestureType.TYPESWIPE);
        }

        public override void OnDisconnect(Controller controller)
        {
            //Note: not dispatched when running in a debugger.
            SafeWriteLine("Disconnected");
        }

        public override void OnExit(Controller controller)
        {
            SafeWriteLine("Exited");
        }

        public override void OnFrame(Controller controller)
        {
            MaintainState();

            // Get the most recent frame and report some basic information
            Frame frame = controller.Frame();

            //SafeWriteLine("Frame id: " + frame.Id
            //            + ", timestamp: " + frame.Timestamp
            //            + ", hands: " + frame.Hands.Count
            //            + ", fingers: " + frame.Fingers.Count
            //            + ", tools: " + frame.Tools.Count
            //            + ", gestures: " + frame.Gestures().Count);

            if (!frame.Hands.IsEmpty)
            {
                if (_trackingHands.Count == 0)
                {
                    Debug.Assert(_trackingMode == TrackingMode.Idle);

                    // Track the first hand
                    _trackingHands.Add(frame.Hands[0].Id);
                    _trackingMode = TrackingMode.Locking;
                    CalcLockTimeout(frame.Timestamp);
                }

                // Get the first hand
                int firstHandId = _trackingHands.First();
                Hand hand = frame.Hands.FirstOrDefault(tmpHand => tmpHand.Id == firstHandId);

                if (hand == null)
                {
                    ResetTrackingMode();
                    return;
                }

                if (_trackingMode == TrackingMode.Locking && hand.TimeVisible >= _lockTimeout)
                {
                    _trackingMode = TrackingMode.Tracking;
                }

                Frame oldFrame = controller.Frame(5);
                float x, y, z, pitch, yaw, roll;
                x = y = z = pitch = yaw = roll = 0.0f;

                //***************
                //  Translation
                //***************
                switch (_currentState.Translation)
                {
                    case CoordinateValues.Absolute:

                        Vector translation = hand.PalmPosition;

                        x = translation.x;
                        y = translation.y;
                        z = translation.z;
                        break;

                    case CoordinateValues.Delta:

                        Vector translationDelta = hand.Translation(oldFrame);

                        x = translationDelta.x;
                        y = translationDelta.y;
                        z = translationDelta.z;
                        break;
                }

                //if (hand.Fingers.Count == 5)
                GetSmoothedValue(DataTypes.X, x);
                GetSmoothedValue(DataTypes.Y, y);
                GetSmoothedValue(DataTypes.Z, z);
                if (hand.TranslationProbability(oldFrame) >= 0.8)
                {
                    if (CoreSettings.CoreSettings.Default.Leap_TranslationSmoothing)
                    {
                        x = GetSmoothedValue(DataTypes.X);
                        y = GetSmoothedValue(DataTypes.Y);
                        z = GetSmoothedValue(DataTypes.Z);
                    }
                }

                //************
                //  Rotation
                //************
                switch (_currentState.Rotation)
                {
                    case CoordinateValues.Absolute:

                        // Get the hand's normal vector and direction
                        Vector normal = hand.PalmNormal;
                        Vector direction = hand.Direction;

                        pitch = direction.Pitch;
                        roll = normal.Roll;
                        yaw = direction.Yaw;
                        break;

                    case CoordinateValues.Delta:

                        pitch = hand.RotationAngle(oldFrame, Vector.XAxis);
                        yaw = hand.RotationAngle(oldFrame, Vector.YAxis);
                        roll = hand.RotationAngle(oldFrame, Vector.ZAxis);
                        break;
                }

                GetSmoothedValue(DataTypes.Pitch, pitch);
                GetSmoothedValue(DataTypes.Roll, roll);
                GetSmoothedValue(DataTypes.Yaw, yaw);
                if (CoreSettings.CoreSettings.Default.Leap_RotationSmoothing)
                {
                    pitch = GetSmoothedValue(DataTypes.Pitch);
                    roll = GetSmoothedValue(DataTypes.Roll);
                    yaw = GetSmoothedValue(DataTypes.Yaw);
                }

                if (_trackingMode == TrackingMode.Tracking)
                {
                    var cam = _mainWindow.UiState.ActiveTab.ActiveCameraController;
                    if (cam != null)
                    {
                        cam.LeapInput(x, y, z, pitch, roll, yaw);
                    }
                    _lastHandSeen = frame.Timestamp;
                }
                //test of fist recognition
                //SafeWriteLine(hand.SphereRadius.ToString());

                //// Check if the hand has any fingers
                //FingerList fingers = hand.Fingers;
                //if (!fingers.Empty)
                //{
                //    // Calculate the hand's average finger tip position
                //    Vector avgPos = Vector.Zero;
                //    foreach (Finger finger in fingers)
                //    {
                //        avgPos += finger.TipPosition;
                //    }
                //    avgPos /= fingers.Count;
                //    SafeWriteLine("Hand has " + fingers.Count
                //                + " fingers, average finger tip position: " + avgPos);
                //}

                //// Get the hand's sphere radius and palm position
                //SafeWriteLine("Hand sphere radius: " + hand.SphereRadius.ToString("n2")
                //            + " mm, palm position: " + hand.PalmPosition);

                //// Get the hand's normal vector and direction
                //Vector normal = hand.PalmNormal;
                //Vector direction = hand.Direction;

                //// Calculate the hand's pitch, roll, and yaw angles
                //SafeWriteLine("Hand pitch: " + direction.Pitch * 180.0f / (float)Math.PI + " degrees, "
                //            + "roll: " + normal.Roll * 180.0f / (float)Math.PI + " degrees, "
                //            + "yaw: " + direction.Yaw * 180.0f / (float)Math.PI + " degrees");
            }
            else if(_trackingHands.Count != 0)
            {
                ResetTrackingMode();
            }

            //// Get gestures
            //GestureList gestures = frame.Gestures();
            //for (int i = 0; i < gestures.Count; i++)
            //{
            //    Gesture gesture = gestures[i];

            //    switch (gesture.Type)
            //    {
            //        case Gesture.GestureType.TYPECIRCLE:
            //            CircleGesture circle = new CircleGesture(gesture);

            //            // Calculate clock direction using the angle between circle normal and pointable
            //            String clockwiseness;
            //            if (circle.Pointable.Direction.AngleTo(circle.Normal) <= Math.PI / 4)
            //            {
            //                //Clockwise if angle is less than 90 degrees
            //                clockwiseness = "clockwise";
            //            }
            //            else
            //            {
            //                clockwiseness = "counterclockwise";
            //            }

            //            float sweptAngle = 0;

            //            // Calculate angle swept since last frame
            //            if (circle.State != Gesture.GestureState.STATESTART)
            //            {
            //                CircleGesture previousUpdate = new CircleGesture(controller.Frame(1).Gesture(circle.Id));
            //                sweptAngle = (circle.Progress - previousUpdate.Progress) * 360;
            //            }

            //            SafeWriteLine("Circle id: " + circle.Id
            //                           + ", " + circle.State
            //                           + ", progress: " + circle.Progress
            //                           + ", radius: " + circle.Radius
            //                           + ", angle: " + sweptAngle
            //                           + ", " + clockwiseness);
            //            break;
            //        case Gesture.GestureType.TYPESWIPE:
            //            SwipeGesture swipe = new SwipeGesture(gesture);
            //            SafeWriteLine("Swipe id: " + swipe.Id
            //                           + ", " + swipe.State
            //                           + ", position: " + swipe.Position
            //                           + ", direction: " + swipe.Direction
            //                           + ", speed: " + swipe.Speed);
            //            break;
            //        case Gesture.GestureType.TYPEKEYTAP:
            //            KeyTapGesture keytap = new KeyTapGesture(gesture);
            //            SafeWriteLine("Tap id: " + keytap.Id
            //                           + ", " + keytap.State
            //                           + ", position: " + keytap.Position
            //                           + ", direction: " + keytap.Direction);
            //            break;
            //        case Gesture.GestureType.TYPESCREENTAP:
            //            ScreenTapGesture screentap = new ScreenTapGesture(gesture);
            //            SafeWriteLine("Tap id: " + screentap.Id
            //                           + ", " + screentap.State
            //                           + ", position: " + screentap.Position
            //                           + ", direction: " + screentap.Direction);
            //            break;
            //        default:
            //            SafeWriteLine("Unknown gesture type.");
            //            break;
            //    }
            //}

            //if (!frame.Hands.IsEmpty || !frame.Gestures().IsEmpty)
            //{
            //    SafeWriteLine("");
            //}
        }

        /// <summary>
        /// Smoothes data over multiple frames ( see Leap_SmoothingWindowSize )
        /// The data is identified with the DataTypes indices
        /// </summary>
        /// <param name="id">Identifier for the data</param>
        /// <param name="newvalue">some new data that should be added to the calculation</param>
        /// <returns>the average value</returns>
        private float GetSmoothedValue(DataTypes id, float newvalue)
        {
            if (CoreSettings.CoreSettings.Default.Leap_SmoothingWindowSize == 0)
            {
                if (_valueHistory[(int)id] != null)
                {
                    _valueHistory[(int)id].Clear();
                    _valueHistory[(int)id].Add(newvalue);
                }
                return newvalue;
            }
            List<float> list;
            if (_valueHistory[(int)id] == null)
            {
                //initialize it
                list = _valueHistory[(int)id] = new List<float>();
            }
            else
            {
                list = _valueHistory[(int)id];
            }

            while(list.Count >= CoreSettings.CoreSettings.Default.Leap_SmoothingWindowSize)
            {
                list.RemoveAt(0);
            }
            list.Add(newvalue);
            float average = list.Average();
            return average;
        }

        /// <summary>
        /// Get the smoothed value for a set of data
        /// </summary>
        /// <param name="id">Identifier for the data</param>
        /// <returns>the average value</returns>
        private float GetSmoothedValue(DataTypes id)
        {
            float average = 0.0f;
            if (_valueHistory[(int)id] != null)
            {
                average = _valueHistory[(int)id].Average();
            }
            return average;
        }

        /// <summary>
        /// Calculate the Timeout for locking on a Hand and tracking it.
        /// </summary>
        /// <param name="RecentTimestamp">the time right now</param>
        public void CalcLockTimeout(long RecentTimestamp)
        {
            //timedelta since we last saw a hand in seconds
            long timedelta = (RecentTimestamp - _lastHandSeen) / (1000 * 1000);
            if (timedelta >= 3.0f)
            {
                _lockTimeout = 1.0f;
            }
            else
            {
                _lockTimeout = 1/4;
            }
        }

        /// <summary>
        /// Reset to default tracking mode
        /// </summary>
        private void ResetTrackingMode()
        {
            _trackingHands.Clear();
            _trackingMode = TrackingMode.Idle;
        }

        /// <summary>
        /// keep the CurrentState in sync with the active camera
        /// </summary>
        private void MaintainState()
        {
            var cam = _mainWindow.UiState.ActiveTab.ActiveCameraController;
            if (cam != null & _camMode != cam.GetCameraMode())
            {
                var mode = cam.GetCameraMode();
                _camMode = mode;
                switch (mode)
                {
                    case CameraMode.Fps:
                        CurrentState = LeapStates.FpsLeapState;
                        break;

                    case CameraMode.Orbit:
                        CurrentState = LeapStates.OrbitLeapState;
                        break;

                    default:
                        break;
                }
            }
            //reset all smoothed values
            foreach (DataTypes datatype in Enum.GetValues(typeof(DataTypes)))
            {
                _valueHistory[(int)datatype].Clear();
            }
        }

    }
}
