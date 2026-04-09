using C2M2.NeuronalDynamics.Simulation;
using System.Collections.Generic;
using UnityEngine;

namespace C2M2.NeuronalDynamics.Interaction
{
    public class InjectedCurrentManager : NDInteractablesManager<InjectedCurrentElectrode>
    {
        public List<InjectedCurrentElectrode> electrodes = new List<InjectedCurrentElectrode>();
        public GameObject electrodePrefab = null;

        private void OnDestroy()
        {
            foreach (InjectedCurrentElectrode electrode in electrodes)
                Destroy(electrode);
        }

        public override GameObject IdentifyBuildPrefab(NDSimulation sim, int index)
        {
            if (electrodePrefab == null) Debug.LogError("No InjectedCurrentElectrode prefab found");
            return electrodePrefab;
        }

        public override bool VertexAvailable(NDSimulation sim, int index)
        {
            float minDist = sim.AverageDendriteRadius * 2;
            SparseSolverTestv1 solver = sim as SparseSolverTestv1;
            if (solver == null) return false;

            lock (solver.injectedCurrentLock)
            {
                foreach (InjectedCurrentElectrode electrode in electrodes)
                {
                    if (electrode.FocusVert == index)
                    {
                        Debug.LogWarning("Electrode already exists on vert [" + index + "]");
                        return false;
                    }
                    float dist = (sim.Verts1D[electrode.FocusVert] - sim.Verts1D[index]).magnitude;
                    if (dist < minDist)
                    {
                        Debug.LogWarning("Electrode too close to electrode on vert [" + electrode.FocusVert + "]");
                        return false;
                    }
                }
            }
            return true;
        }
    }
}
