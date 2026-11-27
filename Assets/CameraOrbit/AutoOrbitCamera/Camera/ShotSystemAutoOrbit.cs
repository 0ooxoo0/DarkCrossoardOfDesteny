using System;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Characters;
using GameCreator.Runtime.Common;
using UnityEngine;
using UnityEngine.Serialization;

namespace Arawn.Runtime.AutoOrbitCamera
{
    /// <summary>
    /// GC2 auto-orbit camera tuned for action-combat framing (God of War / Arkham style):
    /// camera auto-stays behind movement, supports temporary manual override,
    /// optional look-ahead, shoulder offset and combat focus blending.
    /// </summary>
    [Serializable]
    public class ShotSystemAutoOrbit : TShotSystem
    {
        public static readonly int ID = nameof(ShotSystemAutoOrbit).GetHashCode();

        private const float MIN_SMOOTH_TIME = 0.01f;
        private const float INPUT_EPSILON = 0.001f;
        private const float DIRECTION_CHANGE_REANCHOR_RATIO = 0.35f;
        private const float MOVE_EXIT_GRACE_TIME = 0.14f;
        private const float MIN_PLANAR_DIRECTION_SPEED = 0.05f;
        private const float OPPOSITE_TURN_LOCK_ANGLE = 150f;
        private const float OPPOSITE_TURN_UNLOCK_ANGLE = 120f;
        private const float OPPOSITE_TURN_MAX_DELTA = 179.5f;
        private const float TURN_SIGN_CAPTURE_ANGLE = 5f;
        private const float TURN_SIGN_VELOCITY_MIN = 1f;
        private const float RETURN_DELAY_REVERSAL_START_ANGLE = 110f;
        private const float RETURN_DELAY_REVERSAL_FULL_ANGLE = 170f;

        [Header("Target")]
        [SerializeField]
        [Tooltip("The GameObject the camera orbits around (typically the player).")]
        private PropertyGetGameObject m_Target = GetGameObjectPlayer.Create();

        [SerializeField]
        [Tooltip("Vertical offset from the target's pivot (shoulder height).")]
        private PropertyGetDecimal m_Lift = GetDecimalDecimal.Create(1.5f);

        [SerializeField]
        [Tooltip("Horizontal offset from the target's pivot (over-the-shoulder framing).")]
        private PropertyGetDecimal m_Shoulder = GetDecimalDecimal.Create(0f);

        [Header("Combat Focus")]
        [SerializeField]
        [Tooltip("Optional combat target used for lock-on style framing.")]
        private PropertyGetGameObject m_CombatTarget = new PropertyGetGameObject();

        [SerializeField]
        [Tooltip("Blend pivot between player and combat target: 0 = player, 1 = target.")]
        [Range(-1f, 1f)]
        private float m_CombatFocusWeight = 0f;

        [SerializeField]
        [Tooltip("Smooth time when blending to combat focus.")]
        private PropertyGetDecimal m_CombatFocusSmoothTime = GetDecimalDecimal.Create(0.2f);

        [Header("Orbit Distance")]
        [SerializeField]
        [Tooltip("Base orbit distance from the pivot.")]
        private PropertyGetDecimal m_Distance = GetDecimalDecimal.Create(6f);

        [Header("Speed Framing")]
        [SerializeField]
        [Tooltip("Increase distance/FOV as movement speed rises (Arkham-style sprint framing).")]
        private bool m_SpeedFramingEnabled = false;

        [SerializeField]
        [Tooltip("Movement speed where speed framing starts.")]
        private PropertyGetDecimal m_SpeedFramingTriggerSpeed = GetDecimalDecimal.Create(6f);

        [SerializeField]
        [Tooltip("Movement speed where speed framing reaches full effect.")]
        private PropertyGetDecimal m_SpeedFramingFullSpeed = GetDecimalDecimal.Create(8.5f);

        [SerializeField]
        [Tooltip("Orbit distance at full speed framing.")]
        private PropertyGetDecimal m_SpeedFramingMaxDistance = GetDecimalDecimal.Create(8.5f);

        [SerializeField]
        [Tooltip("Fade shoulder offset to zero as speed framing ramps in.")]
        private bool m_SpeedFramingNullifyShoulder = true;

        [SerializeField]
        [Tooltip("Apply an optional FOV boost while speed framing is active.")]
        private bool m_SpeedFramingAffectFov = false;

        [SerializeField]
        [Tooltip("Additional FOV at full speed framing.")]
        private PropertyGetDecimal m_SpeedFramingMaxFovBoost = GetDecimalDecimal.Create(8f);

        [SerializeField]
        [Tooltip("Smooth time for speed-framing ramp-in while accelerating.")]
        [FormerlySerializedAs("m_SpeedFramingSmoothTime")]
        private PropertyGetDecimal m_SpeedFramingSmoothInTime = GetDecimalDecimal.Create(0.2f);

        [SerializeField]
        [Tooltip("Smooth time for speed-framing ramp-out while decelerating.")]
        private PropertyGetDecimal m_SpeedFramingSmoothOutTime = GetDecimalDecimal.Create(0.3f);

        [Header("Pitch")]
        [SerializeField]
        [Tooltip("Base pitch in degrees.")]
        private PropertyGetDecimal m_Pitch = GetDecimalDecimal.Create(20f);

        [Header("Auto-Orbit")]
        [SerializeField]
        [Tooltip("Yaw smoothing time for automatic orbiting.")]
        private PropertyGetDecimal m_OrbitSmoothTime = GetDecimalDecimal.Create(0.25f);

        [SerializeField]
        [Tooltip("Delay before auto-orbit takes control when movement/combat direction appears. " +
                 "Helps avoid instant camera turns from short direction taps. 0 = immediate.")]
        private PropertyGetDecimal m_AutoOrbitEngageDelay = GetDecimalDecimal.Create(0f);

        [SerializeField]
        [Tooltip("Blend time for auto-orbit takeover after engage delay has elapsed. " +
                 "Higher values ease in the auto-orbit direction more gradually. 0 = instant.")]
        private PropertyGetDecimal m_AutoOrbitEngageSmoothTime = GetDecimalDecimal.Create(0.2f);

        [SerializeField]
        [Tooltip("Minimum horizontal speed before movement drives auto-orbit.")]
        private PropertyGetDecimal m_DeadZone = GetDecimalDecimal.Create(0.15f);

        [SerializeField]
        [Tooltip("Extra speed required to enter movement mode from idle. " +
                 "Reduces move/idle jitter from tiny velocity spikes.")]
        private PropertyGetDecimal m_DeadZoneHysteresis = GetDecimalDecimal.Create(0.05f);

        [SerializeField]
        [Tooltip("Yaw offset from movement/target direction.")]
        private PropertyGetDecimal m_YawOffset = GetDecimalDecimal.Create(0f);

        [SerializeField]
        [Tooltip("Flip orbit direction by 180 degrees.")]
        private bool m_InvertOrbit = false;

        [SerializeField]
        [Tooltip("Restart auto-orbit delay logic when movement/combat direction changes sharply.")]
        private bool m_ResetDelaysOnDirectionChange = false;

        [SerializeField]
        [Tooltip("Minimum angle in degrees that counts as a direction change for delay reset.")]
        [Range(0f, 180f)]
        private float m_DirectionChangeResetAngle = 85f;

        [SerializeField]
        [Tooltip("After a direction-change reset, block further direction resets for a short window " +
                 "to keep 180 turns as one clean camera arc.")]
        private PropertyGetDecimal m_DirectionChangeResetLockTime = GetDecimalDecimal.Create(0.45f);

        [Header("Return to Forward")]
        [SerializeField]
        [Tooltip("Delay after stop before returning behind target forward. 0 = disabled.")]
        private PropertyGetDecimal m_ReturnDelay = GetDecimalDecimal.Create(1.5f);

        [SerializeField]
        [Tooltip("Blend time for return-to-forward after return delay elapses. 0 = instant.")]
        private PropertyGetDecimal m_ReturnSmoothTime = GetDecimalDecimal.Create(0.2f);

        [SerializeField]
        [Tooltip("Use target forward as idle return yaw.")]
        private bool m_ReturnToTargetForward = true;

        [Header("Idle Cinematic Orbit")]
        [SerializeField]
        [Tooltip("Enable a slow cinematic orbit around the player when idle. " +
                 "Automatically disables Return to Forward while active.")]
        private bool m_IdleOrbitEnabled = false;

        [SerializeField]
        [Tooltip("Seconds the player must be idle before the cinematic orbit begins.")]
        private PropertyGetDecimal m_IdleOrbitDelay = GetDecimalDecimal.Create(5f);

        [SerializeField]
        [Tooltip("Orbit speed in degrees per second while cinematically orbiting.")]
        private PropertyGetDecimal m_IdleOrbitSpeed = GetDecimalDecimal.Create(15f);

        [SerializeField]
        [Tooltip("Smooth time for ramping the cinematic orbit speed in and out.")]
        private PropertyGetDecimal m_IdleOrbitSmoothTime = GetDecimalDecimal.Create(1.5f);

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Print auto-orbit state transitions and delay timing to the Console.")]
        private bool m_DebugLogs = false;

        [SerializeField]
        [Tooltip("Minimum seconds between repeated wait-state debug logs.")]
        [Min(0f)]
        private float m_DebugLogInterval = 0.2f;

        [Header("Manual Override (Optional)")]
        [SerializeField]
        [Tooltip("Optional manual orbit input (right stick / mouse).")]
        private InputPropertyValueVector2 m_InputRotate = new InputPropertyValueVector2();

        [SerializeField]
        [Tooltip("Manual rotation sensitivity.")]
        private PropertyGetDecimal m_ManualSensitivity = GetDecimalDecimal.Create(1f);

        [SerializeField]
        [Tooltip("Allow manual vertical input to adjust pitch.")]
        private bool m_ManualPitchEnabled = false;

        [SerializeField]
        [Tooltip("Invert vertical input for manual pitch.")]
        private bool m_ManualPitchInvert = false;

