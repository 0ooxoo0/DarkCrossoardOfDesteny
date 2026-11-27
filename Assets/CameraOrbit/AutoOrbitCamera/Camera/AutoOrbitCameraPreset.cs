using System;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace Arawn.Runtime.AutoOrbitCamera
{
    [CreateAssetMenu(
        fileName = "AutoOrbitPreset",
        menuName = "Arawn/Camera/Auto Orbit Preset",
        order = 50
    )]
    [Icon("Assets/Arawn/AutoOrbitCamera/Editor/Icons/orbitCamPreset.png")]
    public class AutoOrbitCameraPreset : ScriptableObject
    {
        [Header("Framing")]
        public PropertyGetDecimal distance = new PropertyGetDecimal(6f);
        public PropertyGetDecimal lift = new PropertyGetDecimal(1.5f);
        public PropertyGetDecimal shoulder = new PropertyGetDecimal(0f);
        public PropertyGetDecimal pitch = new PropertyGetDecimal(20f);

        [Header("Speed Framing")]
        public bool speedFramingEnabled = false;
        public PropertyGetDecimal speedFramingTriggerSpeed = new PropertyGetDecimal(6f);
        public PropertyGetDecimal speedFramingFullSpeed = new PropertyGetDecimal(8.5f);
        public PropertyGetDecimal speedFramingMaxDistance = new PropertyGetDecimal(8.5f);
        public bool speedFramingNullifyShoulder = true;
        public bool speedFramingAffectFov = false;
        public PropertyGetDecimal speedFramingMaxFovBoost = new PropertyGetDecimal(8f);
        public PropertyGetDecimal speedFramingSmoothInTime = new PropertyGetDecimal(0.2f);
        public PropertyGetDecimal speedFramingSmoothOutTime = new PropertyGetDecimal(0.3f);

        [Header("Auto Orbit")]
        public PropertyGetDecimal orbitSmoothTime = new PropertyGetDecimal(0.25f);
        public PropertyGetDecimal autoOrbitEngageDelay = new PropertyGetDecimal(0f);
        public PropertyGetDecimal autoOrbitEngageSmoothTime = new PropertyGetDecimal(0.2f);
        public PropertyGetDecimal deadZone = new PropertyGetDecimal(0.15f);
        public PropertyGetDecimal deadZoneHysteresis = new PropertyGetDecimal(0.05f);
        public PropertyGetDecimal yawOffset = new PropertyGetDecimal(0f);
        public bool invertOrbit = false;
        public bool resetDelaysOnDirectionChange = false;
        [Range(0f, 180f)] public float directionChangeResetAngle = 85f;
        public PropertyGetDecimal directionChangeResetLockTime = new PropertyGetDecimal(0.45f);
        public PropertyGetDecimal returnDelay = new PropertyGetDecimal(1.5f);
        public PropertyGetDecimal returnSmoothTime = new PropertyGetDecimal(0.2f);
        public bool returnToTargetForward = true;

        [Header("Idle Cinematic Orbit")]
        public bool idleOrbitEnabled = false;
        public PropertyGetDecimal idleOrbitDelay = new PropertyGetDecimal(5f);
        public PropertyGetDecimal idleOrbitSpeed = new PropertyGetDecimal(15f);
        public PropertyGetDecimal idleOrbitSmoothTime = new PropertyGetDecimal(1.5f);

        [Header("Manual Override")]
        public PropertyGetDecimal manualSensitivity = new PropertyGetDecimal(1f);
        public bool manualPitchEnabled = false;
        public bool manualPitchInvert = false;
        public PropertyGetDecimal manualPitchMin = new PropertyGetDecimal(5f);
        public PropertyGetDecimal manualPitchMax = new PropertyGetDecimal(60f);
        public PropertyGetDecimal manualRecoverDelay = new PropertyGetDecimal(2f);
        public bool manualFrontCenterEnabled = false;
        [Range(0f, 180f)] public float manualFrontCenterStartAngle = 90f;
        [Range(0f, 180f)] public float manualFrontCenterFullAngle = 150f;
        public PropertyGetDecimal manualFrontCenterSmoothTime = new PropertyGetDecimal(0.3f);

        [Header("Smoothing")]
        public PropertyGetDecimal positionSmoothTime = new PropertyGetDecimal(0.1f);
        public PropertyGetDecimal rotationSmoothTime = new PropertyGetDecimal(0.08f);
        public PropertyGetDecimal targetSmoothTime = new PropertyGetDecimal(0f);

        [Header("Look Ahead")]
        public PropertyGetDecimal lookaheadTime = new PropertyGetDecimal(0f);
        public PropertyGetDecimal lookaheadSmoothTime = new PropertyGetDecimal(0.3f);
        public PropertyGetDecimal maxLookaheadDistance = new PropertyGetDecimal(3f);

        [Header("Combat Focus")]
        [Range(-1f, 1f)] public float combatFocusWeight = 0f;
        public PropertyGetDecimal combatFocusSmoothTime = new PropertyGetDecimal(0.2f);

        [ContextMenu("Load Baseline/God of War")]
        public void LoadGodOfWarBaseline()
        {
            this.distance = new PropertyGetDecimal(5.5f);
            this.lift = new PropertyGetDecimal(1.6f);
            this.shoulder = new PropertyGetDecimal(0.65f);
            this.pitch = new PropertyGetDecimal(18f);
            this.speedFramingEnabled = false;
            this.speedFramingTriggerSpeed = new PropertyGetDecimal(6f);
            this.speedFramingFullSpeed = new PropertyGetDecimal(8.5f);
            this.speedFramingMaxDistance = new PropertyGetDecimal(7.5f);
            this.speedFramingNullifyShoulder = true;
            this.speedFramingAffectFov = false;
            this.speedFramingMaxFovBoost = new PropertyGetDecimal(8f);
            this.speedFramingSmoothInTime = new PropertyGetDecimal(0.2f);
            this.speedFramingSmoothOutTime = new PropertyGetDecimal(0.28f);

            this.orbitSmoothTime = new PropertyGetDecimal(0.22f);
            this.autoOrbitEngageDelay = new PropertyGetDecimal(0.05f);
            this.autoOrbitEngageSmoothTime = new PropertyGetDecimal(0.15f);
            this.deadZone = new PropertyGetDecimal(0.1f);
            this.deadZoneHysteresis = new PropertyGetDecimal(0.05f);
            this.yawOffset = new PropertyGetDecimal(0f);
            this.invertOrbit = false;
            this.resetDelaysOnDirectionChange = false;
            this.directionChangeResetAngle = 85f;
            this.directionChangeResetLockTime = new PropertyGetDecimal(0.45f);
            this.returnDelay = new PropertyGetDecimal(1.2f);
            this.returnSmoothTime = new PropertyGetDecimal(0.2f);
            this.returnToTargetForward = true;

            this.manualSensitivity = new PropertyGetDecimal(1f);
            this.manualPitchEnabled = true;
            this.manualPitchInvert = false;
            this.manualPitchMin = new PropertyGetDecimal(8f);
            this.manualPitchMax = new PropertyGetDecimal(50f);
            this.manualRecoverDelay = new PropertyGetDecimal(1.2f);

            this.positionSmoothTime = new PropertyGetDecimal(0.08f);
            this.rotationSmoothTime = new PropertyGetDecimal(0.06f);
            this.targetSmoothTime = new PropertyGetDecimal(0.04f);

            this.lookaheadTime = new PropertyGetDecimal(0.18f);
            this.lookaheadSmoothTime = new PropertyGetDecimal(0.18f);
            this.maxLookaheadDistance = new PropertyGetDecimal(2.5f);

            this.combatFocusWeight = 0.35f;
            this.combatFocusSmoothTime = new PropertyGetDecimal(0.15f);

            this.Clamp();
        }

        [ContextMenu("Load Baseline/Batman Arkham")]
        public void LoadBatmanArkhamBaseline()
        {
            this.distance = new PropertyGetDecimal(6.2f);
            this.lift = new PropertyGetDecimal(1.4f);
            this.shoulder = new PropertyGetDecimal(0.25f);
            this.pitch = new PropertyGetDecimal(22f);
            this.speedFramingEnabled = false;
            this.speedFramingTriggerSpeed = new PropertyGetDecimal(6f);
            this.speedFramingFullSpeed = new PropertyGetDecimal(8.5f);
            this.speedFramingMaxDistance = new PropertyGetDecimal(8.8f);
            this.speedFramingNullifyShoulder = true;
            this.speedFramingAffectFov = false;
            this.speedFramingMaxFovBoost = new PropertyGetDecimal(10f);
            this.speedFramingSmoothInTime = new PropertyGetDecimal(0.18f);
            this.speedFramingSmoothOutTime = new PropertyGetDecimal(0.26f);

            this.orbitSmoothTime = new PropertyGetDecimal(0.28f);
            this.autoOrbitEngageDelay = new PropertyGetDecimal(0.08f);
            this.autoOrbitEngageSmoothTime = new PropertyGetDecimal(0.2f);
            this.deadZone = new PropertyGetDecimal(0.12f);
            this.deadZoneHysteresis = new PropertyGetDecimal(0.05f);
            this.yawOffset = new PropertyGetDecimal(0f);
            this.invertOrbit = false;
            this.resetDelaysOnDirectionChange = false;
            this.directionChangeResetAngle = 85f;
            this.directionChangeResetLockTime = new PropertyGetDecimal(0.45f);
            this.returnDelay = new PropertyGetDecimal(1.6f);
            this.returnSmoothTime = new PropertyGetDecimal(0.25f);
            this.returnToTargetForward = true;

            this.manualSensitivity = new PropertyGetDecimal(1f);
            this.manualPitchEnabled = true;
            this.manualPitchInvert = false;
            this.manualPitchMin = new PropertyGetDecimal(5f);
            this.manualPitchMax = new PropertyGetDecimal(60f);
            this.manualRecoverDelay = new PropertyGetDecimal(1.8f);

            this.positionSmoothTime = new PropertyGetDecimal(0.12f);
            this.rotationSmoothTime = new PropertyGetDecimal(0.08f);
            this.targetSmoothTime = new PropertyGetDecimal(0.02f);

            this.lookaheadTime = new PropertyGetDecimal(0.1f);
            this.lookaheadSmoothTime = new PropertyGetDecimal(0.25f);
            this.maxLookaheadDistance = new PropertyGetDecimal(2f);

            this.combatFocusWeight = 0.2f;
            this.combatFocusSmoothTime = new PropertyGetDecimal(0.2f);

            this.Clamp();
        }

        public void Clamp()
        {
            this.directionChangeResetAngle = Mathf.Clamp(this.directionChangeResetAngle, 0f, 180f);
            this.combatFocusWeight = Mathf.Clamp(this.combatFocusWeight, -1f, 1f);
            this.manualFrontCenterStartAngle = Mathf.Clamp(this.manualFrontCenterStartAngle, 0f, 180f);
            this.manualFrontCenterFullAngle = Mathf.Clamp(this.manualFrontCenterFullAngle, this.manualFrontCenterStartAngle, 180f);
        }

        private void OnValidate()
        {
            this.Clamp();
        }
    }
}
