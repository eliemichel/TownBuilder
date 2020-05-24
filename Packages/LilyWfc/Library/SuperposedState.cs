using System.Collections.Generic;
using UnityEngine;

namespace LilyWfc
{
    /**
     * Also known as Wave Function, a superposed state is a function that associate a probability to each pure state.
     * In practice, these probabilities are binary, i.e. either 0 or 1, so that we can store all of them in the bits of a single ulong.
     */
    public struct SuperposedState
    {
        ulong _bitfield;
        readonly int _dimension; // total number of pure states, i.e. number of bits actually in use in _bitfields

        public int Dimension { get { return _dimension; } }

        /**
         * Superposed state of maximal entropy, i.e. in which all pure states are equiprobable.
         * It is used for initialization of the state vector.
         */
        public static SuperposedState Equiprobable(int dimension)
        {
            return new SuperposedState(~0ul, dimension);
        }

        /**
         * The opposite of equiprobable, a superposition containing no state (entropy -Infinity)
         * This is only used for initialization, it has no physical meaning
         */
        public static SuperposedState None(int dimension)
        {
            return new SuperposedState(0ul, dimension);
        }

        SuperposedState(ulong bitfield, int dimension)
        {
            Debug.Assert(dimension < 64); // if dimension goes beyond 64, we'll have to use more advanced bitfields
            _bitfield = bitfield;
            _dimension = dimension;
        }

        public SuperposedState(int pureStateIndex, int dimension)
        {
            _dimension = dimension;
            _bitfield = 0ul;
            Add(new PureState(pureStateIndex));
        }

        public bool Equals(SuperposedState other)
        {
            ulong mask = (1ul << _dimension) - 1ul;
            return (other._bitfield & mask) == (_bitfield & mask);
        }

        public override string ToString()
        {
            if (Equals(Equiprobable(_dimension)))
            {
                return "SuperposedState(ALL)";
            }
            else
            {
                return "SuperposedState(" + System.Convert.ToString((long)_bitfield, 2) + ")";
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
            return (_bitfield & (1ul << stateIndex)) != 0;
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
                    l.Add(new PureState(i));
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
            int index = Random.Range(0, pureStates.Length);
            _bitfield = 1ul << pureStates[index].index;
        }

        /**
         * Remove a pure state from the superposition
         */
        public void Remove(PureState s)
        {
            _bitfield &= (~(1ul << s.index));
        }

        /**
         * Add a pure state to the superposition
         */
        public void Add(PureState s)
        {
            _bitfield |= (1ul << s.index);
        }

        /**
         * Return the state masked by another one
         */
        public SuperposedState MaskBy(SuperposedState mask)
        {
            Debug.Assert(mask._dimension == _dimension);
            return new SuperposedState(
                _bitfield & mask._bitfield,
                _dimension
            );
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