        [SerializeField]
        [Tooltip("Minimum pitch allowed during manual pitch override.")]
        private PropertyGetDecimal m_ManualPitchMin = GetDecimalDecimal.Create(5f);

        [SerializeField]
        [Tooltip("Maximum pitch allowed during manual pitch override.")]
        private PropertyGetDecimal m_ManualPitchMax = GetDecimalDecimal.Create(60f);

        [SerializeField]
        [Tooltip("Delay after last manual input before auto-orbit takes over again.")]
        private PropertyGetDecimal m_ManualRecoverDelay = GetDecimalDecimal.Create(2f);

        [SerializeField]
        [Tooltip("When the camera orbits to face the front of the player, " +
                 "smoothly nullify the shoulder offset to center the character.")]
        private bool m_ManualFrontCenterEnabled = false;

        [SerializeField]
        [Tooltip("Angle from behind the target where shoulder starts fading (0 = behind, 180 = fully front).")]
        [Range(0f, 180f)]
        private float m_ManualFrontCenterStartAngle = 90f;

        [SerializeField]
        [Tooltip("Angle from behind the target where shoulder is fully zeroed.")]
        [Range(0f, 180f)]
        private float m_ManualFrontCenterFullAngle = 150f;

        [SerializeField]
        [Tooltip("Smooth time for shoulder fade when looking at front of player.")]
        private PropertyGetDecimal m_ManualFrontCenterSmoothTime = GetDecimalDecimal.Create(0.3f);

        [Header("Smoothing")]
        [SerializeField]
        [Tooltip("Smooth time for camera position.")]
        private PropertyGetDecimal m_PositionSmoothTime = GetDecimalDecimal.Create(0.1f);

        [SerializeField]
        [Tooltip("Smooth time for look rotation.")]
        private PropertyGetDecimal m_RotationSmoothTime = GetDecimalDecimal.Create(0.08f);

        [SerializeField]
        [Tooltip("Optional smoothing for the target position before camera logic.")]
        private PropertyGetDecimal m_TargetSmoothTime = GetDecimalDecimal.Create(0f);

        [Header("Look Ahead")]
        [SerializeField]
        [Tooltip("Seconds of velocity projection used for look-ahead. 0 = disabled.")]
        private PropertyGetDecimal m_LookaheadTime = GetDecimalDecimal.Create(0f);

        [SerializeField]
        [Tooltip("Smooth time for look-ahead offset.")]
        private PropertyGetDecimal m_LookaheadSmoothTime = GetDecimalDecimal.Create(0.3f);

        [SerializeField]
        [Tooltip("Clamp for look-ahead distance in world space.")]
        private PropertyGetDecimal m_MaxLookaheadDistance = GetDecimalDecimal.Create(3f);

        [NonSerialized] private ShotSystemZoom m_Zoom;

        [NonSerialized] private float m_CurrentYaw;
        [NonSerialized] private float m_TargetYaw;
        [NonSerialized] private float m_YawVelocity;

        [NonSerialized] private float m_CurrentPitch;
        [NonSerialized] private float m_PitchVelocity;

        [NonSerialized] private Vector3 m_CurrentPosition;
        [NonSerialized] private Vector3 m_PositionVelocity;

        [NonSerialized] private Quaternion m_CurrentRotation;

        [NonSerialized] private Vector3 m_PreviousRawTargetPos;
        [NonSerialized] private bool m_HasPreviousRawTargetPos;
        [NonSerialized] private Vector3 m_RawVelocity;

        [NonSerialized] private Vector3 m_SmoothedTargetPos;
        [NonSerialized] private Vector3 m_TargetPosVelocity;
        [NonSerialized] private bool m_HasSmoothedTargetPos;

        [NonSerialized] private Vector3 m_PreviousTargetPos;
        [NonSerialized] private bool m_HasPreviousTargetPos;

        [NonSerialized] private Vector3 m_CombatFocusPos;
        [NonSerialized] private Vector3 m_CombatFocusVelocity;
        [NonSerialized] private bool m_HasCombatFocusPos;

        [NonSerialized] private Vector3 m_LookaheadOffset;
        [NonSerialized] private Vector3 m_LookaheadVelocity;

        [NonSerialized] private float m_CurrentSpeedFramingWeight;
        [NonSerialized] private float m_SpeedFramingWeightVelocity;
        [NonSerialized] private Camera m_RuntimeCameraComponent;
        [NonSerialized] private float m_RuntimeBaseFov;
        [NonSerialized] private bool m_HasRuntimeBaseFov;
        [NonSerialized] private bool m_HasAppliedSpeedFramingFov;
        [NonSerialized] private float m_LastAppliedSpeedFramingFovOffset;

        [NonSerialized] private float m_LastMoveTime;
        [NonSerialized] private float m_LastManualInputTime;
        [NonSerialized] private float m_ManualPitchOffset;
        [NonSerialized] private float m_AutoOrbitEngageStartTime;
        [NonSerialized] private bool m_HasAutoOrbitEngageTimer;
        [NonSerialized] private float m_AutoOrbitBlendStartTime;
        [NonSerialized] private float m_AutoOrbitBlendFromYaw;
        [NonSerialized] private float m_AutoOrbitBlendTargetYaw;
        [NonSerialized] private bool m_HasAutoOrbitBlend;
        [NonSerialized] private float m_ReturnBlendStartTime;
        [NonSerialized] private float m_ReturnBlendFromYaw;
        [NonSerialized] private bool m_HasReturnBlend;
        [NonSerialized] private Vector3 m_LastAutoDirection;
        [NonSerialized] private bool m_HasLastAutoDirection;
        [NonSerialized] private bool m_DirectionChangeResetAwaitRearm;
        [NonSerialized] private float m_DirectionChangeResetLockUntilTime;
        [NonSerialized] private bool m_IsMoveStateActive;
        [NonSerialized] private float m_LastDebugLogTime;
        [NonSerialized] private bool m_DebugEngageWaiting;
        [NonSerialized] private bool m_DebugReturnWaiting;
        [NonSerialized] private string m_DebugMode;
        [NonSerialized] private Vector3 m_LastPlanarMoveDirection;
        [NonSerialized] private bool m_HasLastPlanarMoveDirection;
        [NonSerialized] private float m_LastResolvedTurnSign;
        [NonSerialized] private bool m_HasLastResolvedTurnSign;
        [NonSerialized] private bool m_HasOppositeTurnSignLock;
        [NonSerialized] private float m_OppositeTurnSignLock;
        [NonSerialized] private float m_DirectionChangeReturnDelayUntilTime;
        [NonSerialized] private bool m_DebugDirectionReturnDelayWaiting;
        [NonSerialized] private bool m_IsIdleOrbitActive;
        [NonSerialized] private float m_IdleOrbitCurrentSpeed;
        [NonSerialized] private float m_IdleOrbitSpeedVelocity;
        [NonSerialized] private bool m_DebugIdleOrbitWaiting;
        [NonSerialized] private float m_FrontCenterWeight;
        [NonSerialized] private float m_FrontCenterWeightVelocity;

        public override int Id => ID;

        [field: NonSerialized] public GameObject Target { get; private set; }
        [field: NonSerialized] public Vector3 PivotPosition { get; private set; }
        [field: NonSerialized] public float Radius { get; private set; }

        public float YawOffset
        {
            set => this.m_YawOffset = new PropertyGetDecimal(value);
        }

        public float OrbitSmoothTime
        {
            set => this.m_OrbitSmoothTime = new PropertyGetDecimal(value);
        }

        public float CombatFocusWeight
        {
            get => this.m_CombatFocusWeight;
            set => this.m_CombatFocusWeight = Mathf.Clamp(value, -1f, 1f);
        }

        public void SetYaw(float yaw)
        {
            yaw = QuaternionUtils.Convert180(yaw);

            this.m_CurrentYaw = yaw;
            this.m_TargetYaw = yaw;
            this.m_YawVelocity = 0f;
        }

        public void ApplyPreset(AutoOrbitCameraPreset preset)
        {
            if (preset == null) return;

            this.m_Lift = preset.lift;
            this.m_Shoulder = preset.shoulder;
            this.m_Distance = preset.distance;
            this.m_SpeedFramingEnabled = preset.speedFramingEnabled;
            this.m_SpeedFramingTriggerSpeed = preset.speedFramingTriggerSpeed;
            this.m_SpeedFramingFullSpeed = preset.speedFramingFullSpeed;
            this.m_SpeedFramingMaxDistance = preset.speedFramingMaxDistance;
            this.m_SpeedFramingNullifyShoulder = preset.speedFramingNullifyShoulder;
            this.m_SpeedFramingAffectFov = preset.speedFramingAffectFov;
            this.m_SpeedFramingMaxFovBoost = preset.speedFramingMaxFovBoost;
            this.m_SpeedFramingSmoothInTime = preset.speedFramingSmoothInTime;
            this.m_SpeedFramingSmoothOutTime = preset.speedFramingSmoothOutTime;

            this.m_Pitch = preset.pitch;
            this.m_OrbitSmoothTime = preset.orbitSmoothTime;
            this.m_AutoOrbitEngageDelay = preset.autoOrbitEngageDelay;
            this.m_AutoOrbitEngageSmoothTime = preset.autoOrbitEngageSmoothTime;
            this.m_DeadZone = preset.deadZone;
            this.m_DeadZoneHysteresis = preset.deadZoneHysteresis;
            this.m_YawOffset = preset.yawOffset;
            this.m_InvertOrbit = preset.invertOrbit;
            this.m_ResetDelaysOnDirectionChange = preset.resetDelaysOnDirectionChange;
            this.m_DirectionChangeResetAngle = preset.directionChangeResetAngle;
            this.m_DirectionChangeResetLockTime = preset.directionChangeResetLockTime;

            this.m_ReturnDelay = preset.returnDelay;
            this.m_ReturnSmoothTime = preset.returnSmoothTime;
            this.m_ReturnToTargetForward = preset.returnToTargetForward;

            this.m_IdleOrbitEnabled = preset.idleOrbitEnabled;
            this.m_IdleOrbitDelay = preset.idleOrbitDelay;
            this.m_IdleOrbitSpeed = preset.idleOrbitSpeed;
            this.m_IdleOrbitSmoothTime = preset.idleOrbitSmoothTime;

            this.m_ManualSensitivity = preset.manualSensitivity;
            this.m_ManualPitchEnabled = preset.manualPitchEnabled;
            this.m_ManualPitchInvert = preset.manualPitchInvert;
            this.m_ManualPitchMin = preset.manualPitchMin;
            this.m_ManualPitchMax = preset.manualPitchMax;
            this.m_ManualRecoverDelay = preset.manualRecoverDelay;
            this.m_ManualFrontCenterEnabled = preset.manualFrontCenterEnabled;
            this.m_ManualFrontCenterStartAngle = preset.manualFrontCenterStartAngle;
            this.m_ManualFrontCenterFullAngle = preset.manualFrontCenterFullAngle;
            this.m_ManualFrontCenterSmoothTime = preset.manualFrontCenterSmoothTime;

            this.m_PositionSmoothTime = preset.positionSmoothTime;
            this.m_RotationSmoothTime = preset.rotationSmoothTime;
            this.m_TargetSmoothTime = preset.targetSmoothTime;

            this.m_LookaheadTime = preset.lookaheadTime;
            this.m_LookaheadSmoothTime = preset.lookaheadSmoothTime;
            this.m_MaxLookaheadDistance = preset.maxLookaheadDistance;

            this.m_CombatFocusWeight = Mathf.Clamp(preset.combatFocusWeight, -1f, 1f);
            this.m_CombatFocusSmoothTime = preset.combatFocusSmoothTime;
        }

