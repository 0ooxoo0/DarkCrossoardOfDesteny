using System;
using GameCreator.Runtime.Cameras;
using GameCreator.Runtime.Common;
using UnityEngine;

namespace Arawn.Runtime.AutoOrbitCamera
{
    [Title("Auto Orbit")]
    [Category("Auto Orbit")]
    [Image(typeof(IconShotThirdPerson), ColorTheme.Type.Yellow)]

    [Description(
        "Camera automatically orbits behind the target's movement direction. " +
        "Auto-orbit camera — no manual rotation needed, " +
        "very controller-friendly. Optionally accepts manual rotation input " +
        "that is smoothly reclaimed by auto-orbit when input stops.")]

    [Serializable]
    public class ShotTypeAutoOrbit : TShotType
    {
        // =================================================================
        // Exposed Members
        // =================================================================

        [SerializeField] private ShotSystemZoom m_Zoom;
        [SerializeField] private ShotSystemAutoOrbit m_AutoOrbit;

        // =================================================================
        // Members
        // =================================================================

        [NonSerialized] private readonly Transform[] m_Ignore = new Transform[1];

        // =================================================================
        // Properties
        // =================================================================

        public ShotSystemZoom Zoom => this.m_Zoom;
        public ShotSystemAutoOrbit AutoOrbit => this.m_AutoOrbit;

        public override bool UseSmoothPosition => false;
        public override bool UseSmoothRotation => false;

        public override Args Args
        {
            get
            {
                this.m_Args ??= new Args(this.m_ShotCamera, null);
                this.m_Args.ChangeTarget(this.m_AutoOrbit?.Target);
                return this.m_Args;
            }
        }

        public override Transform[] Ignore
        {
            get
            {
                GameObject target = this.m_AutoOrbit?.Target;
                this.m_Ignore[0] = target != null ? target.transform : null;
                return this.m_Ignore;
            }
        }

        public override bool HasTarget => this.m_AutoOrbit?.Target != null;

        public override Vector3 Target => this.m_AutoOrbit?.PivotPosition ?? default;

        // =================================================================
        // Constructor
        // =================================================================

        public ShotTypeAutoOrbit()
        {
            this.m_Zoom = new ShotSystemZoom();
            this.m_AutoOrbit = new ShotSystemAutoOrbit();

            this.m_ShotSystems.Add(this.m_Zoom.Id, this.m_Zoom);
            this.m_ShotSystems.Add(this.m_AutoOrbit.Id, this.m_AutoOrbit);
        }

        // =================================================================
        // Lifecycle Overrides
        // =================================================================

        protected override void OnBeforeAwake(ShotCamera shotCamera)
        {
            base.OnBeforeAwake(shotCamera);
            this.m_Zoom?.OnAwake(this);
            this.m_AutoOrbit?.OnAwake(this);
        }

        protected override void OnBeforeStart(ShotCamera shotCamera)
        {
            base.OnBeforeStart(shotCamera);
            this.m_Zoom?.OnStart(this);
            this.m_AutoOrbit?.OnStart(this);
        }

        protected override void OnBeforeDestroy(ShotCamera shotCamera)
        {
            base.OnBeforeDestroy(shotCamera);
            this.m_Zoom?.OnDestroy(this);
            this.m_AutoOrbit?.OnDestroy(this);
        }

        protected override void OnBeforeEnable(TCamera camera)
        {
            base.OnBeforeEnable(camera);
            this.m_Zoom?.OnEnable(this, camera);
            this.m_AutoOrbit?.OnEnable(this, camera);
        }

        protected override void OnBeforeDisable(TCamera camera)
        {
            base.OnBeforeDisable(camera);
            this.m_Zoom?.OnDisable(this, camera);
            this.m_AutoOrbit?.OnDisable(this, camera);
        }

        protected override void OnBeforeUpdate()
        {
            base.OnBeforeUpdate();

            this.m_Recoil.Update(out float pitch, out float yaw);
            // Recoil can feed into manual yaw offset if desired — for now
            // we don't apply it since auto-orbit controls yaw.

            this.m_Zoom?.OnUpdate(this);
            this.m_AutoOrbit?.OnUpdate(this);
        }

        // =================================================================
        // Gizmos
        // =================================================================

        public override void DrawGizmos(Transform transform)
        {
            base.DrawGizmos(transform);
            if (!Application.isPlaying) return;

            this.m_Zoom?.OnDrawGizmos(this, transform);
            this.m_AutoOrbit?.OnDrawGizmos(this, transform);
        }

        public override void DrawGizmosSelected(Transform transform)
        {
            base.DrawGizmosSelected(transform);
            if (!Application.isPlaying) return;

            this.m_Zoom?.OnDrawGizmosSelected(this, transform);
            this.m_AutoOrbit?.OnDrawGizmosSelected(this, transform);
        }
    }
}
