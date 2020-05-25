using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LilyXwfc
{
    /**
     * Collapse methods exist in two versions, the default one, more efficient,
     * and a coroutine version (returning enumerators) for visualization.
     * This is also used to test implementations.
     */
    public class WaveFunctionCollapse
    {
        readonly WaveFunctionSystem system;
        readonly bool skipInitialConditions;
        readonly bool useBacktracking;

        bool isInconsistent = false; // becomes true when a variable with empty superposed state is found

        // for backtracking, we store the state from time to time, as well as the choices (to avoid making them again)
        Stack<SuperposedState[]> checkpoints;
        Stack<Choice> choices;

        // For debug only
        WaveVariable currentIndex = WaveVariable.Null;
        public WaveVariable CurrentIndex { get { return currentIndex; } }

        class Choice
        {
            public WaveVariable variable;
            public PureState value;

            public Choice(WaveVariable variable, SuperposedState state)
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

        /**
         * Turn skipInitialConditions on if you have no initial condition, to
         * avoid trying to propagate all variables before doing any
         * observation.
         */
        public WaveFunctionCollapse(WaveFunctionSystem system, bool skipInitialConditions = false, bool useBacktracking = true)
        {
            this.system = system;
            this.skipInitialConditions = skipInitialConditions;
            this.useBacktracking = useBacktracking;
        }

        void PushCheckpoint()
        {
            var state = new SuperposedState[system.VariableCount];
            int i = 0;
            foreach (var x in system.Variables())
            {
                state[i++] = system.GetWave(x);
            }
            checkpoints.Push(state);
        }

        void PopCheckpoint()
        {
            var state = checkpoints.Pop();
            int i = 0;
            foreach (var x in system.Variables())
            {
                system.SetWave(x, state[i++]);
            }
        }

        void Reset()
        {
            checkpoints = new Stack<SuperposedState[]>();
            choices = new Stack<Choice>();
            isInconsistent = false;
        }

        /**
         * Return false if the maximum number of steps has been reached
         */
        public bool Collapse(int maxSteps = 20)
        {
            Reset();

            if (!skipInitialConditions)
            {
                foreach (var x in system.Variables()) Propagate(x);
            }

            WaveVariable idx = Observe();
            for (int i = 0; i < maxSteps && !idx.IsNull() && !isInconsistent; ++i)
            {
                Propagate(idx);

                bool backtracked = false;

                // Backtracking
                while (isInconsistent)
                {
                    if (useBacktracking)
                    {
                        Debug.Assert(checkpoints.Count > 0);
                        Debug.Log("Inconsistent state reached at step #" + i + ", backtracking.");
                        isInconsistent = false;
                        PopCheckpoint();

                        // Avoid doing the same choice again
                        var lastChoice = choices.Pop();
                        isInconsistent = !lastChoice.Prevent(system);
                        backtracked = true;
                        idx = lastChoice.variable;
                    }
                    else
                    {
                        Debug.Log("Inconsistent state reached at step #" + i + ", restarting.");
                        system.Reset();
                        isInconsistent = false;
                    }
                }

                if (!backtracked) continue; // ensure Propagate is called

                idx = Observe();
            }
            return idx.IsNull();
        }

        public IEnumerator<bool> CollapseCoroutine(int maxSteps = 20)
        {
            Reset();

            if (!skipInitialConditions)
            {
                foreach (var x in system.Variables())
                {
                    currentIndex = x; yield return x.IsNull();
                    for (var it = PropagateCoroutine(x); it.MoveNext();)
                    {
                        yield return x.IsNull();
                    }
                }
            }

            WaveVariable idx = Observe();
            Debug.Log("initial idx: " + idx);
            for (int i = 0; i < maxSteps && !idx.IsNull() && !isInconsistent; ++i)
            {
                Debug.Log("#################### Propagate from " + idx);

                for (var it = PropagateCoroutine(idx); it.MoveNext() && !isInconsistent;) {
                    currentIndex = idx; yield return idx.IsNull();
                }

                Debug.Log("PropagateCoroutine terminated");

                bool backtracked = false;

                // Backtracking
                while (isInconsistent)
                {
                    if (useBacktracking)
                    {
                        Debug.Assert(checkpoints.Count > 0);
                        Debug.Log("Inconsistent state reached at step #" + i + ", backtracking.");
                        isInconsistent = false;
                        PopCheckpoint();

                        // Avoid doing the same choice again
                        var lastChoice = choices.Pop();
                        Debug.Log("Last choice was " + lastChoice.variable + " := " + lastChoice.value + " from  " + system.GetWave(lastChoice.variable));
                        isInconsistent = !lastChoice.Prevent(system);
                        Debug.Log("Inconsistent after prevent: " + isInconsistent + " (state=" + system.GetWave(lastChoice.variable) + ")");
                        backtracked = true;
                        idx = lastChoice.variable;
                    }
                    else
                    {
                        Debug.Log("Inconsistent state reached at step #" + i + ", restarting.");
                        system.Reset();
                        isInconsistent = false;
                    }
                }

                currentIndex = idx; yield return idx.IsNull();

                if (backtracked) continue;

                idx = Observe();
            }
            currentIndex = idx; yield return idx.IsNull();
        }

        /**
         * Propagate a change that has been made to the wave function at a given index to its neighbors, recursively.
         */
        void Propagate(WaveVariable idx)
        {
            // 1. build neighborhood
            var neighborhood = system.OutgoingConnections(idx);

            // 2. For each neighbor
            for (int i = 0; i < neighborhood.Count; ++i)
            {
                WaveVariable nidx = neighborhood[i].destination;
                int connectionType = neighborhood[i].type;

                // 2a. Test all combinations
                SuperposedState neighborState = system.GetWave(nidx);

                // Build a mask
                SuperposedState allowed = system.rules.AllowedStates(system.GetWave(idx), connectionType, neighborState);

                // Apply the mask to the neighbor
                SuperposedState newNeighborState = neighborState.MaskBy(allowed);
                system.SetWave(nidx, newNeighborState);

                bool changed = !newNeighborState.Equals(neighborState);

                if (changed)
                {
                    if (newNeighborState.Components().Count == 0)
                    {
                        // Inconsistency, abort (This is where we could decide to backtrack)
                        isInconsistent = true;
                        return;
                    }

                    // 2b. Recursive call
                    Propagate(nidx);
                }
            }
        }

        IEnumerator PropagateCoroutine(WaveVariable idx)
        {
            var wave = system.GetWave(idx);
            Debug.Log("Propagate(" + idx + ") (state = " + wave + ") -->");
            // 1. build neighborhood
            var neighborhood = system.OutgoingConnections(idx);

            // 2. For each neighbor
            for (int i = 0; i < neighborhood.Count; ++i)
            {
                WaveVariable nidx = neighborhood[i].destination;
                int connectionType = neighborhood[i].type;

                Debug.Log("neighbor (" + nidx + "), in direction #" + connectionType + " (state = " + system.GetWave(nidx) + ")");

                // 2a. Test all combinations (might get speeded up)
                SuperposedState neighborState = system.GetWave(nidx);

                // Build a mask
                SuperposedState allowed = system.rules.AllowedStates(system.GetWave(idx), connectionType, neighborState);

                // Apply the mask to the neighbor
                SuperposedState newNeighborState = neighborState.MaskBy(allowed);
                Debug.Log("    Masked " + neighborState + " with " + allowed);
                system.SetWave(nidx, newNeighborState);

                bool changed = !newNeighborState.Equals(neighborState);

                if (changed)
                {
                    if (newNeighborState.Components().Count == 0)
                    {
                        Debug.Log("Inconsistency, abort.");
                        isInconsistent = true;
                        yield break;
                    }

                    // 2b. Recursive call
                    currentIndex = nidx;  yield return null;
                    for (var it = PropagateCoroutine(nidx); it.MoveNext() && !isInconsistent;)
                    {
                        yield return null;
                    }
                    Debug.Log("Rec call to PropagateCoroutine terminated with isInconsistent = " + isInconsistent);
                    if (isInconsistent) yield break;
                }
            }
            Debug.Log("<-- Propagate(" + idx + ")");
        }

        /**
         * Observe the less entropic superposed state and return its index
         */
        WaveVariable Observe()
        {
            // 0. Save state for backtracking
            if (useBacktracking)
                PushCheckpoint();
            
            // 1. Find less entropic state
            float minEntropy = Mathf.Infinity;
            List<WaveVariable> argminEntropy = new List<WaveVariable>(); // indices of minimal non null entropy
            foreach (WaveVariable idx in system.Variables())
            {
                SuperposedState s = system.GetWave(idx);
                float entropy = s.Entropy();
                Debug.Assert(entropy == 0 || s.Components().Count > 1);
                if (entropy > 0 && entropy < minEntropy)
                {
                    minEntropy = entropy;
                    argminEntropy.Clear();
                }
                if (entropy == minEntropy)
                {
                    argminEntropy.Add(idx);
                }
            }

            if (argminEntropy.Count == 0)
            {
                Debug.Log("No more superposed state.");
                return WaveVariable.Null;
            }

            // 2. Decohere state
            Debug.Log("min entropy: " + minEntropy + " found in " + argminEntropy.Count + " states");
            int r = Random.Range(0, argminEntropy.Count);
            var selected = argminEntropy[r];
            var wave = system.GetWave(selected);
            wave.Observe();
            system.SetWave(selected, wave);

            // Save choice for backtracking
            if (useBacktracking)
                choices.Push(new Choice(selected, wave));

            return selected;
        }
    }

}
