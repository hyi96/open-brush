﻿// Copyright 2020 The Tilt Brush Authors
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

using UnityEngine;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using MoonSharp.Interpreter;
using ControllerName = TiltBrush.InputManager.ControllerName;
using Random = UnityEngine.Random;

namespace TiltBrush
{

    //TODO: Separate basic pointer management (e.g. enumeration, global operations)
    //from higher-level symmetry code.
    public partial class PointerManager : MonoBehaviour
    {
        static public PointerManager m_Instance;
        const float STRAIGHTEDGE_PRESSURE = 1f;
        const int STRAIGHTEDGE_DRAWIN_FRAMES = 16;
        const int DEBUG_MULTIPLE_NUM_POINTERS = 3;
        const string PLAYER_PREFS_POINTER_ANGLE_OLD = "Pointer_Angle";
        const string PLAYER_PREFS_POINTER_ANGLE = "Pointer_Angle2";

        // ---- Public types

        public enum SymmetryMode
        {
            None,
            SinglePlane,
            FourAroundY,
            DebugMultiple,
            TwoHanded,
            ScriptedSymmetryMode = 6000
        }

        public enum ColorShiftComponent
        {
            Hue,
            Saturation,
            Brightness
        }
        [NonSerialized] public bool m_SymmetryColorShiftEnabled = false;

        [Serializable]
        public struct ColorShiftComponentSetting
        {
            public WaveGenerator.Mode mode;
            public float amp;
            public float freq;
        }

        private static readonly ColorShiftComponentSetting m_defaultColorShiftComponentSetting = new()
        {
            mode = WaveGenerator.Mode.SineWave, amp = 0, freq = 1
        };
        [NonSerialized] public ColorShiftComponentSetting m_SymmetryColorShiftSettingHue = m_defaultColorShiftComponentSetting;
        [NonSerialized] public ColorShiftComponentSetting m_SymmetryColorShiftSettingSaturation = m_defaultColorShiftComponentSetting;
        [NonSerialized] public ColorShiftComponentSetting m_SymmetryColorShiftSettingBrightness = m_defaultColorShiftComponentSetting;


        // Modifying this struct has implications for binary compatibility.
        // The layout should match the most commonly-seen layout in the binary file.
        // See SketchMemoryScript.ReadMemory.
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct ControlPoint
        {
            public Vector3 m_Pos;
            public Quaternion m_Orient;

            public const uint EXTENSIONS = (uint)(
                SketchWriter.ControlPointExtension.Pressure |
                SketchWriter.ControlPointExtension.Timestamp);
            public float m_Pressure;
            public uint m_TimestampMs; // CurrentSketchTime of creation, in milliseconds
        }

        // TODO: all this should be stored in the PointerScript instead of kept alongside
        protected class PointerData
        {
            public PointerScript m_Script;
            // The start of a straightedge stroke.
            public TrTransform m_StraightEdgeXf_CS;
            public bool m_UiEnabled;
        }

        // ---- Private types

        private enum LineCreationState
        {
            // Not drawing a straightedge line.
            WaitingForInput,
            // Have first endpoint but not second endpoint.
            RecordingInput,
            // Have both endpoints; drawing the line over multiple frames.
            // Used for brushes that use straightedge proxies, usually because they
            // need to be drawn over time (like particles)
            ProcessingStraightEdge,
        }

        // ---- Private inspector data

        [SerializeField] private int m_MaxPointers = 1;
        [SerializeField] private GameObject m_MainPointerPrefab;
        [SerializeField] private GameObject m_AuxPointerPrefab;
        [SerializeField] private float m_DefaultPointerAngle = 25.0f;
        [SerializeField] private bool m_DebugViewControlPoints = false;
        [SerializeField] private StraightEdgeGuideScript m_StraightEdgeGuide;
        [SerializeField] private BrushDescriptor m_StraightEdgeProxyBrush;
        [SerializeField] private Transform m_SymmetryWidget;
        [SerializeField] private Vector3 m_SymmetryDebugMultipleOffset = new Vector3(2, 0, 2);
        [SerializeField] private float m_SymmetryPointerStencilBoost = 0.001f;

        [SerializeField] private float m_GestureMinCircleSize;
        [SerializeField] private float m_GestureBeginDist;
        [SerializeField] private float m_GestureCloseLoopDist;
        [SerializeField] private float m_GestureStepDist;
        [SerializeField] private float m_GestureMaxAngle;

        // ---- Private member data

        private int m_NumActivePointers = 1;

        private bool m_PointersRenderingRequested;
        private bool m_PointersRenderingActive;
        private bool m_PointersHideOnControllerLoss;

        private float m_FreePaintPointerAngle;

        private LineCreationState m_CurrentLineCreationState;
        private bool m_LineEnabled = false;
        private int m_EatLineEnabledInputFrames;

        public SymmetryWidget SymmetryWidget => m_SymmetryWidget.GetComponent<SymmetryWidget>();

        /// This array is horrible. It is sort-of a preallocated pool of pointers,
        /// but different ranges are used for different purposes, and the ranges overlap.
        ///
        ///   0       Brush pointer
        ///   1       2-way symmetry for Brush pointer
        ///   1-3     4-way symmetry for Brush pointer
        ///   2-N     (where 2 == NumUserPointers) Playback for timeline-edit sketches
        ///
        /// The only reason we don't have a ton of bugs stemming from systems stomping
        /// over each others' pointers is that we prevent those systems from being
        /// active simultaneously. eg, 4-way symmetry is not allowed during timeline edit mode;
        /// floating-panel mode doesn't actually _use_ the Wand's pointer, etc.
        private PointerData[] m_Pointers;
        private bool m_InPlaybackMode;

        private PointerData m_MainPointerData;
        struct StoredBrushInfo
        {
            public BrushDescriptor brush;
            public float size01;
            public Color color;
        }
        private StoredBrushInfo? m_StoredBrushInfo;

        private bool m_StraightEdgeEnabled; // whether the mode is enabled
        // Brushes which return true for NeedsStraightEdgeProxy() use a proxy brush when displaying the
        // initial straight edge and redraw the line with the real brush at the end. This specifies
        // whether that proxy is currently active:
        private bool m_StraightEdgeProxyActive;
        private CircleGesture m_StraightEdgeGesture;

        private List<ControlPoint> m_StraightEdgeControlPoints_CS;
        private int m_StraightEdgeControlPointIndex;

