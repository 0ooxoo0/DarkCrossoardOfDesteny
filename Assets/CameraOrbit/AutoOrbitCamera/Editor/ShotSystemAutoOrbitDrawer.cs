#if UNITY_EDITOR
using System.IO;
using GameCreator.Editor.Cameras;
using GameCreator.Editor.Common;
using Arawn.Runtime.AutoOrbitCamera;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace Arawn.Editor.AutoOrbitCamera
{
    [CustomPropertyDrawer(typeof(ShotSystemAutoOrbit))]
    public class ShotSystemAutoOrbitDrawer : TShotSystemDrawer
    {
        private const string SESSION_KEY_PREFIX = "arawn.autoorbit.preset";

        protected override string Name(SerializedProperty property) => "Auto Orbit";

        protected override void CreatePropertyContent(VisualElement root, SerializedProperty property)
        {
            this.CreatePresetTools(root, property);

            AddGroup(root, property, "Target", false, "m_Target", "m_Lift", "m_Shoulder");
            AddGroup(
                root,
                property,
                "Combat Focus",
                false,
                "m_CombatTarget",
                "m_CombatFocusWeight",
                "m_CombatFocusSmoothTime"
            );
            AddGroup(root, property, "Orbit Distance", false, "m_Distance");
            AddGroup(
                root,
                property,
                "Speed Framing",
                false,
                "m_SpeedFramingEnabled",
                "m_SpeedFramingTriggerSpeed",
                "m_SpeedFramingFullSpeed",
                "m_SpeedFramingMaxDistance",
                "m_SpeedFramingNullifyShoulder",
                "m_SpeedFramingAffectFov",
                "m_SpeedFramingMaxFovBoost",
                "m_SpeedFramingSmoothInTime",
                "m_SpeedFramingSmoothOutTime"
            );
            AddGroup(root, property, "Pitch", false, "m_Pitch");
            AddGroup(
                root,
                property,
                "Auto-Orbit",
                false,
                "m_OrbitSmoothTime",
                "m_AutoOrbitEngageDelay",
                "m_AutoOrbitEngageSmoothTime",
                "m_DeadZone",
                "m_DeadZoneHysteresis",
                "m_YawOffset",
                "m_InvertOrbit",
                "m_ResetDelaysOnDirectionChange",
                "m_DirectionChangeResetAngle",
                "m_DirectionChangeResetLockTime"
            );
            AddGroup(
                root,
                property,
                "Return to Forward",
                false,
                "m_ReturnDelay",
                "m_ReturnSmoothTime",
                "m_ReturnToTargetForward"
            );
            AddGroup(
                root,
                property,
                "Idle Cinematic Orbit",
                false,
                "m_IdleOrbitEnabled",
                "m_IdleOrbitDelay",
                "m_IdleOrbitSpeed",
                "m_IdleOrbitSmoothTime"
            );
            AddGroup(
                root,
                property,
                "Manual Override",
                false,
                "m_InputRotate",
                "m_ManualSensitivity",
                "m_ManualPitchEnabled",
                "m_ManualPitchInvert",
                "m_ManualPitchMin",
                "m_ManualPitchMax",
                "m_ManualRecoverDelay",
                "m_ManualFrontCenterEnabled",
                "m_ManualFrontCenterStartAngle",
                "m_ManualFrontCenterFullAngle",
                "m_ManualFrontCenterSmoothTime"
            );
            AddGroup(
                root,
                property,
                "Smoothing",
                false,
                "m_PositionSmoothTime",
                "m_RotationSmoothTime",
                "m_TargetSmoothTime"
            );
            AddGroup(
                root,
                property,
                "Look Ahead",
                false,
                "m_LookaheadTime",
                "m_LookaheadSmoothTime",
                "m_MaxLookaheadDistance"
            );
            AddGroup(
                root,
                property,
                "Debug",
                false,
                "m_DebugLogs",
                "m_DebugLogInterval"
            );
        }

        private void CreatePresetTools(VisualElement root, SerializedProperty property)
        {
            VisualElement toolbar = new VisualElement();
            toolbar.style.marginBottom = 8f;
            toolbar.style.paddingBottom = 6f;
            toolbar.style.borderBottomWidth = 1f;
            toolbar.style.borderBottomColor = new Color(0f, 0f, 0f, 0.25f);

            AutoOrbitCameraPreset currentPreset = this.LoadStoredPreset(property);

            ObjectField presetField = new ObjectField("Preset")
            {
                objectType = typeof(AutoOrbitCameraPreset),
                allowSceneObjects = false,
                value = currentPreset
            };

            presetField.RegisterValueChangedCallback(change =>
            {
                AutoOrbitCameraPreset preset = change.newValue as AutoOrbitCameraPreset;
                this.StorePreset(property, preset);
            });

            VisualElement buttons = new VisualElement();
            buttons.style.flexDirection = FlexDirection.Row;
            buttons.style.marginTop = 4f;

            Button buttonApply = new Button(() =>
            {
                AutoOrbitCameraPreset preset = presetField.value as AutoOrbitCameraPreset;
                if (preset == null) return;
                ApplyPreset(property, preset);
            })
            {
                text = "Apply Preset"
            };
            buttonApply.style.marginRight = 6f;

            Button buttonCapture = new Button(() =>
            {
                AutoOrbitCameraPreset preset = presetField.value as AutoOrbitCameraPreset;
                if (preset == null) return;
                CapturePreset(property, preset);
            })
            {
                text = "Capture To Preset"
            };
            buttonCapture.style.marginRight = 6f;

            Button buttonCreate = new Button(() => this.CreatePresetFromCurrent(property, presetField))
            {
                text = "New From Current"
            };

            buttons.Add(buttonApply);
            buttons.Add(buttonCapture);
            buttons.Add(buttonCreate);

            HelpBox help = new HelpBox(
                "Preset workflow: tune one shot, capture to profile, apply to other shots.",
                HelpBoxMessageType.None
            );
            help.style.marginTop = 4f;

            toolbar.Add(presetField);
            toolbar.Add(buttons);
            toolbar.Add(help);

            root.Add(toolbar);
        }

        private static void AddGroup(
            VisualElement root,
            SerializedProperty property,
            string title,
            bool defaultExpanded,
            params string[] fields
        )
        {
            ContentBox group = new ContentBox(title, defaultExpanded);
            group.style.marginTop = 2f;
            group.style.marginBottom = 2f;

            foreach (string fieldName in fields)
            {
                SerializedProperty field = property.FindPropertyRelative(fieldName);
                if (field == null) continue;

                group.Content.Add(new PropertyField(field));
            }

            root.Add(group);
        }

        private static void ApplyPreset(SerializedProperty property, AutoOrbitCameraPreset preset)
        {
            ShotSystemAutoOrbit system = property.boxedValue as ShotSystemAutoOrbit;
            if (system == null) return;

            SerializedObject serializedObject = property.serializedObject;
            serializedObject.Update();

            system.ApplyPreset(preset);
            property.boxedValue = system;

            serializedObject.ApplyModifiedProperties();
            EditorUtility.SetDirty(serializedObject.targetObject);
            ActiveEditorTracker.sharedTracker.ForceRebuild();
        }

        private static void CapturePreset(SerializedProperty property, AutoOrbitCameraPreset preset)
        {
            ShotSystemAutoOrbit system = property.boxedValue as ShotSystemAutoOrbit;
            if (system == null) return;

            Undo.RecordObject(preset, "Capture Auto Orbit Preset");
            system.CapturePreset(preset);

            EditorUtility.SetDirty(preset);
            AssetDatabase.SaveAssetIfDirty(preset);
        }

        private void CreatePresetFromCurrent(SerializedProperty property, ObjectField presetField)
        {
            ShotSystemAutoOrbit system = property.boxedValue as ShotSystemAutoOrbit;
            if (system == null) return;

            string directory = "Assets";
            AutoOrbitCameraPreset selected = presetField.value as AutoOrbitCameraPreset;
            if (selected != null)
            {
                string selectedPath = AssetDatabase.GetAssetPath(selected);
                if (!string.IsNullOrEmpty(selectedPath))
                {
                    directory = Path.GetDirectoryName(selectedPath) ?? directory;
                    directory = directory.Replace("\\", "/");
                }
            }

            string path = EditorUtility.SaveFilePanelInProject(
                "Create Auto Orbit Preset",
                "AutoOrbitPreset",
                "asset",
                "Select where to save the new preset profile.",
                directory
            );

            if (string.IsNullOrEmpty(path)) return;

            AutoOrbitCameraPreset preset = ScriptableObject.CreateInstance<AutoOrbitCameraPreset>();
            system.CapturePreset(preset);

            AssetDatabase.CreateAsset(preset, path);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            this.StorePreset(property, preset);
            presetField.SetValueWithoutNotify(preset);
            EditorGUIUtility.PingObject(preset);
        }

        private void StorePreset(SerializedProperty property, AutoOrbitCameraPreset preset)
        {
            string key = this.GetSessionKey(property);
            if (preset == null)
            {
                SessionState.SetString(key, string.Empty);
                return;
            }

            string path = AssetDatabase.GetAssetPath(preset);
            string guid = AssetDatabase.AssetPathToGUID(path);
            SessionState.SetString(key, guid ?? string.Empty);
        }

        private AutoOrbitCameraPreset LoadStoredPreset(SerializedProperty property)
        {
            string guid = SessionState.GetString(this.GetSessionKey(property), string.Empty);
            if (string.IsNullOrEmpty(guid)) return null;

            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(path)) return null;

            return AssetDatabase.LoadAssetAtPath<AutoOrbitCameraPreset>(path);
        }

        private string GetSessionKey(SerializedProperty property)
        {
            Object target = property.serializedObject.targetObject;
            int id = target != null ? target.GetInstanceID() : 0;
            return $"{SESSION_KEY_PREFIX}.{id}.{property.propertyPath}";
        }
    }
}
#endif