        public void CapturePreset(AutoOrbitCameraPreset preset)
        {
            if (preset == null) return;

            preset.lift = this.m_Lift;
            preset.shoulder = this.m_Shoulder;
            preset.distance = this.m_Distance;
            preset.speedFramingEnabled = this.m_SpeedFramingEnabled;
            preset.speedFramingTriggerSpeed = this.m_SpeedFramingTriggerSpeed;
            preset.speedFramingFullSpeed = this.m_SpeedFramingFullSpeed;
            preset.speedFramingMaxDistance = this.m_SpeedFramingMaxDistance;
            preset.speedFramingNullifyShoulder = this.m_SpeedFramingNullifyShoulder;
            preset.speedFramingAffectFov = this.m_SpeedFramingAffectFov;
            preset.speedFramingMaxFovBoost = this.m_SpeedFramingMaxFovBoost;
            preset.speedFramingSmoothInTime = this.m_SpeedFramingSmoothInTime;
            preset.speedFramingSmoothOutTime = this.m_SpeedFramingSmoothOutTime;

            preset.pitch = this.m_Pitch;
            preset.orbitSmoothTime = this.m_OrbitSmoothTime;
            preset.autoOrbitEngageDelay = this.m_AutoOrbitEngageDelay;
            preset.autoOrbitEngageSmoothTime = this.m_AutoOrbitEngageSmoothTime;
            preset.deadZone = this.m_DeadZone;
            preset.deadZoneHysteresis = this.m_DeadZoneHysteresis;
            preset.yawOffset = this.m_YawOffset;
            preset.invertOrbit = this.m_InvertOrbit;
            preset.resetDelaysOnDirectionChange = this.m_ResetDelaysOnDirectionChange;
            preset.directionChangeResetAngle = this.m_DirectionChangeResetAngle;
            preset.directionChangeResetLockTime = this.m_DirectionChangeResetLockTime;

            preset.returnDelay = this.m_ReturnDelay;
            preset.returnSmoothTime = this.m_ReturnSmoothTime;
            preset.returnToTargetForward = this.m_ReturnToTargetForward;

            preset.idleOrbitEnabled = this.m_IdleOrbitEnabled;
            preset.idleOrbitDelay = this.m_IdleOrbitDelay;
            preset.idleOrbitSpeed = this.m_IdleOrbitSpeed;
            preset.idleOrbitSmoothTime = this.m_IdleOrbitSmoothTime;

            preset.manualSensitivity = this.m_ManualSensitivity;
            preset.manualPitchEnabled = this.m_ManualPitchEnabled;
            preset.manualPitchInvert = this.m_ManualPitchInvert;
            preset.manualPitchMin = this.m_ManualPitchMin;
            preset.manualPitchMax = this.m_ManualPitchMax;
            preset.manualRecoverDelay = this.m_ManualRecoverDelay;
            preset.manualFrontCenterEnabled = this.m_ManualFrontCenterEnabled;
            preset.manualFrontCenterStartAngle = this.m_ManualFrontCenterStartAngle;
            preset.manualFrontCenterFullAngle = this.m_ManualFrontCenterFullAngle;
            preset.manualFrontCenterSmoothTime = this.m_ManualFrontCenterSmoothTime;

            preset.positionSmoothTime = this.m_PositionSmoothTime;
            preset.rotationSmoothTime = this.m_RotationSmoothTime;
            preset.targetSmoothTime = this.m_TargetSmoothTime;

            preset.lookaheadTime = this.m_LookaheadTime;
            preset.lookaheadSmoothTime = this.m_LookaheadSmoothTime;
            preset.maxLookaheadDistance = this.m_MaxLookaheadDistance;

            preset.combatFocusWeight = Mathf.Clamp(this.m_CombatFocusWeight, -1f, 1f);
            preset.combatFocusSmoothTime = this.m_CombatFocusSmoothTime;

            preset.Clamp();
        }

        public override void OnAwake(TShotType shotType)
        {
            base.OnAwake(shotType);

            this.m_Zoom = shotType.GetSystem(ShotSystemZoom.ID) as ShotSystemZoom;
            this.m_InputRotate?.OnStartup();
        }

        public override void OnDestroy(TShotType shotType)
        {
            base.OnDestroy(shotType);
            this.RestoreSpeedFramingFov();
            this.m_InputRotate?.OnDispose();
        }

        public override void OnDisable(TShotType shotType, TCamera camera)
        {
            base.OnDisable(shotType, camera);
            this.RestoreSpeedFramingFov();
        }

        public override void OnEnable(TShotType shotType, TCamera camera)
        {
            base.OnEnable(shotType, camera);

            float cameraYaw = QuaternionUtils.Convert180(camera.transform.eulerAngles.y);
            float cameraPitch = QuaternionUtils.Convert180(camera.transform.eulerAngles.x);

            this.m_CurrentYaw = cameraYaw;
            this.m_TargetYaw = cameraYaw;
            this.m_YawVelocity = 0f;

            this.m_CurrentPitch = cameraPitch;
            this.m_PitchVelocity = 0f;

            this.m_CurrentPosition = camera.transform.position;
            this.m_PositionVelocity = Vector3.zero;

            this.m_CurrentRotation = camera.transform.rotation;

            this.m_PreviousRawTargetPos = Vector3.zero;
            this.m_HasPreviousRawTargetPos = false;
            this.m_RawVelocity = Vector3.zero;

            this.m_SmoothedTargetPos = Vector3.zero;
            this.m_TargetPosVelocity = Vector3.zero;
            this.m_HasSmoothedTargetPos = false;

            this.m_PreviousTargetPos = Vector3.zero;
            this.m_HasPreviousTargetPos = false;

            this.m_CombatFocusPos = Vector3.zero;
            this.m_CombatFocusVelocity = Vector3.zero;
            this.m_HasCombatFocusPos = false;

            this.m_LookaheadOffset = Vector3.zero;
            this.m_LookaheadVelocity = Vector3.zero;
            this.m_CurrentSpeedFramingWeight = 0f;
            this.m_SpeedFramingWeightVelocity = 0f;

            this.m_RuntimeCameraComponent = camera != null ? camera.Get<Camera>() : null;
            this.m_HasRuntimeBaseFov = this.m_RuntimeCameraComponent != null;
            this.m_RuntimeBaseFov = this.m_HasRuntimeBaseFov
                ? this.m_RuntimeCameraComponent.fieldOfView
                : 0f;
            this.m_HasAppliedSpeedFramingFov = false;
            this.m_LastAppliedSpeedFramingFovOffset = 0f;

            this.m_LastMoveTime = float.NegativeInfinity;
            this.m_LastManualInputTime = float.NegativeInfinity;
            this.m_ManualPitchOffset = 0f;
            this.m_AutoOrbitEngageStartTime = 0f;
            this.m_HasAutoOrbitEngageTimer = false;
            this.m_AutoOrbitBlendStartTime = 0f;
            this.m_AutoOrbitBlendFromYaw = 0f;
            this.m_AutoOrbitBlendTargetYaw = 0f;
            this.m_HasAutoOrbitBlend = false;
            this.m_ReturnBlendStartTime = 0f;
            this.m_ReturnBlendFromYaw = 0f;
            this.m_HasReturnBlend = false;
            this.m_LastAutoDirection = Vector3.zero;
            this.m_HasLastAutoDirection = false;
            this.m_DirectionChangeResetAwaitRearm = false;
            this.m_DirectionChangeResetLockUntilTime = 0f;
            this.m_IsMoveStateActive = false;
            this.m_LastDebugLogTime = float.NegativeInfinity;
            this.m_DebugEngageWaiting = false;
            this.m_DebugReturnWaiting = false;
            this.m_DebugMode = string.Empty;
            this.m_LastPlanarMoveDirection = Vector3.zero;
            this.m_HasLastPlanarMoveDirection = false;
            this.m_LastResolvedTurnSign = 0f;
            this.m_HasLastResolvedTurnSign = false;
            this.m_HasOppositeTurnSignLock = false;
            this.m_OppositeTurnSignLock = 0f;
            this.m_DirectionChangeReturnDelayUntilTime = 0f;
            this.m_DebugDirectionReturnDelayWaiting = false;
            this.m_IsIdleOrbitActive = false;
            this.m_IdleOrbitCurrentSpeed = 0f;
            this.m_IdleOrbitSpeedVelocity = 0f;
            this.m_DebugIdleOrbitWaiting = false;
            this.m_FrontCenterWeight = 0f;
            this.m_FrontCenterWeightVelocity = 0f;

            if (this.m_DebugLogs)
            {
                float engageDelay = Mathf.Max(0f, (float)this.m_AutoOrbitEngageDelay.Get(shotType.Args));
                float engageSmooth = Mathf.Max(0f, (float)this.m_AutoOrbitEngageSmoothTime.Get(shotType.Args));
                float returnDelay = Mathf.Max(0f, (float)this.m_ReturnDelay.Get(shotType.Args));
                float returnSmooth = Mathf.Max(0f, (float)this.m_ReturnSmoothTime.Get(shotType.Args));
                float deadZoneExit = Mathf.Max(0f, (float)this.m_DeadZone.Get(shotType.Args));
                float deadZoneEnter = deadZoneExit + Mathf.Max(0f, (float)this.m_DeadZoneHysteresis.Get(shotType.Args));
                float directionResetLockTime = Mathf.Max(0f, (float)this.m_DirectionChangeResetLockTime.Get(shotType.Args));
                float speedFramingStart = Mathf.Max(0f, (float)this.m_SpeedFramingTriggerSpeed.Get(shotType.Args));
                float speedFramingFull = Mathf.Max(speedFramingStart, (float)this.m_SpeedFramingFullSpeed.Get(shotType.Args));
                float speedFramingDistance = Mathf.Max(0f, (float)this.m_SpeedFramingMaxDistance.Get(shotType.Args));
                float speedFramingFov = Mathf.Max(0f, (float)this.m_SpeedFramingMaxFovBoost.Get(shotType.Args));
                float directionResetEffectiveLockTime = Mathf.Max(
                    directionResetLockTime,
                    engageDelay + engageSmooth
                );

                this.DebugLog(
                    "Enabled " +
                    $"engageDelay={engageDelay:F2}s " +
                    $"engageSmooth={engageSmooth:F2}s " +
                    $"returnDelay={returnDelay:F2}s " +
                    $"returnSmooth={returnSmooth:F2}s " +
                    $"deadZoneEnter={deadZoneEnter:F2} " +
                    $"deadZoneExit={deadZoneExit:F2} " +
                    $"moveExitGrace={MOVE_EXIT_GRACE_TIME:F2}s " +
                    $"reversalDelayAngles={RETURN_DELAY_REVERSAL_START_ANGLE:F0}/{RETURN_DELAY_REVERSAL_FULL_ANGLE:F0} " +
                    $"speedFraming={this.m_SpeedFramingEnabled} " +
                    $"speedRange={speedFramingStart:F1}-{speedFramingFull:F1} " +
                    $"speedDistance={speedFramingDistance:F2} " +
                    $"speedFov={speedFramingFov:F1} " +
                    $"resetOnDirChange={this.m_ResetDelaysOnDirectionChange} " +
                    $"dirAngle={this.m_DirectionChangeResetAngle:F1} " +
                    $"dirResetLockBase={directionResetLockTime:F2}s " +
                    $"dirResetLockEffective={directionResetEffectiveLockTime:F2}s"
                );
            }
        }

