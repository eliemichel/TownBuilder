namespace LilyWfc
{

    /**
     * Base vector of the state space. A state is a boolean combination of such states.
     * For procedural content generation, a pure state corresponds to a choice of "module", or "tile"
     */
    public readonly struct PureState
    {
        public readonly int index;
        public PureState(int index)
        {
            this.index = index;
        }

        public override string ToString()
        {
            return "PureState(" + index + ")";
        }
    }

}
