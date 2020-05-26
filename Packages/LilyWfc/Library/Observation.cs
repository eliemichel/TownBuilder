using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LilyWfc
{
    
    /**
     * Class used to store the list of choices performed when doing
     * observations during WFC. We keep track of those for backtracking.
     */
    class Observation
    {
        public WaveVariable variable;
        public PureState value;

        public Observation(WaveVariable variable, SuperposedState state)
        {
            this.variable = variable;
            var comp = state.Components();
            this.value = comp.Count > 0 ? comp[0] : new PureState(0);
        }

        /**
            * Prevent this choice from being made
            * return false iff this make the system inconsistent
            */
        public bool Prevent(WaveFunctionSystem system)
        {
            var s = system.GetWave(variable);
            s.Remove(value);
            system.SetWave(variable, s);
            return s.Components().Count > 0;
        }
    }

}
