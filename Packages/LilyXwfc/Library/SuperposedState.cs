using System.Collections.Generic;
using UnityEngine;

namespace LilyXwfc
{
    /**
     * Also known as Wave Function, a superposed state is a function that associate a probability to each pure state.
     * In practice, these probabilities are binary, i.e. either 0 or 1, so that we can store all of them in the bits of a single ulong.
     */
    public struct SuperposedState
    {
        /**
         * This is the main difference with traditional WFC:
         * there are actually n * Dimension possible states, but we know some
         * classes of exclusion, i.e. superposed states can only belong to one
         * class at a time. Hence we store this class index plus the usual
         * bitfields to know which elements of this class are in the
         * superposition.
         * NB: The exclusion class must be provided by the initial conditions,
         * here Equiprobable() is not possible.
         * The special value of -1 is used for empty superposition, to mean
         * "can switch to an actual exclusion class when needed".
         * Other negative values force the state to remain empty.
         */
        readonly int _exclusionClass;

        ulong _bitfield;
        // number of states in the exclusion class, i.e. number of bits actually in use in _bitfields
        readonly int _localDimension;
        // max of all _localDimension among all exclusion classes, used for convertion to pure states
        readonly int _globalDimension;

        public int GlobalDimension { get { return _globalDimension; } }
        public int DimensionInExclusionClass { get { return _localDimension; } }

        /**
         * Pure state's 'index' field is given absolute, while we store
         * a _bitfield using indices relative to the exclusion class.
         * This returns the offset to apply to a relative state index to make
         * it an absolute one.
         */
        int PureStateOffset {  get { return _exclusionClass * GlobalDimension; } }

        /**
         * Superposed state of maximal entropy, i.e. in which all pure states are equiprobable.
         * It is used for initialization of the state vector.
         */
        public static SuperposedState EquiprobableInClass(int globalDimension, int localDimension, int exclusionClass)
        {
            return new SuperposedState(exclusionClass, ~0ul, globalDimension, localDimension);
        }

        /**
         * The opposite of equiprobable, a superposition containing no state (entropy -Infinity)
         * This is only used for initialization, it has no physical meaning.
         * Even a none state must have an exclusion class
         */
        public static SuperposedState None(int globalDimension, int localDimension, int exclusionClass)
        {
            return new SuperposedState(exclusionClass, 0ul, globalDimension, localDimension);
        }

        /**
         * Create a none superposition with the same exclusion class
         */
        public static SuperposedState None(SuperposedState template)
        {
            return new SuperposedState(template._exclusionClass, 0ul, template._globalDimension, template._localDimension);
        }

        SuperposedState(int exclusionClass, ulong bitfield, int globalDimension, int localDimension)
        {
            if (localDimension < 0) localDimension = globalDimension;
            Debug.Assert(globalDimension < 64); // if dimension goes beyond 64, we'll have to use more advanced bitfields
            _bitfield = bitfield;
            _globalDimension = globalDimension;
            _localDimension = localDimension;
            _exclusionClass = exclusionClass;
        }

        /**
         * Pure state index is given absolute
         */
        public SuperposedState(int exclusionClass, int pureStateIndex, int globalDimension, int localDimension)
        {
            if (localDimension < 0) localDimension = globalDimension;
            _globalDimension = globalDimension;
            _localDimension = localDimension;
            _bitfield = 0ul;
            _exclusionClass = exclusionClass;
            Add(new PureState(pureStateIndex));
        }

        public bool Equals(SuperposedState other)
        {
            ulong mask = (1ul << DimensionInExclusionClass) - 1ul;
            return (other._bitfield & mask) == (_bitfield & mask) && _exclusionClass == other._exclusionClass;
        }