        private SymmetryMode m_CurrentSymmetryMode;
        private SymmetryWidget m_SymmetryWidgetScript;
        private bool m_UseSymmetryWidget = false;
        public Color m_lastChosenColor { get; private set; }
        public Vector3 colorJitter { get; set; }
        public float sizeJitter { get; set; }
        public float positionJitter { get; set; }

        // These variables are legacy for supporting z-fighting control on the sketch surface
        // panel in monoscopic mode.
        private float m_SketchSurfaceLineDepthVarianceBase = 0.0001f;
        private float m_SketchSurfaceLineDepthVariance = 0.01f;
        private float m_SketchSurfaceLineDepthIncrement = 0.0001f;
        private float m_SketchSurfaceLineDepth;
        private bool m_SketchSurfaceLineWasEnabled;
        private List<Color> m_SymmetryPointerColors;
        private List<BrushDescriptor> m_SymmetryPointerBrushes;
        private Vector2[] m_CustomMirrorDomain;

        // ---- events

        public event Action<TiltBrush.BrushDescriptor> OnMainPointerBrushChange
        {
            add { m_MainPointerData.m_Script.OnBrushChange += value; }
            remove { m_MainPointerData.m_Script.OnBrushChange -= value; }
        }

        public event Action OnPointerColorChange = delegate { };

        // ---- public properties

        public PointerScript MainPointer
        {
            get { return m_MainPointerData.m_Script; }
        }