        public override void OnUpdate(TShotType shotType)
        {
            base.OnUpdate(shotType);

            float dt = shotType.ShotCamera.TimeMode.DeltaTime;
            float time = shotType.ShotCamera.TimeMode.Time;

            this.Target = this.m_Target.Get(shotType.Args);
            if (this.Target == null) return;

            Vector3 rawTargetPos = this.Target.transform.position;
            this.UpdateRawVelocity(rawTargetPos, dt);

            Vector3 targetPos = this.ResolveTargetPosition(rawTargetPos, dt, shotType);
            Vector3 planarVelocity = this.ResolvePlanarVelocity(targetPos, dt);
            float planarSpeed = planarVelocity.magnitude;

            this.UpdateSpeedFraming(shotType, planarSpeed, dt);

            bool hasManualInput = this.ProcessManualInput(shotType, time);
            bool manualRecoveryActive = this.IsManualRecoverActive(shotType, time);
            bool manualOverrideActive = hasManualInput || manualRecoveryActive;

            GameObject combatTarget = this.m_CombatTarget.Get(shotType.Args);

            if (!manualOverrideActive)
            {
                this.UpdateAutoYaw(shotType, combatTarget, targetPos, planarVelocity, planarSpeed, time);
            }
            else
            {
                this.DebugLogMode(
                    "MANUAL_OVERRIDE",
                    time,
                    $"manualInput={hasManualInput} recoverActive={manualRecoveryActive}"
                );
                this.m_IsMoveStateActive = false;
                this.ResetAutoOrbitEngageDelay("manual override active", time);
                this.ResetReturnYawBlend("manual override active", time);
                this.m_DebugReturnWaiting = false;
                this.ResetDirectionChangeTracking();
                this.ResetIdleOrbit("manual override active", time);
            }

            this.SmoothYaw(shotType, dt);
            this.SmoothPitch(shotType, dt, manualOverrideActive);
            this.UpdateFrontFaceCenter(shotType, dt);

            Vector3 focusPos = this.ResolveCombatFocus(shotType, targetPos, combatTarget, dt);
            this.UpdateLookahead(shotType, dt);

            this.UpdatePivot(shotType, focusPos);
            this.UpdateRadius(shotType);
            this.UpdateCameraPosition(shotType, dt);
            this.UpdateCameraRotation(shotType, dt);
        }

        public override void OnDrawGizmosSelected(TShotType shotType, Transform transform)
        {
            base.OnDrawGizmosSelected(shotType, transform);

            if (!Application.isPlaying) return;
            if (this.Target == null) return;

            Gizmos.color = GIZMOS_COLOR_ACTIVE;
            Gizmos.DrawWireSphere(this.PivotPosition, 0.15f);
            Gizmos.DrawLine(shotType.Position, this.PivotPosition);
        }

        private void UpdateRawVelocity(Vector3 rawTargetPos, float deltaTime)
        {
            if (this.m_HasPreviousRawTargetPos && deltaTime > float.Epsilon)
            {
                this.m_RawVelocity = (rawTargetPos - this.m_PreviousRawTargetPos) / deltaTime;
                this.m_RawVelocity.y = 0f;
            }
            else
            {
                this.m_RawVelocity = Vector3.zero;
            }

            this.m_PreviousRawTargetPos = rawTargetPos;
            this.m_HasPreviousRawTargetPos = true;
        }

        private Vector3 ResolveTargetPosition(Vector3 rawTargetPos, float deltaTime, TShotType shotType)
        {
            float smoothTime = (float)this.m_TargetSmoothTime.Get(shotType.Args);
            if (smoothTime <= float.Epsilon || deltaTime <= float.Epsilon)
            {
                this.m_SmoothedTargetPos = rawTargetPos;
                this.m_TargetPosVelocity = Vector3.zero;
                this.m_HasSmoothedTargetPos = true;
                return rawTargetPos;
            }

            if (!this.m_HasSmoothedTargetPos)
            {
                this.m_SmoothedTargetPos = rawTargetPos;
                this.m_TargetPosVelocity = Vector3.zero;
                this.m_HasSmoothedTargetPos = true;
            }

            this.m_SmoothedTargetPos = Vector3.SmoothDamp(
                this.m_SmoothedTargetPos,
                rawTargetPos,
                ref this.m_TargetPosVelocity,
                Mathf.Max(MIN_SMOOTH_TIME, smoothTime),
                Mathf.Infinity,
                deltaTime
            );

            return this.m_SmoothedTargetPos;
        }

        private Vector3 ResolvePlanarVelocity(Vector3 targetPos, float deltaTime)
        {
            if (this.m_HasPreviousTargetPos && deltaTime > float.Epsilon)
            {
                Vector3 velocity = (targetPos - this.m_PreviousTargetPos) / deltaTime;
                velocity.y = 0f;

                this.m_PreviousTargetPos = targetPos;
                return velocity;
            }

            this.m_PreviousTargetPos = targetPos;
            this.m_HasPreviousTargetPos = true;
            return Vector3.zero;
        }

        private bool ProcessManualInput(TShotType shotType, float time)
        {
            if (this.m_InputRotate == null || !shotType.IsActive)
            {
                return false;
            }

            this.m_InputRotate.OnUpdate();
            Vector2 input = this.m_InputRotate.Read();

            if (input.sqrMagnitude < INPUT_EPSILON)
            {
                return false;
            }

            float sensitivity = (float)this.m_ManualSensitivity.Get(shotType.Args);

            this.m_TargetYaw += input.x * sensitivity;
            this.m_TargetYaw = QuaternionUtils.Convert180(this.m_TargetYaw);

            if (this.m_ManualPitchEnabled)
            {
                float basePitch = (float)this.m_Pitch.Get(shotType.Args);
                float minPitch = (float)this.m_ManualPitchMin.Get(shotType.Args);
                float maxPitch = (float)this.m_ManualPitchMax.Get(shotType.Args);

                float inputY = input.y * (this.m_ManualPitchInvert ? -1f : 1f);
                this.m_ManualPitchOffset -= inputY * sensitivity;

                float minOffset = minPitch - basePitch;
                float maxOffset = maxPitch - basePitch;
                this.m_ManualPitchOffset = Mathf.Clamp(this.m_ManualPitchOffset, minOffset, maxOffset);
            }

            this.m_LastManualInputTime = time;
            return true;
        }

        private bool IsManualRecoverActive(TShotType shotType, float time)
        {
            float recoverDelay = Mathf.Max(0f, (float)this.m_ManualRecoverDelay.Get(shotType.Args));
            if (recoverDelay <= float.Epsilon) return false;

            return (time - this.m_LastManualInputTime) < recoverDelay;
        }

