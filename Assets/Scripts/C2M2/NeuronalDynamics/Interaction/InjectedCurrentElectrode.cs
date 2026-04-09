using UnityEngine;
using C2M2.NeuronalDynamics.Simulation;

namespace C2M2.NeuronalDynamics.Interaction
{
    public class InjectedCurrentElectrode : NDInteractables
    {
        public double Amplitude = 0.15e-11;  // Amps
        public double Delay = 50e-3;          // seconds
        public double Duration = 100e-3;      // seconds
        public bool IsLive = true;

        public Color activeColor   = new Color(1.0f, 0.55f, 0.0f); // orange
        public Color inactiveColor = new Color(0.45f, 0.45f, 0.45f); // grey

        public InjectedCurrentManager ElectrodeManager
        {
            get { return ((SparseSolverTestv1)simulation).injectedCurrentManager; }
        }

        private void OnDestroy()
        {
            lock (((SparseSolverTestv1)simulation).injectedCurrentLock)
                ElectrodeManager.electrodes.Remove(this);
        }

        public override void Place(int index)
        {
            lock (((SparseSolverTestv1)simulation).injectedCurrentLock)
                ElectrodeManager.electrodes.Add(this);

            transform.localPosition = FocusPos;
            UpdateColor();
        }

        public void ToggleElectrode()
        {
            IsLive = !IsLive;
            UpdateColor();
        }

        public void UpdateColor()
        {
            if (meshRenderer != null)
                meshRenderer.material.color = IsLive ? activeColor : inactiveColor;
        }

        protected override void AddHitEventListeners()
        {
            HitEvent.OnEndPress.AddListener((hit) =>
            {
                if (ElectrodeManager.HoldCount >= ElectrodeManager.DestroyCount)
                    Destroy(gameObject);
                else if (ElectrodeManager.HoldCount > 0)
                    ToggleElectrode();
                ElectrodeManager.HoldCount = 0;
            });
            HitEvent.OnHoldPress.AddListener((hit) =>
            {
                ElectrodeManager.HoldCount += Time.deltaTime;
            });
        }
    }
}