        /// Only call this if you don't want to update m_lastChosenColor
        /// Used by color jitter on new stroke
        private void ChangeAllPointerColorsDirectly(Color value)
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.SetColor(value);
            }
        }

        public Color PointerColor
        {
            get { return m_MainPointerData.m_Script.GetCurrentColor(); }
            set
            {
                ChangeAllPointerColorsDirectly(value);
                m_lastChosenColor = value;
                CalculateMirrorColors();
                OnPointerColorChange();
            }
        }
        public float PointerPressure
        {
            set
            {
                for (int i = 0; i < m_NumActivePointers; ++i)
                {
                    m_Pointers[i].m_Script.SetPressure(value);
                }
            }
        }

        public bool IndicateBrushSize
        {
            set
            {
                for (int i = 0; i < m_NumActivePointers; ++i)
                {
                    m_Pointers[i].m_Script.ShowSizeIndicator(value);
                }
            }
        }

        /// The number of pointers available with GetTransientPointer()
        public int NumTransientPointers { get { return m_Pointers.Length - NumUserPointers; } }

        /// Number of pointers reserved for user (including symmetry)
        /// TODO: handle more intelligently.  Depends on user's access to e.g. 4-way symmetry.
        private int NumUserPointers { get { return 2; } }

        public SymmetryMode CurrentSymmetryMode
        {
            set { SetSymmetryMode(value); }
            get { return m_CurrentSymmetryMode; }
        }

        /// Returns null if the mirror is not active
        public Plane? SymmetryPlane_RS => (m_CurrentSymmetryMode == SymmetryMode.SinglePlane)
            ? (Plane?)m_SymmetryWidgetScript.ReflectionPlane
            : null;

        public bool SymmetryModeEnabled
        {
            get { return m_CurrentSymmetryMode != SymmetryMode.None; }
        }

        public void SymmetryWidgetFromMirror(Mirror data)
        {
            m_SymmetryWidgetScript.FromMirror(data);
        }

        public Mirror SymmetryWidgetToMirror()
        {
            return m_SymmetryWidgetScript.ToMirror();
        }

        public StraightEdgeGuideScript StraightEdgeGuide
        {
            get { return m_StraightEdgeGuide; }
        }

        public bool StraightEdgeModeEnabled
        {
            get { return m_StraightEdgeEnabled; }
            set { m_StraightEdgeEnabled = value; }
        }

        public bool StraightEdgeGuideIsLine
        {
            get { return StraightEdgeGuide.CurrentShape == StraightEdgeGuideScript.Shape.Line; }
        }

        public float FreePaintPointerAngle
        {
            get { return m_FreePaintPointerAngle; }
            set
            {
                m_FreePaintPointerAngle = value;
                PlayerPrefs.SetFloat(PLAYER_PREFS_POINTER_ANGLE, m_FreePaintPointerAngle);
            }
        }
        public bool JitterEnabled => colorJitter.sqrMagnitude > 0 || sizeJitter > 0 || positionJitter > 0;

        public List<Color> SymmetryPointerColors => m_SymmetryPointerColors.ToList();
        public List<BrushDescriptor> SymmetryPointerBrushes => m_SymmetryPointerBrushes.ToList();

        static public void ClearPlayerPrefs()
        {
            PlayerPrefs.DeleteKey(PLAYER_PREFS_POINTER_ANGLE_OLD);
            PlayerPrefs.DeleteKey(PLAYER_PREFS_POINTER_ANGLE);
        }

        // ---- accessors

        public PointerScript GetPointer(ControllerName name)
        {
            return GetPointerData(name).m_Script;
        }

        // Return a pointer suitable for transient use (like for playback)
        // Guaranteed to be different from any non-null return value of GetPointer(ControllerName)
        // Raise exception if not enough pointers
        public PointerScript GetTransientPointer(int i)
        {
            return m_Pointers[NumUserPointers + i].m_Script;
        }

        /// The brush size, using "normalized" values in the range [0,1].
        /// Guaranteed to be in [0,1].
        public float GetPointerBrushSize01(InputManager.ControllerName controller)
        {
            return Mathf.Clamp01(GetPointer(controller).BrushSize01);
        }

        public bool IsStraightEdgeProxyActive()
        {
            return m_StraightEdgeProxyActive;
        }

        public bool IsMainPointerCreatingStroke()
        {
            return m_MainPointerData.m_Script.IsCreatingStroke();
        }

        public bool IsMainPointerProcessingLine()
        {
            return m_CurrentLineCreationState == LineCreationState.ProcessingStraightEdge;
        }

        public static bool MainPointerIsPainting()
        {
            if (
                m_Instance.IsMainPointerProcessingLine()
                || m_Instance.IsMainPointerCreatingStroke()
                || m_Instance.IsLineEnabled()
            )
                return true;

            return false;
        }

        public void SetInPlaybackMode(bool bInPlaybackMode)
        {
            m_InPlaybackMode = bInPlaybackMode;
        }

        public void EatLineEnabledInput()
        {
            m_EatLineEnabledInputFrames = 2;
        }

        /// Causes pointer manager to begin or end a stroke; takes effect next frame.
        public void EnableLine(bool bEnable)
        {
            // If we've been requested to eat input, discard any valid input until we've received
            //  some invalid input.
            if (m_EatLineEnabledInputFrames > 0)
            {
                if (!bEnable)
                {
                    --m_EatLineEnabledInputFrames;
                }
                m_LineEnabled = false;
            }
            else
            {
                m_LineEnabled = bEnable;
            }
        }

        public bool IsLineEnabled()
        {
            return m_LineEnabled;
        }

        public void UseSymmetryWidget(bool bUse)
        {
            m_UseSymmetryWidget = bUse;
        }

        // ---- Unity events

        void Awake()
        {
            m_Instance = this;

            Debug.Assert(m_MaxPointers > 0);
            m_Pointers = new PointerData[m_MaxPointers];

            for (int i = 0; i < m_Pointers.Length; ++i)
            {
                //set our main pointer as the zero index
                bool bMain = (i == 0);
                var data = new PointerData();
                GameObject obj = (GameObject)Instantiate(bMain ? m_MainPointerPrefab : m_AuxPointerPrefab);
                obj.transform.parent = transform;
                data.m_Script = obj.GetComponent<PointerScript>();
                data.m_Script.EnableDebugViewControlPoints(bMain && m_DebugViewControlPoints);
                data.m_Script.ChildIndex = i;
                data.m_UiEnabled = bMain;
                m_Pointers[i] = data;
                if (bMain)
                {
                    m_MainPointerData = data;
                }
            }

            m_CurrentLineCreationState = LineCreationState.WaitingForInput;
            m_StraightEdgeProxyActive = false;
            m_StraightEdgeGesture = new CircleGesture();

            if (m_SymmetryWidget)
            {
                m_SymmetryWidgetScript = m_SymmetryWidget.GetComponent<SymmetryWidget>();
            }

            //initialize rendering requests to default to hiding everything
            m_PointersRenderingRequested = false;
            m_PointersRenderingActive = true;

            m_FreePaintPointerAngle =
                PlayerPrefs.GetFloat(PLAYER_PREFS_POINTER_ANGLE, m_DefaultPointerAngle);
        }

        void Start()
        {
            m_SymmetryPointerColors = new List<Color>();
            m_SymmetryPointerBrushes = new List<BrushDescriptor>();
            SetSymmetryMode(SymmetryMode.None, false);
            m_PointersHideOnControllerLoss = App.VrSdk.GetControllerDof() == VrSdk.DoF.Six;

            // Migrate setting, but only if it's non-zero
            if (PlayerPrefs.HasKey(PLAYER_PREFS_POINTER_ANGLE_OLD))
            {
                var prev = PlayerPrefs.GetFloat(PLAYER_PREFS_POINTER_ANGLE_OLD);
                PlayerPrefs.DeleteKey(PLAYER_PREFS_POINTER_ANGLE_OLD);
                if (prev != 0)
                {
                    PlayerPrefs.SetFloat(PLAYER_PREFS_POINTER_ANGLE, prev);
                }
            }

            RefreshFreePaintPointerAngle();
        }

        void Update()
        {
            if (m_StraightEdgeEnabled && m_CurrentLineCreationState == LineCreationState.RecordingInput)
            {
                m_StraightEdgeGuide.SnapEnabled =
                    InputManager.Brush.GetCommand(InputManager.SketchCommands.MenuContextClick) &&
                    SketchControlsScript.m_Instance.ShouldRespondToPadInput(InputManager.ControllerName.Num);
                m_StraightEdgeGuide.UpdateTarget(MainPointer.transform.position);
            }

            if (SymmetryModeEnabled)
            {
                //if we're not showing the symmetry widget, keep it locked where needed
                if (!m_UseSymmetryWidget)
                {
                    if (m_CurrentSymmetryMode == SymmetryMode.SinglePlane)
                    {
                        m_SymmetryWidget.position = Vector3.zero;
                        m_SymmetryWidget.rotation = Quaternion.identity;
                    }
                    else if (m_CurrentSymmetryMode == SymmetryMode.FourAroundY)
                    {
                        m_SymmetryWidget.position = SketchSurfacePanel.m_Instance.transform.position;
                        m_SymmetryWidget.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
                    }
                    else if (m_CurrentSymmetryMode == SymmetryMode.ScriptedSymmetryMode)
                    {
                        m_SymmetryWidget.position = SketchSurfacePanel.m_Instance.transform.position;
                        m_SymmetryWidget.rotation = SketchSurfacePanel.m_Instance.transform.rotation;
                    }
                }
            }

            //update pointers
            if (!m_InPlaybackMode && !PanelManager.m_Instance.IntroSketchbookMode)
            {
                // This is special code to prevent z-fighting in monoscopic mode.
                float fPointerLift = 0.0f;
                if (App.VrSdk.GetHmdDof() == VrSdk.DoF.None)
                {
                    if (m_LineEnabled)
                    {
                        // If we just became enabled, randomize our pointer lift start point.
                        if (!m_SketchSurfaceLineWasEnabled)
                        {
                            m_SketchSurfaceLineDepth = m_SketchSurfaceLineDepthVarianceBase +
                                UnityEngine.Random.Range(0.0f, m_SketchSurfaceLineDepthVariance);
                        }

                        // While enabled, add depth as a function of distance moved.
                        m_SketchSurfaceLineDepth += m_MainPointerData.m_Script.GetMovementDelta() *
                            m_SketchSurfaceLineDepthIncrement;
                    }
                    else
                    {
                        m_SketchSurfaceLineDepth = m_SketchSurfaceLineDepthVarianceBase;
                    }

                    fPointerLift = m_SketchSurfaceLineDepth;
                    m_SketchSurfaceLineWasEnabled = m_LineEnabled;
                }

                // Update each pointer's line depth with the monoscopic sketch surface pointer lift.
                for (int i = 0; i < m_NumActivePointers; ++i)
                {
                    m_Pointers[i].m_Script.MonoscopicLineDepth = fPointerLift;
                    m_Pointers[i].m_Script.UpdatePointer();
                }
            }

            //update pointer rendering according to state
            if (!m_PointersHideOnControllerLoss || InputManager.Brush.IsTrackedObjectValid)
            {
                //show pointers according to requested visibility
                SetPointersRenderingEnabled(m_PointersRenderingRequested);
            }
            else
            {
                //turn off pointers
                SetPointersRenderingEnabled(false);
                DisablePointerPreviewLine();
            }
        }

        public void StoreBrushInfo()
        {
            m_StoredBrushInfo = new StoredBrushInfo
            {
                brush = MainPointer.CurrentBrush,
                size01 = MainPointer.BrushSize01,
                color = PointerColor,
            };
        }

        public void RestoreBrushInfo()
        {
            if (m_StoredBrushInfo == null) { return; }
            var info = m_StoredBrushInfo.Value;
            SetBrushForAllPointers(info.brush);
            SetAllPointersBrushSize01(info.size01);
            MarkAllBrushSizeUsed();
            PointerColor = info.color;
        }

        public void RefreshFreePaintPointerAngle()
        {
            InputManager.m_Instance.SetControllersAttachAngle(m_FreePaintPointerAngle);
        }

        void SetPointersRenderingEnabled(bool bEnable)
        {
            if (m_PointersRenderingActive != bEnable)
            {
                foreach (PointerData rData in m_Pointers)
                {
                    rData.m_Script.EnableRendering(bEnable && rData.m_UiEnabled);
                }
                m_PointersRenderingActive = bEnable;
            }
        }

        public void EnablePointerStrokeGeneration(bool bActivate)
        {
            foreach (PointerData rData in m_Pointers)
            {
                // Note that pointers with m_UiEnabled=false may still be employed during scene playback.
                rData.m_Script.gameObject.SetActive(bActivate);
            }
        }

        public void EnablePointerLights(bool bEnable)
        {
            foreach (PointerData rData in m_Pointers)
            {
                rData.m_Script.AllowPreviewLight(bEnable && rData.m_UiEnabled);
            }
        }

        public void RequestPointerRendering(bool bEnable)
        {
            m_PointersRenderingRequested = bEnable;
        }

        public void SetPointersAudioForPlayback()
        {
            foreach (PointerData rData in m_Pointers)
            {
                rData.m_Script.SetAudioClipForPlayback();
            }
        }

        private PointerData GetPointerData(ControllerName name)
        {
            // TODO: replace with something better that handles multiple controllers
            switch (name)
            {
                case ControllerName.Brush:
                    return m_Pointers[0];
                default:
                    Debug.AssertFormat(false, "No pointer for controller {0}", name);
                    return null;
            }
        }

        public void AllowPointerPreviewLine(bool bAllow)
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.AllowPreviewLine(bAllow);
            }
        }

        public void DisablePointerPreviewLine()
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.DisablePreviewLine();
            }
        }

        public void ResetPointerAudio()
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.ResetAudio();
            }
        }

        public void SetPointerPreviewLineDelayTimer()
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.SetPreviewLineDelayTimer();
            }
        }

        public void ExplicitlySetAllPointersBrushSize(float fSize)
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.BrushSizeAbsolute = fSize;
            }
        }

        public void MarkAllBrushSizeUsed()
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.MarkBrushSizeUsed();
            }
        }

        public void SetAllPointersBrushSize01(float t)
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.BrushSize01 = t;
            }
        }

        public void AdjustAllPointersBrushSize01(float dt)
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.BrushSize01 += dt;
            }
        }

        public void SetBrushForAllPointers(BrushDescriptor desc)
        {
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.SetBrush(desc);
            }
        }

        public void SetPointerTransform(ControllerName name, Vector3 v, Quaternion q)
        {
            Transform pointer = GetPointer(name).transform;
            pointer.position = v;
            pointer.rotation = q;
            UpdateSymmetryPointerTransforms();
        }

        public void SetMainPointerPosition(Vector3 vPos)
        {
            m_MainPointerData.m_Script.transform.position = vPos;
            UpdateSymmetryPointerTransforms();
        }

        public void SetMainPointerRotation(Quaternion qRot)
        {
            m_MainPointerData.m_Script.transform.rotation = qRot;
            UpdateSymmetryPointerTransforms();
        }

        public void SetMainPointerPositionAndForward(Vector3 vPos, Vector3 vForward)
        {
            m_MainPointerData.m_Script.transform.position = vPos;
            m_MainPointerData.m_Script.transform.forward = vForward;
        }

        public List<TrTransform> GetScriptedTransforms()
        {
            var result = LuaManager.Instance.CallActiveSymmetryScript(LuaNames.Main);
            if (result.Transforms.Count != m_NumActivePointers)
            {
                ChangeNumActivePointers(result.Transforms.Count);
            }

            var trs_CS = new List<TrTransform>();

            int i = 0;
            foreach (var resultTr in result.Transforms)
            {
                TrTransform newTr_CS = TrTransform.identity;
                switch (result.Space)
                {
                    case ScriptCoordSpace.Default:
                        var xfWidget = TrTransform.FromTransform(m_SymmetryWidget);
                        var tr = resultTr;
                        var translation = tr.translation;
                        tr.translation = Vector3.zero;
                        newTr_CS = tr.TransformBy(xfWidget);
                        translation = tr.rotation * translation;
                        newTr_CS.translation += translation;
                        break;
                    case ScriptCoordSpace.Canvas:
                        newTr_CS = resultTr;
                        break;
                    case ScriptCoordSpace.Pointer:
                        Transform rAttachPoint_GS = InputManager.m_Instance.GetBrushControllerAttachPoint();
                        Quaternion pointerRot_GS = rAttachPoint_GS.rotation * FreePaintTool.sm_OrientationAdjust;
                        newTr_CS.translation = pointerRot_GS * resultTr.translation;
                        break;
                }
                trs_CS.Add(newTr_CS);
            }

            return trs_CS;
        }

        private void SetScriptedBrushesAndColors()
        {
            var script = LuaManager.Instance.GetActiveScript(LuaApiCategory.SymmetryScript);

            var luaColors = script.Globals.Get(LuaNames.Colors);
            if (!Equals(luaColors, DynValue.Nil))
            {
                m_SymmetryPointerColors = luaColors.ToObject<List<Color>>();
            }
            else
            {
                m_SymmetryPointerColors.Clear();
            }

            var luaBrushes = script.Globals.Get(LuaNames.Brushes);
            if (!Equals(luaBrushes, DynValue.Nil))
            {
                m_SymmetryPointerBrushes = luaBrushes.Table.Values.Select(
                    x => ApiMethods.LookupBrushDescriptor(x.String)
                ).Where(x => x != null).ToList();
            }
            else
            {
                m_SymmetryPointerBrushes.Clear();
            }
        }

        public void SetSymmetryMode(SymmetryMode mode, bool recordCommand = true)
        {
            if (m_CurrentSymmetryMode == SymmetryMode.ScriptedSymmetryMode)
            {
                LuaManager.Instance.EndActiveScript(LuaApiCategory.SymmetryScript);
            }

            int active = m_NumActivePointers;
            switch (mode)
            {
                case SymmetryMode.None:
                    active = 1;
                    break;
                case SymmetryMode.SinglePlane:
                case SymmetryMode.TwoHanded:
                    active = 2;
                    break;
                case SymmetryMode.FourAroundY:
                    active = 4;
                    break;
                case SymmetryMode.ScriptedSymmetryMode:
                    var script = LuaManager.Instance.GetActiveScript(LuaApiCategory.SymmetryScript);
                    LuaManager.Instance.InitScript(script);
                    var trs = GetScriptedTransforms();
                    active = trs.Count;
                    break;
                case SymmetryMode.DebugMultiple:
                    active = DEBUG_MULTIPLE_NUM_POINTERS;
                    break;
            }
            if (m_NumActivePointers != active)
            {
                ChangeNumActivePointers(active);
            }

            m_CurrentSymmetryMode = mode;
            m_SymmetryWidgetScript.SetMode(m_CurrentSymmetryMode);
            m_SymmetryWidgetScript.Show(m_UseSymmetryWidget && SymmetryModeEnabled);
            if (recordCommand)
            {
                SketchMemoryScript.m_Instance.RecordCommand(
                    new SymmetryWidgetVisibleCommand(m_SymmetryWidgetScript));
            }

        }

        private void ChangeNumActivePointers(int num)
        {
            if (num > m_Pointers.Length)
            {
                Debug.LogWarning($"Not enough pointers for mode. {num} requested, {m_Pointers.Length} available");
                num = m_Pointers.Length;
            }
            m_NumActivePointers = num;
            for (int i = 1; i < m_Pointers.Length; ++i)
            {
                var pointer = m_Pointers[i];
                bool enabled = i < m_NumActivePointers;
                pointer.m_UiEnabled = enabled;
                pointer.m_Script.gameObject.SetActive(enabled);
                pointer.m_Script.EnableRendering(m_PointersRenderingActive && enabled);
                if (enabled)
                {
                    pointer.m_Script.CopyInternals(m_Pointers[0].m_Script);
                }
            }

            App.Switchboard.TriggerMirrorVisibilityChanged();
        }

        public void ResetSymmetryToHome()
        {
            m_SymmetryWidgetScript.ResetToHome();
        }

        public void BringSymmetryToUser()
        {
            m_SymmetryWidgetScript.BringToUser();
        }

        /// Given the position of a main pointer, find a corresponding symmetry position.
        /// Results are undefined unless you pass MainPointer or one of its
        /// dedicated symmetry pointers.
        public TrTransform GetSymmetryTransformFor(PointerScript pointer, TrTransform xfMain)
        {
            int child = pointer.ChildIndex;
            // "active pointers" is the number of pointers the symmetry widget is using,
            // including the main pointer.
            // ScriptedSymmetryMode controls ALL pointers including the pointer 0
            if (child == 0 || m_CurrentSymmetryMode == SymmetryMode.ScriptedSymmetryMode)
            {
                return xfMain;
            }

            // This needs to be kept in sync with UpdateSymmetryPointerTransforms
            switch (m_CurrentSymmetryMode)
            {
                case SymmetryMode.SinglePlane:
                    {
                        return m_SymmetryWidgetScript.ReflectionPlane.ReflectPoseKeepHandedness(xfMain);
                    }

                case SymmetryMode.FourAroundY:
                    {
                        // aboutY is an operator that rotates worldspace objects N degrees around the widget's Y
                        TrTransform aboutY;
                        {
                            var xfWidget = TrTransform.FromTransform(m_SymmetryWidget);
                            float angle = (360f * child) / m_NumActivePointers;
                            aboutY = TrTransform.TR(Vector3.zero, Quaternion.AngleAxis(angle, Vector3.up));
                            // convert from widget-local coords to world coords
                            aboutY = aboutY.TransformBy(xfWidget);
                        }
                        return aboutY * xfMain;
                    }
                case SymmetryMode.ScriptedSymmetryMode:
                    {
                        TrTransform scriptedTr;
                        {
                            scriptedTr = GetScriptedTransforms()[child];
                            // convert from canvas to world coords
                            scriptedTr *= App.Scene.Pose.inverse;
                        }
                        return scriptedTr;
                    }

                case SymmetryMode.DebugMultiple:
                    {
                        var xfLift = TrTransform.T(m_SymmetryDebugMultipleOffset * child);
                        return xfLift * xfMain;
                    }

                case SymmetryMode.TwoHanded:
                    {
                        return TrTransform.T(xfMain.translation - InputManager.m_Instance.GetWandControllerAttachPoint().position);
                    }
                default:
                    return xfMain;
            }
        }


        public void CalculateMirrorColors()
        {
            if (m_SymmetryColorShiftEnabled)
            {
                m_SymmetryPointerColors = new List<Color>();
                for (float i = 0; i < m_NumActivePointers; i++)
                {
                    m_SymmetryPointerColors.Add(CalcColorShift(m_lastChosenColor, i / m_NumActivePointers));
                    // BrushDescriptor desc = BrushCatalog.m_Instance.GetBrush(MainPointer.CurrentBrush.m_Guid);
                    // script.BrushSize01 = GenerateJitteredSize(desc, MainPointer.BrushSize01);
                }
            }
        }


        void UpdateSymmetryPointerTransforms()
        {
            switch (m_CurrentSymmetryMode)
            {
                case SymmetryMode.SinglePlane:
                    {
                        Plane plane = m_SymmetryWidgetScript.ReflectionPlane;
                        TrTransform xf0 = TrTransform.FromTransform(m_MainPointerData.m_Script.transform);
                        TrTransform xf1 = plane.ReflectPoseKeepHandedness(xf0);
                        xf1.ToTransform(m_Pointers[1].m_Script.transform);

                        // This is a hack.
                        // In the event that the user is painting on a plane stencil and that stencil is
                        // orthogonal to the symmetry plane, the main pointer and mirrored pointer will
                        // have the same depth and their strokes will overlap, causing z-fighting.
                        if (WidgetManager.m_Instance.ActiveStencil != null)
                        {
                            m_Pointers[1].m_Script.transform.position +=
                                m_Pointers[1].m_Script.transform.forward * m_SymmetryPointerStencilBoost;
                        }
                        break;
                    }

                case SymmetryMode.FourAroundY:
                    {
                        TrTransform pointer0 = TrTransform.FromTransform(m_MainPointerData.m_Script.transform);
                        // aboutY is an operator that rotates worldspace objects N degrees around the widget's Y
                        TrTransform aboutY;
                        {
                            var xfWidget = TrTransform.FromTransform(m_SymmetryWidget);
                            float angle = 360f / m_NumActivePointers;
                            aboutY = TrTransform.TR(Vector3.zero, Quaternion.AngleAxis(angle, Vector3.up));
                            // convert from widget-local coords to world coords
                            aboutY = xfWidget * aboutY * xfWidget.inverse;
                        }

                        TrTransform cur = TrTransform.identity;
                        for (int i = 1; i < m_NumActivePointers; ++i)
                        {
                            cur = aboutY * cur;         // stack another rotation on top
                            var tmp = (cur * pointer0); // Work around 2018.3.x Mono parse bug
                            tmp.ToTransform(m_Pointers[i].m_Script.transform);
                        }
                        break;
                    }
                case SymmetryMode.ScriptedSymmetryMode:
                    {
                        TrTransform pointer0_GS = TrTransform.FromTransform(m_MainPointerData.m_Script.transform);
                        var trs = GetScriptedTransforms();
                        SetScriptedBrushesAndColors();
                        int pointerIndex = 0;
                        foreach (var tr in trs)
                        {
                            // convert from canvas to world coords
                            // tr *= App.Scene.Pose.inverse;
                            // Apply the transform to the pointer
                            var tmp = tr * pointer0_GS; // Work around 2018.3.x Mono parse bug
                            tmp.ToTransform(m_Pointers[pointerIndex].m_Script.transform);
                            pointerIndex++;
                        }
                        break;
                    }

                case SymmetryMode.DebugMultiple:
                    {
                        var xf0 = m_Pointers[0].m_Script.transform;
                        for (int i = 1; i < m_NumActivePointers; ++i)
                        {
                            var xf = m_Pointers[i].m_Script.transform;
                            xf.position = xf0.position + m_SymmetryDebugMultipleOffset * i;
                            xf.rotation = xf0.rotation;
                        }
                        break;
                    }
                case SymmetryMode.TwoHanded:
                    {
                        var xf = m_Pointers[1].m_Script.transform;
                        xf.position = InputManager.m_Instance.GetWandControllerAttachPoint().position;
                        xf.rotation = InputManager.m_Instance.GetWandControllerAttachPoint().rotation;
                    }
                    break;
            }
        }

        /// Called every frame while Activate is disallowed
        void OnDrawDisallowed()
        {
            InputManager.m_Instance.TriggerHaptics(InputManager.ControllerName.Brush, 0.1f);
        }

        int NumFreePlaybackPointers()
        {
            // TODO: Plumb this info from ScenePlayback so it can emulate pointer usage e.g. while
            // keeping all strokes visible.
            int count = 0;
            for (int i = NumUserPointers; i < m_Pointers.Length; ++i)
            {
                if (!m_Pointers[i].m_Script.IsCreatingStroke())
                {
                    ++count;
                }
            }
            return count;
        }

        /// State-machine update function; always called once per frame.
        public void UpdateLine()
        {
            bool playbackPointersAvailable = m_NumActivePointers <= NumFreePlaybackPointers();

            switch (m_CurrentLineCreationState)
            {
                case LineCreationState.WaitingForInput:
                    if (m_LineEnabled)
                    {
                        if (playbackPointersAvailable)
                        {
                            Transition_WaitingForInput_RecordingInput();
                        }
                        else
                        {
                            OnDrawDisallowed();
                        }
                    }
                    break;

                // TODO: unique state for capturing straightedge 2nd point rather than overload RecordingInput
                case LineCreationState.RecordingInput:
                    if (m_LineEnabled)
                    {
                        if (playbackPointersAvailable)
                        {
                            // Check straightedge gestures.
                            if (m_StraightEdgeEnabled)
                            {
                                CheckGestures();
                            }

                            // check to see if any pointer's line needs to end
                            // TODO: equivalent check during ProcessingStraightEdge
                            bool bStartNewLine = false;
                            for (int i = 0; i < m_NumActivePointers; ++i)
                            {
                                bStartNewLine = bStartNewLine || m_Pointers[i].m_Script.ShouldCurrentLineEnd();
                            }
                            if (bStartNewLine && !m_StraightEdgeEnabled)
                            {
                                //if it has, stop this line and start anew
                                FinalizeLine(isContinue: true);
                                InitiateLine(isContinue: true);
                            }
                        }
                        else if (!m_StraightEdgeEnabled)
                        {
                            OnDrawDisallowed();
                            Transition_RecordingInput_WaitingForInput();
                        }
                    }
                    else
                    {
                        // Transition to either ProcessingStraightEdge or WaitingForInput
                        if (m_StraightEdgeProxyActive)
                        {
                            if (playbackPointersAvailable)
                            {
                                List<ControlPoint> cps = MainPointer.GetControlPoints();
                                FinalizeLine(discard: true);
                                Transition_RecordingInput_ProcessingStraightEdge(cps);
                            }
                            else
                            {
                                OnDrawDisallowed();
                                // cancel the straight edge
                                m_StraightEdgeProxyActive = false;
                                m_StraightEdgeGuide.HideGuide();
                                m_CurrentLineCreationState = LineCreationState.WaitingForInput;
                            }
                        }
                        else
                        {
                            m_StraightEdgeGuide.HideGuide();
                            var stencil = WidgetManager.m_Instance.ActiveStencil;
                            if (stencil != null)
                            {
                                stencil.AdjustLift(1);
                            }
                            Transition_RecordingInput_WaitingForInput();
                        }

                        // Eat up tool scale input for heavy grippers.
                        SketchControlsScript.m_Instance.EatToolScaleInput();
                    }
                    break;

                case LineCreationState.ProcessingStraightEdge:
                    State_ProcessingStraightEdge(terminate: !playbackPointersAvailable);
                    break;
            }
        }

        void CheckGestures()
        {
            m_StraightEdgeGesture.UpdateGesture(MainPointer.transform.position);
            if (m_StraightEdgeGesture.IsGestureComplete())
            {
                // If gesture succeeded, change the line creator.
                if (m_StraightEdgeGesture.DidGestureSucceed())
                {
                    FinalizeLine(discard: true);
                    StraightEdgeGuideScript.Shape nextShape = StraightEdgeGuide.CurrentShape;
                    switch (nextShape)
                    {
                        case StraightEdgeGuideScript.Shape.Line:
                            nextShape = StraightEdgeGuideScript.Shape.Circle;
                            break;
                        case StraightEdgeGuideScript.Shape.Circle:
                            {
                                if (App.Config.IsMobileHardware)
                                {
                                    nextShape = StraightEdgeGuideScript.Shape.Line;
                                }
                                else
                                {
                                    nextShape = StraightEdgeGuideScript.Shape.Sphere;
                                }
                            }
                            break;
                        case StraightEdgeGuideScript.Shape.Sphere:
                            nextShape = StraightEdgeGuideScript.Shape.Line;
                            break;
                    }

                    StraightEdgeGuide.SetTempShape(nextShape);
                    StraightEdgeGuide.ResolveTempShape();
                    InitiateLineAt(m_MainPointerData.m_StraightEdgeXf_CS);
                }

                m_StraightEdgeGesture.ResetGesture();
            }
        }

        private void Transition_WaitingForInput_RecordingInput()
        {
            // Can't check for null as Color is a struct
            // But it's harmless to call this if the color really has been set to black
            if (m_lastChosenColor == Color.black)
            {
                m_lastChosenColor = PointerColor;
            }

            if (JitterEnabled)
            {
                // Bypass the code in the PointerColor setter
                // Size is jittered in PointerScript. Should we also do color there?
                ChangeAllPointerColorsDirectly(GenerateJitteredColor(MainPointer.CurrentBrush.m_ColorLuminanceMin));
            }


            if (m_StraightEdgeEnabled)
            {
                StraightEdgeGuide.SetTempShape(StraightEdgeGuideScript.Shape.Line);
                StraightEdgeGuide.ResolveTempShape();
                m_StraightEdgeGesture.InitGesture(MainPointer.transform.position,
                    m_GestureMinCircleSize, m_GestureBeginDist, m_GestureCloseLoopDist,
                    m_GestureStepDist, m_GestureMaxAngle);
            }

            InitiateLine();
            m_CurrentLineCreationState = LineCreationState.RecordingInput;
            WidgetManager.m_Instance.WidgetsDormant = true;
        }

        public Color GenerateJitteredColor(float colorLuminanceMin)
        {
            return GenerateJitteredColor(m_lastChosenColor, colorLuminanceMin);
        }
        public Color GenerateJitteredColor(Color currentColor, float colorLuminanceMin)
        {
            return ColorPickerUtils.ClampLuminance(CalculateJitteredColor(currentColor), colorLuminanceMin);
        }


        public Color CalculateJitteredColor(Color currentColor)
        {
            Color.RGBToHSV(currentColor, out var h, out var s, out var v);
            return Random.ColorHSV(
                h - colorJitter.x, h + colorJitter.x,
                s - colorJitter.y, s + colorJitter.y,
                v - colorJitter.z, v + colorJitter.z
            );
        }

        private float ActualMod(float x, float m) => (x % m + m) % m;

        public Color CalcColorShift(Color color, float mod)
        {
            Color.RGBToHSV(color, out float h, out float s, out float v);
            h = _CalcColorShiftH(h, mod, m_SymmetryColorShiftSettingHue);
            s = _CalcColorShiftSV(s, mod, m_SymmetryColorShiftSettingSaturation);
            v = _CalcColorShiftSV(v, mod, m_SymmetryColorShiftSettingBrightness);
            return Color.HSVToRGB(ActualMod(h, 1), s, v);
        }

        public static float _CalcColorShiftH(float x, float mod, ColorShiftComponentSetting settings)
        {
            // Expects x to vary from -1 to +1
            return Mathf.LerpUnclamped(
                x,
                x + settings.amp / 2,
                WaveGenerator.Sample(settings.mode, mod, settings.freq));
        }

        public static float _CalcColorShiftSV(float x, float mod, ColorShiftComponentSetting settings)
        {
            // Expects x to vary from -1 to +1
            return Mathf.LerpUnclamped(
                x,
                x + settings.amp / 2,
                WaveGenerator.Sample(settings.mode, mod, settings.freq)
            );
        }

        public float GenerateJitteredSize(BrushDescriptor desc, float currentSize)
        {
            float range = desc.m_BrushSizeRange.y - desc.m_BrushSizeRange.x;
            float jitterValue = Random.Range(-sizeJitter * range, sizeJitter * range) * 0.5f;
            float jitteredBrushSize = currentSize + jitterValue;
            jitteredBrushSize = Mathf.Clamp(jitteredBrushSize, desc.m_BrushSizeRange.x, desc.m_BrushSizeRange.y);
            return jitteredBrushSize;
        }

        public Vector3 GenerateJitteredPosition(Vector3 currentPos, float jitterAmount)
        {
            return currentPos + (Random.insideUnitSphere * jitterAmount);
        }

        private void Transition_RecordingInput_ProcessingStraightEdge(List<ControlPoint> cps)
        {
            Debug.Assert(m_StraightEdgeProxyActive);

            //create straight line
            m_StraightEdgeProxyActive = false;
            m_StraightEdgeGuide.HideGuide();

            m_StraightEdgeControlPoints_CS = cps;
            m_StraightEdgeControlPointIndex = 0;

            // Reset pointer to first control point and init all active pointers.
            SetMainPointerPosition(Coords.CanvasPose * m_StraightEdgeControlPoints_CS[0].m_Pos);

            var canvas = App.Scene.ActiveCanvas;
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                var p = m_Pointers[i];
                TrTransform xf_CS = canvas.AsCanvas[p.m_Script.transform];

                p.m_Script.CreateNewLine(canvas, xf_CS, null);
                p.m_Script.SetPressure(STRAIGHTEDGE_PRESSURE);
                p.m_Script.SetControlPoint(xf_CS, isKeeper: true);
            }

            // Ensure that snap is disabled when we start the stroke.
            m_StraightEdgeGuide.ForceSnapDisabled();

            //do this operation over a series of frames
            m_CurrentLineCreationState = LineCreationState.ProcessingStraightEdge;
        }

        private void Transition_RecordingInput_WaitingForInput()
        {
            // standard mode, just finalize our line and get ready for the next one
            FinalizeLine();

            m_CurrentLineCreationState = LineCreationState.WaitingForInput;

            // Store last stroke for API usage
            try
            {
                SelectionManager.m_Instance.LastStroke = SketchMemoryScript.m_Instance.MostRecentStroke;
            }
            catch (NullReferenceException e) { }
        }

        private void State_ProcessingStraightEdge(bool terminate)
        {
            int cpPerFrame = Mathf.Max(
                m_StraightEdgeControlPoints_CS.Count / STRAIGHTEDGE_DRAWIN_FRAMES, 2);

            TrTransform xfCanvas = Coords.CanvasPose;
            for (int p = 0; p < cpPerFrame &&
                 m_StraightEdgeControlPointIndex < m_StraightEdgeControlPoints_CS.Count;
                 p++, m_StraightEdgeControlPointIndex++)
            {
                ControlPoint cp = m_StraightEdgeControlPoints_CS[m_StraightEdgeControlPointIndex];
                TrTransform xfPointer = xfCanvas * TrTransform.TR(cp.m_Pos, cp.m_Orient);
                SetMainPointerPosition(xfPointer.translation);
                SetMainPointerRotation(xfPointer.rotation);
                for (int i = 0; i < m_NumActivePointers; ++i)
                {
                    m_Pointers[i].m_Script.UpdateLineFromObject();
                }

                var stencil = WidgetManager.m_Instance.ActiveStencil;
                if (stencil != null)
                {
                    stencil.AdjustLift(1);
                }
            }

            // we reached the end!
            if (terminate || m_StraightEdgeControlPointIndex >= m_StraightEdgeControlPoints_CS.Count)
            {
                FinalizeLine();
                m_CurrentLineCreationState = LineCreationState.WaitingForInput;
            }
        }

        // Only called during interactive creation.
        // isContinue is true if the line is the logical (if not physical) continuation
        // of a previous line -- ie, previous line ran out of verts and we transparently
        // stopped and started a new one.
        void InitiateLine(bool isContinue = false)
        {
            // Turn off the preview when we start drawing
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                m_Pointers[i].m_Script.DisablePreviewLine();
                m_Pointers[i].m_Script.AllowPreviewLine(false);
            }

            if (m_StraightEdgeEnabled)
            {
                // This causes the line to be drawn with a proxy brush; and also to be
                // discarded and redrawn upon completion.
                m_StraightEdgeProxyActive = MainPointer.CurrentBrush.NeedsStraightEdgeProxy;
                // Turn on the straight edge and hold on to our start position
                m_StraightEdgeGuide.ShowGuide(MainPointer.transform.position);
                for (int i = 0; i < m_NumActivePointers; ++i)
                {
                    m_Pointers[i].m_StraightEdgeXf_CS = Coords.AsCanvas[m_Pointers[i].m_Script.transform];
                }
            }

            CanvasScript canvas = App.Scene.ActiveCanvas;
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                PointerScript script = m_Pointers[i].m_Script;
                var xfPointer_CS = canvas.AsCanvas[script.transform];

                // Pass in parametric stroke creator.
                ParametricStrokeCreator currentCreator = null;
                if (m_StraightEdgeEnabled)
                {
                    switch (StraightEdgeGuide.CurrentShape)
                    {
                        case StraightEdgeGuideScript.Shape.Line:
                            currentCreator = new LineCreator(xfPointer_CS, flat: true);
                            break;
                        case StraightEdgeGuideScript.Shape.Circle:
                            currentCreator = new CircleCreator(xfPointer_CS);
                            break;
                        case StraightEdgeGuideScript.Shape.Sphere:
                            currentCreator = new SphereCreator(xfPointer_CS, script.BrushSizeAbsolute,
                                canvas.transform.GetUniformScale());
                            break;
                    }
                }

                bool resetColors = true;
                bool resetBrushes = true;
                if (m_SymmetryColorShiftEnabled || CurrentSymmetryMode == SymmetryMode.ScriptedSymmetryMode)
                {
                    if (m_SymmetryPointerColors != null && m_SymmetryPointerColors.Count > 0)
                    {
                        script.SetColor(m_SymmetryPointerColors[i % m_SymmetryPointerColors.Count]);
                        resetColors = false;
                    }

                    if (m_SymmetryPointerBrushes != null && m_SymmetryPointerBrushes.Count > 0)
                    {
                        script.SetBrush(m_SymmetryPointerBrushes[i % m_SymmetryPointerBrushes.Count]);
                        resetBrushes = false;
                    }
                }

                // Ensure brush and color is reset after using scripts
                if (resetBrushes)
                {
                    script.CurrentBrush = MainPointer.CurrentBrush;
                }
                if (resetColors)
                {
                    var color = JitterEnabled ?
                        GenerateJitteredColor(m_lastChosenColor, script.CurrentBrush.m_ColorLuminanceMin) :
                        m_lastChosenColor;
                    script.SetColor(color);
                }

                script.CreateNewLine(
                    canvas, xfPointer_CS, currentCreator,
                    m_StraightEdgeProxyActive ? m_StraightEdgeProxyBrush : null);
                script.SetControlPoint(xfPointer_CS, isKeeper: true);
            }
        }

        void InitiateLineAt(TrTransform mainPointerXf_CS)
        {
            // Set Main Pointer to transform.
            CanvasScript canvas = App.Scene.ActiveCanvas;
            canvas.AsCanvas[m_MainPointerData.m_Script.transform] = mainPointerXf_CS;

            // Update other pointers.
            UpdateSymmetryPointerTransforms();
            InitiateLine(false);
        }

        // Detach and record lines for all active pointers.
        public void FinalizeLine(bool isContinue = false, bool discard = false)
        {
            PointerScript groupStart = null;
            uint groupStartTime = 0;
            //discard or solidify every pointer's active line
            for (int i = 0; i < m_NumActivePointers; ++i)
            {
                var pointer = m_Pointers[i].m_Script;
                // XXX: when would an active pointer not be creating a line?
                if (pointer.IsCreatingStroke())
                {
                    bool bDiscardLine = discard || pointer.ShouldDiscardCurrentLine();
                    if (bDiscardLine)
                    {
                        pointer.DetachLine(bDiscardLine, null, SketchMemoryScript.StrokeFlags.None);
                    }
                    else
                    {
                        SketchMemoryScript.StrokeFlags flags = SketchMemoryScript.StrokeFlags.None;
                        if (groupStart == null)
                        {
                            groupStart = pointer;
                            // Capture this, because stroke becomes invalid after being detached.
                            groupStartTime = groupStart.TimestampMs;
                        }
                        else
                        {
                            flags |= SketchMemoryScript.StrokeFlags.IsGroupContinue;
                            // Verify IsGroupContinue invariant
                            Debug.Assert(pointer.TimestampMs == groupStartTime);
                        }
                        pointer.DetachLine(bDiscardLine, null, flags);
                    }
                }
            }
        }
    }
} // namespace TiltBrush