        private void UpdateSpeedFraming(TShotType shotType, float planarSpeed, float deltaTime)
        {
            float targetWeight = 0f;

            if (this.m_SpeedFramingEnabled)
            {
                float startSpeed = Mathf.Max(0f, (float)this.m_SpeedFramingTriggerSpeed.Get(shotType.Args));
                float fullSpeed = Mathf.Max(0f, (float)this.m_SpeedFramingFullSpeed.Get(shotType.Args));

                if (fullSpeed <= startSpeed + float.Epsilon)
                {
                    targetWeight = planarSpeed >= startSpeed ? 1f : 0f;
                }
                else
                {
                    targetWeight = Mathf.InverseLerp(startSpeed, fullSpeed, planarSpeed);
                }
            }

            bool isRampingIn = targetWeight >= this.m_CurrentSpeedFramingWeight;
            float smoothTime = isRampingIn
                ? Mathf.Max(0f, (float)this.m_SpeedFramingSmoothInTime.Get(shotType.Args))
                : Mathf.Max(0f, (float)this.m_SpeedFramingSmoothOutTime.Get(shotType.Args));
            if (deltaTime <= float.Epsilon || smoothTime <= float.Epsilon)
            {
                this.m_CurrentSpeedFramingWeight = targetWeight;
                this.m_SpeedFramingWeightVelocity = 0f;
            }
            else
            {
                this.m_CurrentSpeedFramingWeight = Mathf.SmoothDamp(
                    this.m_CurrentSpeedFramingWeight,
                    targetWeight,
                    ref this.m_SpeedFramingWeightVelocity,
                    Mathf.Max(MIN_SMOOTH_TIME, smoothTime),
                    Mathf.Infinity,
                    deltaTime
                );
            }

            this.m_CurrentSpeedFramingWeight = Mathf.Clamp01(this.m_CurrentSpeedFramingWeight);
            this.UpdateSpeedFramingFov(shotType);
        }

        private void UpdateSpeedFramingFov(TShotType shotType)
        {
            if (this.m_RuntimeCameraComponent == null) return;

            float currentFov = this.m_RuntimeCameraComponent.fieldOfView;
            this.m_RuntimeBaseFov = this.m_HasAppliedSpeedFramingFov
                ? currentFov - this.m_LastAppliedSpeedFramingFovOffset
                : currentFov;
            this.m_HasRuntimeBaseFov = true;

            if (!this.m_SpeedFramingEnabled || !this.m_SpeedFramingAffectFov)
            {
                this.m_HasAppliedSpeedFramingFov = false;
                this.m_LastAppliedSpeedFramingFovOffset = 0f;
                return;
            }

            float maxFovBoost = Mathf.Max(0f, (float)this.m_SpeedFramingMaxFovBoost.Get(shotType.Args));
            float offset = maxFovBoost * this.m_CurrentSpeedFramingWeight;
            float desiredFov = this.m_RuntimeBaseFov + offset;
            this.m_RuntimeCameraComponent.fieldOfView = desiredFov;
            this.m_HasAppliedSpeedFramingFov = true;
            this.m_LastAppliedSpeedFramingFovOffset = offset;
        }

        private void RestoreSpeedFramingFov()
        {
            if (this.m_RuntimeCameraComponent == null) return;
            if (!this.m_HasRuntimeBaseFov) return;

            if (this.m_HasAppliedSpeedFramingFov)
            {
                this.m_RuntimeCameraComponent.fieldOfView = this.m_RuntimeBaseFov;
            }

            this.m_HasAppliedSpeedFramingFov = false;
            this.m_LastAppliedSpeedFramingFovOffset = 0f;
            this.m_RuntimeCameraComponent = null;
            this.m_HasRuntimeBaseFov = false;
            this.m_RuntimeBaseFov = 0f;
        }

        private void UpdateAutoYaw(
            TShotType shotType,
            GameObject combatTarget,
            Vector3 targetPos,
            Vector3 planarVelocity,
            float planarSpeed,
            float time)
        {
            float yawOffset = (float)this.m_YawOffset.Get(shotType.Args);
            float orbitOffset = yawOffset + (this.m_InvertOrbit ? 180f : 0f);
            float deadZoneExit = Mathf.Max(0f, (float)this.m_DeadZone.Get(shotType.Args));
            float deadZoneEnter = deadZoneExit + Mathf.Max(0f, (float)this.m_DeadZoneHysteresis.Get(shotType.Args));
            float engageDelay = Mathf.Max(0f, (float)this.m_AutoOrbitEngageDelay.Get(shotType.Args));
            float engageSmooth = Mathf.Max(0f, (float)this.m_AutoOrbitEngageSmoothTime.Get(shotType.Args));
            float returnDelay = Mathf.Max(0f, (float)this.m_ReturnDelay.Get(shotType.Args));
            float directionResetLockBaseTime = Mathf.Max(0f, (float)this.m_DirectionChangeResetLockTime.Get(shotType.Args));
            float directionResetLockTime = Mathf.Max(
                directionResetLockBaseTime,
                engageDelay + engageSmooth
            );

            if (combatTarget != null)
            {
                this.DebugLogMode("COMBAT", time, $"planarSpeed={planarSpeed:F2}");
                Vector3 toCombat = combatTarget.transform.position - targetPos;
                toCombat.y = 0f;

                if (toCombat.sqrMagnitude > 0.0001f)
                {
                    this.HandleDirectionChangeReset(toCombat, time, directionResetLockTime, returnDelay);

                    float combatYaw = Mathf.Atan2(toCombat.x, toCombat.z) * Mathf.Rad2Deg;
                    float combatDesiredYaw = this.ResolveContinuousYaw(combatYaw + orbitOffset);
                    this.ApplyAutoYawWithEngageDelay(shotType, combatDesiredYaw, time);
                }

                this.m_LastMoveTime = time;
                this.m_IsMoveStateActive = false;
                this.m_DebugReturnWaiting = false;
                this.ResetReturnYawBlend("combat orbit active", time);
                this.ResetIdleOrbit("combat orbit active", time);
                return;
            }

            bool isMoveDetected = this.m_IsMoveStateActive
                ? planarSpeed > deadZoneExit
                : planarSpeed > deadZoneEnter;
            bool moveGraceActive = false;

            if (!isMoveDetected && this.m_IsMoveStateActive)
            {
                float sinceMove = time - this.m_LastMoveTime;
                if (sinceMove >= 0f && sinceMove < MOVE_EXIT_GRACE_TIME)
                {
                    isMoveDetected = true;
                    moveGraceActive = true;
                }
            }

            if (isMoveDetected)
            {
                Vector3 autoDirection = planarVelocity;
                if (planarSpeed > MIN_PLANAR_DIRECTION_SPEED)
                {
                    this.m_LastPlanarMoveDirection = planarVelocity.normalized;
                    this.m_HasLastPlanarMoveDirection = true;
                }
                else if (this.m_HasLastPlanarMoveDirection)
                {
                    autoDirection = this.m_LastPlanarMoveDirection;
                }
                else
                {
                    Vector3 fallbackDirection = this.Target.transform.forward;
                    fallbackDirection.y = 0f;
                    if (fallbackDirection.sqrMagnitude > 0.0001f)
                    {
                        autoDirection = fallbackDirection.normalized;
                        this.m_LastPlanarMoveDirection = autoDirection;
                        this.m_HasLastPlanarMoveDirection = true;
                    }
                }

                this.DebugLogMode(
                    "MOVE",
                    time,
                    $"speed={planarSpeed:F2} enter={deadZoneEnter:F2} exit={deadZoneExit:F2} " +
                    $"wasActive={this.m_IsMoveStateActive} grace={moveGraceActive}"
                );
                this.HandleDirectionChangeReset(autoDirection, time, directionResetLockTime, returnDelay);

                float movementYaw = Mathf.Atan2(autoDirection.x, autoDirection.z) * Mathf.Rad2Deg;
                float movementDesiredYaw = this.ResolveContinuousYaw(movementYaw + orbitOffset);
                this.ApplyAutoYawWithEngageDelay(shotType, movementDesiredYaw, time);

                if (!moveGraceActive)
                {
                    this.m_LastMoveTime = time;
                }

                this.m_IsMoveStateActive = true;
                this.m_DebugReturnWaiting = false;
                this.ResetReturnYawBlend("movement orbit active", time);
                this.ResetIdleOrbit("movement orbit active", time);
                return;
            }

            float timeSinceMove = time - this.m_LastMoveTime;
            this.DebugLogMode(
                "IDLE",
                time,
                $"speed={planarSpeed:F2} enter={deadZoneEnter:F2} exit={deadZoneExit:F2} " +
                $"wasActive={this.m_IsMoveStateActive} sinceMove={timeSinceMove:F2}s"
            );

            this.m_IsMoveStateActive = false;
            this.ResetAutoOrbitEngageDelay("idle/no auto direction", time);
            this.ResetDirectionChangeTracking();

            float idleOrbitDelay = this.m_IdleOrbitEnabled
                ? Mathf.Max(0f, (float)this.m_IdleOrbitDelay.Get(shotType.Args))
                : 0f;

            if (this.m_IdleOrbitEnabled && idleOrbitDelay > float.Epsilon && timeSinceMove >= idleOrbitDelay)
            {
                if (!this.m_IsIdleOrbitActive)
                {
                    this.m_IsIdleOrbitActive = true;
                    this.DebugLog(
                        $"Idle cinematic orbit started (sinceMove={timeSinceMove:F2}s >= delay={idleOrbitDelay:F2}s)"
                    );
                }

                this.m_DebugReturnWaiting = false;
                this.ResetReturnYawBlend("idle cinematic orbit active", time);

                float targetSpeed = (float)this.m_IdleOrbitSpeed.Get(shotType.Args);
                float rampSmooth = Mathf.Max(MIN_SMOOTH_TIME, (float)this.m_IdleOrbitSmoothTime.Get(shotType.Args));
                float dt = shotType.ShotCamera.TimeMode.DeltaTime;

                this.m_IdleOrbitCurrentSpeed = Mathf.SmoothDamp(
                    this.m_IdleOrbitCurrentSpeed,
                    targetSpeed,
                    ref this.m_IdleOrbitSpeedVelocity,
                    rampSmooth,
                    Mathf.Infinity,
                    dt
                );

                this.m_TargetYaw += this.m_IdleOrbitCurrentSpeed * dt;
                this.m_TargetYaw = QuaternionUtils.Convert180(this.m_TargetYaw);
                return;
            }

            if (this.m_IdleOrbitEnabled && idleOrbitDelay > float.Epsilon && timeSinceMove < idleOrbitDelay)
            {
                float remaining = idleOrbitDelay - timeSinceMove;
                if (!this.m_DebugIdleOrbitWaiting)
                {
                    this.DebugLog(
                        $"Idle orbit waiting started remaining={remaining:F2}s " +
                        $"(sinceMove={timeSinceMove:F2}s delay={idleOrbitDelay:F2}s)"
                    );
                    this.m_DebugIdleOrbitWaiting = true;
                }
                else
                {
                    this.DebugLogThrottled(
                        time,
                        $"Idle orbit waiting remaining={remaining:F2}s (sinceMove={timeSinceMove:F2}s)"
                    );
                }
            }

            if (returnDelay <= float.Epsilon)
            {
                this.m_DebugReturnWaiting = false;
                this.DebugLogThrottled(time, "Return delay disabled (<= 0).");
                this.ResetReturnYawBlend("return delay disabled", time);
                return;
            }

            if (timeSinceMove < returnDelay)
            {
                float remaining = returnDelay - timeSinceMove;
                if (!this.m_DebugReturnWaiting)
                {
                    this.DebugLog(
                        $"Return waiting started remaining={remaining:F2}s " +
                        $"(sinceMove={timeSinceMove:F2}s delay={returnDelay:F2}s)"
                    );
                    this.m_DebugReturnWaiting = true;
                }
                else
                {
                    this.DebugLogThrottled(
                        time,
                        $"Return waiting remaining={remaining:F2}s (sinceMove={timeSinceMove:F2}s)"
                    );
                }

                this.ResetReturnYawBlend("waiting for return delay", time, true);
                return;
            }

            if (this.m_DebugReturnWaiting)
            {
                this.DebugLog(
                    $"Return delay elapsed (sinceMove={timeSinceMove:F2}s >= delay={returnDelay:F2}s)"
                );
                this.m_DebugReturnWaiting = false;
            }

            if (!this.m_ReturnToTargetForward || this.Target == null)
            {
                this.DebugLogThrottled(
                    time,
                    $"Return blocked returnToTargetForward={this.m_ReturnToTargetForward} targetNull={this.Target == null}"
                );
                this.ResetReturnYawBlend("return blocked by settings/target", time);
                return;
            }

            float forwardYaw = this.Target.transform.eulerAngles.y;
            float returnDesiredYaw = this.ResolveContinuousYaw(forwardYaw + orbitOffset);
            this.ApplyReturnYawWithSmooth(shotType, returnDesiredYaw, time);
        }

