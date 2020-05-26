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

        /**
         * If setting non uniform initial states, e.g. because setting boundary
         * constraints, we propagate from all states before any observation to
         * limit the chances of inconsistent state.
         */
        readonly bool skipInitialConditions;

        /**
         * Use backtracking when reaching an inconsistent state, i.e. roll back
         * to the last choice and do another one. This converges faster because
         * otherwise the solving is restarted from scratch and enables the
         * detection of unsolvable systems, but requires memory to store
         * (in so called checkpoints) the whole state before each observation.
         * TODO: add an option to save only a few checkpoints.
         */
        readonly bool useBacktracking;

        bool isInconsistent = false; // becomes true when a variable with empty superposed state is found

        // for backtracking, we store the state from time to time, as well as the choices (to avoid making them again)
        Stack<SuperposedState[]> checkpoints;
        Stack<Observation> obervations;

        // For debug only
        WaveVariable currentIndex = WaveVariable.Null;
        public WaveVariable CurrentIndex { get { return currentIndex; } }

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

        void Reset()
        {
            checkpoints = new Stack<SuperposedState[]>();
            obervations = new Stack<Observation>();
            isInconsistent = false;
        }

        /**
         * Before doing any observation, we propagate from any variable that
         * was initialized with a non uniform distribution.
         */
        bool PropagateFromInitialConditions()
        {
            foreach (var x in system.Variables())
            {
                //if (system.GetWave(x).Equals(equiprobable)) continue;
                if (!Propagate(x)) return false;
            }
            return true;
        }

        /**
         * Return false if the system could not be solved in maxSteps steps
         */
        public bool Collapse(int maxSteps = 20)
        {
            Reset();

            if (!skipInitialConditions)
            {
                if (!PropagateFromInitialConditions())
                {
                    Debug.Log("System is not solvable");
                    return false;
                }
            }

            WaveVariable lastChangedVariable = Observe();
            for (int i = 0; i < maxSteps && !lastChangedVariable.IsNull(); ++i)
            {
                if (Propagate(lastChangedVariable))
                {
                    lastChangedVariable = Observe();
                }
                else
                {
                    if (useBacktracking)
                    {
                        Debug.Log("Inconsistent state reached at step #" + i + ", backtracking.");
                        lastChangedVariable = Backtrack();
                        if (lastChangedVariable.IsNull())
                        {
                            Debug.Log("System is not solvable");
                        }
                    }
                    else
                    {
                        Debug.Log("Inconsistent state reached at step #" + i + ", restarting.");
                        system.Reset();
                        return Collapse(maxSteps - i - 1);
                    }
                }
            }
            return lastChangedVariable.IsNull();
        }

        /**
         * Rollback the system to before a previous choice and remove the last
         * choice from the possible values (since it lead to an inconsistent
         * state).
         * Return the variable of the last choice, that must be propagated
         * because since we removed a possible value its neighbors might be
         * affected before doing any further observation.
         */
        public WaveVariable Backtrack()
        {
            if (checkpoints.Count == 0)
            {
                // Cannot backtrack, we are already back to the initial state,
                // this means that the system is unsolvable.
                return WaveVariable.Null;
            }

            // Restore the system state from the last checkpoint.
            PopCheckpoint();

            // Avoid doing the same choice again, if possible
            var lastChoice = obervations.Pop();
            if (lastChoice.Prevent(system))
            {
                return lastChoice.variable;
            }
            else
            {
                // If it cannot be prevented, recursively backtrack to the previous point
                return Backtrack();
            }
        }

        /**
         * Save the current state of the system. 
         * You must push a choice to 'obervations' each time you call this.
         */
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

        /**
         * Get the last saved state and remove it from the checkpoint stack.
         * You must pop a choice from 'obervations' each time you call this.
         */
        void PopCheckpoint()
        {
            var state = checkpoints.Pop();
            int i = 0;
            foreach (var x in system.Variables())
            {
                system.SetWave(x, state[i++]);
            }
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
            for (int i = 0; i < maxSteps && !idx.IsNull() && !isInconsistent; ++i)
            {
                Debug.Log("#################### Propagate from " + idx);

                for (var it = PropagateCoroutine(idx); it.MoveNext();)
                {
                    currentIndex = idx; yield return idx.IsNull();
                }

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
                        var lastChoice = obervations.Pop();
                        isInconsistent = !lastChoice.Prevent(system);
                    }
                    else
                    {
                        Debug.Log("Inconsistent state reached at step #" + i + ", restarting.");
                        system.Reset();
                        isInconsistent = false;
                    }
                }

                currentIndex = idx; yield return idx.IsNull();

                idx = Observe();
            }
            currentIndex = idx; yield return idx.IsNull();
        }

        /**
         * Propagate a change that has been made to the wave function at a
         * given index to its neighbors, recursively, and return false if there
         * was an inconsistent state found.
         */
        bool Propagate(WaveVariable idx)
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
                        // Inconsistency, abort
                        isInconsistent = true;
                        return false;
                    }

                    // 2b. Recursive call
                    if (!Propagate(nidx)) return false;
                }
            }

            return true;
        }

        IEnumerator PropagateCoroutine(WaveVariable idx)
        {
            Debug.Log("Propagate(" + idx + ") (state = " + system.GetWave(idx) + ") -->");
            // 1. build neighborhood
            var neighborhood = system.OutgoingConnections(idx);

            // 2. For each neighbor
            for (int i = 0; i < neighborhood.Count; ++i)
            {
                WaveVariable nidx = neighborhood[i].destination;
                int connectionType = neighborhood[i].type;

                Debug.Log("neighbor (" + nidx + "), in direction #" + connectionType + " (state = " + system.GetWave(nidx) + ")");

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
                        yield break;
                    }

                    // 2b. Recursive call
                    currentIndex = nidx; yield return null;
                    for (var it = PropagateCoroutine(nidx); it.MoveNext();)
                    {
                        yield return null;
                    }
                }
            }
            Debug.Log("<-- Propagate(" + idx + ")");
        }

        /**
         * Observe the less entropic superposed state and return its index
         */
        WaveVariable Observe()
        {
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
                //Debug.Log("No more superposed state.");
                return WaveVariable.Null;
            }

            // 2. Save state for backtracking
            if (useBacktracking)
                PushCheckpoint();

            // 3. Decohere state
            //Debug.Log("min entropy: " + minEntropy + " found in " + argminEntropy.Count + " states");
            int r = Random.Range(0, argminEntropy.Count);
            var selected = argminEntropy[r];
            var wave = system.GetWave(selected);
            wave.Observe();
            system.SetWave(selected, wave);

            // 4. Save choice for backtracking
            if (useBacktracking)
                obervations.Push(new Observation(selected, wave));

            return selected;
        }
    }

}