        public override string ToString()
        {
            if (Equals(EquiprobableInClass(_globalDimension, _localDimension, _exclusionClass)))
            {
                return "SuperposedState(ALL " + DimensionInExclusionClass + " in x" + _exclusionClass + ")";
            }
            else
            {
                ulong mask = (1ul << DimensionInExclusionClass) - 1ul;
                return "SuperposedState(" + System.Convert.ToString((long)(_bitfield & mask), 2) + " in x" + _exclusionClass + ")";
            }
        }

        /**
         * Return the coefficient along a given basic state.
         * Coef is boolean because we only consider boolean combinations.
         */
        public bool Project(PureState state)
        {
            return Project(state.index);
        }
        bool Project(int stateIndex)
        {
            int relativeStateIndex = stateIndex - PureStateOffset;
            return
                relativeStateIndex >= 0 && relativeStateIndex < DimensionInExclusionClass // check that we are in the same class of exclusion
                && (_bitfield & (1ul << relativeStateIndex)) != 0;
        }

        /**
         * Return the list of pure states included in this superposition
         */
        public List<PureState> Components()
        {
            List<PureState> l = new List<PureState>();
            for (int i = 0; i < DimensionInExclusionClass; ++i)
            {
                var ps = new PureState(PureStateOffset + i);
                if (Project(ps))
                {
                    l.Add(ps);
                }
            }
            return l;
        }

        /**
         * Randomly project the state onto one of the superposed pure states
         * Assumes that state is observable (i.e. non null)
         */
        public void Observe()
        {
            var pureStates = Components().ToArray();
            Debug.Assert(pureStates.Length >= 1);
            int compIndex = Random.Range(0, pureStates.Length);
            int relativeStateIndex = pureStates[compIndex].index - PureStateOffset;
            Debug.Assert(relativeStateIndex >= 0 && relativeStateIndex < DimensionInExclusionClass);
            _bitfield = 1ul << relativeStateIndex;
        }

        /**
         * Remove a pure state from the superposition
         */
        public void Remove(PureState s)
        {
            int relativeStateIndex = s.index - PureStateOffset;
            if (relativeStateIndex >= 0 && relativeStateIndex < DimensionInExclusionClass)
            {
                _bitfield &= (~(1ul << relativeStateIndex));
            }
        }

        /**
         * Add a pure state to the superposition.
         */
        public void Add(PureState s)
        {
            int relativeStateIndex = s.index - PureStateOffset;
            if (relativeStateIndex >= 0 && relativeStateIndex < DimensionInExclusionClass)
            {
                _bitfield |= (1ul << relativeStateIndex);
            }
            else
            {
                // If we try to add a pure state that was not in the exclusion
                // class of this superposed state, then the states collapses to
                // empty state
                _bitfield = 0ul;
                Debug.Assert(false, "must not add a pure state from a different exclusion class");
            }
        }

        /**
         * Return the state masked by another one, ie the intersection of state sets
         */
        public SuperposedState MaskBy(SuperposedState mask)
        {
            Debug.Assert(mask.GlobalDimension == GlobalDimension);
            if (mask._exclusionClass != _exclusionClass)
            {
                return SuperposedState.None(this);
            }
            else
            {
                return new SuperposedState(
                    _exclusionClass,
                    _bitfield & mask._bitfield,
                    GlobalDimension,
                    DimensionInExclusionClass
                );
            }
        }

        /**
         * Technically, this returns 2^entropy-1 (-1 to keep entropy = 0 when no choice)
         */
        public float Entropy()
        {
            float ent = 0;
            for (int i = 0; i < DimensionInExclusionClass; ++i)
            {
                var ps = new PureState(PureStateOffset + i);
                if (Project(ps))
                {
                    ent += 1;
                }
            }
            return Mathf.Max(0, ent - 1);
        }

        /**
         * Return the pure states contained in the same exclusion class
         */
        public IEnumerable<PureState> NonExcludedPureStates()
        {
            for (int i = 0; i < DimensionInExclusionClass; ++i)
            {
                yield return new PureState(i + PureStateOffset);
            }
        }
    }

}