        private void ApplyAutoYawWithEngageDelay(TShotType shotType, float desiredYaw, float time)
        {
            if (time < this.m_DirectionChangeReturnDelayUntilTime)
            {
                float remaining = this.m_DirectionChangeReturnDelayUntilTime - time;
                if (!this.m_DebugDirectionReturnDelayWaiting)
                {
                    this.DebugLog(
                        $"Direction reversal delay waiting started remaining={remaining:F2}s"
                    );
                    this.m_DebugDirectionReturnDelayWaiting = true;
                }
                else
                {
                    this.DebugLogThrottled(
                        time,
                        $"Direction reversal delay waiting remaining={remaining:F2}s"
                    );
                }

                return;
            }

            if (this.m_DirectionChangeReturnDelayUntilTime > 0f)
            {
                if (this.m_DebugDirectionReturnDelayWaiting)
                {
                    this.DebugLog("Direction reversal delay elapsed.");
                }

                this.m_DirectionChangeReturnDelayUntilTime = 0f;
                this.m_DebugDirectionReturnDelayWaiting = false;
            }

            float engageDelay = Mathf.Max(0f, (float)this.m_AutoOrbitEngageDelay.Get(shotType.Args));
            if (engageDelay <= float.Epsilon)
            {
                if (this.m_DebugEngageWaiting)
                {
                    this.DebugLog("Engage delay cleared because engageDelay <= 0.");
                    this.m_DebugEngageWaiting = false;
                }

                this.m_TargetYaw = desiredYaw;
                this.ResetAutoOrbitEngageDelay("engageDelay <= 0 (immediate)", time);
                return;
            }

            if (!this.m_HasAutoOrbitEngageTimer)
            {
                this.m_HasAutoOrbitEngageTimer = true;
                this.m_AutoOrbitEngageStartTime = time;
                this.m_HasAutoOrbitBlend = false;
                this.DebugLog(
                    $"Engage timer started delay={engageDelay:F2}s " +
                    $"desiredYaw={QuaternionUtils.Convert180(desiredYaw):F1}"
                );
            }

            float engageElapsed = time - this.m_AutoOrbitEngageStartTime;
            if (engageElapsed < engageDelay)
            {
                float remaining = engageDelay - engageElapsed;
                if (!this.m_DebugEngageWaiting)
                {
                    this.DebugLog(
                        $"Engage waiting started remaining={remaining:F2}s " +
                        $"(delay={engageDelay:F2}s)"
                    );
                    this.m_DebugEngageWaiting = true;
                }
                else
                {
                    this.DebugLogThrottled(
                        time,
                        $"Engage waiting remaining={remaining:F2}s"
                    );
                }

                return;
            }

            if (this.m_DebugEngageWaiting)
            {
                this.DebugLog(
                    $"Engage delay elapsed (elapsed={engageElapsed:F2}s >= delay={engageDelay:F2}s)"
                );
                this.m_DebugEngageWaiting = false;
            }

            float engageSmooth = Mathf.Max(0f, (float)this.m_AutoOrbitEngageSmoothTime.Get(shotType.Args));
            if (engageSmooth <= float.Epsilon)
            {
                this.m_TargetYaw = desiredYaw;
                this.DebugLogThrottled(time, "Engage apply immediate after delay (engageSmooth <= 0).");
                return;
            }

            if (!this.m_HasAutoOrbitBlend)
            {
                this.m_HasAutoOrbitBlend = true;
                this.m_AutoOrbitBlendStartTime = time;
                this.m_AutoOrbitBlendFromYaw = this.m_CurrentYaw;
                this.m_AutoOrbitBlendTargetYaw = desiredYaw;
                this.DebugLog(
                    $"Engage blend started smooth={engageSmooth:F2}s " +
                    $"from={QuaternionUtils.Convert180(this.m_AutoOrbitBlendFromYaw):F1} " +
                    $"to={QuaternionUtils.Convert180(this.m_AutoOrbitBlendTargetYaw):F1}"
                );
            }

            float t = Mathf.Clamp01((time - this.m_AutoOrbitBlendStartTime) / engageSmooth);
            if (t < 1f)
            {
                // Keep the blend endpoint fixed during takeover so 180 reversals don't
                // oscillate when desiredYaw jitters between left/right equivalents.
                float blendedYaw = Mathf.LerpAngle(
                    this.m_AutoOrbitBlendFromYaw,
                    this.m_AutoOrbitBlendTargetYaw,
                    t
                );
                this.m_TargetYaw = blendedYaw;
                return;
            }

            this.m_TargetYaw = desiredYaw;
        }

        private void ResetAutoOrbitEngageDelay(string reason = "", float time = float.NaN)
        {
            bool hadState = this.m_HasAutoOrbitEngageTimer || this.m_HasAutoOrbitBlend || this.m_DebugEngageWaiting;
            if (hadState && this.m_DebugLogs)
            {
                string why = string.IsNullOrEmpty(reason) ? string.Empty : $" reason={reason}";
                if (float.IsNaN(time) || this.ShouldDebugLog(time))
                {
                    this.DebugLog($"Engage reset.{why}");
                }
            }

            this.m_HasAutoOrbitEngageTimer = false;
            this.m_AutoOrbitEngageStartTime = 0f;
            this.m_HasAutoOrbitBlend = false;
            this.m_AutoOrbitBlendStartTime = 0f;
            this.m_AutoOrbitBlendFromYaw = 0f;
            this.m_AutoOrbitBlendTargetYaw = 0f;
            this.m_DebugEngageWaiting = false;
        }

        private void HandleDirectionChangeReset(
            Vector3 desiredDirection,
            float time,
            float resetLockTime,
            float returnDelay
        )
        {
            desiredDirection.y = 0f;
            if (desiredDirection.sqrMagnitude <= 0.0001f)
            {
                this.ResetDirectionChangeTracking();
                return;
            }

            Vector3 worldDirection = desiredDirection.normalized;
            Quaternion cameraYawRotation = Quaternion.Euler(0f, this.m_CurrentYaw, 0f);
            Vector3 direction = Quaternion.Inverse(cameraYawRotation) * worldDirection;
            direction.y = 0f;
            if (direction.sqrMagnitude <= 0.0001f)
            {
                this.ResetDirectionChangeTracking();
                return;
            }

            direction.Normalize();
            if (!this.m_HasLastAutoDirection)
            {
                this.m_LastAutoDirection = direction;
                this.m_HasLastAutoDirection = true;
                return;
            }

            if (!this.m_ResetDelaysOnDirectionChange)
            {
                this.m_LastAutoDirection = direction;
                this.m_DirectionChangeResetAwaitRearm = false;
                this.m_DirectionChangeResetLockUntilTime = 0f;
                this.m_DirectionChangeReturnDelayUntilTime = 0f;
                this.m_DebugDirectionReturnDelayWaiting = false;
                return;
            }

            float angleThreshold = Mathf.Clamp(this.m_DirectionChangeResetAngle, 0f, 180f);
            float deltaAngle = Vector3.Angle(this.m_LastAutoDirection, direction);
            float rearmThreshold = Mathf.Max(1f, angleThreshold * DIRECTION_CHANGE_REANCHOR_RATIO);

            if (this.m_DirectionChangeResetAwaitRearm)
            {
                this.m_LastAutoDirection = direction;

                if (time < this.m_DirectionChangeResetLockUntilTime)
                {
                    float remaining = this.m_DirectionChangeResetLockUntilTime - time;
                    this.DebugLogThrottled(time, $"Direction reset lock active remaining={remaining:F2}s");
                    return;
                }

                if (deltaAngle > rearmThreshold)
                {
                    this.DebugLogThrottled(
                        time,
                        $"Direction reset waiting rearm deltaAngle={deltaAngle:F1} rearm={rearmThreshold:F1}"
                    );
                    return;
                }

                this.m_DirectionChangeResetAwaitRearm = false;
                this.m_DirectionChangeResetLockUntilTime = 0f;
                this.DebugLog($"Direction reset re-armed deltaAngle={deltaAngle:F1}");
                return;
            }

            if (deltaAngle >= angleThreshold)
            {
                this.DebugLog(
                    $"Direction change reset deltaAngle={deltaAngle:F1} threshold={angleThreshold:F1} " +
                    $"dir=({direction.x:F2},{direction.z:F2})"
                );
                this.ResetAutoOrbitEngageDelay("direction changed", time);
                this.ResetReturnYawBlend("direction changed", time);
                this.m_LastMoveTime = time;
                this.ApplyDirectionReversalReturnDelay(direction, returnDelay, time);

                // Re-anchor from the new direction so another reset needs another meaningful turn.
                this.m_LastAutoDirection = direction;

                if (resetLockTime > float.Epsilon)
                {
                    this.m_DirectionChangeResetAwaitRearm = true;
                    this.m_DirectionChangeResetLockUntilTime = time + resetLockTime;
                    this.DebugLog($"Direction reset lock started duration={resetLockTime:F2}s");
                }
                return;
            }

            if (deltaAngle <= rearmThreshold)
            {
                this.m_LastAutoDirection = direction;
            }

            this.m_HasLastAutoDirection = true;
        }

