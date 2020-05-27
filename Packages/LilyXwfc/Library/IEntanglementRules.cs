namespace LilyXwfc
{
    /**
     * Entanglement rules define the interaction between neighboring variables
     * in a WaveFunctionSystem.
     */
    public interface IEntanglementRules
    {
        /**
         * Return the probability that two variables X and Y are respectively
         * in states x and y knowing that they are related by a connection of
         * type #connectionType.
         * 
         * In this variant, it may be context dependent, i.e. depend on the slots
         * so we provide a loop associated to the slot of x (loop.next.vert is
         * the slot of y). An attribute "adjacency" must be present on it.
         */
        bool Allows(PureState x, BMesh.Loop connection, PureState y);

        /**
         * Contrary to regular WFC, the superposition must depend on y because
         * a single SuperposedState object cannot hold all the allowed neighbors
         */
        SuperposedState AllowedStates(SuperposedState x, BMesh.Loop connection, SuperposedState y);

        /**
         * The number of dimensions within a given class may be inferior to the
         * maximum number of dimensions provided to the system (this is used by
         * EquiprobableInClass for instance). Return -1 to mean maximum.
         */
        int DimensionInExclusionClass(int exclusionClass);
    }

    /**
     * Provide default implementation of optional methods of IEntanglementRules
     */
    public abstract class AEntanglementRules : IEntanglementRules
    {
        public abstract bool Allows(PureState x, BMesh.Loop connection, PureState y);

        public virtual SuperposedState AllowedStates(SuperposedState x, BMesh.Loop connection, SuperposedState y)
        {
            SuperposedState z = SuperposedState.None(y);
            foreach (PureState xc in x.Components())
            {
                foreach (PureState zc in z.NonExcludedPureStates())
                {
                    if (Allows(xc, connection, zc))
                    {
                        z.Add(zc);
                    }
                }
            }
            return z;
        }

        public virtual int DimensionInExclusionClass(int exclusionClass)
        {
            return -1;
        }
    }
}
