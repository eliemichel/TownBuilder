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
        int _exclusionClass;

        ulong _bitfield;
        readonly int _dimension; // total number of pure states, i.e. number of bits actually in use in _bitfields

        public int Dimension { get { return _dimension; } }

        /**
         * Pure state's 'index' field is given absolute, while we store
         * a _bitfield using indices relative to the exclusion class.
         * This returns the offset to apply to a relative state index to make
         * it an absolute one.
         */
        int PureStateOffset {  get { return _exclusionClass * Dimension; } }

        /**
         * Superposed state of maximal entropy, i.e. in which all pure states are equiprobable.
         * It is used for initialization of the state vector.
         */
        public static SuperposedState EquiprobableInClass(int dimension, int exclusionClass)
        {
            return new SuperposedState(exclusionClass, ~0ul, dimension);
        }

        /**
         * The opposite of equiprobable, a superposition containing no state (entropy -Infinity)
         * This is only used for initialization, it has no physical meaning
         */
        public static SuperposedState None(int dimension)
        {
            return new SuperposedState(-1, 0ul, dimension);
        }

        SuperposedState(int exclusionClass, ulong bitfield, int dimension)
        {
            Debug.Assert(dimension < 64); // if dimension goes beyond 64, we'll have to use more advanced bitfields
            _bitfield = bitfield;
            _dimension = dimension;
            _exclusionClass = exclusionClass;
        }

        /**
         * Pure state index is given absolute
         */
        public SuperposedState(int exclusionClass, int pureStateIndex, int dimension)
        {
            _dimension = dimension;
            _bitfield = 0ul;
            _exclusionClass = exclusionClass;
            Add(new PureState(pureStateIndex));
        }

        public bool Equals(SuperposedState other)
        {
            ulong mask = (1ul << _dimension) - 1ul;
            return (other._bitfield & mask) == (_bitfield & mask) && _exclusionClass == other._exclusionClass;
        }

        public override string ToString()
        {
            if (Equals(EquiprobableInClass(_dimension, _exclusionClass)))
            {
                return "SuperposedState(ALL in x" + _exclusionClass + ")";
            }
            else
            {
                ulong mask = (1ul << _dimension) - 1ul;
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
                relativeStateIndex >= 0 && relativeStateIndex < Dimension // check that we are in the same class of exclusion
                && (_bitfield & (1ul << relativeStateIndex)) != 0;
        }

        /**
         * Return the list of pure states included in this superposition
         */
        public List<PureState> Components()
        {
            List<PureState> l = new List<PureState>();
            for (int i = 0; i < _dimension; ++i)
            {
                if (Project(i))
                {
                    l.Add(new PureState(PureStateOffset + i));
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
            Debug.Assert(relativeStateIndex >= 0 && relativeStateIndex < Dimension);
            _bitfield = 1ul << relativeStateIndex;
        }

        /**
         * Remove a pure state from the superposition
         */
        public void Remove(PureState s)
        {
            int relativeStateIndex = s.index - PureStateOffset;
            if (relativeStateIndex >= 0 && relativeStateIndex < Dimension)
            {
                _bitfield &= (~(1ul << relativeStateIndex));
            }
        }

        /**
         * Add a pure state to the superposition
         */
        public void Add(PureState s)
        {
            if (_exclusionClass == -1)
            {
                _exclusionClass = s.index / Dimension;
            }

            int relativeStateIndex = s.index - PureStateOffset;
            if (relativeStateIndex >= 0 && relativeStateIndex < Dimension)
            {
                _bitfield |= (1ul << relativeStateIndex);
            }
            else
            {
                // If we try to add a pure state that was not in the exclusion
                // class of this superposed state, then the states collapses to
                // empty state
                _bitfield = 0ul;
                _exclusionClass = -2; // freezes emptiness
            }
        }

        /**
         * Return the state masked by another one, ie the intersection of state sets
         */
        public SuperposedState MaskBy(SuperposedState mask)
        {
            Debug.Assert(mask.Dimension == Dimension);
            if (mask._exclusionClass != _exclusionClass)
            {
                return SuperposedState.None(Dimension);
            }
            else
            {
                return new SuperposedState(
                    _exclusionClass,
                    _bitfield & mask._bitfield,
                    Dimension
                );
            }
        }

        /**
         * Technically, this returns 2^entropy-1 (-1 to keep entropy = 0 when no choice)
         */
        public float Entropy()
        {
            float ent = 0;
            for (int i = 0; i < _dimension; ++i)
            {
                if (Project(i))
                {
                    ent += 1;
                }
            }
            return Mathf.Max(0, ent - 1);
        }
    }

}