        private void ApplyDirectionReversalReturnDelay(Vector3 localDirection, float returnDelay, float time)
        {
            this.m_DirectionChangeReturnDelayUntilTime = 0f;
            this.m_DebugDirectionReturnDelayWaiting = false;

            if (returnDelay <= float.Epsilon) return;

            float localYaw = Mathf.Abs(Mathf.Atan2(localDirection.x, localDirection.z) * Mathf.Rad2Deg);
            float reversalFactor = Mathf.InverseLerp(
                RETURN_DELAY_REVERSAL_START_ANGLE,
                RETURN_DELAY_REVERSAL_FULL_ANGLE,
                localYaw
            );

            if (reversalFactor <= float.Epsilon) return;

            float scaledDelay = returnDelay * reversalFactor;
            this.m_DirectionChangeReturnDelayUntilTime = time + scaledDelay;

            this.DebugLog(
                $"Direction reversal return-delay gate started delay={scaledDelay:F2}s " +
                $"factor={reversalFactor:F2} localYaw={localYaw:F1}"
            );
        }

        private void ResetDirectionChangeTracking()
        {
            this.m_HasLastAutoDirection = false;
            this.m_LastAutoDirection = Vector3.zero;
            this.m_DirectionChangeResetAwaitRearm = false;
            this.m_DirectionChangeResetLockUntilTime = 0f;
            this.m_LastPlanarMoveDirection = Vector3.zero;
            this.m_HasLastPlanarMoveDirection = false;
            this.m_HasOppositeTurnSignLock = false;
            this.m_OppositeTurnSignLock = 0f;
            this.m_DirectionChangeReturnDelayUntilTime = 0f;
            this.m_DebugDirectionReturnDelayWaiting = false;
        }

        private void ApplyReturnYawWithSmooth(TShotType shotType, float desiredYaw, float time)
        {
            float returnSmooth = Mathf.Max(0f, (float)this.m_ReturnSmoothTime.Get(shotType.Args));
            if (returnSmooth <= float.Epsilon)
            {
                this.m_TargetYaw = desiredYaw;
                this.ResetReturnYawBlend("returnSmooth <= 0 (immediate)", time);
                return;
            }

            if (!this.m_HasReturnBlend)
            {
                this.m_HasReturnBlend = true;
                this.m_ReturnBlendStartTime = time;
                this.m_ReturnBlendFromYaw = this.m_CurrentYaw;
                this.DebugLog(
                    $"Return blend started smooth={returnSmooth:F2}s " +
                    $"from={QuaternionUtils.Convert180(this.m_ReturnBlendFromYaw):F1} " +
                    $"to={QuaternionUtils.Convert180(desiredYaw):F1}"
                );
            }

            float t = Mathf.Clamp01((time - this.m_ReturnBlendStartTime) / returnSmooth);
            float blendedYaw = Mathf.LerpAngle(this.m_ReturnBlendFromYaw, desiredYaw, t);
            this.m_TargetYaw = blendedYaw;
        }

        private float ResolveContinuousYaw(float yaw)
        {
            float wrappedYaw = QuaternionUtils.Convert180(yaw);
            float deltaToCurrent = Mathf.DeltaAngle(this.m_CurrentYaw, wrappedYaw);

            // Opposite-facing targets are inherently ambiguous (+/-180). Lock one side
            // for the full opposite-turn phase to avoid left-right wobble.
            if (Mathf.Abs(deltaToCurrent) >= OPPOSITE_TURN_LOCK_ANGLE)
            {
                if (!this.m_HasOppositeTurnSignLock)
                {
                    this.m_OppositeTurnSignLock = this.ResolvePreferredTurnSign();
                    if (Mathf.Abs(this.m_OppositeTurnSignLock) < float.Epsilon)
                    {
                        this.m_OppositeTurnSignLock = 1f;
                    }

                    this.m_HasOppositeTurnSignLock = true;
                }

                float oppositeDelta = Mathf.Sign(this.m_OppositeTurnSignLock) *
                    Mathf.Min(Mathf.Abs(deltaToCurrent), OPPOSITE_TURN_MAX_DELTA);

                this.CaptureResolvedTurnSign(oppositeDelta);
                return this.m_CurrentYaw + oppositeDelta;
            }

            if (this.m_HasOppositeTurnSignLock && Mathf.Abs(deltaToCurrent) <= OPPOSITE_TURN_UNLOCK_ANGLE)
            {
                this.m_HasOppositeTurnSignLock = false;
                this.m_OppositeTurnSignLock = 0f;
            }

            float continuousYaw = this.m_TargetYaw + Mathf.DeltaAngle(this.m_TargetYaw, wrappedYaw);
            float continuousDelta = Mathf.DeltaAngle(this.m_CurrentYaw, continuousYaw);
            this.CaptureResolvedTurnSign(continuousDelta);

            return continuousYaw;
        }

        private void CaptureResolvedTurnSign(float delta)
        {
            if (Mathf.Abs(delta) < TURN_SIGN_CAPTURE_ANGLE) return;

            this.m_LastResolvedTurnSign = Mathf.Sign(delta);
            this.m_HasLastResolvedTurnSign = true;
        }

        private float ResolvePreferredTurnSign()
        {
            if (Mathf.Abs(this.m_YawVelocity) >= TURN_SIGN_VELOCITY_MIN)
            {
                return Mathf.Sign(this.m_YawVelocity);
            }

            if (this.m_HasLastResolvedTurnSign)
            {
                return Mathf.Sign(this.m_LastResolvedTurnSign);
            }

            return 1f;
        }

        private void ResetReturnYawBlend(string reason = "", float time = float.NaN, bool preserveWaitState = false)
        {
            bool hadState = this.m_HasReturnBlend;
            if (hadState && this.m_DebugLogs)
            {
                string why = string.IsNullOrEmpty(reason) ? string.Empty : $" reason={reason}";
                if (float.IsNaN(time) || this.ShouldDebugLog(time))
                {
                    this.DebugLog($"Return blend reset.{why}");
                }
            }

            this.m_HasReturnBlend = false;
            this.m_ReturnBlendStartTime = 0f;
            this.m_ReturnBlendFromYaw = 0f;

            if (!preserveWaitState)
            {
                this.m_DebugReturnWaiting = false;
            }
        }

        private void ResetIdleOrbit(string reason = "", float time = float.NaN)
        {
            bool hadState = this.m_IsIdleOrbitActive;
            if (hadState && this.m_DebugLogs)
            {
                string why = string.IsNullOrEmpty(reason) ? string.Empty : $" reason={reason}";
                if (float.IsNaN(time) || this.ShouldDebugLog(time))
                {
                    this.DebugLog($"Idle orbit reset.{why}");
                }
            }

            this.m_IsIdleOrbitActive = false;
            this.m_IdleOrbitCurrentSpeed = 0f;
            this.m_IdleOrbitSpeedVelocity = 0f;
            this.m_DebugIdleOrbitWaiting = false;
        }

        private void SmoothYaw(TShotType shotType, float deltaTime)
        {
            if (deltaTime <= float.Epsilon) return;

            float smoothTime = Mathf.Max(MIN_SMOOTH_TIME, (float)this.m_OrbitSmoothTime.Get(shotType.Args));

            this.m_CurrentYaw = Mathf.SmoothDampAngle(
                this.m_CurrentYaw,
                this.m_TargetYaw,
                ref this.m_YawVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );

            this.m_CurrentYaw = QuaternionUtils.Convert180(this.m_CurrentYaw);
        }

        private void SmoothPitch(TShotType shotType, float deltaTime, bool manualOverrideActive)
        {
            float basePitch = (float)this.m_Pitch.Get(shotType.Args);
            float smoothTime = Mathf.Max(MIN_SMOOTH_TIME, (float)this.m_OrbitSmoothTime.Get(shotType.Args));

            if (this.m_ManualPitchEnabled && !manualOverrideActive && deltaTime > float.Epsilon)
            {
                this.m_ManualPitchOffset = Mathf.Lerp(
                    this.m_ManualPitchOffset,
                    0f,
                    1f - Mathf.Exp(-4f * deltaTime / smoothTime)
                );

                if (Mathf.Abs(this.m_ManualPitchOffset) < 0.001f)
                {
                    this.m_ManualPitchOffset = 0f;
                }
            }

            float targetPitch = basePitch + this.m_ManualPitchOffset;

            if (this.m_ManualPitchEnabled)
            {
                float minPitch = (float)this.m_ManualPitchMin.Get(shotType.Args);
                float maxPitch = (float)this.m_ManualPitchMax.Get(shotType.Args);
                targetPitch = Mathf.Clamp(targetPitch, minPitch, maxPitch);
            }

            if (deltaTime <= float.Epsilon)
            {
                this.m_CurrentPitch = targetPitch;
                return;
            }

            this.m_CurrentPitch = Mathf.SmoothDamp(
                this.m_CurrentPitch,
                targetPitch,
                ref this.m_PitchVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );
        }

        private Vector3 ResolveCombatFocus(TShotType shotType, Vector3 targetPos, GameObject combatTarget, float deltaTime)
        {
            float weight = Mathf.Clamp(this.m_CombatFocusWeight, -1f, 1f);
            if (combatTarget == null || Mathf.Abs(weight) <= 0.0001f)
            {
                this.m_HasCombatFocusPos = false;
                this.m_CombatFocusVelocity = Vector3.zero;
                return targetPos;
            }

            Vector3 desiredFocus = Vector3.LerpUnclamped(targetPos, combatTarget.transform.position, weight);
            float smoothTime = (float)this.m_CombatFocusSmoothTime.Get(shotType.Args);

            if (smoothTime <= float.Epsilon || deltaTime <= float.Epsilon)
            {
                this.m_CombatFocusPos = desiredFocus;
                this.m_HasCombatFocusPos = true;
                this.m_CombatFocusVelocity = Vector3.zero;
                return desiredFocus;
            }

            if (!this.m_HasCombatFocusPos)
            {
                this.m_CombatFocusPos = desiredFocus;
                this.m_CombatFocusVelocity = Vector3.zero;
                this.m_HasCombatFocusPos = true;
            }

            this.m_CombatFocusPos = Vector3.SmoothDamp(
                this.m_CombatFocusPos,
                desiredFocus,
                ref this.m_CombatFocusVelocity,
                Mathf.Max(MIN_SMOOTH_TIME, smoothTime),
                Mathf.Infinity,
                deltaTime
            );

            return this.m_CombatFocusPos;
        }

        private void UpdateLookahead(TShotType shotType, float deltaTime)
        {
            float lookaheadTime = Mathf.Max(0f, (float)this.m_LookaheadTime.Get(shotType.Args));

            if (lookaheadTime <= float.Epsilon || deltaTime <= float.Epsilon)
            {
                this.DampLookaheadToZero(shotType, deltaTime);
                return;
            }

            Vector3 desiredOffset = this.m_RawVelocity * lookaheadTime;

            float maxDistance = Mathf.Max(0f, (float)this.m_MaxLookaheadDistance.Get(shotType.Args));
            if (maxDistance > float.Epsilon)
            {
                desiredOffset = Vector3.ClampMagnitude(desiredOffset, maxDistance);
            }

            float smoothTime = Mathf.Max(MIN_SMOOTH_TIME, (float)this.m_LookaheadSmoothTime.Get(shotType.Args));
            this.m_LookaheadOffset = Vector3.SmoothDamp(
                this.m_LookaheadOffset,
                desiredOffset,
                ref this.m_LookaheadVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );
        }

        private void DampLookaheadToZero(TShotType shotType, float deltaTime)
        {
            if (this.m_LookaheadOffset.sqrMagnitude <= 0.0001f || deltaTime <= float.Epsilon)
            {
                this.m_LookaheadOffset = Vector3.zero;
                this.m_LookaheadVelocity = Vector3.zero;
                return;
            }

            float smoothTime = Mathf.Max(MIN_SMOOTH_TIME, (float)this.m_LookaheadSmoothTime.Get(shotType.Args));
            this.m_LookaheadOffset = Vector3.SmoothDamp(
                this.m_LookaheadOffset,
                Vector3.zero,
                ref this.m_LookaheadVelocity,
                smoothTime,
                Mathf.Infinity,
                deltaTime
            );
        }

        private void UpdateFrontFaceCenter(TShotType shotType, float deltaTime)
        {
            float targetWeight = 0f;

            if (this.m_ManualFrontCenterEnabled && this.Target != null)
            {
                float targetForwardYaw = this.Target.transform.eulerAngles.y;
                float facingAngle = Mathf.Abs(Mathf.DeltaAngle(this.m_CurrentYaw, targetForwardYaw));

                float startAngle = Mathf.Clamp(this.m_ManualFrontCenterStartAngle, 0f, 180f);
                float fullAngle = Mathf.Clamp(this.m_ManualFrontCenterFullAngle, startAngle, 180f);

                if (fullAngle <= startAngle + float.Epsilon)
                {
                    targetWeight = facingAngle >= startAngle ? 1f : 0f;
                }
                else
                {
                    targetWeight = Mathf.InverseLerp(startAngle, fullAngle, facingAngle);
                }
            }

            float smoothTime = Mathf.Max(MIN_SMOOTH_TIME,
                (float)this.m_ManualFrontCenterSmoothTime.Get(shotType.Args));

            if (deltaTime <= float.Epsilon || smoothTime <= float.Epsilon)
            {
                this.m_FrontCenterWeight = targetWeight;
                this.m_FrontCenterWeightVelocity = 0f;
            }
            else
            {
                this.m_FrontCenterWeight = Mathf.SmoothDamp(
                    this.m_FrontCenterWeight,
                    targetWeight,
                    ref this.m_FrontCenterWeightVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime
                );
            }

            this.m_FrontCenterWeight = Mathf.Clamp01(this.m_FrontCenterWeight);
        }

        private void UpdatePivot(TShotType shotType, Vector3 focusPos)
        {
            float lift = (float)this.m_Lift.Get(shotType.Args);
            float shoulder = (float)this.m_Shoulder.Get(shotType.Args);
            if (this.m_SpeedFramingEnabled && this.m_SpeedFramingNullifyShoulder)
            {
                shoulder *= 1f - this.m_CurrentSpeedFramingWeight;
            }

            if (this.m_ManualFrontCenterEnabled)
            {
                shoulder *= 1f - this.m_FrontCenterWeight;
            }

            Quaternion yawRotation = Quaternion.Euler(0f, this.m_CurrentYaw, 0f);
            Vector3 shoulderOffset = yawRotation * Vector3.right * shoulder;

            this.PivotPosition = focusPos + Vector3.up * lift + shoulderOffset + this.m_LookaheadOffset;
        }

        private void UpdateRadius(TShotType shotType)
        {
            float baseDistance = Mathf.Max(0.01f, (float)this.m_Distance.Get(shotType.Args));
            if (this.m_SpeedFramingEnabled)
            {
                float maxDistance = Mathf.Max(baseDistance, (float)this.m_SpeedFramingMaxDistance.Get(shotType.Args));
                baseDistance = Mathf.Lerp(baseDistance, maxDistance, this.m_CurrentSpeedFramingWeight);
            }

            if (this.m_Zoom == null)
            {
                this.Radius = baseDistance;
                return;
            }

            float minDistance = this.m_Zoom.MinDistance;
            float range = Mathf.Max(0f, baseDistance - minDistance);
            this.Radius = minDistance + range * this.m_Zoom.Level;
        }

        private void UpdateCameraPosition(TShotType shotType, float deltaTime)
        {
            Quaternion orbitRotation = Quaternion.Euler(this.m_CurrentPitch, this.m_CurrentYaw, 0f);
            Vector3 direction = orbitRotation * Vector3.forward;
            Vector3 desiredPosition = this.PivotPosition - direction * this.Radius;

            float smoothTime = Mathf.Max(MIN_SMOOTH_TIME, (float)this.m_PositionSmoothTime.Get(shotType.Args));
            if (deltaTime <= float.Epsilon)
            {
                this.m_CurrentPosition = desiredPosition;
            }
            else
            {
                this.m_CurrentPosition = Vector3.SmoothDamp(
                    this.m_CurrentPosition,
                    desiredPosition,
                    ref this.m_PositionVelocity,
                    smoothTime,
                    Mathf.Infinity,
                    deltaTime
                );
            }

            shotType.Position = this.m_CurrentPosition;
        }

        private void UpdateCameraRotation(TShotType shotType, float deltaTime)
        {
            Vector3 lookDirection = this.PivotPosition - this.m_CurrentPosition;
            if (lookDirection.sqrMagnitude <= 0.0001f)
            {
                shotType.Rotation = this.m_CurrentRotation;
                return;
            }

            Quaternion desiredRotation = Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
            float smoothTime = Mathf.Max(0f, (float)this.m_RotationSmoothTime.Get(shotType.Args));

            if (smoothTime <= float.Epsilon || deltaTime <= float.Epsilon)
            {
                this.m_CurrentRotation = desiredRotation;
            }
            else
            {
                float t = 1f - Mathf.Exp(-4f * deltaTime / smoothTime);
                this.m_CurrentRotation = Quaternion.Slerp(this.m_CurrentRotation, desiredRotation, t);
            }

            shotType.Rotation = this.m_CurrentRotation;
        }

        private void DebugLogMode(string mode, float time, string details)
        {
            if (!this.m_DebugLogs) return;

            if (this.m_DebugMode != mode)
            {
                this.m_DebugMode = mode;
                this.m_LastDebugLogTime = time;
                this.DebugLog($"Mode -> {mode}. {details}");
                return;
            }

            this.DebugLogThrottled(time, $"Mode {mode}. {details}");
        }

        private void DebugLogThrottled(float time, string message)
        {
            if (!this.m_DebugLogs) return;
            if (!this.ShouldDebugLog(time)) return;

            this.DebugLog(message);
        }

        private bool ShouldDebugLog(float time)
        {
            float minInterval = Mathf.Max(0f, this.m_DebugLogInterval);
            if (minInterval <= float.Epsilon)
            {
                this.m_LastDebugLogTime = time;
                return true;
            }

            if ((time - this.m_LastDebugLogTime) < minInterval) return false;

            this.m_LastDebugLogTime = time;
            return true;
        }

        private void DebugLog(string message)
        {
            if (!this.m_DebugLogs) return;

            string targetName = this.Target != null ? this.Target.name : "null";
            Debug.Log($"[AutoOrbit:{targetName}] {message}");
        }


    }
}
